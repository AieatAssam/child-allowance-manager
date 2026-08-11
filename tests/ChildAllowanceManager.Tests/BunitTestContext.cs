using Bunit;
using ChildAllowanceManager.Components;
using ChildAllowanceManager.Common.Interfaces;
using ChildAllowanceManager.Services;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using MudBlazor.Services;

namespace ChildAllowanceManager.Tests;

public static class BunitTestContext
{
    public static BunitContext Create()
    {
        var context = new BunitContext();
        context.JSInterop.Mode = JSRuntimeMode.Loose;
        context.Services.AddMudServices();
        context.Services.Add(ServiceDescriptor.Singleton<ILogger<OperationRunner>>(
            NullLogger<OperationRunner>.Instance));
        context.Services.Add(new ServiceDescriptor(
            typeof(OperationRunner), typeof(OperationRunner), ServiceLifetime.Scoped));
        context.Services.AddSingleton<ITenantAuthorizationService, TenantAuthorizationService>();
        context.Services.AddSingleton<IInvitationService, RecordingInvitationService>();
        return context;
    }
}
