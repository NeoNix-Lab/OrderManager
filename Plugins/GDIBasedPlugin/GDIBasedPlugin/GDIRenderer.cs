using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Linq;
using RowanBridge;
using TradingPlatform.BusinessLayer;
using TradingPlatform.BusinessLayer.Native;
using TradingPlatform.PresentationLayer.Plugins;
using TradingPlatform.PresentationLayer.Renderers;
using TradingPlatform.PresentationLayer;
using PluginApplication = TradingPlatform.PresentationLayer.Plugins.Application;

namespace GDIBasedPlugin
{
    public class GDIRenderer : Renderer
    {
        #region properties

        private BufferedGraphic bufferedGraphic;

        public Color Color { get; set; }

        #endregion

        private readonly Font menuFont = new Font("Segoe UI Semibold", 11f, FontStyle.Bold);
        private readonly Font tableHeaderFont = new Font("Segoe UI Semibold", 10f, FontStyle.Bold);
        private readonly Font tableRowFont = new Font("Segoe UI", 10f, FontStyle.Regular);
        private readonly Font chartAxisFont = new Font("Segoe UI", 9f, FontStyle.Regular);

        private SolidBrush menuBackgroundBrush = new SolidBrush(Color.FromArgb(35, 35, 40));
        private SolidBrush menuTextBrush = new SolidBrush(Color.White);
        private SolidBrush tableMenuBrush = new SolidBrush(Color.FromArgb(40, 45, 55));
        private SolidBrush tableMenuTextBrush = new SolidBrush(Color.WhiteSmoke);
        private SolidBrush tableHeaderBrush = new SolidBrush(Color.FromArgb(45, 50, 60));
        private SolidBrush tableHeaderTextBrush = new SolidBrush(Color.White);
        private SolidBrush tableRowBrushEven = new SolidBrush(Color.FromArgb(35, 35, 40));
        private SolidBrush tableRowBrushOdd = new SolidBrush(Color.FromArgb(42, 42, 48));
        private SolidBrush tableRowTextBrush = new SolidBrush(Color.WhiteSmoke);
        private SolidBrush tableRowMutedTextBrush = new SolidBrush(Color.FromArgb(180, 180, 190));
        private SolidBrush chartBackgroundBrush = new SolidBrush(Color.FromArgb(30, 30, 36));
        private SolidBrush chartFillBrush = new SolidBrush(Color.FromArgb(60, 0, 122, 204));
        private SolidBrush chartAxisTextBrush = new SolidBrush(Color.FromArgb(180, 180, 190));
        private SolidBrush logHeaderBrush = new SolidBrush(Color.FromArgb(38, 42, 50));
        private SolidBrush logBackgroundBrush = new SolidBrush(Color.FromArgb(24, 27, 33));
        private SolidBrush logTextBrush = new SolidBrush(Color.FromArgb(220, 220, 225));

        private Pen tableGridPen = new Pen(Color.FromArgb(60, 255, 255, 255), 1f);
        private Pen tableMenuSeparatorPen = new Pen(Color.FromArgb(80, 255, 255, 255), 1f);
        private Pen chartAxisPen = new Pen(Color.FromArgb(80, 255, 255, 255), 1f);
        private Pen chartLinePen = new Pen(Color.FromArgb(255, 0, 122, 204), 2f);
        private Pen logSeparatorPen = new Pen(Color.FromArgb(55, 255, 255, 255), 1f);

        private Color panelBackgroundColor = Color.FromArgb(30, 30, 36);

        private readonly string[] menuItems = { "Overview", "Orders", "Exposure", "Logs" };
        private readonly (string Id, string Label, bool HasDropdown)[] tableMenuTemplate =
        {
            ("filter", "Filter", true),
            ("group", "Group", false),
            ("export", "Export", false),
            ("reload", "Reload", false)
        };

        private readonly (string Symbol, string Side, string Quantity, double Pnl)[] sampleOrders =
        {
            ("6EU4", "Buy", "3", 125.30),
            ("NQU4", "Sell", "1", -83.10),
            ("GCZ4", "Buy", "2", 342.55),
            ("CLX4", "Sell", "2", 18.90),
            ("FDAXZ4", "Buy", "1", -45.25),
            ("ZNZ4", "Buy", "4", 210.75)
        };

        private double[] chartValues = Enumerable.Range(0, 120)
            .Select(i =>
            {
                var t = i / 12d;
                return Math.Sin(t) * 6 + Math.Cos(t / 2d) * 2 + 50;
            })
            .ToArray();

