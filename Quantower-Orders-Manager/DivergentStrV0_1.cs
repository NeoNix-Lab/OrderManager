using DivergentStrV0_1.OperationSystemAdv;
using DivergentStrV0_1.Strategies;
using DivergentStrV0_1.Utils;
using System;
using System.Collections.Generic;
using System.Diagnostics.Metrics;
using System.Linq;
using TradingPlatform.BusinessLayer;

namespace DivergentStrV0_1
{
    public class DivergentStrV0_1 : Strategy
    {
        #region ====== Keys for Settings UI ======
        private const string KEY_STRAT = "######## Strategy Settings ######";
        private const string KEY_ENTRY = "######## Entry Conditions ######";
        private const string KEY_EXIT = "######## Exit Conditions ######";
        private const string KEY_ATR = "######## Atr Indicator Settings ######";
        private const string KEY_DELTA = "######## Delta Settings ######";
        private const string KEY_SESS = "######## Sessions Settings ######";
        private const string KEY_SESS_COUNT = "######## Custom sessions count ######";
        private const string KEY_SESS_USEDEFAULT = "Use Default Sessions";
        #endregion

        #region ====== UI Toggles ======
        private bool _uiShowEnv = true;
        private bool _uiShowStrat = true;
        private bool _uiShowAtr = false;
        private bool _uiShowDelta = false;
        #endregion

        #region ====== ATR Settings (UI) ======
        private int _uiAtrLen = 14;
        private bool _uiAtrNormalize = true;
        private double _uiAtrSlopeThr = 0.015;
        private double _uiAtrSlippageMultiplier = 0.0;

        private int _uiRvolShortLen = 14;
        private int _uiRvolLongLen = 60;

        private bool _uiHmaUsePrice = true;
        private int _uiHmaLen = 14;
        private bool _uiUseAtrScaledHma = false;
        #endregion

        #region ====== Delta Settings (UI) ======
        private bool _uiDeltaUseMedian = false;
        private double _uiDeltaLookback = 30.0;
        private double _uiDeltaThresholdMult = 2.0;
        private double _uiDeltaStrengthLookback = 30.0;
        private double _uiDeltaStrengthMult = 2.0;
        #endregion

        #region ====== Sessions ======
        private int _CustomSessionsCount = 0;
        private bool _UseDefaultSessions = true;
        private List<SimpleSessionUtc> _CustomSessions = new List<SimpleSessionUtc>();
        private Dictionary<int, List<DayOfWeek>> _sessionDays = new Dictionary<int, List<DayOfWeek>>();
        #endregion

        #region ====== Strategy Parameters ======
        private double _quantity = 1000.0;
        private double _minSlInTicks = 20.0;
        private double _maxSlInTicks = 500.0;
        private double _maxTpInTicks = 1000.0;
        private bool _debugMode = false;
        private int _maxOpen = 3;
        private double _maxSessionLossUsd = 100.0;
        private int _verbosityFrequency = 3;
        private int _slippageAtrPeriod = 14;

        // Entry / Exit conditions
        private bool _entryUseRVOL = true;
        private bool _entryUseVDPS = true;
        private bool _entryUseVDStrong = true;
        private bool _entryUseHMA = true;
        private bool _entryUseVDtV = false;
        private bool _entryUseVDP = false;
        private int _entryMinConditions = 2;

        private bool _exitUseRVOL = true;
        private bool _exitUseVDPS = true;
        private bool _exitUseVDStrong = true;
        private bool _exitUseHMA = true;
        private bool _exitUseVDtV = false;
        private bool _exitUseVDP = true;
        private int _exitMinConditions = 1;
        #endregion

        #region ====== Input Parameters (Quantower Integration) ======
        [InputParameter("Symbol", 0)]
        public Symbol _Symbol;

        [InputParameter("Account", 1)]
        public Account _Account;

