using ChildAllowanceManager.Components;
using Bunit;
using FluentValidation;
using FluentValidation.Results;
using Microsoft.Extensions.DependencyInjection;
using MudBlazor;

namespace ChildAllowanceManager.Tests;

public class UiResilienceTests
{
    [Fact]
    public async Task Unexpected_failure_returns_generic_error_and_error_snackbar()
    {
        await using var context = BunitTestContext.Create();
        var provider = context.Render<MudSnackbarProvider>();
        var runner = context.Services.GetRequiredService<OperationRunner>();

        var result = await runner.RunAsync(
            () => throw new Exception("database detail"),
            failureMessage: "Nothing was changed.");

        Assert.False(result.Succeeded);
        Assert.Equal("Nothing was changed.", result.ErrorMessage);
        provider.WaitForAssertion(() => Assert.Contains("Nothing was changed.", provider.Markup));
        Assert.DoesNotContain("database detail", provider.Markup);
    }

    [Fact]
    public async Task Validation_failure_shows_validation_message()
    {
        await using var context = BunitTestContext.Create();
        var provider = context.Render<MudSnackbarProvider>();
        var runner = context.Services.GetRequiredService<OperationRunner>();

        var result = await runner.RunAsync(
            () => throw new ValidationException([
                new ValidationFailure("amount", "Amount must be positive.")])) ;

        Assert.False(result.Succeeded);
        Assert.Equal("Amount must be positive.", result.ErrorMessage);
        provider.WaitForAssertion(() => Assert.Contains("Amount must be positive.", provider.Markup));
    }

    [Fact]
    public async Task Expected_domain_failure_preserves_its_message()
    {
        await using var context = BunitTestContext.Create();
        var provider = context.Render<MudSnackbarProvider>();
        var runner = context.Services.GetRequiredService<OperationRunner>();

        var result = await runner.RunAsync(
            () => throw new KeyNotFoundException("Family was not found."));

        Assert.False(result.Succeeded);
        Assert.Equal("Family was not found.", result.ErrorMessage);
        provider.WaitForAssertion(() => Assert.Contains("Family was not found.", provider.Markup));
    }

    [Fact]
    public async Task Successful_operation_returns_value_and_success_snackbar()
    {
        await using var context = BunitTestContext.Create();
        var provider = context.Render<MudSnackbarProvider>();
        var runner = context.Services.GetRequiredService<OperationRunner>();

        var (outcome, value) = await runner.RunAsync<string>(
            () => Task.FromResult("saved"), successMessage: "Saved.");

        Assert.True(outcome.Succeeded);
        Assert.Equal("saved", value);
        provider.WaitForAssertion(() => Assert.Contains("Saved.", provider.Markup));
    }

    [Fact]
    public async Task Cancellation_returns_quietly_when_the_operation_is_cancelled()
    {
        await using var context = BunitTestContext.Create();
        var provider = context.Render<MudSnackbarProvider>();
        var runner = context.Services.GetRequiredService<OperationRunner>();
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();

        var result = await runner.RunAsync(
            async () =>
            {
                await Task.Yield();
                throw new OperationCanceledException(cancellation.Token);
            }, cancellationToken: cancellation.Token);

        Assert.False(result.Succeeded);
        Assert.Null(result.ErrorMessage);
        Assert.DoesNotContain("Something went wrong", provider.Markup);
    }
}
