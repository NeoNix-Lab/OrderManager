using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;

namespace RowanBridge;

public enum RowanBridgeCommandType
{
    Custom = 0,
    StartStrategy,
    StopStrategy,
    RefreshSnapshot,
    ApplySettings,
    ResetMetrics
}

public sealed class RowanBridgeCommand
{
    public RowanBridgeCommand(
        RowanBridgeCommandType type,
        IReadOnlyDictionary<string, object?>? payload = null,
        string? correlationId = null,
        DateTime? timestampUtc = null)
    {
        Type = type;
        Payload = payload ??
                  new ReadOnlyDictionary<string, object?>(new Dictionary<string, object?>());
        CorrelationId = string.IsNullOrWhiteSpace(correlationId)
            ? Guid.NewGuid().ToString("N")
            : correlationId!;
        TimestampUtc = timestampUtc ?? DateTime.UtcNow;
    }

    public RowanBridgeCommandType Type { get; }

    public IReadOnlyDictionary<string, object?> Payload { get; }

    public string CorrelationId { get; }

    public DateTime TimestampUtc { get; }
}

public sealed class RowanBridgeCommandEventArgs : EventArgs
{
    public RowanBridgeCommandEventArgs(RowanBridgeCommand command)
    {
        Command = command ?? throw new ArgumentNullException(nameof(command));
    }

    public RowanBridgeCommand Command { get; }
}
