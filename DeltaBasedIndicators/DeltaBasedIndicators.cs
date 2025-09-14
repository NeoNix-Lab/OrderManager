// Copyright QUANTOWER LLC. © 2017-2023. All rights reserved.

using System;
using System.Drawing;
using System.Linq;
using TradingPlatform.BusinessLayer;

namespace DeltaBasedIndicators
{

	//📝 TODO: [ADD Logging System]
	//📝 TODO: [Clean Close]
	//📝 TODO: [Use Smoler HD]
	//📝 TODO: [Allow historical]
	//📝 TODO: [fix graphics]
	//📝 TODO: [Update Settings]


	public class DeltaBasedIndicators : Indicator , IVolumeAnalysisIndicator
	{
		[InputParameter("Avarage Price Delta Settings", 0)]
		public readonly string _tag__uno = "#############";

		[InputParameter("Use Median", 1)]
		public bool _Use_Median = false;

		[InputParameter("Threshold Multiplaier", 2)]
		public double _Trh= 2;

		[InputParameter("LoockBackWindow", 3)]
		public int _LoockBackWindow= 30;

		[InputParameter("Delta Strenght Settings", 0)]
		public readonly string _tag__due = "#############";

		[InputParameter("LoockBackWindow", 3)]
		public int _LoockBackWindow_Strength = 30;

		[InputParameter("Threshold Multiplaier", 2)]
		public double _Trh_Strenght = 2;

		[InputParameter("Threshold Multiplaier", 2)]
		public Session session;

        private RingBuffer<double> _DeltaBuffer;
		private RingBuffer<double> _DeltaBuffer_Strenght;
		private RingBuffer<double> _PriceBuffer;
		private bool volumeReady = false;

		private CustomSession _session = new CustomSession();
        public DeltaBasedIndicators()
			: base()
		{
			// Defines indicator's name and description.
			Name = "DeltaBasedIndicators";
			Description = "My indicator's annotation";

			// Defines line on demand with particular parameters.
			AddLineSeries("Avareg Price Delta", Color.CadetBlue, 1, LineStyle.Solid);
			AddLineSeries("Delta Strenght", Color.Red, 1, LineStyle.Solid);
			AddLineSeries("Delta Divergent", Color.Gold, 1, LineStyle.Solid);
            // By default indicator will be applied on main window of the chart
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
			_DeltaBuffer = new RingBuffer<double>(_LoockBackWindow);
			_DeltaBuffer_Strenght = new RingBuffer<double>(_LoockBackWindow_Strength);
			_PriceBuffer = new RingBuffer<double>(_LoockBackWindow);
			//this.HistoricalData.VolumeAnalysisCalculationProgress.ProgressChanged += VolumeAnalysisCalculationProgress_ProgressChanged;
		}

		//private void VolumeAnalysisCalculationProgress_ProgressChanged(object sender, VolumeAnalysisTaskEventArgs e)
		//{
		//	this.volumeReady = e.ProgressPercent == 100;
		//}

		/// <summary>
		/// Calculation entry point. This function is called when a price data updates. 
		/// Will be runing under the HistoricalBar mode during history loading. 
		/// Under NewTick during realtime. 
		/// Under NewBar if start of the new bar is required.
		/// </summary>
		/// <param name="args">Provides data of updating reason and incoming price.</param>
		protected override void OnUpdate(UpdateArgs args)
		{
			if (!this.volumeReady)
				return;

			if (!this._PriceBuffer.IsFull)
				this.FillPriceBuffer();

			else
			{
				if (_Use_Median) /*TODO : forse vuole l escursione non l intero*/
					this._PriceBuffer.Add(this.HistoricalData[1][PriceType.Median]);
				else
					this._PriceBuffer.Add(Math.Abs(this.HistoricalData[1][PriceType.Close] - this.HistoricalData[1][PriceType.Open]));
			}

			double delta = this.HistoricalData[1].VolumeAnalysisData.Total.Delta;

			if (!this._DeltaBuffer.IsFull)
				this.FillDeltaBuffer(this._LoockBackWindow, this._DeltaBuffer);

			else
				this._DeltaBuffer.Add(Math.Abs(delta));

			if (!this._DeltaBuffer_Strenght.IsFull)
				this.FillDeltaBuffer(_LoockBackWindow_Strength, this._DeltaBuffer_Strenght);
			else
				this._DeltaBuffer_Strenght.Add(Math.Abs(delta));

			var priceMedian = this._PriceBuffer.ToArray().Average();
			var deltaMedian = this._DeltaBuffer.ToArray().Average();
			var deltaMedian_strenght = this._DeltaBuffer_Strenght.ToArray().Average();


			var APAVD = priceMedian / deltaMedian;
			var CPVD = this._PriceBuffer.ToArray().Last() / this._DeltaBuffer.ToArray().Last();

			bool isOkey = CPVD > APAVD * this._Trh;
			bool isOkeyStrenght = Math.Abs(delta) > deltaMedian_strenght*this._Trh_Strenght;

			double value = !isOkey ? 0 : delta > 0 ? 1 : -1;
			double value_strenght = !isOkeyStrenght ? 0 : delta > 0 ? 2 : -2;

			bool isDivergent = Math.Sign(value) != Math.Sign(this.HistoricalData[1][PriceType.Close] - this.HistoricalData[1][PriceType.Open]);
			var delta_resoult = delta == 0 ? 0 : isDivergent ? 3 : -3;

			

            SetValue(value);
			SetValue(value_strenght,1);
			SetValue(delta_resoult, 2);

			Calculate_Strength();
        }

		private void Calculate_Strength()
		{
			var buy_streng = 0;

			for (int i = 0; i <3; i++)
                if (this.LinesSeries[i].GetValue() > 0)
                    buy_streng++;

			if (buy_streng >= 2)
                for (int i = 0; i < 3; i++)
                    if (this.LinesSeries[i].GetValue() > 0)
						this.LinesSeries[i].SetMarker(0,new IndicatorLineMarker(Color.Green, IndicatorLineMarkerIconType.UpArrow));

            var sell_streng = 0;

            for (int i = 0; i < 3; i++)
                if (this.LinesSeries[i].GetValue() < 0)
                    sell_streng++;

			if (sell_streng >= 2)
				for (int i = 0; i < 3; i++)
					if (this.LinesSeries[i].GetValue() < 0)
                        this.LinesSeries[i].SetMarker(0, new IndicatorLineMarker(Color.Red, IndicatorLineMarkerIconType.DownArrow));

        }

        private void FillDeltaBuffer(int period, RingBuffer<double> ringBuffer)
		{
			if (this.Count < period)
				return; /*TODO : log TODO : Verifica che delta sia == buy - sell*/

			for (int i = period; i > 0; i--)
				ringBuffer.Add(Math.Abs(this.HistoricalData[i].VolumeAnalysisData.Total.Delta));

		}

        private void FillPriceBuffer()
		{
			if (this.Count < this._LoockBackWindow)
				return; /*TODO : log*/

			for (int i = this._LoockBackWindow;  i > 0; i--)
			{
				if (this._Use_Median) /*TODO : forse vuole l escursione non l intero*/
					this._PriceBuffer.Add(this.HistoricalData[i][PriceType.Median]);
				else
					this._PriceBuffer.Add(Math.Abs(this.HistoricalData[i][PriceType.Close] - this.HistoricalData[i][PriceType.Open]));

			}
		}
	}
}
