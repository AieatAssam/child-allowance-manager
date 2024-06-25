using System.Globalization;
using System.Net;
using System.Security.Claims;
using System.Security.Principal;
using Azure.Monitor.OpenTelemetry.AspNetCore;
using ChildAllowanceManager.Common.Interfaces;
using ChildAllowanceManager.Common.Models;
using ChildAllowanceManager.Components;
using ChildAllowanceManager.Services;
using ChildAllowanceManager.Workers;
using MudBlazor.Services;
using Newtonsoft.Json.Linq;
using Quartz;

// Edit culture to match the desired one
CultureInfo.DefaultThreadCurrentCulture = new CultureInfo("en-GB");

var builder = WebApplication.CreateBuilder(args);
builder.Services.AddOpenTelemetry().UseAzureMonitor();

// Add services to the container.
builder.Services.AddRazorComponents()
    .AddInteractiveServerComponents();

builder.Services.AddMudServices();

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


builder.Services.AddScoped<IDataService, DataService>();
builder.Services.AddScoped<ITransactionService, TransactionService>();

var app = builder.Build();

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

app.MapRazorComponents<App>()
    .AddInteractiveServerRenderMode();

// from https://learn.microsoft.com/en-us/answers/questions/1373805/how-to-get-user-name-for-blazor-server-application
app.Use(async (context, next) =>
{
    // Create a user on current thread from provided header
    if (context.Request.Headers.ContainsKey("X-MS-CLIENT-PRINCIPAL-ID"))
    {
        // Read headers from Azure
        var azureAppServicePrincipalIdHeader = context.Request.Headers["X-MS-CLIENT-PRINCIPAL-ID"][0];
        var azureAppServicePrincipalNameHeader = context.Request.Headers["X-MS-CLIENT-PRINCIPAL-NAME"][0];

        #region extract claims via call /.auth/me

        //invoke /.auth/me
        var cookieContainer = new CookieContainer();
        HttpClientHandler handler = new HttpClientHandler()
        {
            CookieContainer = cookieContainer
        };
        string uriString = $"{context.Request.Scheme}://{context.Request.Host}";
        foreach (var c in context.Request.Cookies)
        {
            cookieContainer.Add(new Uri(uriString), new Cookie(c.Key, c.Value));
        }

        string jsonResult = string.Empty;
        using (HttpClient client = new HttpClient(handler))
        {
            var res = await client.GetAsync($"{uriString}/.auth/me");
            jsonResult = await res.Content.ReadAsStringAsync();
        }

        //parse json
        var obj = JArray.Parse(jsonResult);
        string user_id = obj[0]["user_id"].Value<string>(); //user_id

        // Create claims id
        List<Claim> claims = new List<Claim>();
        foreach (var claim in obj[0]["user_claims"])
        {
            claims.Add(new Claim(claim["typ"].ToString(), claim["val"].ToString()));
        }

        // Set user in current context as claims principal
        var identity = new GenericIdentity(azureAppServicePrincipalNameHeader);
        identity.AddClaims(claims);

        #endregion

        // Set current thread user to identity
        context.User = new GenericPrincipal(identity, null);
    }

    await next.Invoke();
});


app.Run();