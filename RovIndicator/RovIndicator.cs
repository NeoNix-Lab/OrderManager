// Copyright QUANTOWER LLC. © 2017-2023. All rights reserved.

using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Net.WebSockets;
using TradingPlatform.BusinessLayer;

namespace RovIndicator
{

    public class RvoIndicator : Indicator
    {
        #region Input / Attributi e campi

        [InputParameter("Short Length", 0, 2, 200, 1)]
        public int _LenShort = 14;

        [InputParameter("Long Length", 1, 5, 500, 1)]
        public int _LenLong = 60;

        [InputParameter("Windosw Lenght", 2)]
        public int _WLen = 6;


        [InputParameter("HMA Length", 5, 2, 200, 1)]
        public int _HmaLen = 14;

        [InputParameter("Use ATR Normalization", 8)]
        public bool _UseAtrNorm = true;

        [InputParameter("ATR Length", 9, 2, 200, 1)]
        public int _AtrLen = 14;

        [InputParameter("Threshold", 10, 0.0, 1.0, 0.01)]
        public double _ThrSlope = 0.015;

        [InputParameter("UsePrice", 10)]
        public bool _UsePrice = true;

        #endregion
        // Buffers (si riempiono in OnUpdate)
        private Indicator atrInd;
       
        private double prevRvolNormalized = 0;
        private double currentRvoNormalized = 0;
        private double prevsmoothedRvol = 0;
        private double currentsmoothedRvol = 0;
        private bool rvolOkey = false;


        private RingBuffer<double> rvolShortBuf;
        private RingBuffer<double> rvolLongBuf;
        private RingBuffer<double> hullBuffer;

        // Line indices (ordine di AddLineSeries)
        private const int LINE_RVOL_S = 0;
        private const int LINE_RVOL_L = 1;
        private const int LINE_RVOL_HMA = 2;
        private const int LINE_COMPOSITE = 3;
        private const int LINE_RVOL_NORM = 4;
        private const int LINE_DELTA = 5;
        private const int LINE_BASE = 6;

        public RvoIndicator()
        {
            Name = "RVOL (evolved)";
            Description = "RVOL short/long + HMA (Rvol or PriceSlope/ATR), composite & thresholds";
            SeparateWindow = true;

            // Linee consigliate
            AddLineSeries("RvolShort", Color.Orange, 1, LineStyle.Solid);
            AddLineSeries("RvolLong", Color.SteelBlue, 1, LineStyle.Solid);
            AddLineSeries("Rvol_HMA", Color.ForestGreen, 1, LineStyle.Solid);
            AddLineSeries("Composite", Color.DarkViolet, 2, LineStyle.Solid);
            AddLineSeries("RvolNorm", Color.Red, 2, LineStyle.Solid);
            AddLineSeries("Delta", Color.Gray, 1, LineStyle.Dash);
            AddLineSeries("BaseMin", Color.Gold, 1, LineStyle.DashDot);
        }

        protected override void OnInit()
        {
            this.atrInd = Core.Indicators.BuiltIn.ATR(this._AtrLen, MaMode.SMMA);
            this.HistoricalData.AddIndicator(this.atrInd);

            this.rvolShortBuf = new RingBuffer<double>(this._LenShort);
            this.rvolLongBuf = new RingBuffer<double>(this._LenLong);
            this.hullBuffer = new RingBuffer<double>(this._HmaLen);
        }

        protected override void OnUpdate(UpdateArgs args)
        {
            this.prevsmoothedRvol = this.currentsmoothedRvol;
            this.prevRvolNormalized = this.currentRvoNormalized;

            var vol = this.HistoricalData[1][PriceType.Volume];


            this.rvolShortBuf.Add(vol);
            this.rvolLongBuf.Add(vol);
            if (this._UsePrice)
                this.hullBuffer.Add(this.HistoricalData[1][PriceType.Close]);
            else
                this.hullBuffer.Add(vol);

            if (rvolLongBuf.IsFull && rvolShortBuf.IsFull && hullBuffer.IsFull)
            {
                var currentRvolL = vol/rvolLongBuf.ToArray().Average();
                var currentRvolS = vol/rvolShortBuf.ToArray().Average();

                var hullArray = hullBuffer.ToArray();

                //#TODO : Implement HMA function
                var hullAvarage = hullArray.Average();

                this.currentsmoothedRvol = (currentRvolS + currentRvolL + hullAvarage) / 3;
            }

            this.currentRvoNormalized = this.currentsmoothedRvol/this.atrInd.GetValue(1);

            if (this.prevRvolNormalized != 0 && this.prevsmoothedRvol != 0)
                this.rvolOkey = Math.Abs(prevRvolNormalized - currentRvoNormalized) > this._ThrSlope;


            var value = 0.0;

            if (rvolOkey)
                value = this.currentsmoothedRvol > this.prevRvolNormalized ? 1.0 : -1.0;
            SetValue(value);
            SetValue(this.rvolOkey ? 2 : -2, 1);

            //SetValue(this.prevRvolNormalized);
            //SetValue(currentRvoNormalized, 1);
        }

        
    }

    public static class TA
    {
        // Ritorna SOLO l'ultimo valore della HMA(n)
        public static double HMA_Last(double[] values, int n)
        {
            if (values == null) throw new ArgumentNullException(nameof(values));
            if (values.Length == 0 || n < 2) return double.NaN;

            int half = n / 2;
            int sroot = (int)Math.Round(Math.Sqrt(n));
            if (sroot < 1) sroot = 1;

            var wHalf = new RollingWma(half);
            var wFull = new RollingWma(n);
            var wOut = new RollingWma(sroot);

            double last = double.NaN;

            for (int i = 0; i < values.Length; i++)
            {
                double a = wHalf.Push(values[i]); // WMA(n/2)
                double b = wFull.Push(values[i]); // WMA(n)

                if (!double.IsNaN(a) && !double.IsNaN(b))
                    last = wOut.Push(2.0 * a - b);
                else
                    _ = wOut.Push(double.NaN); // avanza warm-up
            }

            return last; // NaN finché non ci sono abbastanza campioni
        }

        // --- helper interno: WMA rolling O(1) ---
        private sealed class RollingWma
        {
            private readonly int p;
            private readonly double[] buf;
            private int count, idx;
            private double sum, wsum, denom;

            public RollingWma(int period)
            {
                p = Math.Max(1, period);
                buf = new double[p];
                denom = p * (p + 1) / 2.0;
            }

            public double Push(double v)
            {
                if (p == 1) return v;

                if (count < p)
                {
                    buf[count] = v;
                    sum += v;
                    wsum += v * (count + 1);
                    count++;
                    return (count == p) ? (wsum / denom) : double.NaN;
                }

                double old = buf[idx];
                buf[idx] = v;
                idx = (idx + 1) % p;

                wsum = wsum + p * v - sum;
                sum = sum + v - old;

                return wsum / denom;
            }
        }
    }
}
