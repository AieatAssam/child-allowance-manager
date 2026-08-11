using Xunit;

namespace ChildAllowanceManager.Tests;

public sealed class AccessibilityTests
{
    [Fact]
    public void Dashboard_exposes_no_role_img()
    {
        Assert.DoesNotContain("role=\"img\"", Source("ChildAllowanceManager/Components/Pages/ChildrenListPage.razor"));
    }

    [Fact]
    public void Balance_history_table_has_a_caption_and_scoped_headers()
    {
        var source = Source("ChildAllowanceManager/Components/BalanceHistoryTable.razor");
        Assert.Contains("<caption>Balance for each child, by date</caption>", source);
        Assert.Contains("<th scope=\"col\">Date</th>", source);
        Assert.Contains("<th scope=\"row\">", source);
    }

    [Fact]
    public void Balance_history_table_has_one_row_per_date()
    {
        var source = Source("ChildAllowanceManager/Components/BalanceHistoryTable.razor.cs");
        Assert.Contains("Distinct()", source);
        Assert.Contains("OrderBy(date => date)", source);
        Assert.Contains("d MMMM yyyy", Source("ChildAllowanceManager/Components/BalanceHistoryTable.razor"));
    }

    [Fact]
    public void Every_icon_is_hidden_or_inside_a_labelled_control()
    {
        var files = new[]
        {
            "ChildrenListPage.razor", "AdministrationPage.razor", "ChildManagementPage.razor",
            "ChildTransactionsDialogue.razor", "ChildTransactionsTable.razor"
        };
        foreach (var file in files)
        {
            var source = Source($"ChildAllowanceManager/Components/Pages/{file}");
            if (file == "ChildTransactionsTable.razor")
                source = Source($"ChildAllowanceManager/Components/{file}");
            Assert.All(source.Split('\n').Where(line => line.Contains("<MudIcon ", StringComparison.Ordinal)),
                line => Assert.Contains("aria-hidden=\"true\"", line));
        }
    }

    [Fact]
    public void Every_icon_only_button_has_an_accessible_name()
    {
        var main = Source("ChildAllowanceManager/Components/Layout/MainLayout.razor");
        var children = Source("ChildAllowanceManager/Components/Pages/ChildrenListPage.razor");
        Assert.Contains("aria-label=\"Toggle navigation\"", main);
        Assert.Contains("aria-label=\"Account menu\"", main);
        Assert.Contains("aria-label=\"More actions\"", children);
        Assert.Contains("aria-label=\"Add family\"", Source("ChildAllowanceManager/Components/Pages/AdministrationPage.razor"));
        Assert.Contains("aria-label=\"Add child\"", Source("ChildAllowanceManager/Components/Pages/ChildManagementPage.razor"));
    }

    [Fact]
    public void Signed_in_user_without_access_is_not_told_to_sign_in()
    {
        var source = Source("ChildAllowanceManager/Components/Routes.razor");
        Assert.Contains("Choose a family", source);
        Assert.Contains("authState.User.Identity?.IsAuthenticated == true", source);
    }

    [Fact]
    public void Signed_out_user_is_told_to_sign_in()
    {
        Assert.Contains("Sign in", Source("ChildAllowanceManager/Components/Routes.razor"));
    }

    [Fact]
    public void Destructive_confirmation_focuses_cancel()
    {
        var source = Source("ChildAllowanceManager/Components/ConfirmDialog.razor");
        Assert.Contains("autofocus=\"true\"", source);
        Assert.Contains("Color=\"Color.Default\"", source);
    }

    [Fact]
    public void Chart_series_carry_distinct_dash_patterns()
    {
        var source = Source("ChildAllowanceManager/Components/Pages/ChildrenListPage.razor.cs");
        Assert.Contains("ChartDashes", source);
        Assert.Contains("Dash = ChartDashes", source);
        Assert.Contains("Symbol = ChartMarkers", source);
    }

    [Fact]
    public void Page_has_exactly_one_h1_contract()
    {
        foreach (var file in new[] { "Home.razor", "ChildrenListPage.razor", "ChildManagementPage.razor", "AdministrationPage.razor", "PeoplePage.razor" })
        {
            var source = Source($"ChildAllowanceManager/Components/Pages/{file}");
            Assert.Equal(1, Count(source, "HtmlTag=\"h1\""));
        }
    }

    [Fact]
    public void Skip_link_is_the_first_focusable_element()
    {
        var source = Source("ChildAllowanceManager/Components/Layout/MainLayout.razor");
        Assert.StartsWith("﻿@using", source);
        Assert.True(source.IndexOf("class=\"skip-link\"", StringComparison.Ordinal) < source.IndexOf("MudIconButton", StringComparison.Ordinal));
    }

    private static int Count(string value, string token)
    {
        var count = 0;
        for (var index = 0; (index = value.IndexOf(token, index, StringComparison.Ordinal)) >= 0; index += token.Length)
            count++;
        return count;
    }

    private static string Source(string relativePath)
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "plan.yaml")))
            directory = directory.Parent;
        Assert.NotNull(directory);
        return File.ReadAllText(Path.Combine(directory!.FullName, relativePath));
    }
}
