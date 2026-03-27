using System;
using System.Collections.Generic;
using System.Linq;
using cAlgo.API;

/*
 * Volume Profile Indicator for cTrader
 * ─────────────────────────────────────────────────────────────────────────────
 * Displays a summary volume profile for a specific time range, drawn using
 * cTrader's native Chart drawing objects (rectangles, lines, text).
 *
 * Installation:
 *   1. cTrader → Automate → New Indicator
 *   2. Replace all code with this file and Build (F6)
 *   3. Add to any chart
 * ─────────────────────────────────────────────────────────────────────────────
 */

namespace cAlgo.Indicators
{
    [Indicator(IsOverlay = true, TimeZone = TimeZones.UTC, AccessRights = AccessRights.None)]
    public class VolumeProfile : Indicator
    {
        // ═══════════════════════════════════════════════════════════════
        //  GENERAL
        // ═══════════════════════════════════════════════════════════════

        [Parameter("Time Range Value", DefaultValue = 1, MinValue = 1, MaxValue = 52,
            Group = "General")]
        public int TimeRangeValue { get; set; }

        [Parameter("Time Range Unit (Day/Week/Month)", DefaultValue = "Day",
            Group = "General")]
        public string TimeRangeUnit { get; set; }

        [Parameter("Tick Interval (pips)", DefaultValue = 10, MinValue = 1, MaxValue = 10000,
            Group = "General")]
        public int TickIntervalPips { get; set; }

        [Parameter("RTH Only (07:00-16:00 UTC)", DefaultValue = false,
            Group = "General")]
        public bool RthDataOnly { get; set; }

        // ═══════════════════════════════════════════════════════════════
        //  DISPLAY
        // ═══════════════════════════════════════════════════════════════

        [Parameter("Profile Width (bars)", DefaultValue = 30, MinValue = 5, MaxValue = 200,
            Group = "Display")]
        public int ProfileWidthBars { get; set; }

        [Parameter("Bar Color", DefaultValue = "#804FC3F7",
            Group = "Display")]
        public string BarColor { get; set; }

        [Parameter("POC Bar Color Enabled", DefaultValue = true,
            Group = "Display")]
        public bool PocBarColorEnabled { get; set; }

        [Parameter("POC Bar Color", DefaultValue = "#FFFFA500",
            Group = "Display")]
        public string PocBarColor { get; set; }

        [Parameter("POC Line Display", DefaultValue = true,
            Group = "Display")]
        public bool PocLineDisplay { get; set; }

        [Parameter("POC Line Color", DefaultValue = "#FFFFA500",
            Group = "Display")]
        public string PocLineColor { get; set; }

        [Parameter("Show Bid/Ask Split", DefaultValue = false,
            Group = "Display")]
        public bool ShowBidAsk { get; set; }

        [Parameter("Bid Color", DefaultValue = "#80FF4444",
            Group = "Display")]
        public string BidColor { get; set; }

        [Parameter("Ask Color", DefaultValue = "#8044FF44",
            Group = "Display")]
        public string AskColor { get; set; }

        [Parameter("Show Volume Labels", DefaultValue = true,
            Group = "Display")]
        public bool ShowVolumeLabels { get; set; }

        [Parameter("Format Volume as K/M", DefaultValue = true,
            Group = "Display")]
        public bool FormatVolumeKM { get; set; }

        // ═══════════════════════════════════════════════════════════════
        //  VALUE AREA
        // ═══════════════════════════════════════════════════════════════

        [Parameter("Show Value Area", DefaultValue = false,
            Group = "Value Area")]
        public bool ShowValueArea { get; set; }

        [Parameter("Value Area Range %", DefaultValue = 70.0, MinValue = 1.0, MaxValue = 100.0,
            Group = "Value Area")]
        public double ValueAreaRangePct { get; set; }

        [Parameter("VA Bar Color Enabled", DefaultValue = true,
            Group = "Value Area")]
        public bool VaBarColorEnabled { get; set; }

        [Parameter("VA Bar Color", DefaultValue = "#806090A0",
            Group = "Value Area")]
        public string VaBarColor { get; set; }

        [Parameter("Range Lines Display", DefaultValue = true,
            Group = "Value Area")]
        public bool RangeLinesDisplay { get; set; }

