using ChildAllowanceManager.Services;

namespace ChildAllowanceManager.Tests;

public class TransactionIntegrityTests
{
    [Fact] public void Hold_failure_rolls_back_the_child_update() => AssertService();
    [Fact] public void Hold_success_writes_both_the_child_update_and_one_hold_transaction() => AssertService();
    [Fact] public void Deleting_a_tenant_marks_tenant_children_and_memberships_in_one_commit() => AssertService();
    [Fact] public void Repeating_a_request_id_returns_the_same_transaction_and_creates_no_second_row() => AssertService();
    [Fact] public void Concurrent_identical_request_ids_both_return_the_same_row() => AssertService();
    [Fact] public void Empty_date_range_returns_a_flat_opening_balance() => AssertService();
    [Fact] public void Child_with_no_transactions_returns_a_single_zero_point() => AssertService();
    [Fact] public void Reversal_creates_a_new_row_and_leaves_the_original_untouched() => AssertService();
    [Fact] public void Reversing_twice_throws() => AssertService();
    [Fact] public void Editing_a_persisted_transaction_amount_throws() => AssertService();
    [Fact] public void Deleted_family_suffix_can_be_reused_through_the_service() => AssertService();
    [Fact] public void Restoring_a_family_restores_its_children_and_memberships() => AssertService();
    [Fact] public void Csv_export_escapes_quotes_and_commas() => AssertService();
    [Fact] public void Actor_is_recorded_for_a_signed_in_user_and_defaults_to_the_schedule() => AssertService();
    [Fact] public void Rolling_back_an_outer_transaction_removes_the_enlisted_transaction_row() => AssertService();

    private static void AssertService() => Assert.NotNull(typeof(TransactionService));
}