        private readonly object interactionSync = new();
        private readonly List<MenuEntry> tableMenuEntries = new();
        private readonly string[] filterOptions = { "All Orders", "Active Orders", "Pending Orders", "Closed Orders" };
        private const int DropdownItemHeight = 22;

        private bool filterDropdownOpen;
        private int hoveredMenuIndex = -1;
        private int hoveredDropdownIndex = -1;
        private Rectangle dropdownBounds = Rectangle.Empty;
        private string selectedFilterOption = "All Orders";
        private readonly object logSync = new();
        private readonly LinkedList<UiLogEntry> logEntries = new();
        private const int MaxLogEntries = 250;
        private const int MaxLogVisible = 14;
        private bool _bridgeConnected;
        private DateTime _lastNotificationUtc = DateTime.MinValue;
        private readonly object snapshotSync = new();
        private RowanStrategySnapshot? _latestSnapshot;
        private List<RowanMetric> _latestMetrics = new();
        private IReadOnlyDictionary<string, object?> _latestCustomData = new Dictionary<string, object?>();

        public GDIRenderer(IRenderingNativeControl native)
           : base(native)
        {
            native.MouseDownNative += this.OnMouseDown;
            native.MouseUpNative += this.OnMouseUp;
            native.MouseMoveNative += this.OnMouseMove;

            this.Color = Color.Black;
            this.bufferedGraphic = new BufferedGraphic(this.Draw, this.Refresh, native.DisposeImage, native.IsDisplayed, BufferedGraphicRequiredThreadType.SeparateThreadIfAvailable);

            ApplyTheme();
        }

        public void RedrawBufferedGraphic()
        {
            this.bufferedGraphic.IsDirty = true;
        }

        protected virtual void Draw(Graphics gr)
        {
            gr.SmoothingMode = SmoothingMode.AntiAlias;
            gr.Clear(panelBackgroundColor);

            Rectangle bounds = this.Bounds;
            if (bounds.Width <= 0 || bounds.Height <= 0)
                return;

            const int padding = 14;
            const int menuHeight = 42;
            const int statusHeight = 120;
            const int tableMenuHeight = 28;
            const int tableHeaderHeight = 28;
            const int tableRowHeight = 24;
            const int visibleRows = 6;
            const int logSectionHeight = 180;

            int tableHeight = tableMenuHeight + tableHeaderHeight + tableRowHeight * visibleRows + padding;
            int remainingHeight = bounds.Height - menuHeight - statusHeight - tableHeight - logSectionHeight - padding * 4;
            int chartHeight = Math.Max(remainingHeight, 120);

            Rectangle menuRect = new Rectangle(bounds.X, bounds.Y, bounds.Width, menuHeight);
            Rectangle statusRect = new Rectangle(bounds.X + padding, menuRect.Bottom + padding, bounds.Width - padding * 2, statusHeight);
            Rectangle tableRect = new Rectangle(bounds.X + padding, statusRect.Bottom + padding, bounds.Width - padding * 2, tableHeight);
            Rectangle chartRect = new Rectangle(bounds.X + padding, tableRect.Bottom + padding, bounds.Width - padding * 2, chartHeight);
            Rectangle logRect = new Rectangle(bounds.X + padding, chartRect.Bottom + padding, bounds.Width - padding * 2, logSectionHeight);

            DrawMenu(gr, menuRect);
            DrawStatusPanel(gr, statusRect);
            DrawOrdersTable(gr, tableRect, tableMenuHeight, tableHeaderHeight, tableRowHeight);
            DrawChart(gr, chartRect);
            DrawLogPanel(gr, logRect);
        }

        public override nint Render() => bufferedGraphic.CurrentImage;

        public override void Dispose()
        {
            bufferedGraphic?.Dispose();
            DisposeThemeResources();
            menuFont.Dispose();
            tableHeaderFont.Dispose();
            tableRowFont.Dispose();
            chartAxisFont.Dispose();

            base.Dispose();
        }

        public override void OnResize()
        {
            base.OnResize();

            Rectangle bounds = this.Bounds;
            if (bounds.Width == 0 || bounds.Height == 0)
                return;

            try
            {
                bufferedGraphic.Resize(bounds.Width, bounds.Height);
                bufferedGraphic.IsDirty = true;
            }
            catch
            {
            }
        }

        #region Mouse Processing

