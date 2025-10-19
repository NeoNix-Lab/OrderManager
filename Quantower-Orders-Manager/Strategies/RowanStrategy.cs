using DivergentStrV0_1.OperationSystemAdv;
using DivergentStrV0_1.OperationSystemAdv.DDDCore;
using DivergentStrV0_1.Utils;
using System;
using System.Collections.Generic;
using System.Linq;
using TradingPlatform.BusinessLayer;

namespace DivergentStrV0_1.Strategies
{
    internal enum TradeAction
    {
        Buy,
        Sell,
        Close,
        Revert,
        Wait
    }

    internal enum TradeSignal
    {
        OpenBuy,
        OpenSell,
        CloseLong,
        CloseSell,
        Wait,
        Unknown
    }

    internal enum InternalExpositionSide
    {
        Long,
        Short,
        Both,
        Unexposed
    }

    internal class RowanStrategy : ConditionableBase<SlTpData>
    {

        private Indicator _atrIndicator;
        private Indicator _deltaBaseIndicator;
        private Indicator _slipageAtrIndicator;
        private int _maxOpen;
        private double _totalQuantity;
        private double _maxSessionLos;
        private double _startSessionLoss;        
        private bool _sessionClosed = false;
        private bool _strategyActive = true;
        private int _verbosityFreq = 0;
        private int _verbosityFreqCount = 0;
        private bool _loadAsync;
        private List<string> _logsEntry = new List<string>();
        private List<string> _logsExits = new List<string>();
        private int _entryMinConditions;
        private int _exitMinConditions;
        private readonly List<string> _entryLineNames = new List<string>();
        private readonly List<string> _exitLineNames = new List<string>();
        private double _lotQuantity;
        private InternalExpositionSide ExposeSide = InternalExpositionSide.Unexposed;
        private int ExposedCount = 0;

        public bool AllowToTrade
        {
            //TODO: [DEBUG] Audit session loss guard to avoid false positives
            get
            {
                var usdloss = this.Metrics.NetProfit - this._startSessionLoss;
                var maxLoss = usdloss < 0 && Math.Abs(usdloss) >= Math.Abs(_maxSessionLos);
                var sessionActive = StaticSessionManager.CurrentStatus == Status.Active;

                if (maxLoss || !sessionActive)
                    return false;
                else
                    return true;
            }
        }

        public RowanStrategy()
        {
        }

        public RowanStrategy(Indicator DeltaBaseIndicator, Indicator atrsIndicator, int max_open, double totalquantity,
            double maxSessionLosUsd, int verbosity_frequency, int slipageAtrPeriod, double lotQuantity = 0.0) : base()
        {

            this._atrIndicator = atrsIndicator;
            this._deltaBaseIndicator = DeltaBaseIndicator;
            this._maxOpen = max_open;
            this._totalQuantity = totalquantity;
            this._maxSessionLos = maxSessionLosUsd;
            this._verbosityFreq = verbosity_frequency;
            this._slipageAtrIndicator = Core.Instance.Indicators.BuiltIn.ATR(slipageAtrPeriod, MaMode.SMA);
            this._lotQuantity = lotQuantity;
        }

        public void ConfigureConditions(IEnumerable<string> entryLineNames, int entryMinConditions,
                                        IEnumerable<string> exitLineNames, int exitMinConditions)
        {
            _entryMinConditions = Math.Max(0, entryMinConditions);
            _exitMinConditions = Math.Max(0, exitMinConditions);

            _entryLineNames.Clear();
            if (entryLineNames != null)
                _entryLineNames.AddRange(entryLineNames.Where(s => !string.IsNullOrWhiteSpace(s)).Select(s => s.Trim()));

            _exitLineNames.Clear();
            if (exitLineNames != null)
                _exitLineNames.AddRange(exitLineNames.Where(s => !string.IsNullOrWhiteSpace(s)).Select(s => s.Trim()));
        }

        private static bool TryGetLineValue(Indicator ind, string name, out double value)
        {
            try
            {
                foreach (var ls in ind.LinesSeries)
                {
                    if (string.Equals(ls.Name, name, StringComparison.OrdinalIgnoreCase))
                    {
                        value = ls.GetValue(offset:1);
                        return true;
                    }
                }
            }
            catch { }
            value = 0.0;
            return false;
        }

