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
using ChildAllowanceManager.Services;
using ChildAllowanceManager.Workers;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authentication.MicrosoftAccount;
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
if (StartupConfiguration.UseAzureMonitor(builder.Environment, builder.Configuration))
    builder.Services.AddOpenTelemetry().UseAzureMonitor();

// Add services to the container.
builder.Services.AddRazorComponents()
    .AddInteractiveServerComponents();

builder.Services.AddMudServices();
builder.Services.AddHttpContextAccessor();

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
    options.UseNpgsql(postgresConnection));
builder.Services.AddHealthChecks().AddDbContextCheck<AllowanceDbContext>();

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
                    var user = await userService.GetUserByEmailAsync(email, CancellationToken.None);
                    if (user == null)
                    {
                        var name = identity.FindFirst(ClaimTypes.Name)?.Value;
                        user = await userService.InitializeUserAsync(email, name ?? string.Empty, null, CancellationToken.None);
                    }
                    else
                    {
                        user.LastLoggedIn = DateTimeOffset.UtcNow;
                        await userService.UpsertUserAsync(user, CancellationToken.None);
                    }

                    foreach (var tenantId in user.Tenants.Distinct())
                        identity.AddClaim(new Claim(CustomClaimTypes.Tenant, tenantId));
                }
            }
            context.RunClaimActions();
        };
        options.Events.OnTicketReceived += async context =>
        {
            var tenant = context.Request.Cookies.TryGetValue("current_tenant", out var currentTenant) ? currentTenant : null;

            if (!string.IsNullOrEmpty(tenant) &&
                context.Principal?.HasClaim(CustomClaimTypes.Tenant, tenant) == true &&
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
        .WithDescription("Minute past midnight")
        .WithCronSchedule(CronScheduleBuilder.DailyAtHourAndMinute(0, 1).InTimeZone(TimeZoneInfo.Utc)
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

builder.Services.AddScoped<IChildService, ChildService>();
builder.Services.AddScoped<ITenantService, TenantService>();
builder.Services.AddScoped<ITransactionService, TransactionService>();
builder.Services.AddScoped<IUserService, UserService>();
builder.Services.AddScoped<IClaimsTransformation, ClaimEnrichmentTransformer>();
builder.Services.AddScoped<ICurrentContextService, CurrentContextService>();
builder.Services.AddScoped<ITenantNotificationService, TenantNotificationService>();

builder.Services.AddSingleton<IGlobalNotificationService, GlobalNotificationService>();

var app = builder.Build();

app.Use(async (context, next) =>
{
    context.Response.Headers.XFrameOptions = "SAMEORIGIN";
    await next();
});

if (StartupConfiguration.ShouldMigrate(app.Environment, args))
{
    await using var scope = app.Services.CreateAsyncScope();
    var db = scope.ServiceProvider.GetRequiredService<AllowanceDbContext>();
    await db.Database.MigrateAsync();
    if (app.Environment.IsDevelopment())
        await new DevelopmentDataSeeder(db).SeedAsync();
}

app.UseResponseCompression();

app.UseRequestLocalization();


// Configure the HTTP request pipeline.
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Error", createScopeForErrors: true);
    
    // The default HSTS value is 30 days. You may want to change this for production scenarios, see https://aka.ms/aspnetcore-hsts.
    app.UseHsts();
}

app.UseHttpsRedirection();

app.UseStaticFiles();
app.UseAntiforgery();

app.UseAuthentication();
app.UseAuthorization();

var frameAncestors = builder.Configuration.GetSection("FrameAncestors").Get<string[]>() ?? [];
var frameAncestorsPolicy = StartupPolicy.BuildFrameAncestorsPolicy(frameAncestors);
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
            context.Response.Redirect($"/{DevelopmentDataSeeder.TenantSuffix}/children");
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

app.MapHealthChecks("/health");
app.Run();
