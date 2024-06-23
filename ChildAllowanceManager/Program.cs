using ChildAllowanceManager.Common.Interfaces;
using ChildAllowanceManager.Common.Models;
using ChildAllowanceManager.Components;
using ChildAllowanceManager.Services;
using MudBlazor.Services;

var builder = WebApplication.CreateBuilder(args);

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

app.Run();