        [InputParameter("From Time (UTC)", 2)]
        public DateTime _fomTime = DateTime.UtcNow.AddDays(-30.0);

        [InputParameter("Async Mode", 3)]
        public bool _inputDebugMode = false;

        [InputParameter("Period", 4)]
        public Period _period = Period.MIN1;
        #endregion

        #region ====== Runtime State ======
        private HistoricalData hd;
        private Indicator AtrIndicator;
        private Indicator DeltaIndicato;
        private RowanStrategy _strategy;
        private IConditionable _conditionable = new RowanStrategy();
        private bool Debug = false;
        private bool readyToGo;
        #endregion

        #region ====== Public Properties ======
        public double Quantity => _quantity;
        public double MinSlInTicks => _minSlInTicks;
        public double MaxSlInTicks => _maxSlInTicks;
        public double MaxTpInTicks => _maxTpInTicks;
        public bool DebugMode => Debug;
        public IReadOnlyList<SimpleSessionUtc> CustomSessions => _CustomSessions.AsReadOnly();
        public int CustomSessionsCount => _CustomSessionsCount;

        private double procesPercent => hd?.VolumeAnalysisCalculationProgress?.ProgressPercent ?? 0.0;
        private bool volumesLoaded => hd?.VolumeAnalysisCalculationProgress?.ProgressPercent == 100;
        #endregion

        #region ====== Constructor ======
        public DivergentStrV0_1()
        {
            this.Name = this._conditionable.StrategyName;
            this.Description = "Rowan Strategy";
        }
        #endregion

        #region ====== Lifecycle ======
        protected override void OnCreated()
        {
            Core.Instance.Loggers.Log("[Lifecycle] OnCreated", LoggingLevel.System);
        }