        [Parameter("Range Lines Color", DefaultValue = "#C06090A0",
            Group = "Value Area")]
        public string RangeLinesColor { get; set; }

        // ═══════════════════════════════════════════════════════════════
        //  HVN / LVN
        // ═══════════════════════════════════════════════════════════════

        [Parameter("HVN/LVN Display (None/HVN/LVN/Both)", DefaultValue = "None",
            Group = "HVN/LVN")]
        public string HvnLvnDisplayType { get; set; }

        [Parameter("HVN/LVN Sensitivity", DefaultValue = 2, MinValue = 1, MaxValue = 10,
            Group = "HVN/LVN")]
        public int HvnLvnSensitivity { get; set; }

        [Parameter("HVN Color", DefaultValue = "#8000BFFF",
            Group = "HVN/LVN")]
        public string HvnColor { get; set; }

        [Parameter("LVN Color", DefaultValue = "#80FF6600",
            Group = "HVN/LVN")]
        public string LvnColor { get; set; }

        // ═══════════════════════════════════════════════════════════════
        //  PRIVATE STATE
        // ═══════════════════════════════════════════════════════════════

        private readonly SortedDictionary<double, (double Total, double Bid, double Ask)>
            _profile = new SortedDictionary<double, (double, double, double)>();

        private double _maxVolume;
        private double _totalVolume;
        private double _pocPrice;
        private double _vahPrice;
        private double _valPrice;
        private double _tickSize;
        private int    _lastBuiltIndex = -1;
        private readonly HashSet<double> _hvnLevels = new HashSet<double>();
        private readonly HashSet<double> _lvnLevels = new HashSet<double>();
        private const string Prefix = "VP_";

        // ═══════════════════════════════════════════════════════════════
        //  LIFECYCLE
        // ═══════════════════════════════════════════════════════════════

        protected override void Initialize()
        {
            _tickSize = TickIntervalPips * Symbol.PipSize;
        }

        public override void Calculate(int index)
        {
            if (index != Bars.Count - 1) return;
            if (index == _lastBuiltIndex) return;

            _lastBuiltIndex = index;
            BuildProfile(index);
            DrawProfile(index);
        }

        // ═══════════════════════════════════════════════════════════════
        //  PROFILE CALCULATION
        // ═══════════════════════════════════════════════════════════════

        private void BuildProfile(int index)
        {
            _profile.Clear();
            _hvnLevels.Clear();
            _lvnLevels.Clear();
            _tickSize = TickIntervalPips * Symbol.PipSize;

            DateTime periodStart = GetPeriodStart(Bars.OpenTimes[index]);

            for (int i = index; i >= 0; i--)
            {
                DateTime barTime = Bars.OpenTimes[i];
                if (barTime < periodStart) break;

                if (RthDataOnly && (barTime.Hour < 7 || barTime.Hour >= 16)) continue;

                double high = Bars.HighPrices[i];
                double low  = Bars.LowPrices[i];
                double vol  = Bars.TickVolumes[i];
                double mid  = (high + low) / 2.0;

                double range      = high - low;
                int    numBuckets = Math.Max(1, (int)Math.Round(range / _tickSize));
                double volPerBkt  = vol / numBuckets;

                for (int b = 0; b < numBuckets; b++)
                {
                    double price  = low + b * _tickSize;
                    double bucket = RoundToBucket(price);

                    double bidVol = bucket < mid ? volPerBkt * 0.6 : volPerBkt * 0.4;
                    double askVol = volPerBkt - bidVol;

                    if (!_profile.ContainsKey(bucket))
                        _profile[bucket] = (0, 0, 0);

                    var ex = _profile[bucket];
                    _profile[bucket] = (ex.Total + volPerBkt, ex.Bid + bidVol, ex.Ask + askVol);
                }
            }

            if (_profile.Count == 0) return;

            _maxVolume   = _profile.Values.Max(v => v.Total);
            _totalVolume = _profile.Values.Sum(v => v.Total);
            _pocPrice    = _profile.OrderByDescending(kv => kv.Value.Total).First().Key;

            if (ShowValueArea)               CalculateValueArea();
            if (HvnLvnDisplayType != "None") FindHvnLvn();
        }