        private void OnMouseDown(NativeMouseEventArgs e)
        {
            Point cursor = new Point(e.X, e.Y);
            bool requiresRedraw = false;

            lock (interactionSync)
            {
                if (filterDropdownOpen && dropdownBounds.Contains(cursor))
                {
                    int optionIndex = (cursor.Y - dropdownBounds.Y - 4) / DropdownItemHeight;
                    if (optionIndex >= 0 && optionIndex < filterOptions.Length)
                    {
                        selectedFilterOption = filterOptions[optionIndex];
                        filterDropdownOpen = false;
                        hoveredDropdownIndex = -1;
                        requiresRedraw = true;
                    }
                }
                else
                {
                    bool clickedMenu = false;
                    for (int i = 0; i < tableMenuEntries.Count; i++)
                    {
                        var entry = tableMenuEntries[i];
                        if (entry.Bounds.Contains(cursor))
                        {
                            clickedMenu = true;
                            if (entry.Id == "filter")
                            {
                                filterDropdownOpen = !filterDropdownOpen;
                                if (!filterDropdownOpen)
                                    hoveredDropdownIndex = -1;
                            }
                            else
                            {
                                filterDropdownOpen = false;
                                hoveredDropdownIndex = -1;
                            }
                            requiresRedraw = true;
                            break;
                        }
                    }

                    if (!clickedMenu && filterDropdownOpen)
                    {
                        filterDropdownOpen = false;
                        hoveredDropdownIndex = -1;
                        requiresRedraw = true;
                    }
                }
            }

            if (requiresRedraw)
                RedrawBufferedGraphic();
        }

        private void OnMouseMove(NativeMouseEventArgs e)
        {
            Point cursor = new Point(e.X, e.Y);
            bool requiresRedraw = false;

            lock (interactionSync)
            {
                int newHover = -1;
                for (int i = 0; i < tableMenuEntries.Count; i++)
                {
                    if (tableMenuEntries[i].Bounds.Contains(cursor))
                    {
                        newHover = i;
                        break;
                    }
                }

                if (newHover != hoveredMenuIndex)
                {
                    hoveredMenuIndex = newHover;
                    requiresRedraw = true;
                }

                if (filterDropdownOpen && dropdownBounds.Contains(cursor))
                {
                    int optionIndex = (cursor.Y - dropdownBounds.Y - 4) / DropdownItemHeight;
                    if (optionIndex < 0 || optionIndex >= filterOptions.Length)
                        optionIndex = -1;

                    if (optionIndex != hoveredDropdownIndex)
                    {
                        hoveredDropdownIndex = optionIndex;
                        requiresRedraw = true;
                    }
                }
                else if (hoveredDropdownIndex != -1)
                {
                    hoveredDropdownIndex = -1;
                    requiresRedraw = true;
                }
            }

            if (requiresRedraw)
                RedrawBufferedGraphic();
        }

        private void OnMouseUp(NativeMouseEventArgs obj)
        {
        }

        #endregion

        public override void OnThemeChanged()
        {
            base.OnThemeChanged();
            ApplyTheme();
            RedrawBufferedGraphic();
        }

        private void DrawMenu(Graphics gr, Rectangle menuRect)
        {
            using (LinearGradientBrush gradient = new LinearGradientBrush(menuRect, ((SolidBrush)menuBackgroundBrush).Color, AdjustBrightness(((SolidBrush)menuBackgroundBrush).Color, 0.08f), LinearGradientMode.Vertical))
            {
                gr.FillRectangle(gradient, menuRect);
            }

            int itemMargin = 18;
            int currentX = menuRect.X + itemMargin;
            foreach (string item in menuItems)
            {
                gr.DrawString(item, menuFont, menuTextBrush, currentX, menuRect.Y + (menuRect.Height - menuFont.Height) / 2f + 1);
                currentX += (int)gr.MeasureString(item, menuFont).Width + itemMargin;
            }
        }

