using ChildAllowanceManager.Common.Models;
using Microsoft.AspNetCore.Components;

namespace ChildAllowanceManager.Components;

public partial class BalanceHistoryTable : CancellableComponentBase
{
    [Parameter]
    public IEnumerable<ChildWithBalanceHistory> History { get; set; } = [];

    /// The colour index a child owns elsewhere on the page, so the table's swatches match
    /// their card border and chart line. Defaults to the first colour when not supplied.
    [Parameter]
    public Func<string, int>? AccentIndex { get; set; }

    private ChildWithBalanceHistory[] _children = [];
    private BalanceHistoryTableRow[] _rows = [];

    protected override void OnParametersSet()
    {
        _children = (History ?? []).ToArray();
        _rows = BuildRows(_children);
    }

    private int AccentIndexFor(string childId) => AccentIndex?.Invoke(childId) ?? 0;

    private static BalanceHistoryTableRow[] BuildRows(ChildWithBalanceHistory[] children)
    {
        var dates = children
            .SelectMany(child => child.BalanceHistory)
            .Select(entry => entry.Timestamp.UtcDateTime.Date)
            .Distinct()
            .OrderBy(date => date)
            .ToArray();

        return dates.Select(date => new BalanceHistoryTableRow(
            date,
            children.Select(child => child.BalanceHistory
                .Where(entry => entry.Timestamp.UtcDateTime.Date == date)
                .Select(entry => (decimal?)entry.Balance)
                .LastOrDefault())
                .ToArray()))
            .ToArray();
    }

    private sealed record BalanceHistoryTableRow(DateTime Date, decimal?[] Balances);
}