        private void CalculateValueArea()
        {
            var keys   = _profile.Keys.ToList();
            int pocIdx = keys.IndexOf(_pocPrice);

            double target      = _totalVolume * (ValueAreaRangePct / 100.0);
            double accumulated = _profile[_pocPrice].Total;
            int    upPtr       = pocIdx + 1;
            int    downPtr     = pocIdx - 1;

            _vahPrice = _pocPrice + _tickSize;
            _valPrice = _pocPrice;

            while (accumulated < target)
            {
                double upVol   = upPtr   < keys.Count ? _profile[keys[upPtr]].Total   : 0;
                double downVol = downPtr >= 0          ? _profile[keys[downPtr]].Total : 0;

                if (upVol == 0 && downVol == 0) break;

                if (upVol >= downVol)
                {
                    accumulated += upVol;
                    _vahPrice    = keys[upPtr] + _tickSize;
                    upPtr++;
                }
                else
                {
                    accumulated += downVol;
                    _valPrice    = keys[downPtr];
                    downPtr--;
                }
            }
        }

        private void FindHvnLvn()
        {
            var keys = _profile.Keys.ToList();
            int s    = HvnLvnSensitivity;

            for (int i = s; i < keys.Count - s; i++)
            {
                double vol   = _profile[keys[i]].Total;
                bool   isHvn = true, isLvn = true;

                for (int j = i - s; j <= i + s; j++)
                {
                    if (j == i) continue;
                    double nv = _profile[keys[j]].Total;
                    if (vol <= nv) isHvn = false;
                    if (vol >= nv) isLvn = false;
                }

                if (isHvn) _hvnLevels.Add(keys[i]);
                if (isLvn) _lvnLevels.Add(keys[i]);
            }
        }

        // ═══════════════════════════════════════════════════════════════
        //  DRAWING
        // ═══════════════════════════════════════════════════════════════

        private void DrawProfile(int index)
        {
            // Remove all previous objects belonging to this indicator
            var toRemove = Chart.FindAllObjects<ChartObject>()
                .Where(o => o.Name.StartsWith(Prefix))
                .ToList();
            foreach (var obj in toRemove)
                Chart.RemoveObject(obj.Name);

            if (_profile.Count == 0) return;

            DateTime anchorTime    = Bars.OpenTimes[index];
            DateTime rightEdgeTime = GetFutureBarTime(index, ProfileWidthBars);

            int objCount = 0;

            foreach (var kvp in _profile)
            {
                double price       = kvp.Key;
                var    vol         = kvp.Value;
                double barFraction = vol.Total / _maxVolume;
                int    barBars     = Math.Max(1, (int)Math.Round(barFraction * ProfileWidthBars));
                DateTime barRight  = GetFutureBarTime(index, barBars);

                double priceTop = price + _tickSize;
                double priceBot = price;

                bool isPoc  = Math.Abs(price - _pocPrice) < _tickSize * 0.5;
                bool isInVa = ShowValueArea && VaBarColorEnabled
                              && price >= _valPrice && price < _vahPrice;
                bool isHvn  = _hvnLevels.Contains(price);
                bool isLvn  = _lvnLevels.Contains(price);

                if (ShowBidAsk)
                {
                    int askBars = Math.Max(1, (int)Math.Round((vol.Ask / _maxVolume) * ProfileWidthBars));
                    int bidBars = Math.Max(1, (int)Math.Round((vol.Bid / _maxVolume) * ProfileWidthBars));

                    DateTime askRight = GetFutureBarTime(index, askBars);
                    DateTime bidRight = GetFutureBarTime(index, askBars + bidBars);

                    var askRect = Chart.DrawRectangle(
                        Prefix + "ask_" + objCount,
                        anchorTime, priceTop, askRight, priceBot,
                        ParseColor(AskColor), 1, LineStyle.Solid);
                    askRect.IsFilled = true;

                    var bidRect = Chart.DrawRectangle(
                        Prefix + "bid_" + objCount,
                        askRight, priceTop, bidRight, priceBot,
                        ParseColor(BidColor), 1, LineStyle.Solid);
                    bidRect.IsFilled = true;
                }
                else
                {
                    string colHex = BarColor;
                    if      (HvnLvnDisplayType is "HVN" or "Both" && isHvn)  colHex = HvnColor;
                    else if (HvnLvnDisplayType is "LVN" or "Both" && isLvn)  colHex = LvnColor;
                    else if (isPoc && PocBarColorEnabled)                      colHex = PocBarColor;
                    else if (isInVa)                                           colHex = VaBarColor;

                    var rect = Chart.DrawRectangle(
                        Prefix + "bar_" + objCount,
                        anchorTime, priceTop, barRight, priceBot,
                        ParseColor(colHex), 1, LineStyle.Solid);
                    rect.IsFilled = true;
                }

                // Volume label
                if (ShowVolumeLabels && barFraction > 0.15)
                {
                    double midPrice  = price + _tickSize * 0.5;
                    DateTime txtTime = GetFutureBarTime(index, 1);
                    Chart.DrawText(
                        Prefix + "lbl_" + objCount,
                        FormatVolume(vol.Total),
                        txtTime, midPrice,
                        Color.White);
                }

                objCount++;
            }

            // ── POC line ──────────────────────────────────────────────
            if (PocLineDisplay && _pocPrice > 0)
            {
                double pocMid = _pocPrice + _tickSize * 0.5;
                Chart.DrawTrendLine(
                    Prefix + "poc",
                    anchorTime,    pocMid,
                    rightEdgeTime, pocMid,
                    ParseColor(PocLineColor), 1, LineStyle.Solid);

                Chart.DrawText(
                    Prefix + "poc_lbl", "POC",
                    rightEdgeTime, pocMid,
                    ParseColor(PocLineColor));
            }

            // ── VAH / VAL lines ───────────────────────────────────────
            if (ShowValueArea && RangeLinesDisplay && _vahPrice > _valPrice)
            {
                Color rc = ParseColor(RangeLinesColor);

                Chart.DrawTrendLine(
                    Prefix + "vah",
                    anchorTime, _vahPrice, rightEdgeTime, _vahPrice,
                    rc, 1, LineStyle.Dots);
                Chart.DrawText(
                    Prefix + "vah_lbl", "VAH",
                    rightEdgeTime, _vahPrice, rc);

                Chart.DrawTrendLine(
                    Prefix + "val",
                    anchorTime, _valPrice, rightEdgeTime, _valPrice,
                    rc, 1, LineStyle.Dots);
                Chart.DrawText(
                    Prefix + "val_lbl", "VAL",
                    rightEdgeTime, _valPrice, rc);
            }
        }

