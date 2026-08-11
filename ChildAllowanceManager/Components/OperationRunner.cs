using FluentValidation;
using Microsoft.Extensions.Logging;
using MudBlazor;

namespace ChildAllowanceManager.Components;

public sealed record OperationOutcome(bool Succeeded, string? ErrorMessage)
{
    public static readonly OperationOutcome Success = new(true, null);
}

/// Runs a service call with uniform error handling, so no page has to write its own
/// try/catch and no exception reaches the Blazor circuit.
public sealed class OperationRunner(ISnackbar snackbar, ILogger<OperationRunner> logger)
{
    public async Task<OperationOutcome> RunAsync(
        Func<Task> action,
        string? successMessage = null,
        string failureMessage = "Something went wrong. Nothing was changed.",
        CancellationToken cancellationToken = default)
    {
        var (outcome, _) = await RunAsync(async () =>
        {
            await action();
            return true;
        }, successMessage, failureMessage, cancellationToken);
        return outcome;
    }

    public Task<(OperationOutcome Outcome, T? Value)> RunAsync<T>(
        Func<Task<T>> action,
        string? successMessage = null,
        string failureMessage = "Something went wrong. Nothing was changed.",
        CancellationToken cancellationToken = default) => ExecuteAsync(
            action, successMessage, failureMessage, cancellationToken);

    private async Task<(OperationOutcome Outcome, T? Value)> ExecuteAsync<T>(
        Func<Task<T>> action,
        string? successMessage,
        string failureMessage,
        CancellationToken cancellationToken)
    {
        try
        {
            var value = await action();
            if (successMessage is not null)
                snackbar.Add(successMessage, MudBlazor.Severity.Success);
            return (OperationOutcome.Success, value);
        }
        catch (ValidationException ex)
        {
            var message = string.Join(" ", ex.Errors.Select(error => error.ErrorMessage));
            logger.LogInformation(ex, "Validation failed while running an operation.");
            snackbar.Add(message, MudBlazor.Severity.Warning);
            return (new OperationOutcome(false, message), default);
        }
        catch (InvalidOperationException ex)
        {
            logger.LogWarning(ex, "Invalid operation while running an operation.");
            snackbar.Add(ex.Message, MudBlazor.Severity.Warning);
            return (new OperationOutcome(false, ex.Message), default);
        }
        catch (KeyNotFoundException ex)
        {
            logger.LogWarning(ex, "Missing item while running an operation.");
            snackbar.Add(ex.Message, MudBlazor.Severity.Warning);
            return (new OperationOutcome(false, ex.Message), default);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            return (new OperationOutcome(false, null), default);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Unexpected failure while running an operation.");
            snackbar.Add(failureMessage, MudBlazor.Severity.Error);
            return (new OperationOutcome(false, failureMessage), default);
        }
    }
}
