namespace ChildAllowanceManager.Common.Models;

public record ChildWithBalanceHistory(string ChildId, string ChildName, string TenantId, BalanceHistoryEntry[] BalanceHistory);