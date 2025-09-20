// Copyright QUANTOWER LLC. © 2017-2023. All rights reserved.

using System;
using System.Drawing;
using System.Linq;
using TradingPlatform.BusinessLayer;

namespace DeltaBasedIndicators
{
	/// <summary>
	/// Delta-based indicator exposing four discrete flags only:
	///  0: APAVD_Flag (±1)
	///  1: VD_Strength_Flag (±2)
	///  2: VD_Price_Divergent_Flag (±3)
	///  3: VD_to_Volume_Flag (±4)
	/// Robust to warm-up and zero-division; uses HistoricalData[1] for non-repainting context.
	/// </summary>
	public class DeltaBasedIndicators : Indicator , IVolumeAnalysisIndicator
	{
		// APAVD (Average Price move to Average VD) settings
		[InputParameter("APAVD Settings", 0)]
		public readonly string _tag__uno = "#############";

		[InputParameter("Delta: Use Median", 1)]
		public bool _Use_Median = false;

		[InputParameter("Delta: Threshold Multiplier", 2)]
		public double _Trh= 2;

		[InputParameter("Delta: Lookback", 3)]
		public int _LoockBackWindow= 30;

		// VD Strength settings
		[InputParameter("VD Strength Settings", 10)]
		public readonly string _tag__due = "#############";

		[InputParameter("Delta Strength: Lookback", 11)]
		public int _LoockBackWindow_Strength = 30;

		[InputParameter("Delta Strength: Threshold Multiplier", 12)]
		public double _Trh_Strenght = 2;

		// VD/Volume settings
		[InputParameter("VD/Volume Settings", 20)]
		public readonly string _tag__tre = "#############";

		[InputParameter("VDtV Lookback", 21)]
		public int _LoockBackWindow_VDtV = 30;

		[InputParameter("VDtV Threshold", 22)]
		public double _Trh_VDtV = 2;

        [InputParameter("Force Volume Ready", 23)]
        public bool _forceVolume = false;

        private RingBuffer<double> _DeltaBuffer;
		private RingBuffer<double> _DeltaBuffer_Strenght;
		private RingBuffer<double> _PriceBuffer;
		private RingBuffer<double> _VolumeBuffer;
		private RingBuffer<double> _DeltaBuffer_VDtV;
		private bool volumeReady = false;
		public DeltaBasedIndicators()
			: base()
		{
			// Defines indicator's name and description.
			Name = "DeltaBasedIndicators";
			Description = "My indicator's annotation";

			// Output lines: all flags only
			AddLineSeries("APAVD_Flag", Color.CadetBlue, 1, LineStyle.Solid);
			AddLineSeries("VD_Strength_Flag", Color.Red, 1, LineStyle.Solid);
			AddLineSeries("VD_Price_Divergent_Flag", Color.Gold, 1, LineStyle.Solid);
			AddLineSeries("VD_to_Volume_Flag", Color.Purple, 1, LineStyle.Solid);

			SeparateWindow = true;
		}

		public bool IsRequirePriceLevelsCalculation => false;

		public void VolumeAnalysisData_Loaded()
		{
			this.volumeReady = true;
		}

		/// <summary>
		/// This function will be called after creating an indicator as well as after its input params reset or chart (symbol or timeframe) updates.
		/// </summary>
		protected override void OnInit()
		{
			this.UpdateType = IndicatorUpdateType.OnBarClose;
			_DeltaBuffer = new RingBuffer<double>(_LoockBackWindow);
			_DeltaBuffer_Strenght = new RingBuffer<double>(_LoockBackWindow_Strength);
			_PriceBuffer = new RingBuffer<double>(_LoockBackWindow);
			_VolumeBuffer = new RingBuffer<double>(_LoockBackWindow_VDtV);
			_DeltaBuffer_VDtV = new RingBuffer<double>(_LoockBackWindow_VDtV);
		}