        private void DrawOrdersTable(Graphics gr, Rectangle tableRect, int menuHeight, int headerHeight, int rowHeight)
        {
            if (tableRect.Width <= 0 || tableRect.Height <= 0)
                return;

            string[] headers = { "Symbol", "Side", "Quantity", "PnL (USD)" };
            int[] columnWidths =
            {
                (int)(tableRect.Width * 0.32),
                (int)(tableRect.Width * 0.16),
                (int)(tableRect.Width * 0.16),
                tableRect.Width - (int)(tableRect.Width * 0.64)
            };

            int hoverIndex;
            bool dropdownOpen;
            int dropdownHover;
            string filterSnapshot;

            lock (interactionSync)
            {
                hoverIndex = hoveredMenuIndex;
                dropdownOpen = filterDropdownOpen;
                dropdownHover = hoveredDropdownIndex;
                filterSnapshot = selectedFilterOption;
            }

            Rectangle menuRect = new Rectangle(tableRect.X, tableRect.Y, tableRect.Width, menuHeight);
            gr.FillRectangle(tableMenuBrush, menuRect);

            int itemSpacing = 16;
            int itemX = menuRect.X + 10;
            var theme = PluginApplication.Instance?.Themes?.SelectedTheme;
            List<MenuEntry> entries = new List<MenuEntry>();

            for (int i = 0; i < tableMenuTemplate.Length; i++)
            {
                var template = tableMenuTemplate[i];
                string text = template.Id == "filter"
                    ? $"{template.Label}: {filterSnapshot}"
                    : template.Label;

                SizeF measured = gr.MeasureString(text, tableRowFont);
                Size textSize = new Size((int)Math.Ceiling(measured.Width), (int)Math.Ceiling(measured.Height));
                Rectangle itemRect = new Rectangle(itemX - 6, menuRect.Y + 2, textSize.Width + 12, menuRect.Height - 4);

                if (hoverIndex == i || (dropdownOpen && template.Id == "filter"))
                {
                    Color accent = theme?.AccentColor ?? Color.FromArgb(0, 122, 204);
                    using SolidBrush highlight = new SolidBrush(Color.FromArgb(dropdownOpen && template.Id == "filter" ? 120 : 70, accent));
                    gr.FillRectangle(highlight, itemRect);
                }

                gr.DrawString(text, tableRowFont, tableMenuTextBrush, itemX, menuRect.Y + (menuRect.Height - tableRowFont.Height) / 2f + 1);
                entries.Add(new MenuEntry(template.Id, itemRect, template.HasDropdown));
                itemX += textSize.Width + itemSpacing;
            }

            lock (interactionSync)
            {
                tableMenuEntries.Clear();
                tableMenuEntries.AddRange(entries);

                if (!filterDropdownOpen)
                    dropdownBounds = Rectangle.Empty;
                if (hoveredMenuIndex >= tableMenuEntries.Count)
                    hoveredMenuIndex = -1;
            }

            gr.DrawLine(tableMenuSeparatorPen, menuRect.X, menuRect.Bottom - 1, menuRect.Right, menuRect.Bottom - 1);

            Rectangle headerRect = new Rectangle(tableRect.X, menuRect.Bottom, tableRect.Width, headerHeight);
            gr.FillRectangle(tableHeaderBrush, headerRect);

            int colX = tableRect.X;
            for (int i = 0; i < headers.Length; i++)
            {
                gr.DrawString(headers[i], tableHeaderFont, tableHeaderTextBrush, colX + 8, headerRect.Y + (headerRect.Height - tableHeaderFont.Height) / 2f);
                if (i < headers.Length - 1)
                    gr.DrawLine(tableGridPen, colX + columnWidths[i], headerRect.Y, colX + columnWidths[i], headerRect.Bottom);
                colX += columnWidths[i];
            }

            if (dropdownOpen)
            {
                MenuEntry filterEntry = entries.FirstOrDefault(entry => entry.Id == "filter");
                Rectangle dropRect = filterEntry.Bounds != Rectangle.Empty
                    ? new Rectangle(
                        filterEntry.Bounds.X,
                        filterEntry.Bounds.Bottom + 4,
                        Math.Max(160, filterEntry.Bounds.Width + 20),
                        filterOptions.Length * DropdownItemHeight + 8)
                    : Rectangle.Empty;

                if (dropRect != Rectangle.Empty)
                {
                    using SolidBrush dropdownBrush = new SolidBrush(Color.FromArgb(235, tableRowBrushEven.Color));
                    using Pen dropdownBorder = new Pen(Color.FromArgb(140, tableGridPen.Color), 1f);
                    gr.FillRectangle(dropdownBrush, dropRect);
                    gr.DrawRectangle(dropdownBorder, dropRect);

                    for (int i = 0; i < filterOptions.Length; i++)
                    {
                        Rectangle optionRect = new Rectangle(dropRect.X + 4, dropRect.Y + 4 + i * DropdownItemHeight, dropRect.Width - 8, DropdownItemHeight);
                        if (dropdownHover == i)
                        {
                            Color accent = theme?.AccentColor ?? Color.FromArgb(0, 122, 204);
                            using SolidBrush hoverBrush = new SolidBrush(Color.FromArgb(80, accent));
                            gr.FillRectangle(hoverBrush, optionRect);
                        }

                        using SolidBrush optionTextBrush = new SolidBrush(filterOptions[i] == filterSnapshot
                            ? (theme?.AccentTextColor ?? Color.White)
                            : tableRowTextBrush.Color);
                        gr.DrawString(filterOptions[i], tableRowFont, optionTextBrush, optionRect.X + 4, optionRect.Y + (optionRect.Height - tableRowFont.Height) / 2f + 1);
                    }

                    lock (interactionSync)
                    {
                        dropdownBounds = dropRect;
                    }
                }
            }
            else
            {
                lock (interactionSync)
                {
                    dropdownBounds = Rectangle.Empty;
                }
            }

            int rowY = headerRect.Bottom;
            for (int rowIndex = 0; rowIndex < sampleOrders.Length; rowIndex++)
            {
                Rectangle rowRect = new Rectangle(tableRect.X, rowY, tableRect.Width, rowHeight);
                gr.FillRectangle(rowIndex % 2 == 0 ? tableRowBrushEven : tableRowBrushOdd, rowRect);

                var order = sampleOrders[rowIndex];
                int cellX = tableRect.X;

                DrawCellText(gr, order.Symbol, tableRowFont, tableRowTextBrush, cellX + 8, rowRect);
                cellX += columnWidths[0];

                var sideColor = order.Side.Equals("Buy", StringComparison.OrdinalIgnoreCase)
                    ? theme?.BuyColor ?? Color.LimeGreen
                    : theme?.SellColor ?? Color.IndianRed;
                using (SolidBrush sideBrush = new SolidBrush(sideColor))
                {
                    DrawCellText(gr, order.Side, tableRowFont, sideBrush, cellX + 8, rowRect);
                }
                cellX += columnWidths[1];

                DrawCellText(gr, order.Quantity, tableRowFont, tableRowMutedTextBrush, cellX + 8, rowRect);
                cellX += columnWidths[2];

                using (SolidBrush pnlBrush = new SolidBrush(order.Pnl >= 0
                    ? theme?.SuccessColor ?? Color.LimeGreen
                    : theme?.DangerColor ?? Color.IndianRed))
                {
                    DrawCellText(gr, order.Pnl.ToString("+#0.00;-#0.00"), tableRowFont, pnlBrush, cellX + 8, rowRect);
                }

                gr.DrawLine(tableGridPen, tableRect.X, rowRect.Bottom, tableRect.Right, rowRect.Bottom);
                rowY += rowHeight;
            }

            gr.DrawRectangle(tableGridPen, new Rectangle(tableRect.X, tableRect.Y, tableRect.Width, menuHeight + headerHeight + rowHeight * sampleOrders.Length));
        }

