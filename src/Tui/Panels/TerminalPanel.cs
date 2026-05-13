using Spectre.Console;
using Spectre.Console.Rendering;
using MQTop.Services;
using MQTop.Tui.Helpers;

namespace MQTop.Tui.Panels;

public static class TerminalPanel
{
    private const int MaxVisible = 20;

    public static IRenderable Render(DashboardState state, string vendorFilter, int scrollOffset)
    {
        var terminals = state.GetAllTerminals()
            .Where(t => vendorFilter == "All" ||
                        string.Equals(t.Vendor, vendorFilter, StringComparison.OrdinalIgnoreCase))
            .ToList();

        if (terminals.Count == 0)
        {
            return new Panel(new Markup("[grey italic]No terminals connected yet[/]"))
                .Header("[bold]Terminals[/]")
                .Border(BoxBorder.Rounded)
                .Expand();
        }

        var total = terminals.Count;
        var offset = Math.Clamp(scrollOffset, 0, Math.Max(0, total - 1));
        var page = terminals.Skip(offset).Take(MaxVisible).ToList();

        var table = new Table()
            .Border(TableBorder.MinimalHeavyHead)
            .Expand()
            .AddColumn("")
            .AddColumn("[grey]SerialNo[/]")
            .AddColumn("[grey]Vendor[/]")
            .AddColumn(new TableColumn("[grey]Cmd[/]").RightAligned())
            .AddColumn(new TableColumn("[grey]Resp[/]").RightAligned())
            .AddColumn(new TableColumn("[grey]Drop[/]").RightAligned())
            .AddColumn(new TableColumn("[grey]Success %[/]").RightAligned())
            .AddColumn("[grey]Last command[/]")
            .AddColumn(new TableColumn("[grey]LastSeen[/]").RightAligned());

        foreach (var t in page)
        {
            var rate = t.SuccessRate;
            var rateColor = ColorHelper.SuccessRate(rate);
            var seenColor = ColorHelper.LastSeen(t.LastSeen, t.Status);
            var dropped = Interlocked.Read(ref t.DroppedCount);

            table.AddRow(
                ColorHelper.StatusDot(t.Status),
                $"[white]{Markup.Escape(t.SerialNo)}[/]",
                Markup.Escape(t.Vendor),
                FormatHelper.Number(Interlocked.Read(ref t.CmdCount)),
                FormatHelper.Number(Interlocked.Read(ref t.ResponseCount)),
                $"[{ColorHelper.Dropped(dropped)}]{FormatHelper.Number(dropped)}[/]",
                $"[{rateColor}]{FormatHelper.Percent(rate)}[/]",
                Markup.Escape(t.LastCommandType ?? "—"),
                $"[{seenColor}]{FormatHelper.DurationSince(t.LastSeen)}[/]");
        }

        var header = total > MaxVisible
            ? $"[bold]Terminals[/]  [grey]({offset + 1}-{offset + page.Count} / {total})  ↑/↓ scroll[/]"
            : $"[bold]Terminals[/]  [grey]({total})[/]";

        return new Panel(table)
            .Header(header, Justify.Left)
            .Border(BoxBorder.Rounded)
            .Expand();
    }
}
