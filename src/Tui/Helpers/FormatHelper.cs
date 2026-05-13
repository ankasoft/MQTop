using System.Globalization;

namespace MQTop.Tui.Helpers;

public static class FormatHelper
{
    public static string Uptime(TimeSpan ts)
    {
        if (ts.TotalSeconds < 1) return "—";
        var parts = new List<string>();
        if (ts.Days > 0) parts.Add($"{ts.Days}d");
        if (ts.Hours > 0) parts.Add($"{ts.Hours}h");
        if (ts.Minutes > 0) parts.Add($"{ts.Minutes}m");
        if (parts.Count == 0) parts.Add($"{ts.Seconds}s");
        return string.Join(" ", parts);
    }

    public static string UptimeFromSeconds(string secondsRaw)
    {
        var trimmed = (secondsRaw ?? string.Empty).Trim();
        var spaceIdx = trimmed.IndexOf(' ');
        if (spaceIdx > 0) trimmed = trimmed[..spaceIdx];
        if (!long.TryParse(trimmed, NumberStyles.Any, CultureInfo.InvariantCulture, out var seconds))
            return secondsRaw ?? "—";
        return Uptime(TimeSpan.FromSeconds(seconds));
    }

    public static string Bytes(long value)
    {
        if (value < 0) return "—";
        double v = value;
        string[] units = { "B", "KB", "MB", "GB", "TB" };
        int u = 0;
        while (v >= 1024 && u < units.Length - 1)
        {
            v /= 1024;
            u++;
        }
        return u == 0
            ? $"{(long)v} {units[u]}"
            : $"{v.ToString("0.##", CultureInfo.InvariantCulture)} {units[u]}";
    }

    public static string BytesPerSecond(double value) => $"{Bytes((long)value)}/s";

    public static string DurationSince(DateTime utcTimestamp)
    {
        if (utcTimestamp == default) return "—";
        var diff = DateTime.UtcNow - utcTimestamp;
        if (diff.TotalSeconds < 1) return "now";
        if (diff.TotalSeconds < 60) return $"{(int)diff.TotalSeconds}s";
        if (diff.TotalMinutes < 60) return $"{(int)diff.TotalMinutes}m";
        if (diff.TotalHours < 24) return $"{(int)diff.TotalHours}h";
        return $"{(int)diff.TotalDays}d";
    }

    public static string Load(double value) =>
        value.ToString("0.00", CultureInfo.InvariantCulture);

    public static string Number(long value) =>
        value.ToString("N0", CultureInfo.InvariantCulture);

    public static string Number(int value) =>
        value.ToString("N0", CultureInfo.InvariantCulture);

    public static string Percent(double value) =>
        value.ToString("0.00", CultureInfo.InvariantCulture) + "%";
}
