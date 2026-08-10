
namespace ChildAllowanceManager.Common.Models;

public class AllowanceTransaction: BaseItem
{
    public decimal Balance { get; set; }
    public decimal TransactionAmount { get; set; }
    public string Description { get; set; } = string.Empty;
    public string ChildId { get; set; } = string.Empty;
    public string TenantId { get; set; } = string.Empty;
    public DateTimeOffset TransactionTimestamp { get; set; }
    public TransactionType TransactionType { get; set; }

    public DateTime? AllowanceDate { get; set; }

    /// Email of the person who performed this action, lowercased. Null for
    /// transactions created by the scheduled allowance job.
    public string? ActorEmail { get; set; }

    /// Display name of the actor at the time of the action, or "Allowance schedule"
    /// for job-created rows. Denormalised on purpose - the audit trail must not
    /// change when a user later renames themselves.
    public string ActorName { get; set; } = "Allowance schedule";

    /// Caller-supplied de-duplication key. When set, a second AddTransaction with the
    /// same (TenantId, RequestId) returns the existing row instead of creating another.
    public string? RequestId { get; set; }

    /// When this row is a correction, the Id of the transaction it reverses.
    /// Corrections are additive - history is never edited. See P04-T6.
    public string? ReversesTransactionId { get; set; }

    /// Why a correction was made. Required when ReversesTransactionId is set.
    public string? CorrectionReason { get; set; }

    public ChildConfiguration? Child { get; set; }
    public TenantConfiguration? Tenant { get; set; }

}
