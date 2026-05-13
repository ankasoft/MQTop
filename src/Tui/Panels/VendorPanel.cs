using Spectre.Console;
using Spectre.Console.Rendering;
using MQTop.Services;
using MQTop.Tui.Helpers;

namespace MQTop.Tui.Panels;

public static class VendorPanel
{
    public static IRenderable Render(DashboardState state, string vendorFilter)
    {
        var vendors = state.Vendors.Values
            .Where(v => vendorFilter == "All" ||
                        string.Equals(v.Name, vendorFilter, StringComparison.OrdinalIgnoreCase))
            .OrderBy(v => v.Name, StringComparer.OrdinalIgnoreCase)
            .ToList();

        if (vendors.Count == 0)
        {
            return new Panel(new Markup("[grey]No vendor data yet[/]"))
                .Header("[bold]Vendors[/]")
                .Border(BoxBorder.Rounded)
                .Expand();
        }

        var table = new Table()
            .Border(TableBorder.MinimalHeavyHead)
            .AddColumn("[grey]Vendor[/]")
            .AddColumn(new TableColumn("[grey]Online[/]").RightAligned())
            .AddColumn(new TableColumn("[grey]Offline[/]").RightAligned())
            .AddColumn(new TableColumn("[grey]Total[/]").RightAligned())
            .AddColumn(new TableColumn("[grey]Cmd[/]").RightAligned())
            .AddColumn(new TableColumn("[grey]Resp[/]").RightAligned())
            .AddColumn(new TableColumn("[grey]Drop[/]").RightAligned())
            .AddColumn(new TableColumn("[grey]Success %[/]").RightAligned());

        foreach (var v in vendors)
        {
            var rate = v.SuccessRate;
            var rateColor = ColorHelper.SuccessRate(rate);
            table.AddRow(
                $"[bold]{Markup.Escape(v.Name)}[/]",
                $"[green]{v.OnlineCount}[/]",
                $"[red]{v.OfflineCount}[/]",
                v.TotalTerminals.ToString(),
                FormatHelper.Number(Interlocked.Read(ref v.TotalCmdCount)),
                FormatHelper.Number(Interlocked.Read(ref v.TotalResponseCount)),
                $"[{ColorHelper.Dropped(Interlocked.Read(ref v.TotalDroppedCount))}]{FormatHelper.Number(Interlocked.Read(ref v.TotalDroppedCount))}[/]",
                $"[{rateColor}]{FormatHelper.Percent(rate)}[/]");
        }

        return new Panel(table)
            .Header("[bold]Vendors[/]", Justify.Left)
            .Border(BoxBorder.Rounded)
            .Expand();
    }
}
