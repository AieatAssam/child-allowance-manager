using System.Globalization;
using System.Net.Http.Headers;
using System.Security.Claims;
using System.Text.Json;
using Azure.Monitor.OpenTelemetry.AspNetCore;
using ChildAllowanceManager;
using ChildAllowanceManager.Common.Interfaces;
using ChildAllowanceManager.Common.Models;
using ChildAllowanceManager.Components;
using ChildAllowanceManager.Data;
using ChildAllowanceManager.HealthChecks;
using ChildAllowanceManager.Migrations;
using ChildAllowanceManager.Services;
using ChildAllowanceManager.Workers;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authentication.MicrosoftAccount;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.AspNetCore.ResponseCompression;
using Microsoft.EntityFrameworkCore;
using MudBlazor.Services;
using Quartz;

// Edit culture to match the desired one
var cultureInfo = new CultureInfo("en-GB")
{
    NumberFormat =
    {
        CurrencySymbol = "£"
    }
};
CultureInfo.DefaultThreadCurrentCulture = cultureInfo;
CultureInfo.DefaultThreadCurrentUICulture = cultureInfo;

var builder = WebApplication.CreateBuilder(args);
if (StartupConfiguration.UseAzureMonitor(builder.Configuration))
    builder.Services.AddOpenTelemetry().UseAzureMonitor();

// Add services to the container.
builder.Services.AddRazorComponents()
    .AddInteractiveServerComponents();

builder.Services.AddMudServices();
builder.Services.AddHttpContextAccessor();
builder.Services.Configure<ForwardedHeadersOptions>(options =>
{
    options.ForwardedHeaders =
        ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto;
    options.KnownIPNetworks.Clear();
    options.KnownProxies.Clear();
});

var configuration = builder.Configuration;
var postgresConnection = configuration.GetConnectionString("Postgres") ?? string.Empty;
if (!StartupPolicy.IsConfigured(postgresConnection))
{
    throw new InvalidOperationException(
        "ConnectionStrings:Postgres is required and must not be the placeholder value. " +
        "Set ConnectionStrings__Postgres to a PostgreSQL connection string, for example " +
        "Host=localhost;Port=5432;Database=child_allowance_manager;Username=postgres;Password=postgres");
}
builder.Services.AddDbContext<AllowanceDbContext>(options =>
    options.UseNpgsql(postgresConnection), ServiceLifetime.Transient);
builder.Services.AddDataProtection()
    .PersistKeysToDbContext<AllowanceDbContext>();
builder.Services.AddHealthChecks()
    .AddDbContextCheck<AllowanceDbContext>(tags: ["ready"])
    .AddCheck<MigrationHealthCheck>("migrations", tags: ["ready"]);

var authentication = builder.Services.AddAuthentication(CookieAuthenticationDefaults.AuthenticationScheme)
    .AddCookie(CookieAuthenticationDefaults.AuthenticationScheme, options =>
    {
        options.ExpireTimeSpan = TimeSpan.FromDays(7);
        options.Cookie.MaxAge = options.ExpireTimeSpan; // optional
        options.SlidingExpiration = true;
        options.LoginPath = "/login";
        options.LogoutPath = "/logout";
    });