        private void DrawChart(Graphics gr, Rectangle chartRect)
        {
            if (chartRect.Width <= 0 || chartRect.Height <= 0)
                return;

            gr.FillRectangle(chartBackgroundBrush, chartRect);

            var theme = PluginApplication.Instance?.Themes?.SelectedTheme;
            double min = chartValues.Min();
            double max = chartValues.Max();
            if (Math.Abs(max - min) < double.Epsilon)
            {
                max += 1;
                min -= 1;
            }

            int labelCount = 4;
            for (int i = 0; i <= labelCount; i++)
            {
                float t = i / (float)labelCount;
                int y = chartRect.Bottom - (int)(t * chartRect.Height);
                gr.DrawLine(chartAxisPen, chartRect.X, y, chartRect.Right, y);
                double value = min + (max - min) * t;
                gr.DrawString(value.ToString("0.0"), chartAxisFont, chartAxisTextBrush, chartRect.X + 6, y - chartAxisFont.Height);
            }

            if (chartValues.Length < 2)
                return;

            PointF[] points = new PointF[chartValues.Length];
            for (int i = 0; i < chartValues.Length; i++)
            {
                float x = chartRect.X + (float)i / (chartValues.Length - 1) * chartRect.Width;
                float y = chartRect.Bottom - (float)((chartValues[i] - min) / (max - min) * chartRect.Height);
                points[i] = new PointF(x, y);
            }

            using (GraphicsPath fillPath = new GraphicsPath())
            {
                fillPath.AddLines(points);
                fillPath.AddLine(points[^1].X, chartRect.Bottom, points[0].X, chartRect.Bottom);
                fillPath.CloseFigure();
                gr.FillPath(chartFillBrush, fillPath);
            }

            gr.DrawLines(chartLinePen, points);
            gr.DrawString("Session PnL momentum", menuFont, menuTextBrush, chartRect.X + 6, chartRect.Y + 6);
            gr.DrawString($"Theme: {theme?.Name ?? "Default"}", chartAxisFont, chartAxisTextBrush, chartRect.Right - 140, chartRect.Y + 6);
        }

