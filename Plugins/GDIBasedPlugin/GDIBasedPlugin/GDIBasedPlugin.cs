using System;
using System.Collections.Generic;
using System.Drawing;
using RowanBridge;
using TradingPlatform.BusinessLayer;
using TradingPlatform.PresentationLayer.Plugins;

namespace GDIBasedPlugin
{
    public class GDIBasedPlugin : Plugin
    {
        private GDIRenderer gdiRenderer;
        private IDisposable notificationSubscription;
        private EventHandler<bool>? bridgeStatusHandler;

        /// <summary>
        /// Plugin meta information
        /// </summary>
        public static PluginInfo GetInfo()
        {
            var windowParameters = NativeWindowParameters.Panel;
            windowParameters.AllowDrop = true;
            windowParameters.BrowserUsageType = BrowserUsageType.None;

            return new PluginInfo()
            {
                Name = "GDIBasedPlugin_2",
                Title = "GDIBasedPlugin_2",
                Group = PluginGroup.Misc,
                ShortName = "GDI",
                SortIndex = 35,
                AllowSettings = true,
                WindowParameters = windowParameters,
                CustomProperties = new Dictionary<string, object>()
                {
                    {PluginInfo.Const.ALLOW_MANUAL_CREATION, true }
                }
            };
        }

        /// <summary>
        /// Default plugin size - can be an absolute value or a multiple of UnitSize (UnitSize depends on the monitor resolution)
        /// </summary>
        public override Size DefaultSize => new Size(this.UnitSize.Width * 1, this.UnitSize.Height * 2);

        /// <summary>
        /// Initialize called once on plugin creation
        /// </summary>
        public override void Initialize()
        {
            base.Initialize();

            this.gdiRenderer = new GDIRenderer(this.Window.CreateRenderingControl("GDIRenderer"));
            var hub = RowanBridgeHub.Instance;
            bridgeStatusHandler = (_, connected) =>
            {
                this.gdiRenderer?.UpdateBridgeConnection(connected);
                if (connected)
                    this.gdiRenderer?.UpdateBridgeHeartbeat(RowanBridgeHub.Instance.LastNotificationUtc);
            };
            hub.NotificationSubscriptionChanged += bridgeStatusHandler;
            notificationSubscription = hub.SubscribeToNotifications(OnBridgeNotification);
            this.gdiRenderer?.UpdateBridgeConnection(hub.HasNotificationSubscribers);
            if (hub.LastNotificationUtc != DateTime.MinValue)
                this.gdiRenderer?.UpdateBridgeHeartbeat(hub.LastNotificationUtc);
        }

        /// <summary>
        /// Populate called on plugin creation and each time when any connection get connected/disconnected
        /// </summary>
        public override void Populate(PluginParameters args = null)
        {
            base.Populate(args);

            // Redraw renderer
            this.gdiRenderer?.RedrawBufferedGraphic();
        }

        public override void Dispose()
        {
            notificationSubscription?.Dispose();
            notificationSubscription = null;
            if (bridgeStatusHandler != null)
            {
                RowanBridgeHub.Instance.NotificationSubscriptionChanged -= bridgeStatusHandler;
                bridgeStatusHandler = null;
            }

            if (this.gdiRenderer != null)
            {
                this.gdiRenderer.UpdateBridgeConnection(false);
                this.gdiRenderer.Dispose();
                this.gdiRenderer = null;
            }

            base.Dispose();
        }

        public override IList<SettingItem> Settings
        {
            get
            {
                var result = base.Settings;

                if (this.gdiRenderer != null)
                    result.Add(new SettingItemColor("Color", this.gdiRenderer.Color));

                return result;
            }
            set
            {
                base.Settings = value;

                if (value.GetItemByPath("Color") is SettingItemColor color && this.gdiRenderer != null)
                {
                    this.gdiRenderer.Color = (Color)color.Value;
                    this.gdiRenderer.RedrawBufferedGraphic();
                }
            }
        }

        protected override void OnLayoutUpdated()
        {
            base.OnLayoutUpdated();

            if (this.gdiRenderer != null)
                this.gdiRenderer.Layout.Margin = this.NonClientMargin;
        }

        private void OnBridgeNotification(object sender, RowanNotification notification)
        {
            this.gdiRenderer?.AddLogEntry(notification);
        }
    }
}