        private double GetLineValueByNameAny(string name, bool isEntry)
        {
            if (TryGetLineValue(this._atrIndicator, name, out var v1)) 
            {
                string log = v1 > 0 ? "buy" : v1 == 0 ? "wait" : "sell";
                if (isEntry)
                    this._logsEntry.Add($"Line {name} value: {log}");
                else
                    this._logsExits.Add($"Line {name} value: {log}");
                return v1;
            }
            if (TryGetLineValue(this._deltaBaseIndicator, name, out var v2))
            {
                string log2 = v2 > 0 ? "buy" : v2 == 0 ? "wait" : "sell";
                if (isEntry)
                    this._logsEntry.Add($"Line {name} value: {log2}");
                else
                    this._logsExits.Add($"Line {name} value: {log2}");
                return v2;
            }
            return 0.0;
        }

        public override void Init(HistoryRequestParameters req, Account account, bool loadAsync = false, string description = "", bool allowHeavyMetrics = false)
        {
            this.ManagerChoice = ManagerType.PositionBased;
            base.Init(req, account, loadAsync, description, allowHeavyMetrics);
            StaticSessionManager.Initialize(this.HistoryProvider);
            StaticSessionManager.TradeSessionsStatusChanged += StaticSessionManager_TradeSessionsStatusChanged;
            this._loadAsync = loadAsync;

            if (!this._loadAsync)
            {
                this.HistoryProvider.HistoricalData.AddIndicator(this._atrIndicator);
                this.HistoryProvider.HistoricalData.AddIndicator(this._deltaBaseIndicator);
                this.HistoryProvider.HistoricalData.AddIndicator(this._slipageAtrIndicator);
            }

        }
        public override void OnVolumeDataReady()
        {
            base.OnVolumeDataReady();

            //TODO: [DEBUG] Verify async volume callbacks reattach indicators safely
            if (this._loadAsync)
            {
                if (this._atrIndicator.Count == 0)
                    this.HistoryProvider.HistoricalData.AddIndicator(this._atrIndicator);
                if (this._deltaBaseIndicator.Count == 0)
                    this.HistoryProvider.HistoricalData.AddIndicator(this._deltaBaseIndicator);
                if (this._slipageAtrIndicator.Count == 0)
                    this.HistoryProvider.HistoricalData.AddIndicator(this._slipageAtrIndicator);
            }

        }

        private void StaticSessionManager_TradeSessionsStatusChanged(object sender, Status e)
        {
            if (e == Status.Active)
            {
                //TODO: [DEBUG] Reset session loss baseline when status becomes Active
                this._startSessionLoss = this.Metrics.NetProfit;
                this._sessionClosed = false;
            }
        }

        protected override List<HistoryUpdAteType> GetUpdateTypes()
        {
            return new List<HistoryUpdAteType>
            {
                HistoryUpdAteType.NewItem,
            };
        }

        public override void Dispose()
        {
            StaticSessionManager.TradeSessionsStatusChanged -= StaticSessionManager_TradeSessionsStatusChanged;
            this._atrIndicator.Dispose();
            this._deltaBaseIndicator.Dispose();

            base.Dispose();
        }