        // ═══════════════════════════════════════════════════════════════
        //  HELPERS
        // ═══════════════════════════════════════════════════════════════

        private DateTime GetFutureBarTime(int currentIndex, int barsAhead)
        {
            if (barsAhead == 0)
                return Bars.OpenTimes[currentIndex];

            TimeSpan barDuration = Bars.OpenTimes.Count > 1
                ? Bars.OpenTimes[currentIndex] - Bars.OpenTimes[currentIndex - 1]
                : TimeSpan.FromMinutes(1);

            return Bars.OpenTimes[currentIndex] + barDuration * barsAhead;
        }

        private double RoundToBucket(double price)
            => Math.Floor(price / _tickSize) * _tickSize;

        private Color ParseColor(string hex)
        {
            try   { return Color.FromHex(hex); }
            catch { return Color.Gray; }
        }

        private string FormatVolume(double vol)
        {
            if (!FormatVolumeKM)   return vol.ToString("F0");
            if (vol >= 1_000_000)  return (vol / 1_000_000.0).ToString("F1") + "M";
            if (vol >= 1_000)      return (vol / 1_000.0).ToString("F1") + "K";
            return vol.ToString("F0");
        }

        private DateTime GetPeriodStart(DateTime barTime)
        {
            switch (TimeRangeUnit.Trim().ToLower())
            {
                case "week":
                {
                    int daysSinceMonday = ((int)barTime.DayOfWeek + 6) % 7;
                    DateTime weekStart  = barTime.Date.AddDays(-daysSinceMonday);
                    return weekStart.AddDays(-7 * (TimeRangeValue - 1));
                }
                case "month":
                {
                    var monthStart = new DateTime(barTime.Year, barTime.Month, 1);
                    return monthStart.AddMonths(-(TimeRangeValue - 1));
                }
                default:
                    return barTime.Date.AddDays(-(TimeRangeValue - 1));
            }
        }
    }
}
