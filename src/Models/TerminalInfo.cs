namespace MQTop.Models;

public class TerminalInfo
{
    public string SerialNo { get; set; } = string.Empty;
    public string Vendor { get; set; } = string.Empty;
    public string Status { get; set; } = "offline";
    public DateTime LastSeen { get; set; }
    public DateTime ConnectedAt { get; set; }
    public long CmdCount;
    public long ResponseCount;
    public long DroppedCount;
    public string LastCommandType { get; set; } = "—";
    public DateTime LastCommandAt { get; set; }

    public double SuccessRate
    {
        get
        {
            var cmd = Interlocked.Read(ref CmdCount);
            if (cmd == 0) return 100.0;
            var resp = Interlocked.Read(ref ResponseCount);
            return Math.Min(100.0, (double)resp / cmd * 100.0);
        }
    }
}
