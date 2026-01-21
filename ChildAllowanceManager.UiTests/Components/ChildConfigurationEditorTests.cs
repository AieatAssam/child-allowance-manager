using Bunit;
using ChildAllowanceManager.Common.Models;
using ChildAllowanceManager.Components;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using MudBlazor;
using MudBlazor.Services;
using Shouldly;

namespace ChildAllowanceManager.UiTests.Components;

[TestClass]
public class ChildConfigurationEditorTests
{
    private Bunit.TestContext _context = default!;

    [TestInitialize]
    public void SetUp()
    {
        _context = new Bunit.TestContext();
        _context.Services.AddMudServices();
        _context.JSInterop.Mode = JSRuntimeMode.Loose;
    }

    [TestCleanup]
    public void TearDown()
    {
        _context.Dispose();
    }

    [TestMethod]
    public void RendersBirthdayAllowanceFieldWhenBirthDateProvided()
    {
        var child = new ChildConfiguration
        {
            FirstName = "Sam",
            LastName = "Smith",
            BirthDate = DateTime.Today
        };

        var cut = _context.Render(builder =>
        {
            builder.OpenComponent<MudPopoverProvider>(0);
            builder.CloseComponent();
            builder.OpenComponent<ChildConfigurationEditor>(1);
            builder.AddAttribute(2, "Child", child);
            builder.CloseComponent();
        });

        cut.FindComponents<MudNumericField<decimal?>>().Count.ShouldBe(1);
        cut.FindComponents<MudNumericField<decimal>>().Count.ShouldBe(1);
    }

    [TestMethod]
    public void DoesNotRenderBirthdayAllowanceFieldWhenBirthDateMissing()
    {
        var child = new ChildConfiguration
        {
            FirstName = "Sam",
            LastName = "Smith"
        };

        var cut = _context.Render(builder =>
        {
            builder.OpenComponent<MudPopoverProvider>(0);
            builder.CloseComponent();
            builder.OpenComponent<ChildConfigurationEditor>(1);
            builder.AddAttribute(2, "Child", child);
            builder.CloseComponent();
        });

        cut.FindComponents<MudNumericField<decimal?>>().Count.ShouldBe(0);
        cut.FindComponents<MudNumericField<decimal>>().Count.ShouldBe(1);
    }
}