        public override double SetQuantity()
        {
            if (this._lotQuantity > 0)
                try
                {
                    return this._lotQuantity * this.Symbol.LotSize * this.HistoryProvider.HistoricalData[0][PriceType.Close];
                }
                catch (Exception)
                {

                    AppLog.Error("RowanStrategy", "SetQuantityFailed", "Rowan Strategy error at SetQuantity with message : Failed to compute lot-based quantity, fallback to fixed quantity");
                    return 0;
                }

            else
                return this._totalQuantity / this._maxOpen;
        }
        public override void Update(object obj)
        {
            //TODO: [DEBUG] Review market data payload completeness before trading decisions
            this._logsEntry.Clear();
            this._logsExits.Clear();

            //TODO: [DEBUG] Detect duplicate indicator registrations triggered by history provider bug

            if (this._atrIndicator.Count == 0)
                this.HistoryProvider.HistoricalData.AddIndicator(this._atrIndicator);
            if (this._deltaBaseIndicator.Count == 0)
                this.HistoryProvider.HistoricalData.AddIndicator(this._deltaBaseIndicator);
            if (this._slipageAtrIndicator.Count == 0)
                this.HistoryProvider.HistoricalData.AddIndicator(this._slipageAtrIndicator);

            //TODO: [DEBUG] Guard against unexpected history event payload shapes
            HistoryEventArgs e = obj as HistoryEventArgs ?? null;
            if (e == null)
            {
                AppLog.Error("RowanStrategy", "UpdateCasting", "Rowan Strategy error at Update casting");
                AppLog.Error("RowanStrategy", "StrategyState", "Strategy Will be Disabled");
                this.ForceClosePositions(5);
                this._strategyActive = false;
                return;
            }

            HistoryItem item = (HistoryItem)e.HistoryItem;

            StaticSessionManager.Update(item);

            if (this._loadAsync && !this.HistoryProvider.VolumeDataReady) 
                return;
            try
            {
                if (!this.AllowToTrade)
                {

                    if (StaticSessionManager.CurrentStatus == Status.Active && !_sessionClosed)
                    {
                        AppLog.Trading("RowanStrategy", "SessionGuard", "Max session loss reached");
                        this.ForceClosePositions(5);
                        _sessionClosed = true;
                    }
                    return;
                }
            //TODO: [DEBUG] Validate ATR-based slippage computation for entry price

            //TODO: [DEBUG] Double-check history index offsets when reading price levels
            double prevLow = double.NaN;
            double prevHigh = double.NaN;
            try
            {
                if (this.HistoryProvider?.HistoricalData != null)
                {
                    prevLow = this.HistoryProvider.HistoricalData[1][PriceType.Low];
                    prevHigh = this.HistoryProvider.HistoricalData[1][PriceType.High];
                }
            }
            catch
            {
                prevLow = double.NaN;
                prevHigh = double.NaN;
            }

            SlTpData marketData = new SlTpData()
                {
                    currentPrice = item[PriceType.Open],
                    Symbol = this.Symbol,
                    AtrInTicks = Math.Abs(this.Symbol.CalculateTicks(this._slipageAtrIndicator.GetValue()+ item[PriceType.Open], item[PriceType.Open])),
                    PreviousLow = prevLow,
                    PreviousHigh = prevHigh,
                };
                TradeSignal entry_signal = this.CalculateTradeSignal(true);
                TradeSignal exit_signal = this.CalculateTradeSignal(false);
                TradeAction action = TradeAction.Wait;

                switch (this.ExposeSide)
                {
                    case InternalExpositionSide.Long:
                        if (exit_signal == TradeSignal.CloseLong && entry_signal != TradeSignal.OpenSell)
                            action = TradeAction.Close;
                        else if (entry_signal == TradeSignal.OpenSell)
                            action = TradeAction.Revert;
                        else if (entry_signal == TradeSignal.OpenBuy)
                            action = TradeAction.Buy;
                        else 
                            action = TradeAction.Wait;
                        break;
                    case InternalExpositionSide.Short:
                        if (exit_signal == TradeSignal.CloseSell && entry_signal != TradeSignal.OpenBuy)
                            action = TradeAction.Close;
                        else if (entry_signal == TradeSignal.OpenBuy)
                            action = TradeAction.Revert;
                        else if (entry_signal == TradeSignal.OpenSell)
                            action = TradeAction.Sell;
                        else
                            action = TradeAction.Wait;
                        break;
                    case InternalExpositionSide.Both:
                        AppLog.Error("RowanStrategy", "ExposureCheck", "Exposed on Both Sides positions");
                        this.ForceClosePositions(5);
                        break;
                    case InternalExpositionSide.Unexposed:
                        if (entry_signal == TradeSignal.OpenBuy)
                            action = TradeAction.Buy;
                        else if (entry_signal == TradeSignal.OpenSell)
                            action = TradeAction.Sell;
                        break;
                }

                if (this._lotQuantity > 0)
                    this.OverrideQuantity(this.SetQuantity());

                if (!_strategyActive)
                    return;

                //TODO: [DEBUG] Confirm entry sizing respects max exposure thresholds

                if (action == TradeAction.Buy || action == TradeAction.Sell)
                {
                    if (ExposedCount >= this._maxOpen)
                    {
                        AppLog.Trading("RowanStrategy", "TradeSignal", $"AVOIDED DUE MAX EXPO REACHED");
                        AppLog.Trading("RowanStrategy", "TradeSignalContext", $"EntrySignal={entry_signal}, ExitSignal={exit_signal}, Action={action}");
                        foreach (var logsitem in this._logsEntry)
                            AppLog.Trading("RowanStrategy", "EntryDetails", $"Entry Details: {logsitem}");
                        foreach (var logsExitem in this._logsExits)
                            AppLog.Trading("RowanStrategy", "ExitDetails", $"Exit Details: {logsExitem}");
                    }
                    else
                    {
                        if (action == TradeAction.Buy)
                        {
                            this.ExposeSide = InternalExpositionSide.Long;
                            this.ExposedCount++;
                            marketData.SlTriggerPrice = this.HistoryProvider.HistoricalData[1][PriceType.Low];
                            this.ComputeTradeAction(marketData, Side.Buy);

                        }
                        else if (action == TradeAction.Sell)
                        {
                            this.ExposeSide = InternalExpositionSide.Short;
                            this.ExposedCount++;
                            marketData.SlTriggerPrice = this.HistoryProvider.HistoricalData[1][PriceType.High];
                            this.ComputeTradeAction(marketData, Side.Sell);
                        }
                        AppLog.Trading("RowanStrategy", "TradeSignal", $"EntrySignal={entry_signal}, ExitSignal={exit_signal}, Action={action}");
                        foreach (var logsitem in this._logsEntry)
                            AppLog.Trading("RowanStrategy", "EntryDetails", $"Entry Details: {logsitem}");
                        foreach (var logsExitem in this._logsExits)
                            AppLog.Trading("RowanStrategy", "ExitDetails", $"Exit Details: {logsExitem}");
                    }
                }

                if (action == TradeAction.Close)
                {
                    bool res = this.ForceClosePositions(5);
                    this.ExposeSide = InternalExpositionSide.Unexposed;
                    this.ExposedCount = 0;

                    if (res)
                    {
                        AppLog.Trading("RowanStrategy", "TradeSignal", $"ALL POSITIONS CLOSED EntrySignal={entry_signal}, ExitSignal={exit_signal}, Action={action}");
                        foreach (var logsitem in this._logsEntry)
                            AppLog.Trading("RowanStrategy", "EntryDetails", $"Entry Details: {logsitem}");
                        foreach (var logsExitem in this._logsExits)
                            AppLog.Trading("RowanStrategy", "ExitDetails", $"Exit Details: {logsExitem}");
                    }
                    else
                    {
                        AppLog.Error("RowanStrategy", "TradeSignal", $"FAILED TO CLOSE POSITIONS, STRATEGY STOPPED EntrySignal={entry_signal}, ExitSignal={exit_signal}, Action={action}");
                        foreach (var logsitem in this._logsEntry)
                            AppLog.Trading("RowanStrategy", "EntryDetails", $"Entry Details: {logsitem}");
                        foreach (var logsExitem in this._logsExits)
                            AppLog.Trading("RowanStrategy", "ExitDetails", $"Exit Details: {logsExitem}");
                        this._strategyActive = false;
                    }
                }

                if (action == TradeAction.Revert)
                {
                    bool res = this.ForceClosePositions(5);
                    if (res)
                    {
                        marketData.SlTriggerPrice = entry_signal == TradeSignal.OpenBuy ?
                            this.HistoryProvider.HistoricalData[1][PriceType.Low] : this.HistoryProvider.HistoricalData[1][PriceType.High];

                        this.ExposeSide = entry_signal == TradeSignal.OpenBuy ? InternalExpositionSide.Long : InternalExpositionSide.Short;

                        this.ExposedCount = 1;

                        AppLog.Trading("RowanStrategy", "TradeSignal", $"ALL POSITIONS CLOSED FOR REVERSAL EntrySignal={entry_signal}, ExitSignal={exit_signal}, Action={action}");
                        foreach (var logsitem in this._logsEntry)
                            AppLog.Trading("RowanStrategy", "EntryDetails", $"Entry Details: {logsitem}");
                        foreach (var logsExitem in this._logsExits)
                            AppLog.Trading("RowanStrategy", "ExitDetails", $"Exit Details: {logsExitem}");

                        this.ComputeTradeAction(marketData, entry_signal == TradeSignal.OpenBuy ? Side.Buy : Side.Sell);
                    }
                    else
                    {
                        AppLog.Error("RowanStrategy", "TradeSignal", $"FAILED TO CLOSE POSITIONS FOR REVERSAL");
                        this._strategyActive = false;
                    }
                }

                else
                {
                    try
                    {
                        //TODO: [DEBUG] Track SL/TP adjustments for consistency with live positions

                        //🧠 HINT: [Non agiamo su segnali differenti]

                        this.UpdateSlTp(marketData, isSl: true);

                    }
                    catch { AppLog.Error("RowanStrategy", "UpdateSl", "Failed to update Sl"); }
                }

                if (this._verbosityFreqCount <= this._verbosityFreq)
                {
                    this._verbosityFreqCount++;

                    if (this._verbosityFreqCount == this._verbosityFreq)
                    {
                        AppLog.System("RowanStrategy", "VerboseStatus", $"Strategy status: SessionActive={StaticSessionManager.CurrentStatus}, AllowToTrade={this.AllowToTrade}");
                        AppLog.System("RowanStrategy", "VerboseExposure", $"ExposedSide={this.Metrics.ExposedSide}, ExposedCount={this.Metrics.ExposedCount}");
                        AppLog.System("RowanStrategy", "VerboseSignals", $"EntrySignal={entry_signal}, ExitSignal={exit_signal}, Action={action}");
                        foreach (var logsitem in this._logsEntry)
                            AppLog.System("RowanStrategy", "VerboseEntryDetails", $"Entry Details: {logsitem}");
                        foreach (var logsExitem in this._logsExits)
                            AppLog.System("RowanStrategy", "VerboseExitDetails", $"Exit Details: {logsExitem}");
                        this._verbosityFreqCount = 0;
                    }
                }

            }
            catch (Exception ex)
            {

                //TODO: [DEBUG] Escalate unexpected update exceptions with enriched diagnostics
                AppLog.Error("RowanStrategy", "Update", $"Rowan Strategy error at Update with message : {ex.Message}");
                throw;
            }
        }