        protected override void OnRun()
        {
            // Indicators
            this.AtrIndicator = Core.Instance.Indicators.CreateIndicator(
                Core.Instance.Indicators.All.FirstOrDefault(x => x.Name == "RVOL (evolved)"));

            this.DeltaIndicato = Core.Instance.Indicators.CreateIndicator(
                Core.Instance.Indicators.All.FirstOrDefault(x => x.Name == "DeltaBasedIndicators"));

            // Configure ATR Indicator
            this.AtrIndicator.Settings = new List<SettingItem>
            {
                new SettingItemInteger("Short Length", _uiRvolShortLen),
                new SettingItemInteger("Long Length", _uiRvolLongLen),
                new SettingItemBoolean("Use Price for HMA", _uiHmaUsePrice),
                new SettingItemInteger("HMA Length", _uiHmaLen),
                new SettingItemBoolean("Use ATR-scaled HMA", _uiUseAtrScaledHma),
                new SettingItemInteger("ATR Length", _uiAtrLen),
                new SettingItemBoolean("Use ATR Normalization", _uiAtrNormalize),
                new SettingItemDouble("Slope Threshold (norm.)", _uiAtrSlopeThr)
            };

            // Configure Delta Indicator
            this.DeltaIndicato.Settings = new List<SettingItem>
            {
                new SettingItemBoolean("Force Volume Ready", true),
                new SettingItemBoolean("Delta: Use Median", _uiDeltaUseMedian),
                new SettingItemDouble("Delta: Lookback", _uiDeltaLookback),
                new SettingItemDouble("Delta: Threshold Multiplier", _uiDeltaThresholdMult),
                new SettingItemDouble("Delta Strength: Lookback", _uiDeltaStrengthLookback),
                new SettingItemDouble("Delta Strength: Threshold Multiplier", _uiDeltaStrengthMult)
            };

            // History request
            if (!_conditionable.Initialized)
            {
                var req = new HistoryRequestParameters
                {
                    Aggregation = new HistoryAggregationTime(_period, HistoryType.Last),
                    FromTime = _fomTime,
                    ToTime = default,
                    Symbol = _Symbol
                };

                // Sessions
                if (_UseDefaultSessions || _CustomSessionsCount == 0 || _CustomSessions.Count == 0)
                {
                    foreach (var s in OffMarketUtc.Build())
                        StaticSessionManager.AddSession(s, Utils.SessionType.Target);

                    foreach (var sv in InMarketUtc.Build())
                        StaticSessionManager.AddSession(sv, Utils.SessionType.Trade);
                }
                else
                {
                    foreach (var cs in _CustomSessions)
                        StaticSessionManager.AddSession(cs, Utils.SessionType.Trade);
                }

                // Strategy init
                _strategy = new RowanStrategy(
                    this.DeltaIndicato,
                    this.AtrIndicator,
                    _maxOpen,
                    _quantity,
                    _maxSessionLossUsd,
                    _verbosityFrequency,
                    Math.Max(1, _slippageAtrPeriod));

                // Configure entry / exit conditions
                var entryLineNames = new List<string>();
                if (_entryUseRVOL) entryLineNames.Add("RvolSignal");
                if (_entryUseHMA) entryLineNames.Add("HMA_Direction");
                if (_entryUseVDPS) entryLineNames.Add("APAVD_Flag");
                if (_entryUseVDStrong) entryLineNames.Add("VD_Strength_Flag");
                if (_entryUseVDtV) entryLineNames.Add("VD_to_Volume_Flag");
                if (_entryUseVDP) entryLineNames.Add("VD_Price_Divergent_Flag");

                var exitLineNames = new List<string>();
                if (_exitUseRVOL) exitLineNames.Add("RvolSignal");
                if (_exitUseHMA) exitLineNames.Add("HMA_Direction");
                if (_exitUseVDPS) exitLineNames.Add("APAVD_Flag");
                if (_exitUseVDStrong) exitLineNames.Add("VD_Strength_Flag");
                if (_exitUseVDtV) exitLineNames.Add("VD_to_Volume_Flag");
                if (_exitUseVDP) exitLineNames.Add("VD_Price_Divergent_Flag");

                _strategy.ConfigureConditions(entryLineNames, _entryMinConditions, exitLineNames, _exitMinConditions);

                _strategy.InjectStrategy(new RowanSlTpStrategy(
                    (int)Math.Round(_minSlInTicks),
                    (int)Math.Round(_maxSlInTicks))
                {
                    MinTpInTicks = (int)Math.Max(1, Math.Round(_maxTpInTicks)),
                    AtrSlippageMultiplier = Math.Max(0.0, Math.Min(2.0, _uiAtrSlippageMultiplier))
                });

                _strategy.Init(req, _Account, _inputDebugMode, "", false);
                _conditionable = _strategy;
            }
        }

        protected override void OnStop()
        {
            readyToGo = false;
            StaticSessionManager.Dispose();
            _conditionable?.Dispose();
        }

        protected override void OnRemove()
        {
            // Cleanup if needed
        }

