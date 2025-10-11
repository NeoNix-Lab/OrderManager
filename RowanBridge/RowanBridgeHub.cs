using System;
using System.Threading;

namespace RowanBridge;

/// <summary>
/// Singleton hub that coordinates data exchange between the Rowan strategy and any UI components.
/// </summary>
public sealed class RowanBridgeHub
{
    private static readonly Lazy<RowanBridgeHub> _lazy = new(() => new RowanBridgeHub());

    private readonly object _sync = new();
    private RowanStrategySnapshot _snapshot = RowanStrategySnapshot.Empty;
    private RowanSettings _settings = RowanSettings.Empty;
    private int _notificationSubscriberCount;
    private long _lastStatusChangeTicks;
    private long _lastNotificationTicks;

    private RowanBridgeHub()
    {
    }

    public static RowanBridgeHub Instance => _lazy.Value;

    public event EventHandler<RowanStrategySnapshotChangedEventArgs>? SnapshotUpdated;

    public event EventHandler<RowanSettingsChangedEventArgs>? SettingsUpdated;

    public event EventHandler<RowanBridgeCommandEventArgs>? CommandRequested;

    public event EventHandler<RowanNotification>? NotificationPublished;

    /// <summary>
    /// Raised whenever notification subscribers change from 0 to 1 or vice versa.
    /// The bool payload indicates whether at least one subscriber is currently active.
    /// </summary>
    public event EventHandler<bool>? NotificationSubscriptionChanged;

    public RowanStrategySnapshot CurrentSnapshot
    {
        get
        {
            lock (_sync)
            {
                return _snapshot;
            }
        }
    }

    public RowanSettings CurrentSettings
    {
        get
        {
            lock (_sync)
            {
                return _settings;
            }
        }
    }

    public bool HasNotificationSubscribers => Volatile.Read(ref _notificationSubscriberCount) > 0;

    public DateTime LastNotificationUtc
    {
        get
        {
            long ticks = Interlocked.Read(ref _lastNotificationTicks);
            return ticks == 0 ? DateTime.MinValue : new DateTime(ticks, DateTimeKind.Utc);
        }
    }

    public DateTime LastStatusChangeUtc
    {
        get
        {
            long ticks = Interlocked.Read(ref _lastStatusChangeTicks);
            return ticks == 0 ? DateTime.MinValue : new DateTime(ticks, DateTimeKind.Utc);
        }
    }

    public void PublishSnapshot(RowanStrategySnapshot snapshot)
    {
        if (snapshot == null)
            throw new ArgumentNullException(nameof(snapshot));

        EventHandler<RowanStrategySnapshotChangedEventArgs>? handler;
        RowanStrategySnapshot previous;
        lock (_sync)
        {
            previous = _snapshot;
            _snapshot = snapshot;
            handler = SnapshotUpdated;
        }

        handler?.Invoke(this, new RowanStrategySnapshotChangedEventArgs(previous, snapshot));
    }

    public void PublishSettings(RowanSettings settings)
    {
        if (settings == null)
            throw new ArgumentNullException(nameof(settings));

        EventHandler<RowanSettingsChangedEventArgs>? handler;
        RowanSettings previous;
        lock (_sync)
        {
            previous = _settings;
            _settings = settings;
            handler = SettingsUpdated;
        }

        handler?.Invoke(this, new RowanSettingsChangedEventArgs(previous, settings));
    }

    public void PublishNotification(RowanNotification notification)
    {
        if (notification == null)
            throw new ArgumentNullException(nameof(notification));

        Interlocked.Exchange(ref _lastNotificationTicks, notification.TimestampUtc.Ticks);
        NotificationPublished?.Invoke(this, notification);
    }

    public void RequestCommand(RowanBridgeCommand command)
    {
        if (command == null)
            throw new ArgumentNullException(nameof(command));

        CommandRequested?.Invoke(this, new RowanBridgeCommandEventArgs(command));
    }

    public IDisposable SubscribeToSnapshots(EventHandler<RowanStrategySnapshotChangedEventArgs> handler)
    {
        SnapshotUpdated += handler ?? throw new ArgumentNullException(nameof(handler));
        return new DisposableAction(() => SnapshotUpdated -= handler);
    }

    public IDisposable SubscribeToSettings(EventHandler<RowanSettingsChangedEventArgs> handler)
    {
        SettingsUpdated += handler ?? throw new ArgumentNullException(nameof(handler));
        return new DisposableAction(() => SettingsUpdated -= handler);
    }

    public IDisposable SubscribeToNotifications(EventHandler<RowanNotification> handler)
    {
        NotificationPublished += handler ?? throw new ArgumentNullException(nameof(handler));
        UpdateNotificationSubscriberCount(+1);
        return new DisposableAction(() =>
        {
            NotificationPublished -= handler;
            UpdateNotificationSubscriberCount(-1);
        });
    }

    public IDisposable SubscribeToCommands(EventHandler<RowanBridgeCommandEventArgs> handler)
    {
        CommandRequested += handler ?? throw new ArgumentNullException(nameof(handler));
        return new DisposableAction(() => CommandRequested -= handler);
    }

    private void UpdateNotificationSubscriberCount(int delta)
    {
        int newValue = Interlocked.Add(ref _notificationSubscriberCount, delta);

        if (newValue < 0)
        {
            Interlocked.Exchange(ref _notificationSubscriberCount, 0);
            newValue = 0;
        }

        bool shouldRaise =
            (delta > 0 && newValue == 1) ||
            (delta < 0 && newValue == 0);

        if (shouldRaise)
        {
            Interlocked.Exchange(ref _lastStatusChangeTicks, DateTime.UtcNow.Ticks);
            NotificationSubscriptionChanged?.Invoke(this, newValue > 0);
        }
    }
}

public sealed class RowanStrategySnapshotChangedEventArgs : EventArgs
{
    public RowanStrategySnapshotChangedEventArgs(RowanStrategySnapshot previous, RowanStrategySnapshot current)
    {
        Previous = previous ?? throw new ArgumentNullException(nameof(previous));
        Current = current ?? throw new ArgumentNullException(nameof(current));
    }

    public RowanStrategySnapshot Previous { get; }

    public RowanStrategySnapshot Current { get; }
}

public sealed class RowanSettingsChangedEventArgs : EventArgs
{
    public RowanSettingsChangedEventArgs(RowanSettings previous, RowanSettings current)
    {
        Previous = previous ?? throw new ArgumentNullException(nameof(previous));
        Current = current ?? throw new ArgumentNullException(nameof(current));
    }

    public RowanSettings Previous { get; }

    public RowanSettings Current { get; }
}
