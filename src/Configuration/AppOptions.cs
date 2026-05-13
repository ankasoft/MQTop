namespace MQTop.Configuration;

public class MqttOptions
{
    public string BrokerUrl { get; set; } = "mqtt://localhost:1883";
    public string Username { get; set; } = string.Empty;
    public string Password { get; set; } = string.Empty;
    public string ClientId { get; set; } = "mosquitto-monitor";
    public int SysTopicInterval { get; set; } = 10;
}

public class DashboardOptions
{
    public int RefreshIntervalMs { get; set; } = 1000;
    public int TerminalTimeoutSeconds { get; set; } = 300;
    public List<string> Vendors { get; set; } = new();
}

public class AlertsOptions
{
    public long DroppedMessagesThreshold { get; set; } = 1;
    public int InflightMessagesThreshold { get; set; } = 10;
    public int StoredMessagesThreshold { get; set; } = 50;
    public double SuccessRateWarningThreshold { get; set; } = 99.0;
    public double SuccessRateCriticalThreshold { get; set; } = 95.0;
}
