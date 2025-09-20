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

        // Active line names and X-of-Y thresholds
        private int _entryMinConditions;
        private int _exitMinConditions;
        private readonly List<string> _entryLineNames = new List<string>();
        private readonly List<string> _exitLineNames = new List<string>();

        public bool AllowToTrade
        {
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
            double maxSessionLosUsd, int verbosity_frequency, int slipageAtrPeriod) : base()
        {

            this._atrIndicator = atrsIndicator;
            this._deltaBaseIndicator = DeltaBaseIndicator;
            this._maxOpen = max_open;
            this._totalQuantity = totalquantity;
            this._maxSessionLos = maxSessionLosUsd;
            this._verbosityFreq = verbosity_frequency;
            this._slipageAtrIndicator = Core.Instance.Indicators.BuiltIn.ATR(slipageAtrPeriod, MaMode.SMA);
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

            //📝 TODO: [DEBUG] debug this

            base.OnVolumeDataReady();

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

                //📝 TODO: [Debug Required]
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

        public override double SetQuantity() => this._totalQuantity / this._maxOpen;

        //📝 TODO: [Critical] passare un MarketData object con tutti i dati necessari per le decisioni di trade

        #region 🐞 BUG [Bug noto da risolvere #5] 
        //BUG #5 vengono aperti short mentre la strategia e long 
        #endregion

        public override void Update(object obj)
        {
            this._logsEntry.Clear();
            this._logsExits.Clear();

            //🧠 HINT: [FLOW] ritento l inserimento degli indicatori a causa del bug noto #1
            if (this._atrIndicator.Count == 0)
                this.HistoryProvider.HistoricalData.AddIndicator(this._atrIndicator);
            if (this._deltaBaseIndicator.Count == 0)
                this.HistoryProvider.HistoricalData.AddIndicator(this._deltaBaseIndicator);
            if (this._slipageAtrIndicator.Count == 0)
                this.HistoryProvider.HistoricalData.AddIndicator(this._slipageAtrIndicator);

            //🧠 HINT: [INFO] Base entrypoint dal history provider creato in condizional base tramite il costruttore statico
            HistoryEventArgs e = obj as HistoryEventArgs ?? null;
            if (e == null)
            {
                Core.Instance.Loggers.Log("[RowanStrategy] Rowan Strategy error at Update casting", LoggingLevel.Error);
                Core.Instance.Loggers.Log("[RowanStrategy] Strategy Will be Disabled", LoggingLevel.Error);
                this.ForceClosePositions(5);

                #region 🐞 BUG [Bug noto da risolvere]
                //BUG #4
                #endregion

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
                        Core.Instance.Loggers.Log("[RowanStrategy] Max session loss reached ", LoggingLevel.Trading);
                        this.ForceClosePositions(5);
                        _sessionClosed = true;
                    }
                    return;
                }


                //📝 TODO: [REQUIRED] aggiungere slippage atr
                //📝 TODO: [REQUIRED] verificare se e hd [0], [1] oppure [1], [2]

                SlTpData marketData = new SlTpData()
                {
                    currentPrice = item[PriceType.Open],
                    Symbol = this.Symbol,
                    AtrInTicks = Math.Abs(this.Symbol.CalculateTicks(this._slipageAtrIndicator.GetValue()+ item[PriceType.Open], item[PriceType.Open])),
                };

                //🧠 HINT: [Flusso] ripeto con Market Data
                //SlTpData marketData = (SlTpData)obj;
               

                TradeSignal entry_signal = this.CalculateTradeSignal(true);
                TradeSignal exit_signal = this.CalculateTradeSignal(false);
                TradeAction action = TradeAction.Wait;

                switch (this.Metrics.ExposedSide)
                {
                    case ExpositionSide.Long:
                        if (exit_signal == TradeSignal.CloseLong && entry_signal != TradeSignal.OpenSell)
                            action = TradeAction.Close;
                        else if (entry_signal == TradeSignal.OpenSell)
                            action = TradeAction.Revert;
                        else if (entry_signal == TradeSignal.OpenBuy)
                            action = TradeAction.Buy;
                        else 
                            action = TradeAction.Wait;
                        break;
                    case ExpositionSide.Short:
                        if (exit_signal == TradeSignal.CloseSell && entry_signal != TradeSignal.OpenBuy)
                            action = TradeAction.Close;
                        else if (entry_signal == TradeSignal.OpenBuy)
                            action = TradeAction.Revert;
                        else if (entry_signal == TradeSignal.OpenSell)
                            action = TradeAction.Sell;
                        else
                            action = TradeAction.Wait;
                        break;
                    case ExpositionSide.Both:
                        Core.Instance.Loggers.Log("[RowanStrategy] Exposed on Both Sides positions ", LoggingLevel.Error);
                        this.ForceClosePositions(5);
                        //this._strategyActive = false;
                        break;
                    case ExpositionSide.Unexposed:
                        if (entry_signal == TradeSignal.OpenBuy)
                            action = TradeAction.Buy;
                        else if (entry_signal == TradeSignal.OpenSell)
                            action = TradeAction.Sell;
                        break;
                }

                if (!_strategyActive)
                    return;

                if (action == TradeAction.Buy || action == TradeAction.Sell)
                {
                    if (this.Metrics.ExposedAmount >= _maxOpen * this.Quantity)
                    {
                        Core.Instance.Loggers.Log("[RowanStrategy] " + $"[TRADE SIGNAL] AVOIDED DUE MAX EXPO REACHED", LoggingLevel.Trading);
                        Core.Instance.Loggers.Log("[RowanStrategy] " + $"EntrySignal={entry_signal}, ExitSignal={exit_signal}, Action={action}", LoggingLevel.Trading);
                        foreach (var logsitem in this._logsEntry)
                            Core.Instance.Loggers.Log("[RowanStrategy] " + $"Entry Details: {logsitem}", LoggingLevel.Trading);
                        foreach (var logsExitem in this._logsExits)
                            Core.Instance.Loggers.Log("[RowanStrategy] " + $"Exit Details: {logsExitem}", LoggingLevel.Trading);
                    }
                       
                    else
                    {
                        if (action == TradeAction.Buy)
                        {
                            marketData.SlTriggerPrice = this.HistoryProvider.HistoricalData[1][PriceType.Low];
                            this.ComputeTradeAction(marketData, Side.Buy);

                        }
                        else if (action == TradeAction.Sell)
                        {
                            marketData.SlTriggerPrice = this.HistoryProvider.HistoricalData[1][PriceType.High];
                            this.ComputeTradeAction(marketData, Side.Sell);
                        }
                        Core.Instance.Loggers.Log("[RowanStrategy] " + $"[TRADE SIGNAL] EntrySignal={entry_signal}, ExitSignal={exit_signal}, Action={action}", LoggingLevel.Trading);
                        foreach (var logsitem in this._logsEntry)
                            Core.Instance.Loggers.Log("[RowanStrategy] " + $"Entry Details: {logsitem}", LoggingLevel.Trading);
                        foreach (var logsExitem in this._logsExits)
                            Core.Instance.Loggers.Log("[RowanStrategy] " + $"Exit Details: {logsExitem}", LoggingLevel.Trading);
                    }
                }


                //📝 TODO: [DEBUG] check if this logic works and correctly effects on items

                if (action == TradeAction.Close)
                {
                    bool res = this.ForceClosePositions(5);

                    if (res)
                    {
                        Core.Instance.Loggers.Log("[RowanStrategy] " + $"[TRADE SIGNAL] ALL POSITIONS CLOSED EntrySignal={entry_signal}, ExitSignal={exit_signal}, Action={action}", LoggingLevel.Trading);
                        foreach (var logsitem in this._logsEntry)
                            Core.Instance.Loggers.Log("[RowanStrategy] " + $"Entry Details: {logsitem}", LoggingLevel.Trading);
                        foreach (var logsExitem in this._logsExits)
                            Core.Instance.Loggers.Log("[RowanStrategy] " + $"Exit Details: {logsExitem}", LoggingLevel.Trading);
                    }
                    else
                    {
                        Core.Instance.Loggers.Log("[RowanStrategy] " + $"[TRADE SIGNAL] FAILED TO CLOSE POSITIONS, STRATEGY STOPPED EntrySignal={entry_signal}, ExitSignal={exit_signal}, Action={action}", LoggingLevel.Error);
                        foreach (var logsitem in this._logsEntry)
                            Core.Instance.Loggers.Log("[RowanStrategy] " + $"Entry Details: {logsitem}", LoggingLevel.Trading);
                        foreach (var logsExitem in this._logsExits)
                            Core.Instance.Loggers.Log("[RowanStrategy] " + $"Exit Details: {logsExitem}", LoggingLevel.Trading);
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

                        Core.Instance.Loggers.Log("[RowanStrategy] " + $"[TRADE SIGNAL] ALL POSITIONS CLOSED FOR REVERSAL EntrySignal={entry_signal}, ExitSignal={exit_signal}, Action={action}", LoggingLevel.Trading);
                        foreach (var logsitem in this._logsEntry)
                            Core.Instance.Loggers.Log("[RowanStrategy] " + $"Entry Details: {logsitem}", LoggingLevel.Trading);
                        foreach (var logsExitem in this._logsExits)
                            Core.Instance.Loggers.Log("[RowanStrategy] " + $"Exit Details: {logsExitem}", LoggingLevel.Trading);

                        this.ComputeTradeAction(marketData, entry_signal == TradeSignal.OpenBuy ? Side.Buy : Side.Sell);
                    }
                    else
                    {
                        Core.Instance.Loggers.Log("[RowanStrategy] " + $"[TRADE SIGNAL] FAILED TO CLOSE POSITIONS FOR REVERSAL", LoggingLevel.Error);
                        this._strategyActive = false;
                    }
                }

                else
                {
                    try
                    {
                        this.UpdateSlTp(marketData, isSl: true);

                    }
                    catch { Utils.AppLog.Error("Update Sl", "Failed to update Sl"); }
                }

                if (this._verbosityFreqCount <= this._verbosityFreq)
                {
                    this._verbosityFreqCount++;

                    if (this._verbosityFreqCount == this._verbosityFreq)
                    {
                        Core.Instance.Loggers.Log("[RowanStrategy] " + $"[VERBOSE] Strategy status: SessionActive={StaticSessionManager.CurrentStatus}, AllowToTrade={this.AllowToTrade}", LoggingLevel.System);
                        Core.Instance.Loggers.Log("[RowanStrategy] " + $"[VERBOSE] ExposedSide={this.Metrics.ExposedSide}, ExposedCount={this.Metrics.ExposedCount}", LoggingLevel.System);
                        Core.Instance.Loggers.Log("[RowanStrategy] " + $"[VERBOSE] EntrySignal={entry_signal}, ExitSignal={exit_signal}, Action={action}", LoggingLevel.System);
                        foreach (var logsitem in this._logsEntry)
                            Core.Instance.Loggers.Log("[RowanStrategy] " + $"Entry Details: {logsitem}", LoggingLevel.System);
                        foreach (var logsExitem in this._logsExits)
                            Core.Instance.Loggers.Log("[RowanStrategy] " + $"Exit Details: {logsExitem}", LoggingLevel.System);
                        this._verbosityFreqCount = 0;
                    }
                }


            }
            catch (Exception ex)
            {

                //📝 TODO: [Log]
                Core.Instance.Loggers.Log("[RowanStrategy] " + $"Rowan Strategy error at Update with message : {ex.Message}", LoggingLevel.Error);
                throw;
            }
        }

        private void ComputeTradeAction(SlTpData data, Side side ) => this.Trade(side, data.currentPrice, data, data);

        private bool ForceClosePositions(int max_attempt)
        {
            var posId = Core.Instance.Positions.Where(p => p.Symbol == this.Symbol && p.Account == this.Account)
                .Select(p => p.Id).ToList();
            var objs = this._manager.Items.Where(x => x.Position != null && posId.Contains(x.Position.Id)).ToList();
            foreach (var obj in objs)
            {

                #region 🧪 HACK [Soluzione temporanea]
                //Sistemare questa porcheria
                #endregion

                var o = obj as TpSlItemPosition;
                o.TryUpdateStatus(true);
            }

            return true;
        }

        private TradeSignal CalculateTradeSignal(bool entrySign)
        {
            List<string> activeNames = entrySign ? _entryLineNames : _exitLineNames;

            int longCount = 0, shortCount = 0;

            void Tally(double v, ref int pos, ref int neg)
            {
                if (v > 0) pos++;
                else if (v < 0) neg++;
            }

            // Entry tally via active names
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

