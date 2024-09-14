namespace ChildAllowanceManager.Common.Models;

public record ChildWithBalanceHistory(string ChildId, string TenantId, BalanceHistoryEntry[] BalanceHistory);