        private void DrawStatusPanel(Graphics gr, Rectangle statusRect)
        {
            if (statusRect.Width <= 0 || statusRect.Height <= 0)
                return;

            gr.FillRectangle(chartBackgroundBrush, statusRect);
            Rectangle headerRect = new Rectangle(statusRect.X, statusRect.Y, statusRect.Width, 28);
            gr.FillRectangle(tableMenuBrush, headerRect);

            RowanStrategySnapshot? snapshot;
            List<RowanMetric> metrics;
            IReadOnlyDictionary<string, object?> customData;
            lock (snapshotSync)
            {
                snapshot = _latestSnapshot;
                metrics = _latestMetrics.ToList();
                customData = _latestCustomData;
            }

            string headerStatus = snapshot != null
                ? $"{snapshot.Lifecycle} @ {snapshot.TimestampUtc.ToLocalTime():HH:mm:ss}"
                : "Awaiting snapshot...";
            gr.DrawString("Strategy Snapshot", tableHeaderFont, tableHeaderTextBrush, headerRect.X + 8, headerRect.Y + (headerRect.Height - tableHeaderFont.Height) / 2f);

            using (SolidBrush statusBrush = new SolidBrush(_bridgeConnected ? Color.FromArgb(166, 227, 161) : Color.FromArgb(255, 201, 134)))
            {
                SizeF statusSize = gr.MeasureString(headerStatus, tableRowFont);
                float statusX = Math.Max(headerRect.X + 8, headerRect.Right - statusSize.Width - 8);
                gr.DrawString(headerStatus, tableRowFont, statusBrush, statusX, headerRect.Y + (headerRect.Height - tableRowFont.Height) / 2f + 1);
            }

            Rectangle contentRect = new Rectangle(statusRect.X + 8, headerRect.Bottom + 6, statusRect.Width - 16, statusRect.Height - headerRect.Height - 12);

            if (snapshot == null)
            {
                gr.DrawString("No data received yet from the strategy bridge.", tableRowFont, logTextBrush, contentRect.X, contentRect.Y);
                return;
            }

            float lineHeight = tableRowFont.Height + 4;
            float leftX = contentRect.X;
            float rightX = contentRect.X + contentRect.Width / 2f;
            float cursorLeft = contentRect.Y;
            float cursorRight = contentRect.Y;

            customData.TryGetValue("Symbol", out var symValue);
            gr.DrawString($"Symbol: {symValue?.ToString() ?? "n/a"}", tableRowFont, logTextBrush, leftX, cursorLeft);
            cursorLeft += lineHeight;

            customData.TryGetValue("Account", out var accValue);
            gr.DrawString($"Account: {accValue?.ToString() ?? "n/a"}", tableRowFont, logTextBrush, leftX, cursorLeft);
            cursorLeft += lineHeight;

            gr.DrawString($"Net PnL: {snapshot.NetProfit:F2} USD", tableRowFont, logTextBrush, leftX, cursorLeft);
            cursorLeft += lineHeight;

            gr.DrawString($"Lifecycle: {snapshot.Lifecycle}", tableRowFont, logTextBrush, leftX, cursorLeft);

            gr.DrawString($"Active Orders: {snapshot.ActiveOrders}", tableRowFont, logTextBrush, rightX, cursorRight);
            cursorRight += lineHeight;
            gr.DrawString($"Open Positions: {snapshot.OpenPositions}", tableRowFont, logTextBrush, rightX, cursorRight);
            cursorRight += lineHeight;
            if (customData.TryGetValue("UseLotSystem", out var uls))
            {
                gr.DrawString($"Use Lot System: {uls}", tableRowFont, logTextBrush, rightX, cursorRight);
                cursorRight += lineHeight;
            }
            gr.DrawString($"Bridge logging: {_bridgeConnected}", tableRowFont, logTextBrush, rightX, cursorRight);
            cursorRight += lineHeight;

            if (metrics.Count > 0)
            {
                float metricsY = Math.Max(cursorLeft, cursorRight) + lineHeight / 2f;
                gr.DrawString("Key metrics:", tableRowFont, logTextBrush, leftX, metricsY);
                metricsY += lineHeight;
                foreach (var metric in metrics.Take(4))
                {
                    string unit = string.IsNullOrWhiteSpace(metric.Unit) ? string.Empty : metric.Unit;
                    string formatted = unit == "%" ? $"{metric.Value:F2}{unit}" : $"{metric.Value:F2} {unit}".Trim();
                    gr.DrawString($"• {metric.Name}: {formatted}", tableRowFont, logTextBrush, leftX, metricsY);
                    metricsY += lineHeight;
                }
            }
        }

