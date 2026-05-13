namespace MQTop.Models;

public enum AlertLevel
{
    Warning,
    Critical
}

public class Alert
{
    public AlertLevel Level { get; set; }
    public string Message { get; set; } = string.Empty;
}