        public override IList<SettingItem> Settings
        {
            get
            {
                var settings = base.Settings;

                #region ===== 100x — Sessions =====
                settings.Add(new SettingItemBoolean(KEY_SESS, false)
                {
                    Text = KEY_SESS,
                    SortIndex = 1000,
                });

                settings.Add(new SettingItemInteger(KEY_SESS_COUNT, 0)
                {
                    Text = KEY_SESS_COUNT,
                    SortIndex = 1000,
                    Minimum = 0,
                    Maximum = 3,
                    Relation = new SettingItemRelationVisibility(KEY_SESS, true)
                });

                settings.Add(new SettingItemBoolean(KEY_SESS_USEDEFAULT, false)
                {
                    Text = KEY_SESS_USEDEFAULT,
                    SortIndex = 1000,
                    Value = false
                });

                for (int i = 0; i < 3; i++)
                {
                    SettingItemRelationVisibility relation =
                        (i == 0) ? new SettingItemRelationVisibility(KEY_SESS_COUNT, 1, 2, 3) :
                        (i == 1) ? new SettingItemRelationVisibility(KEY_SESS_COUNT, 2, 3) :
                                   new SettingItemRelationVisibility(KEY_SESS_COUNT, 3);

                    SimpleSessionUtc current = (this._CustomSessions.Count > i) ? this._CustomSessions[i] : null;

                    settings.Add(new SettingItemDateTime($"session{i + 1}Start", current != null
                        ? DateTime.Today.AddHours(current.Open.Hour).AddMinutes(current.Open.Minute)
                        : DateTime.UtcNow)
                    {
                        Text = $"Session {i + 1} start (UTC)",
                        SortIndex = 1001 + i * 2,
                        Relation = relation
                    });

                    settings.Add(new SettingItemDateTime($"session{i + 1}End", current != null
                        ? DateTime.Today.AddHours(current.Close.Hour).AddMinutes(current.Close.Minute)
                        : DateTime.UtcNow)
                    {
                        Text = $"Session {i + 1} end (UTC)",
                        SortIndex = 1002 + i * 2,
                        Relation = relation
                    });

                    foreach (DayOfWeek day in Enum.GetValues(typeof(DayOfWeek)))
                    {
                        settings.Add(new SettingItemBoolean($"session{i + 1}On{day}", true)
                        {
                            Text = $"Session {i + 1} on {day}",
                            SortIndex = 1003 + i * 2,
                            Relation = relation
                        });
                    }
                }
                #endregion

                #region ===== 300x — Strategy =====
                settings.Add(new SettingItemBoolean(KEY_STRAT, _uiShowStrat)
                {
                    Text = KEY_STRAT,
                    SortIndex = 3000
                });

                settings.Add(new SettingItemDouble("Quantity", _quantity)
                {
                    Text = "Quantity",
                    SortIndex = 3001,
                    Minimum = 0.0001,
                    Maximum = 1_000_000,
                    Increment = 0.0001,
                    Relation = new SettingItemRelationVisibility(KEY_STRAT, true)
                });

                settings.Add(new SettingItemDouble("Min SL In Ticks", _minSlInTicks)
                {
                    Text = "Min SL In Ticks",
                    SortIndex = 3003,
                    Minimum = 1,
                    Maximum = 15000,
                    Increment = 1,
                    Relation = new SettingItemRelationVisibility(KEY_STRAT, true)
                });

                settings.Add(new SettingItemDouble("Max SL In Ticks", _maxSlInTicks)
                {
                    Text = "Max SL In Ticks",
                    SortIndex = 3004,
                    Minimum = 1,
                    Maximum = 50000,
                    Increment = 1,
                    Relation = new SettingItemRelationVisibility(KEY_STRAT, true)
                });

                settings.Add(new SettingItemDouble("Max Tp In Ticks", _maxTpInTicks)
                {
                    Text = "Max TP In Ticks",
                    SortIndex = 3005,
                    Minimum = 1,
                    Maximum = 50000,
                    Increment = 1,
                    Relation = new SettingItemRelationVisibility(KEY_STRAT, true)
                });

                settings.Add(new SettingItemInteger("Max Open Positions", _maxOpen)
                {
                    Text = "Max Open Positions",
                    SortIndex = 3006,
                    Minimum = 1,
                    Maximum = 100,
                    Relation = new SettingItemRelationVisibility(KEY_STRAT, true)
                });

                settings.Add(new SettingItemInteger("Min Trade Signal", _entryMinConditions)
                {
                    Text = "Min Trade Signal",
                    SortIndex = 3007,
                    Minimum = 0,
                    Maximum = 100,
                    Relation = new SettingItemRelationVisibility(KEY_STRAT, true)
                });

                settings.Add(new SettingItemInteger("Min Close Signal", _exitMinConditions)
                {
                    Text = "Min Close Signal",
                    SortIndex = 3008,
                    Minimum = 0,
                    Maximum = 100,
                    Relation = new SettingItemRelationVisibility(KEY_STRAT, true)
                });

                settings.Add(new SettingItemDouble("Max Session Loss USD", _maxSessionLossUsd)
                {
                    Text = "Max Session Loss USD",
                    SortIndex = 3009,
                    Minimum = -1000000,
                    Maximum = 1000000,
                    Increment = 0.01,
                    Relation = new SettingItemRelationVisibility(KEY_STRAT, true)
                });

                settings.Add(new SettingItemInteger("Verbosity Frequency", _verbosityFrequency)
                {
                    Text = "Verbosity Frequency",
                    SortIndex = 3010,
                    Minimum = 0,
                    Maximum = 1000,
                    Relation = new SettingItemRelationVisibility(KEY_STRAT, true)
                });

                settings.Add(new SettingItemInteger("Slippage ATR Period", _slippageAtrPeriod)
                {
                    Text = "Slippage ATR Period",
                    SortIndex = 3011,
                    Minimum = 1,
                    Maximum = 1000,
                    Relation = new SettingItemRelationVisibility(KEY_STRAT, true)
                });

                settings.Add(new SettingItemDouble("ATR Slippage Multiplier", _uiAtrSlippageMultiplier)
                {
                    Text = "ATR Slippage Multiplier",
                    SortIndex = 3012,
                    Minimum = 0.0,
                    Maximum = 2.0,
                    Increment = 0.01,
                    Relation = new SettingItemRelationVisibility(KEY_STRAT, true)
                });
                #endregion

                #region ===== 320x — Entry Conditions =====
                settings.Add(new SettingItemBoolean("######## Entry Conditions ######", true)
                {
                    Text = "######## Entry Conditions ######",
                    SortIndex = 3020,
                    Relation = new SettingItemRelationVisibility(KEY_STRAT, true)
                });

                settings.Add(new SettingItemInteger("Entry: Min Conditions", _entryMinConditions)
                {
                    Text = "Entry: Min Conditions — minimum true among selected",
                    SortIndex = 3021,
                    Minimum = 0,
                    Maximum = 6,
                    Relation = new SettingItemRelationVisibility("######## Entry Conditions ######", true)
                });

                settings.Add(new SettingItemBoolean("Entry Use: RVOL", _entryUseRVOL)
                {
                    Text = "Entry Use: RVOL — Normalized RVOL momentum",
                    SortIndex = 3022,
                    Relation = new SettingItemRelationVisibility("######## Entry Conditions ######", true)
                });

                settings.Add(new SettingItemBoolean("Entry Use: VDPS", _entryUseVDPS)
                {
                    Text = "Entry Use: VDPS — Price/Delta Ratio (APAVD)",
                    SortIndex = 3023,
                    Relation = new SettingItemRelationVisibility("######## Entry Conditions ######", true)
                });

                settings.Add(new SettingItemBoolean("Entry Use: VDstrong", _entryUseVDStrong)
                {
                    Text = "Entry Use: VDstrong — Delta Strength (|VD| vs avg |VD|)",
                    SortIndex = 3024,
                    Relation = new SettingItemRelationVisibility("######## Entry Conditions ######", true)
                });

                settings.Add(new SettingItemBoolean("Entry Use: HMA", _entryUseHMA)
                {
                    Text = "Entry Use: HMA — HMA Direction (Close vs HMA)",
                    SortIndex = 3025,
                    Relation = new SettingItemRelationVisibility("######## Entry Conditions ######", true)
                });

                settings.Add(new SettingItemBoolean("Entry Use: VDtV", _entryUseVDtV)
                {
                    Text = "Entry Use: VDtV — Delta-to-Volume Ratio (|VD|/Volume)",
                    SortIndex = 3026,
                    Relation = new SettingItemRelationVisibility("######## Entry Conditions ######", true)
                });

                settings.Add(new SettingItemBoolean("Entry Use: VDP", _entryUseVDP)
                {
                    Text = "Entry Use: VDP — VD-Price Divergence",
                    SortIndex = 3027,
                    Relation = new SettingItemRelationVisibility("######## Entry Conditions ######", true)
                });
                #endregion

                #region ===== 330x — Exit Conditions =====
                settings.Add(new SettingItemBoolean("######## Exit Conditions ######", true)
                {
                    Text = "######## Exit Conditions ######",
                    SortIndex = 3030,
                    Relation = new SettingItemRelationVisibility(KEY_STRAT, true)
                });

                settings.Add(new SettingItemInteger("Exit: Min Conditions", _exitMinConditions)
                {
                    Text = "Exit: Min Conditions — minimum true among selected",
                    SortIndex = 3031,
                    Minimum = 0,
                    Maximum = 6,
                    Relation = new SettingItemRelationVisibility("######## Exit Conditions ######", true)
                });

                settings.Add(new SettingItemBoolean("Exit Use: RVOL", _exitUseRVOL)
                {
                    Text = "Exit Use: RVOL — Normalized RVOL momentum",
                    SortIndex = 3032,
                    Relation = new SettingItemRelationVisibility("######## Exit Conditions ######", true)
                });

                settings.Add(new SettingItemBoolean("Exit Use: VDPS", _exitUseVDPS)
                {
                    Text = "Exit Use: VDPS — Price/Delta Ratio (APAVD)",
                    SortIndex = 3033,
                    Relation = new SettingItemRelationVisibility("######## Exit Conditions ######", true)
                });

                settings.Add(new SettingItemBoolean("Exit Use: VDstrong", _exitUseVDStrong)
                {
                    Text = "Exit Use: VDstrong — Delta Strength (|VD| vs avg |VD|)",
                    SortIndex = 3034,
                    Relation = new SettingItemRelationVisibility("######## Exit Conditions ######", true)
                });

                settings.Add(new SettingItemBoolean("Exit Use: HMA", _exitUseHMA)
                {
                    Text = "Exit Use: HMA — HMA Direction (Close vs HMA)",
                    SortIndex = 3035,
                    Relation = new SettingItemRelationVisibility("######## Exit Conditions ######", true)
                });

                settings.Add(new SettingItemBoolean("Exit Use: VDtV", _exitUseVDtV)
                {
                    Text = "Exit Use: VDtV — Delta-to-Volume Ratio (|VD|/Volume)",
                    SortIndex = 3036,
                    Relation = new SettingItemRelationVisibility("######## Exit Conditions ######", true)
                });

                settings.Add(new SettingItemBoolean("Exit Use: VDP", _exitUseVDP)
                {
                    Text = "Exit Use: VDP — VD-Price Divergence",
                    SortIndex = 3037,
                    Relation = new SettingItemRelationVisibility("######## Exit Conditions ######", true)
                });
                #endregion

                #region ===== 400x — ATR =====
                settings.Add(new SettingItemBoolean(KEY_ATR, _uiShowAtr)
                {
                    Text = KEY_ATR,
                    SortIndex = 4000
                });

                settings.Add(new SettingItemInteger(nameof(_uiAtrLen), _uiAtrLen)
                {
                    Text = "ATR Length",
                    SortIndex = 4001,
                    Minimum = 2,
                    Maximum = 200,
                    Relation = new SettingItemRelationVisibility(KEY_ATR, true)
                });

                settings.Add(new SettingItemBoolean(nameof(_uiAtrNormalize), _uiAtrNormalize)
                {
                    Text = "Use ATR Normalization",
                    SortIndex = 4002,
                    Relation = new SettingItemRelationVisibility(KEY_ATR, true)
                });

                settings.Add(new SettingItemDouble(nameof(_uiAtrSlopeThr), _uiAtrSlopeThr)
                {
                    Text = "Slope Threshold (norm.)",
                    SortIndex = 4003,
                    Minimum = 0.0,
                    Maximum = 1.0,
                    Increment = 0.001,
                    Relation = new SettingItemRelationVisibility(KEY_ATR, true)
                });
                #endregion

                #region ===== 500x — Delta =====
                settings.Add(new SettingItemBoolean(KEY_DELTA, _uiShowDelta)
                {
                    Text = KEY_DELTA,
                    SortIndex = 5000
                });

                settings.Add(new SettingItemBoolean(nameof(_uiDeltaUseMedian), _uiDeltaUseMedian)
                {
                    Text = "Delta: Use Median",
                    SortIndex = 5001,
                    Relation = new SettingItemRelationVisibility(KEY_DELTA, true)
                });

                settings.Add(new SettingItemDouble(nameof(_uiDeltaLookback), _uiDeltaLookback)
                {
                    Text = "Delta: Lookback",
                    SortIndex = 5002,
                    Minimum = 5,
                    Maximum = 1000,
                    Relation = new SettingItemRelationVisibility(KEY_DELTA, true)
                });

                settings.Add(new SettingItemDouble(nameof(_uiDeltaThresholdMult), _uiDeltaThresholdMult)
                {
                    Text = "Delta: Threshold Multiplier",
                    SortIndex = 5003,
                    Minimum = 0.1,
                    Maximum = 20.0,
                    Increment = 0.1,
                    Relation = new SettingItemRelationVisibility(KEY_DELTA, true)
                });

                settings.Add(new SettingItemDouble(nameof(_uiDeltaStrengthLookback), _uiDeltaStrengthLookback)
                {
                    Text = "Delta Strength: Lookback",
                    SortIndex = 5004,
                    Minimum = 5,
                    Maximum = 1000,
                    Relation = new SettingItemRelationVisibility(KEY_DELTA, true)
                });

                settings.Add(new SettingItemDouble(nameof(_uiDeltaStrengthMult), _uiDeltaStrengthMult)
                {
                    Text = "Delta Strength: Threshold Multiplier",
                    SortIndex = 5005,
                    Minimum = 0.1,
                    Maximum = 20.0,
                    Increment = 0.1,
                    Relation = new SettingItemRelationVisibility(KEY_DELTA, true)
                });
                #endregion

                return settings;
            }
        }


