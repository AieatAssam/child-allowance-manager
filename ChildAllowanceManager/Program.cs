using System.Globalization;
using System.Net;
using System.Security.Claims;
using System.Security.Principal;
using System.Text;
using Azure.Monitor.OpenTelemetry.AspNetCore;
using ChildAllowanceManager.Common.Interfaces;
using ChildAllowanceManager.Common.Models;
using ChildAllowanceManager.Components;
using ChildAllowanceManager.Services;
using ChildAllowanceManager.Workers;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authentication.MicrosoftAccount;
using Microsoft.AspNetCore.Components.Server.ProtectedBrowserStorage;
using Microsoft.AspNetCore.Identity;
using MudBlazor.Services;
using Newtonsoft.Json.Linq;
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
builder.Services.AddOpenTelemetry().UseAzureMonitor();

// Add services to the container.
builder.Services.AddRazorComponents()
    .AddInteractiveServerComponents();

builder.Services.AddMudServices();

builder.Services.AddHttpContextAccessor();

var configuration = builder.Configuration;

builder.Services.AddAuthentication(CookieAuthenticationDefaults.AuthenticationScheme)
    .AddCookie(CookieAuthenticationDefaults.AuthenticationScheme, options =>
    {
        options.ExpireTimeSpan = TimeSpan.FromMinutes(30);
        options.Cookie.MaxAge = options.ExpireTimeSpan; // optional
        options.SlidingExpiration = true;
    })
    .AddMicrosoftAccount("Microsoft", "Microsoft", options =>
    {
        options.ClientId = configuration["Authentication:Microsoft:ClientId"];
        options.ClientSecret = configuration["Authentication:Microsoft:ClientSecret"];
        //options.CallbackPath = "/signin-microsoft";
        options.SaveTokens = true;
        options.Scope.Add("User.Read");
        options.Events.OnTicketReceived += async context =>
        {
            if (context.Principal?.Identity is ClaimsIdentity identity)
            {
                var userService = context.HttpContext.RequestServices.GetRequiredService<IUserService>();
                var email = identity.FindFirst(ClaimTypes.Email)?.Value;
                var tenant = context.Request.Cookies.TryGetValue("current_tenant", out var currentTenant) ? currentTenant : null;
                
                if (email != null)
                {
                    var user = await userService.GetUserByEmail(email, CancellationToken.None);
                    if (user == null)
                    {
                        var name = identity.FindFirst(ClaimTypes.Name)?.Value;
                        if (!string.IsNullOrEmpty(tenant))
                        {
                            identity.AddClaim(new Claim("current_tenant", tenant));
                        }

                        await userService.InitializeUser(email, name, tenant, CancellationToken.None);
                    }
                    else
                    {
                        identity.AddClaim(new Claim("current_tenant", user.Tenants[0]));
                        if (user.ExtraClaims.Contains(ValidClaims.Admin))
                        {
                            if (identity.HasClaim(x => x.Type == identity.RoleClaimType))
                            {
                                identity.AddClaim(new Claim(identity.RoleClaimType, ValidClaims.Admin));
                            }
                        }
                        if (user.ExtraClaims.Contains(ValidClaims.Parent))
                        {
                            if (identity.HasClaim(x => x.Type == identity.RoleClaimType))
                            {
                                identity.AddClaim(new Claim(identity.RoleClaimType, ValidClaims.Parent));
                            }
                        }
                    }
                }
                if (!string.IsNullOrEmpty(tenant))
                {
                    var dataService = context.HttpContext.RequestServices.GetRequiredService<IDataService>();
                    var redirectTenant = await dataService.GetTenant(tenant);
                    if (redirectTenant is not null)
                    {
                        context.ReturnUri = $"/{redirectTenant.UrlSuffix}/children";
                    }
                }
            }
        };
        //options.Scope.Add("offline_access");
    });

builder.Services.AddHttpClient();
builder.Services.AddCosmosRepository(options =>
{
    // serverless throughput will ensure that shared database throughput is used
    options.ContainerBuilder.Configure<ChildConfiguration>(config =>
        config.WithServerlessThroughput());
    options.ContainerBuilder.Configure<TenantConfiguration>(config =>
        config.WithServerlessThroughput());
    options.ContainerBuilder.Configure<AllowanceTransaction>(config =>
        config.WithServerlessThroughput());
    options.ContainerBuilder.Configure<User>(config =>
        config.WithServerlessThroughput());
});

// scheduling support
builder.Services.AddTransient<DailyAllowanceJob>();
builder.Services.AddQuartz(q =>
{
    var jobKey = new JobKey(nameof(DailyAllowanceJob));
    q.AddJob<DailyAllowanceJob>(opts => opts.WithIdentity(jobKey));

    q.AddTrigger(opts => opts
        .ForJob(jobKey)
        .WithIdentity($"{nameof(DailyAllowanceJob)}-trigger")
        //This Cron interval can be described as "run every minute" (when second is zero)
        .WithCronSchedule(CronScheduleBuilder.DailyAtHourAndMinute(0, 0).InTimeZone(TimeZoneInfo.Utc)
            .WithMisfireHandlingInstructionFireAndProceed()));
});
builder.Services.AddQuartzHostedService(config =>
{
    config.AwaitApplicationStarted = true;
    config.WaitForJobsToComplete = true;
});

builder.Services.AddCascadingAuthenticationState();

builder.Services.AddScoped<IDataService, DataService>();
builder.Services.AddScoped<ITransactionService, TransactionService>();
builder.Services.AddScoped<IUserService, UserService>();
builder.Services.AddScoped<ICurrentContextService, CurrentContextService>();

var app = builder.Build();

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

app.MapRazorComponents<App>()
    .AddInteractiveServerRenderMode();

// Choose an authentication type
app.Map("/login", signinApp =>
{
    signinApp.Run(async context =>
    {
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

app.Run();