        private void DrawLogPanel(Graphics gr, Rectangle logRect)
        {
            if (logRect.Width <= 0 || logRect.Height <= 0)
                return;

            gr.FillRectangle(logBackgroundBrush, logRect);

            Rectangle headerRect = new Rectangle(logRect.X, logRect.Y, logRect.Width, 30);
            gr.FillRectangle(logHeaderBrush, headerRect);
            gr.DrawString("Log stream", tableHeaderFont, tableHeaderTextBrush, headerRect.X + 8, headerRect.Y + (headerRect.Height - tableHeaderFont.Height) / 2f);

            string statusText = _bridgeConnected ? "Bridge: Connected" : "Bridge: Waiting...";
            if (_lastNotificationUtc != DateTime.MinValue)
            {
                statusText += $" | Last: {_lastNotificationUtc.ToLocalTime():HH:mm:ss}";
            }

            using (SolidBrush statusBrush = new SolidBrush(_bridgeConnected ? Color.FromArgb(166, 227, 161) : Color.FromArgb(255, 201, 134)))
            {
                SizeF statusSize = gr.MeasureString(statusText, tableRowFont);
                float statusX = Math.Max(logRect.X + 8, headerRect.Right - statusSize.Width - 8);
                gr.DrawString(statusText, tableRowFont, statusBrush, statusX, headerRect.Y + (headerRect.Height - tableRowFont.Height) / 2f + 1);
            }

            gr.DrawLine(logSeparatorPen, logRect.X, headerRect.Bottom, logRect.Right, headerRect.Bottom);

            UiLogEntry[] snapshot;
            lock (logSync)
            {
                snapshot = logEntries.TakeLast(Math.Min(MaxLogVisible, logEntries.Count)).ToArray();
            }

            if (snapshot.Length == 0)
            {
                using SolidBrush muted = new SolidBrush(Color.FromArgb(140, logTextBrush.Color));
                gr.DrawString("No log entries yet.", tableRowFont, muted, logRect.X + 8, headerRect.Bottom + 10);
                return;
            }

            int lineHeight = tableRowFont.Height + 4;
            int startY = headerRect.Bottom + 8;

            for (int i = 0; i < snapshot.Length; i++)
            {
                int y = startY + i * lineHeight;
                if (y > logRect.Bottom - lineHeight)
                    break;

                var entry = snapshot[i];
                using SolidBrush brush = new SolidBrush(GetLogLevelColor(entry.Level));
                string time = entry.Timestamp.ToLocalTime().ToString("HH:mm:ss");
                gr.DrawString($"{time} [{entry.Level}] {entry.Message}", tableRowFont, brush, logRect.X + 8, y);
            }
        }

        private static void DrawCellText(Graphics gr, string text, Font font, Brush brush, int x, Rectangle bounds)
        {
            gr.DrawString(text, font, brush, x, bounds.Y + (bounds.Height - font.Height) / 2f + 1);
        }

        private static Color AdjustBrightness(Color color, float factor)
        {
            int Clamp(int value) => Math.Max(0, Math.Min(255, value));

            int r = Clamp((int)(color.R + 255 * factor));
            int g = Clamp((int)(color.G + 255 * factor));
            int b = Clamp((int)(color.B + 255 * factor));
            return Color.FromArgb(color.A, r, g, b);
        }

        private void ApplyTheme()
        {
            DisposeThemeResources();

            var theme = PluginApplication.Instance?.Themes?.SelectedTheme;
            if (theme == null)
                return;

            panelBackgroundColor = theme.PrimaryBackColor;

            menuBackgroundBrush = new SolidBrush(theme.AccentColor);
            menuTextBrush = new SolidBrush(theme.ActiveTextColor);

            tableMenuBrush = new SolidBrush(Color.FromArgb(255, theme.SecondaryBackColor));
            tableMenuTextBrush = new SolidBrush(theme.SecondaryTextColor);
            tableHeaderBrush = new SolidBrush(theme.MiddleBackColor);
            tableHeaderTextBrush = new SolidBrush(theme.PrimaryTextColor);
            tableRowBrushEven = new SolidBrush(theme.PrimaryBackColor);
            tableRowBrushOdd = new SolidBrush(theme.SecondaryBackColor);
            tableRowTextBrush = new SolidBrush(theme.PrimaryTextColor);
            tableRowMutedTextBrush = new SolidBrush(Color.FromArgb(200, theme.SecondaryTextColor));

            chartBackgroundBrush = new SolidBrush(theme.SecondaryBackColor);
            chartFillBrush = new SolidBrush(Color.FromArgb(60, theme.AccentColor));
            chartAxisTextBrush = new SolidBrush(Color.FromArgb(200, theme.SecondaryTextColor));
            logHeaderBrush = new SolidBrush(theme.MiddleBackColor);
            logBackgroundBrush = new SolidBrush(theme.PrimaryBackColor);
            logTextBrush = new SolidBrush(theme.PrimaryTextColor);

            tableGridPen = new Pen(Color.FromArgb(70, theme.PrimaryBorderColor), 1f);
            tableMenuSeparatorPen = new Pen(Color.FromArgb(90, theme.PrimaryBorderColor), 1f);
            chartAxisPen = new Pen(Color.FromArgb(80, theme.SecondaryTextColor), 1f);
            chartLinePen = new Pen(theme.AccentColor, 2f);
            logSeparatorPen = new Pen(Color.FromArgb(70, theme.PrimaryBorderColor), 1f);

            lock (interactionSync)
            {
                filterDropdownOpen = false;
                hoveredDropdownIndex = -1;
                hoveredMenuIndex = -1;
                dropdownBounds = Rectangle.Empty;
            }
        }

