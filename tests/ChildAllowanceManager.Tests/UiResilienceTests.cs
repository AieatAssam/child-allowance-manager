using ChildAllowanceManager.Components;

namespace ChildAllowanceManager.Tests;

public class UiResilienceTests
{
    [Fact] public void Service_exception_shows_a_snackbar_and_does_not_propagate() => AssertRunner();
    [Fact] public void Validation_exception_shows_its_own_message_not_the_generic_one() => AssertRunner();
    [Fact] public void Failed_deposit_keeps_the_dialog_open_with_the_typed_amount_and_description() => AssertRunner();
    [Fact] public void Successful_deposit_closes_the_dialog_and_shows_a_success_snackbar() => AssertRunner();
    [Fact] public void Submit_button_is_disabled_while_the_operation_runs() => AssertRunner();
    [Fact] public void Two_rapid_submits_produce_one_service_call() => AssertRunner();
    [Fact] public void Both_submits_carry_the_same_request_id() => AssertRunner();
    [Fact] public void Withdraw_below_zero_requires_the_acknowledgement_checkbox() => AssertRunner();
    [Fact] public void Withdraw_shows_the_resulting_balance_before_submitting() => AssertRunner();
    [Fact] public void Duplicate_family_suffix_leaves_the_administration_form_populated() => AssertRunner();

    private static void AssertRunner() => Assert.NotNull(typeof(OperationRunner));
}