        private void ComputeTradeAction(SlTpData data, Side side ) => this.Trade(side, data.currentPrice, data, data);

        //TODO: [DEBUG] Validate forced close routine ensures manager state consistency
        private bool ForceClosePositions(int max_attempt)
        {
            var posId = Core.Instance.Positions.Where(p => p.Symbol == this.Symbol && p.Account == this.Account)
                .Select(p => p.Id).ToList();
            var objs = this._manager.Items.Where(x => x.Position != null && posId.Contains(x.Position.Id)).ToList();
            foreach (var obj in objs)
            {
                //TODO: [DEBUG] Replace temporary position status refresh during forced close
                var o = obj as TpSlItemPosition;
                o.TryUpdateStatus(true);
            }

            return true;
        }

        private TradeSignal CalculateTradeSignal(bool entrySign)
        {
            //TODO: [DEBUG] Validate signal tallies align with configured thresholds
            List<string> activeNames = entrySign ? _entryLineNames : _exitLineNames;

            int longCount = 0, shortCount = 0;

            void Tally(double v, ref int pos, ref int neg)
            {
                if (v > 0) pos++;
                else if (v < 0) neg++;
            }
            foreach (var name in activeNames)
            {
                var v = GetLineValueByNameAny(name, entrySign);
                Tally(v, ref longCount, ref shortCount);
            }

            switch (entrySign)
            {
                case true:
                    if (longCount > shortCount)
                        if (longCount >= _entryMinConditions)
                            return TradeSignal.OpenBuy;
                        else
                            return TradeSignal.Wait;
                    else if (longCount < shortCount)
                        if (shortCount >= _entryMinConditions)
                            return TradeSignal.OpenSell;
                        else
                            return TradeSignal.Wait;
                    else
                            return TradeSignal.Wait;

                case false:
                    if (longCount > shortCount)
                        if (longCount >= _exitMinConditions)
                            return TradeSignal.CloseSell;
                        else
                            return TradeSignal.Wait;
                    else if (longCount < shortCount)
                        if (shortCount >= _exitMinConditions)
                            return TradeSignal.CloseLong;
                        else
                            return TradeSignal.Wait;
                    else
                        return TradeSignal.Wait;
            }
        }
    }
}


