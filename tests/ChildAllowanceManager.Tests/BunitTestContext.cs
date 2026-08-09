using Bunit;
using MudBlazor.Services;

namespace ChildAllowanceManager.Tests;

public static class BunitTestContext
{
    public static BunitContext Create()
    {
        var context = new BunitContext();
        context.JSInterop.Mode = JSRuntimeMode.Loose;
        context.Services.AddMudServices();
        return context;
    }
}