if (!builder.Environment.IsDevelopment())
{
    authentication.AddMicrosoftAccount("Microsoft", "Microsoft", options =>
    {
        var clientId = configuration["Authentication:Microsoft:ClientId"] ?? string.Empty;
        if (!StartupPolicy.IsConfigured(clientId))
            throw new InvalidOperationException(
                "Authentication:Microsoft:ClientId is required and must not be the placeholder value.");
        options.ClientId = clientId;

        var clientSecret = configuration["Authentication:Microsoft:ClientSecret"] ?? string.Empty;
        if (!StartupPolicy.IsConfigured(clientSecret))
            throw new InvalidOperationException(
                "Authentication:Microsoft:ClientSecret is required and must not be the placeholder value.");
        options.ClientSecret = clientSecret;
        //options.CallbackPath = "/signin-microsoft";
        options.SaveTokens = true;
        options.Scope.Add("User.Read");
        options.Events.OnCreatingTicket += async context =>
        {
            
            var request = new HttpRequestMessage(HttpMethod.Get, "https://graph.microsoft.com/v1.0/me");
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", context.AccessToken);
            var response = await context.Backchannel.SendAsync(request, context.HttpContext.RequestAborted);
            response.EnsureSuccessStatusCode();
            var json = JsonSerializer.Deserialize<JsonElement>(await response.Content.ReadAsStringAsync());
            context.RunClaimActions(json);
            
            if (context.Principal?.Identity is ClaimsIdentity identity)
            {
                var userService = context.HttpContext.RequestServices.GetRequiredService<IUserService>();
                var email = identity.FindFirst(ClaimTypes.Email)?.Value;
                if (email != null)
                {
                    var cancellationToken = context.HttpContext.RequestAborted;
                    var user = await userService.GetUserByEmailAsync(email, cancellationToken);
                    if (user == null)
                    {
                        var name = identity.FindFirst(ClaimTypes.Name)?.Value;
                        user = await userService.InitializeUserAsync(email, name ?? string.Empty, null, cancellationToken);
                    }
                    else
                    {
                        user.LastLoggedIn = DateTimeOffset.UtcNow;
                        await userService.UpsertUserAsync(user, cancellationToken);
                    }

                    await context.HttpContext.RequestServices.GetRequiredService<IInvitationService>()
                        .AcceptPendingAsync(email, identity.FindFirst(ClaimTypes.Name)?.Value ?? string.Empty,
                            cancellationToken);

                }
            }
            context.RunClaimActions();
        };
        options.Events.OnTicketReceived += async context =>
        {
            var tenant = context.Request.Cookies.TryGetValue("current_tenant", out var currentTenant) ? currentTenant : null;
            var canViewTenant = tenant is not null && context.Principal is not null &&
                context.HttpContext.RequestServices.GetRequiredService<ITenantAuthorizationService>()
                    .CanView(context.Principal, tenant);

            if (!string.IsNullOrEmpty(tenant) &&
                canViewTenant &&
                string.IsNullOrEmpty(context.ReturnUri?.Trim('/')))
            {
                // no return uri specified, so set one for user's tenant
                var tenantService = context.HttpContext.RequestServices.GetRequiredService<ITenantService>();
                var redirectTenant = await tenantService.GetTenant(tenant);
                if (redirectTenant is not null)
                {
                    context.ReturnUri = $"/{redirectTenant.UrlSuffix}/children";
                }
            }
        };
        //options.Scope.Add("offline_access");
    });
}

builder.Services.AddHttpClient();

// scheduling support
builder.Services.AddTransient<DailyAllowanceJob>();
builder.Services.AddQuartz(q =>
{
    var jobKey = new JobKey(nameof(DailyAllowanceJob));
    q.AddJob<DailyAllowanceJob>(opts => opts.WithIdentity(jobKey));

    q.AddTrigger(opts => opts
        .ForJob(jobKey)
        .WithIdentity($"{nameof(DailyAllowanceJob)}-trigger")
        // Families choose their own time zone, so the job wakes every hour and pays only the families whose local day has just started. The job is idempotent per
        // (child, allowance date), so extra wake-ups are harmless.
        .WithDescription("Hourly; pays each family at its own local 00:01")
        .WithCronSchedule("0 1 * * * ?", x => x
            .InTimeZone(TimeZoneInfo.Utc)
            .WithMisfireHandlingInstructionFireAndProceed()));
});
builder.Services.AddQuartzHostedService(config =>
{
    config.AwaitApplicationStarted = true;
    config.WaitForJobsToComplete = true;
});

// Notification support
builder.Services.AddSignalR();
builder.Services.AddResponseCompression(opts =>
{
    opts.MimeTypes = ResponseCompressionDefaults.MimeTypes.Concat(
        ["application/octet-stream"]);
});

builder.Services.AddCascadingAuthenticationState();

// Data services own transient DbContexts; keep the service lifetime transient so
// concurrent Blazor components do not share one context instance.
builder.Services.AddTransient<IChildService, ChildService>();
builder.Services.AddTransient<ITenantService, TenantService>();
builder.Services.AddTransient<TransactionService>();
builder.Services.AddTransient<ITransactionService>(sp => sp.GetRequiredService<TransactionService>());
builder.Services.AddTransient<UserService>();
builder.Services.AddTransient<IUserService>(sp => sp.GetRequiredService<UserService>());
builder.Services.AddTransient<MembershipService>();
builder.Services.AddTransient<IMembershipService>(sp => sp.GetRequiredService<MembershipService>());
builder.Services.AddScoped<ITenantAuthorizationService, TenantAuthorizationService>();
builder.Services.AddTransient<IInvitationService, InvitationService>();
builder.Services.AddTransient<IShareLinkService, ShareLinkService>();
builder.Services.AddScoped<IClaimsTransformation, ClaimEnrichmentTransformer>();
builder.Services.AddScoped<ICurrentContextService, CurrentContextService>();
builder.Services.AddScoped<ITenantNotificationService, TenantNotificationService>();
builder.Services.AddScoped<OperationRunner>();