        private void DisposeThemeResources()
        {
            menuBackgroundBrush?.Dispose();
            menuTextBrush?.Dispose();
            tableMenuBrush?.Dispose();
            tableMenuTextBrush?.Dispose();
            tableHeaderBrush?.Dispose();
            tableHeaderTextBrush?.Dispose();
            tableRowBrushEven?.Dispose();
            tableRowBrushOdd?.Dispose();
            tableRowTextBrush?.Dispose();
            tableRowMutedTextBrush?.Dispose();
            chartBackgroundBrush?.Dispose();
            chartFillBrush?.Dispose();
            chartAxisTextBrush?.Dispose();
            logHeaderBrush?.Dispose();
            logBackgroundBrush?.Dispose();
            logTextBrush?.Dispose();

            tableGridPen?.Dispose();
            tableMenuSeparatorPen?.Dispose();
            chartAxisPen?.Dispose();
            chartLinePen?.Dispose();
            logSeparatorPen?.Dispose();
        }

        private readonly struct MenuEntry
        {
            public MenuEntry(string id, Rectangle bounds, bool hasDropdown)
            {
                Id = id;
                Bounds = bounds;
                HasDropdown = hasDropdown;
            }

            public string Id { get; }
            public Rectangle Bounds { get; }
            public bool HasDropdown { get; }
        }

        private readonly struct UiLogEntry
        {
            public UiLogEntry(DateTime timestamp, NotificationLevel level, string message)
            {
                Timestamp = timestamp;
                Level = level;
                Message = message;
            }

            public DateTime Timestamp { get; }
            public NotificationLevel Level { get; }
            public string Message { get; }
        }

        private static Color GetLogLevelColor(NotificationLevel level) => level switch
        {
            NotificationLevel.Warning => Color.FromArgb(255, 227, 178),
            NotificationLevel.Error => Color.FromArgb(255, 128, 128),
            NotificationLevel.Success => Color.FromArgb(166, 227, 161),
            NotificationLevel.Debug => Color.FromArgb(148, 148, 255),
            _ => Color.FromArgb(210, 210, 210)
        };

        public void AddLogEntry(RowanNotification notification)
        {
            if (notification == null)
                return;

            lock (logSync)
            {
                _bridgeConnected = true;
                _lastNotificationUtc = notification.TimestampUtc;
                string combinedMessage = string.IsNullOrWhiteSpace(notification.Source)
                    ? notification.Message
                    : $"{notification.Source} {notification.Message}";
                logEntries.AddLast(new UiLogEntry(notification.TimestampUtc, notification.Level, combinedMessage));
                while (logEntries.Count > MaxLogEntries)
                    logEntries.RemoveFirst();
            }

            RedrawBufferedGraphic();
        }

        public void UpdateBridgeConnection(bool isConnected)
        {
            if (_bridgeConnected == isConnected)
                return;

            _bridgeConnected = isConnected;
            RedrawBufferedGraphic();
        }

        public void UpdateBridgeHeartbeat(DateTime timestampUtc)
        {
            if (timestampUtc == DateTime.MinValue)
                return;

            _lastNotificationUtc = timestampUtc;
            RedrawBufferedGraphic();
        }

        public void UpdateSnapshot(RowanStrategySnapshot snapshot)
        {
            if (snapshot == null)
                return;

            lock (snapshotSync)
            {
                _latestSnapshot = snapshot;
                _latestMetrics = snapshot.Metrics?.ToList() ?? new List<RowanMetric>();
                _latestCustomData = snapshot.CustomData != null
                    ? new Dictionary<string, object?>(snapshot.CustomData)
                    : new Dictionary<string, object?>();
            }

            UpdateBridgeHeartbeat(snapshot.TimestampUtc);
            UpdateBridgeConnection(true);
        }
    }
}
