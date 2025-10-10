using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;

namespace RowanBridge;

/// <summary>
/// Describes a high level lifecycle status for the Rowan strategy.
/// </summary>
public enum StrategyLifecycleStatus
{
    Unknown = 0,
    Created,
    Initializing,
    Running,
    Paused,
    Stopped,
    Completed,
    Faulted
}

/// <summary>
/// Represents a metric/value pair published by the strategy.
/// </summary>
public sealed class RowanMetric
{
    public RowanMetric(string name, double value, string? unit = null, DateTime? timestampUtc = null)
    {
        Name = string.IsNullOrWhiteSpace(name) ? throw new ArgumentException("Metric name cannot be empty.", nameof(name)) : name;
        Value = value;
        Unit = unit;
        TimestampUtc = timestampUtc ?? DateTime.UtcNow;
    }

    public string Name { get; }

    public double Value { get; }

    public string? Unit { get; }

    public DateTime TimestampUtc { get; }
}

/// <summary>
/// Snapshot of the most relevant data exposed by the Rowan strategy.
/// </summary>
public sealed class RowanStrategySnapshot
{
    private RowanStrategySnapshot(
        DateTime timestampUtc,
        StrategyLifecycleStatus lifecycle,
        double netProfit,
        int activeOrders,
        int openPositions,
        IReadOnlyList<RowanMetric> metrics,
        IReadOnlyDictionary<string, object?> customData)
    {
        TimestampUtc = timestampUtc;
        Lifecycle = lifecycle;
        NetProfit = netProfit;
        ActiveOrders = activeOrders;
        OpenPositions = openPositions;
        Metrics = metrics;
        CustomData = customData;
    }

    public DateTime TimestampUtc { get; }

    public StrategyLifecycleStatus Lifecycle { get; }

    public double NetProfit { get; }

    public int ActiveOrders { get; }

    public int OpenPositions { get; }

    public IReadOnlyList<RowanMetric> Metrics { get; }

    public IReadOnlyDictionary<string, object?> CustomData { get; }

    public static RowanStrategySnapshot Empty { get; } = new RowanStrategySnapshot(
        DateTime.MinValue,
        StrategyLifecycleStatus.Unknown,
        0d,
        0,
        0,
        Array.AsReadOnly(Array.Empty<RowanMetric>()),
        new ReadOnlyDictionary<string, object?>(new Dictionary<string, object?>()));

    public static RowanStrategySnapshot Create(
        DateTime timestampUtc,
        StrategyLifecycleStatus lifecycle,
        double netProfit,
        int activeOrders,
        int openPositions,
        IEnumerable<RowanMetric>? metrics = null,
        IReadOnlyDictionary<string, object?>? customData = null)
    {
        var metricList = metrics != null
            ? Array.AsReadOnly(new List<RowanMetric>(metrics).ToArray())
            : Empty.Metrics;

        var data = customData ?? Empty.CustomData;

        return new RowanStrategySnapshot(
            timestampUtc,
            lifecycle,
            netProfit,
            activeOrders,
            openPositions,
            metricList,
            data);
    }

    public RowanStrategySnapshot WithCustomData(IReadOnlyDictionary<string, object?> customData) =>
        new RowanStrategySnapshot(
            TimestampUtc,
            Lifecycle,
            NetProfit,
            ActiveOrders,
            OpenPositions,
            Metrics,
            customData);
}