builder.Services.AddSingleton<IGlobalNotificationService, GlobalNotificationService>();

var app = builder.Build();
var frameAncestors = builder.Configuration.GetSection("FrameAncestors").Get<string[]>() ?? [];
var frameAncestorsPolicy = StartupPolicy.BuildFrameAncestorsPolicy(frameAncestors);

app.Use(async (context, next) =>
{
    context.Response.OnStarting(() =>
    {
        if (frameAncestors.Length == 0)
            context.Response.Headers.XFrameOptions = "SAMEORIGIN";
        else
            context.Response.Headers.Remove("X-Frame-Options");
        if (context.Request.Path.StartsWithSegments("/share"))
        {
            context.Response.Headers["Referrer-Policy"] = "no-referrer";
            context.Response.Headers["X-Robots-Tag"] = "noindex, nofollow";
        }
        return Task.CompletedTask;
    });
    await next();
});

var migrateOnly = args.Contains("--migrate", StringComparer.OrdinalIgnoreCase);
if (migrateOnly || app.Environment.IsDevelopment())
{
    await using var scope = app.Services.CreateAsyncScope();
    var db = scope.ServiceProvider.GetRequiredService<AllowanceDbContext>();

    var migrationLogger = scope.ServiceProvider
        .GetRequiredService<ILoggerFactory>().CreateLogger("Migrations");
    try
    {
        // Startup migration has no HTTP request or component lifetime to provide a cancellation token.
        await BaselineCompatibility.EnsureBaselineRecordedAsync(db, CancellationToken.None);
        await db.Database.MigrateAsync();
        migrationLogger.LogInformation("Database migrations applied.");
    }
    catch (Exception ex)
    {
        migrationLogger.LogError(ex, "Database migration failed.");
        if (migrateOnly)
            return 1;
        throw;
    }

    if (app.Environment.IsDevelopment() && !migrateOnly)
        await new DevelopmentDataSeeder(db).SeedAsync();
}

if (migrateOnly)
    return 0;

app.UseResponseCompression();

app.UseRequestLocalization();


// Configure the HTTP request pipeline.
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Error", createScopeForErrors: true);
    
    // The default HSTS value is 30 days. You may want to change this for production scenarios, see https://aka.ms/aspnetcore-hsts.
    app.UseHsts();
}

app.UseForwardedHeaders();
app.UseHttpsRedirection();

app.UseStaticFiles();
app.UseAntiforgery();

app.UseAuthentication();
app.UseAuthorization();

app.MapStaticAssets();
app.MapRazorComponents<App>()
    // Embedding is denied by default. Add explicit origins to the FrameAncestors
    // configuration array to allow trusted embedders.
    .AddInteractiveServerRenderMode(o => o.ContentSecurityFrameAncestorsPolicy = frameAncestorsPolicy);

// Choose an authentication type
app.Map("/login", signinApp =>
{
    signinApp.Run(async context =>
    {
        if (app.Environment.IsDevelopment())
        {
            var claims = new[]
            {
                new Claim(ClaimTypes.NameIdentifier, DevelopmentDataSeeder.UserEmail),
                new Claim(ClaimTypes.Name, "Local Demo Parent"),
                new Claim(ClaimTypes.Email, DevelopmentDataSeeder.UserEmail),
                new Claim(ClaimTypes.Role, ValidRoles.Admin),
                new Claim(ClaimTypes.Role, ValidRoles.Parent),
                new Claim(CustomClaimTypes.Tenant, DevelopmentDataSeeder.TenantId),
                new Claim("current_tenant", DevelopmentDataSeeder.TenantId)
            };
            var principal = new ClaimsPrincipal(new ClaimsIdentity(
                claims, CookieAuthenticationDefaults.AuthenticationScheme));
            await context.SignInAsync(CookieAuthenticationDefaults.AuthenticationScheme, principal);
            context.Response.Redirect("/");
            return;
        }

        await context.ChallengeAsync(MicrosoftAccountDefaults.AuthenticationScheme
            , new AuthenticationProperties() { RedirectUri = "/" });
    });
});

app.Map("/logout", signoutApp =>
{
    signoutApp.Run(async context =>
    {
        var response = context.Response;
        await context.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
        response.Redirect("/");
    });
});

app.MapHealthChecks("/health", new HealthCheckOptions
{
    Predicate = check => !check.Tags.Contains("ready")
});
app.MapHealthChecks("/health/ready", new HealthCheckOptions
{
    Predicate = check => check.Tags.Contains("ready")
});
app.Run();
return 0;