		/// <summary>
		/// Calculation entry point. This function is called when a price data updates. 
		/// Will be runing under the HistoricalBar mode during history loading. 
		/// Under NewTick during realtime. 
		/// Under NewBar if start of the new bar is required.
		/// </summary>
		/// <param name="args">Provides data of updating reason and incoming price.</param>
		protected override void OnUpdate(UpdateArgs args)
		{
			// Wait until volume analysis is fully available
			if (!this.volumeReady && ! _forceVolume)
				return;

			// Maintain rolling buffers (price)
			if (!this._PriceBuffer.IsFull)
				this.FillPriceBuffer();

			else
			{
				if (_Use_Median)
					this._PriceBuffer.Add(this.HistoricalData[1][PriceType.Median]);
				else
					this._PriceBuffer.Add(Math.Abs(this.HistoricalData[1][PriceType.Close] - this.HistoricalData[1][PriceType.Open]));
			}

			// VD (delta) for current bar
			double delta = this.HistoricalData[1].VolumeAnalysisData.Total.Delta;

			// Maintain rolling buffers (delta for APAVD)
			if (!this._DeltaBuffer.IsFull)
				this.FillDeltaBuffer(this._LoockBackWindow, this._DeltaBuffer);

			else
				this._DeltaBuffer.Add(Math.Abs(delta));

			// Maintain rolling buffers (delta for Strength)
			if (!this._DeltaBuffer_Strenght.IsFull)
				this.FillDeltaBuffer(_LoockBackWindow_Strength, this._DeltaBuffer_Strenght);
			else
				this._DeltaBuffer_Strenght.Add(Math.Abs(delta));

			// VDtV buffers
			if (!this._VolumeBuffer.IsFull)
				this.FillVolumeBuffer();
			else
				this._VolumeBuffer.Add(this.HistoricalData[1][PriceType.Volume]);

			if (!this._DeltaBuffer_VDtV.IsFull)
				this.FillDeltaBuffer(_LoockBackWindow_VDtV, this._DeltaBuffer_VDtV);
			else
				this._DeltaBuffer_VDtV.Add(Math.Abs(delta));

			// Ensure buffers are ready before using averages
			if (!this._PriceBuffer.IsFull || !this._DeltaBuffer.IsFull || !this._DeltaBuffer_Strenght.IsFull)
				return;

			var priceMedian = this._PriceBuffer.ToArray().Average();
			var deltaMedian = this._DeltaBuffer.ToArray().Average();
			var deltaMedian_strenght = this._DeltaBuffer_Strenght.ToArray().Average();


			double APAVD;
			if (deltaMedian > 0)
				APAVD = priceMedian / deltaMedian;
			else
				APAVD = double.PositiveInfinity; // force isOkey to false

			var lastDeltaAbs = Math.Abs(this._DeltaBuffer.ToArray().Last());
			double CPVD = lastDeltaAbs > 0 ? this._PriceBuffer.ToArray().Last() / lastDeltaAbs : 0.0;

			bool isOkey = !double.IsInfinity(APAVD) && !double.IsNaN(APAVD) && CPVD > APAVD * this._Trh;
			bool isOkeyStrenght = Math.Abs(delta) > deltaMedian_strenght*this._Trh_Strenght;

			double value = !isOkey ? 0 : (delta > 0 ? 1 : -1);
			double value_strenght = !isOkeyStrenght ? 0 : (delta > 0 ? 1 : -1);

			// Divergenza: segno del VD vs direzione del prezzo
			int priceSign = Math.Sign(this.HistoricalData[1][PriceType.Close] - this.HistoricalData[1][PriceType.Open]);
			int vdSign = Math.Sign(delta);
			int delta_resoult = (priceSign != 0 && vdSign != 0 && vdSign != priceSign) ? (vdSign > 0 ? 1 : -1) : 0;

			

			// Scale flags for readability: line0=±1, line1=±2, line2=±3, line3=±4
			SetValue(value); // ±1 or 0
			int strengthFlag = value_strenght == 0 ? 0 : (value_strenght > 0 ? 2 : -2);
			SetValue(strengthFlag, 1);
			int divFlag = delta_resoult == 0 ? 0 : (delta_resoult > 0 ? 3 : -3);
			SetValue(divFlag, 2);

			// VDtV flag (|VD|/Vol vs medie)
			if (this._VolumeBuffer.IsFull && this._DeltaBuffer_VDtV.IsFull)
			{
				var avgVD_v = this._DeltaBuffer_VDtV.ToArray().Average();
				var avgVol = this._VolumeBuffer.ToArray().Average();
				var vdNow = Math.Abs(delta);
				var volNow = this.HistoricalData[1][PriceType.Volume];

				double baseRatio = (avgVol > 0) ? (avgVD_v / avgVol) : double.NaN;
				double curRatio = (volNow > 0) ? (vdNow / volNow) : 0.0;

				bool strongVDtV = !double.IsNaN(baseRatio) && !double.IsInfinity(baseRatio) && baseRatio > 0 && curRatio > baseRatio * this._Trh_VDtV;
				int vdtvFlag = strongVDtV ? (delta > 0 ? 1 : -1) : 0;
				int vdtvFlagScaled = vdtvFlag == 0 ? 0 : (vdtvFlag > 0 ? 4 : -4);
				SetValue(vdtvFlagScaled, 3);
			}

		}


		private void FillDeltaBuffer(int period, RingBuffer<double> ringBuffer)
		{
			if (this.Count < period)
				return; // Not enough bars to seed the buffer

			for (int i = period; i > 0; i--)
				ringBuffer.Add(Math.Abs(this.HistoricalData[i].VolumeAnalysisData.Total.Delta));

		}

		private void FillVolumeBuffer()
		{
			if (this.Count < this._LoockBackWindow_VDtV)
				return;

			for (int i = this._LoockBackWindow_VDtV; i > 0; i--)
				this._VolumeBuffer.Add(this.HistoricalData[i][PriceType.Volume]);
		}

		private void FillPriceBuffer()
		{
			if (this.Count < this._LoockBackWindow)
				return; // Not enough bars to seed the buffer

			for (int i = this._LoockBackWindow;  i > 0; i--)
			{
				if (this._Use_Median)
					this._PriceBuffer.Add(this.HistoricalData[i][PriceType.Median]);
				else
					this._PriceBuffer.Add(Math.Abs(this.HistoricalData[i][PriceType.Close] - this.HistoricalData[i][PriceType.Open]));

			}
		}
	}
}