        protected override void OnInitializeMetrics(Meter meter)
        {
            base.OnInitializeMetrics(meter);

            meter.CreateObservableGauge("DivergentStrV0_1_account_balance_usd",
                () => _Account?.Balance ?? 0.0,
                "$", "Account Balance (USD)");

            meter.CreateObservableGauge("DivergentStrV0_1_exposure_base_usd",
                () =>
                {
                    try
                    {
                        double valueOrDefault = _conditionable?.Metrics?.ExposedAmount ?? 0;
                        if (_conditionable is RowanStrategy rs &&
                            rs.HistoryProvider?.HistoricalData != null &&
                            rs.HistoryProvider.HistoricalData.Count > 0)
                        {
                            double price = rs.HistoryProvider.HistoricalData[0, SeekOriginHistory.Begin][PriceType.Close];
                            return valueOrDefault * Math.Abs(price);
                        }
                    }
                    catch { }
                    return 0.0;
                },
                "$", "Exposure in base currency (USD)");

            meter.CreateObservableGauge("DivergentStrV0_1_gross_pnl_usd",
                () => _conditionable?.Metrics?.GrossProfit ?? 0,
                "$", "Gross PnL (USD)");

            meter.CreateObservableGauge("DivergentStrV0_1_net_pnl_usd",
                () => _conditionable?.Metrics?.NetProfit ?? 0,
                "$", "Net PnL (USD)");

            meter.CreateObservableGauge("DivergentStrV0_1_trade_session_active_flag",
                () => StaticSessionManager.CurrentStatus == Status.Active ? 1 : 0,
                "flag", "Trade Session Active (1/0)");
        }
        #endregion
    }
}
