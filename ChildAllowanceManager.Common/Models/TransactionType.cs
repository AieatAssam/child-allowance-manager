namespace ChildAllowanceManager.Common.Models;

/// Persisted as an integer. Numeric values are pinned to existing rows.
public enum TransactionType
{
    DailyAllowance = 0,
    BirthdayAllowance = 1,
    Withdrawal = 2,
    Deposit = 3,

    /// Reserved. No product flow. Do not surface in the UI.
    Transfer = 4,

    /// Balance corrections, including reversing entries.
    Adjustment = 5,

    /// Reserved. No product flow. Do not surface in the UI.
    Interest = 6,

    Hold = 7,

    /// Reserved. No product flow. Do not surface in the UI.
    Other = 8
}
