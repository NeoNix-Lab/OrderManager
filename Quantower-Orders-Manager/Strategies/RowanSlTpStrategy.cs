using DivergentStrV0_1.OperationSystemAdv;
using DivergentStrV0_1.OperationSystemAdv.DDDCore;
using DivergentStrV0_1.Utils;
using System;
using System.Collections.Generic;
using System.Linq;
using TradingPlatform.BusinessLayer;

namespace DivergentStrV0_1.Strategies
{
    public struct SlTpData
    {
        public Symbol Symbol { get; set; }

        public double SlTriggerPrice { get; set; }
        public double currentPrice { get; set; }
        public double AtrInTicks { get; set; }
    }

    internal class RowanSlTpStrategy : ISlTpStrategy<SlTpData>
    {
        //TODO: [DEBUG] Expose ATR and TP distance tunables through validated parameters

        public int max_slInTicks { get; set; }
        public int min_slInTicks { get; set; }

        public int MinTpInTicks { get; set; }
        public int MaxTpInTicks { get; set; }
        //TODO: [DEBUG] Keep ATR slippage multiplier inside [0,2] guardrails
        public double AtrSlippageMultiplier { get; set; } = 0.0;
        private int delta_InTicks;

        public RowanSlTpStrategy(int min_Tick, int max_Tick)
        {
            if (min_Tick <= 0)
                throw new ArgumentException("Min ticks must be positive", nameof(min_Tick));
            if (max_Tick <= 0)
                throw new ArgumentException("Max ticks must be positive", nameof(max_Tick));
            if (min_Tick > max_Tick)
                throw new ArgumentException("Min ticks cannot be greater than max ticks");

            this.max_slInTicks = max_Tick;
            this.min_slInTicks = min_Tick;
            delta_InTicks = Math.Abs(min_Tick - max_Tick);
        }

        public List<double> CalculateSl(SlTpData marketData, Side side, double entry_price)
        {
            //TODO: [DEBUG] Derive SL from previous candle range plus ATR multiplier, enforcing min/max distance guards

            var sl_temp = marketData.Symbol.CalculateTicks(entry_price, marketData.SlTriggerPrice);
            //TODO: [DEBUG] Confirm ATR slippage contribution respects configured bounds
            var extraTicks = Math.Max(0, (int)Math.Round(Math.Abs(marketData.AtrInTicks) * Math.Max(0.0, Math.Min(2.0, this.AtrSlippageMultiplier))));
            var desiredAbsTicks = Math.Abs(sl_temp) + extraTicks;
            var sl = (int)Math.Clamp(Math.Ceiling(desiredAbsTicks), this.min_slInTicks, this.max_slInTicks);

            double sl_price = side == Side.Buy
                ? marketData.Symbol.CalculatePrice(entry_price, -sl)
                : marketData.Symbol.CalculatePrice(entry_price, +sl);

            return new List<double> { sl_price };
        }

        public List<double> CalculateTp(SlTpData marketData, Side side, double entry_price)
        {
            //TODO: [DEBUG] Ensure TP level cache is refreshed before evaluating targets
            if (!StaticSessionManager.TpLevels.Levels.Any())
                StaticSessionManager.CalculateTPLevels();

            int minTpTicks = Math.Max(1, this.MinTpInTicks);
            int maxTpTicks = this.MaxTpInTicks <= 0 ? minTpTicks : Math.Max(minTpTicks, this.MaxTpInTicks);

            double GetTargetPrice(int ticks) => marketData.Symbol.CalculatePrice(entry_price, side == Side.Buy ? ticks : -ticks);

            double minTargetPrice = GetTargetPrice(minTpTicks);
            double maxTargetPrice = GetTargetPrice(maxTpTicks);

            var candidateTargets = new List<double>();
            foreach (var level in StaticSessionManager.TpLevels.Levels)
            {
                foreach (var candidate in new[] { level.High, level.Low })
                {
                    if (double.IsNaN(candidate) || double.IsInfinity(candidate))
                        continue;

                    double ticks = marketData.Symbol.CalculateTicks(entry_price, candidate);
                    bool isValidDirection = side == Side.Buy ? ticks > 0 : ticks < 0;
                    double absTicks = Math.Abs(ticks);

                    if (isValidDirection && absTicks >= minTpTicks && absTicks <= maxTpTicks)
                        candidateTargets.Add(candidate);
                }
            }

            double selectedTpItem;
            if (candidateTargets.Count > 0)
            {
                selectedTpItem = candidateTargets
                    .OrderBy(price => Math.Abs(marketData.Symbol.CalculateTicks(entry_price, price)))
                    .First();
            }
            else
            {
                //TODO: [DEBUG] Verify fallback target clamps to configured bounds when no TP level matches
                selectedTpItem = minTargetPrice;
                double fallbackTicks = Math.Abs(marketData.Symbol.CalculateTicks(entry_price, selectedTpItem));
                if (fallbackTicks > maxTpTicks)
                    selectedTpItem = maxTargetPrice;
            }

            return new List<double> { selectedTpItem };
        }

        public Func<double, double> UpdateSl(SlTpData marketData, ITpSlItems item)
        {
            //TODO: [DEBUG] Reconcile SL adjustments with live order book state
            //TODO: [DEBUG] Rework SL trailing logic around previous candle structure and order lifecycle management

            try
            {
                return current_sl =>
                {
                    //TODO: [DEBUG] Replace placeholder delta check with candle-based recalculation
                    var delta = marketData.Symbol.CalculateTicks(current_sl, marketData.currentPrice);
                    bool isOut = delta > this.delta_InTicks;

                    if (!isOut)
                        return current_sl;

                    //TODO: [DEBUG] Flesh out trailing branch coverage for all order states
                    return item.Side switch
                    {
                        Side.Buy => marketData.Symbol.CalculatePrice(marketData.currentPrice, -max_slInTicks),
                        Side.Sell => marketData.Symbol.CalculatePrice(marketData.currentPrice, +max_slInTicks),
                        _ => current_sl
                    };
                };
            }
            catch (Exception)
            {
                //TODO: [DEBUG] Log SL update failures with contextual data
                //TODO: [DEBUG] Decide fallback strategy when SL recalculation fails
                return current_sl => current_sl;
            }
        }

        public Func<double, double> UpdateTp(SlTpData marketData, ITpSlItems item)
        {
            //TODO: [DEBUG] Implement TP update policy (fixed vs trailing) instead of throwing
            //TODO: [DEBUG] Return current TP until dynamic management is implemented

            throw new NotImplementedException();
        }
    }
}

