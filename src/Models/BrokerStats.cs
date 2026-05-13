namespace MQTop.Models;

public class BrokerStats
{
    public string Version { get; set; } = "—";
    public string Uptime { get; set; } = "—";
    public int ClientsConnected { get; set; }
    public int ClientsDisconnected { get; set; }
    public int ClientsMaximum { get; set; }
    public int ClientsTotal { get; set; }
    public int ClientsExpired { get; set; }

    public long MessagesReceived { get; set; }
    public long MessagesSent { get; set; }
    public long MessagesDropped { get; set; }
    public int MessagesInflight { get; set; }
    public int MessagesStored { get; set; }

    public long PublishMessagesReceived { get; set; }
    public long PublishMessagesSent { get; set; }
    public long PublishMessagesDropped { get; set; }

    public long BytesReceived { get; set; }
    public long BytesSent { get; set; }

    public double LoadConnections1Min { get; set; }
    public double LoadConnections5Min { get; set; }
    public double LoadConnections15Min { get; set; }

    public double LoadMsgReceived1Min { get; set; }
    public double LoadMsgReceived5Min { get; set; }
    public double LoadMsgReceived15Min { get; set; }

    public double LoadMsgSent1Min { get; set; }
    public double LoadMsgSent5Min { get; set; }
    public double LoadMsgSent15Min { get; set; }

    public double LoadMsgDropped1Min { get; set; }

    public double LoadBytesReceived1Min { get; set; }
    public double LoadBytesSent1Min { get; set; }

    public int SubscriptionsCount { get; set; }
    public int RetainedMessages { get; set; }

    public bool IsConnected { get; set; }
    public DateTime? LastConnectedAt { get; set; }
    public DateTime? LastDisconnectedAt { get; set; }
    public DateTime? LastReconnectAttemptAt { get; set; }
    public int ReconnectAttempts { get; set; }
}
