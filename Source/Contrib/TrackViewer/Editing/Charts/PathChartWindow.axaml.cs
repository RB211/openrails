// COPYRIGHT 2026 by the Open Rails project.
//
// This file is part of Open Rails.
//
// Open Rails is free software: you can redistribute it and/or modify
// it under the terms of the GNU General Public License as published by
// the Free Software Foundation, either version 3 of the License, or
// (at your option) any later version.
//
// Open Rails is distributed in the hope that it will be useful,
// but WITHOUT ANY WARRANTY; without even the implied warranty of
// MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  See the
// GNU General Public License for more details.
//
// You should have received a copy of the GNU General Public License
// along with Open Rails.  If not, see <http://www.gnu.org/licenses/>.

using System;
using System.Collections.Generic;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.VisualTree;

namespace ORTS.TrackViewer.Editing.Charts
{
    /// <summary>
    /// Interaction logic for PathChartWindow.axaml. Lives on the Avalonia UI thread.
    /// </summary>
    partial class PathChartWindow : Window
    {
        /// <summary>Raised when the user closes (hides) the window</summary>
        public event Action Hidden;

        private Dictionary<string, Canvas> chartCanvasses;
        private Dictionary<string, Canvas> legendCanvasses;
        private Dictionary<string, ISubChart> subCharts;
        private PathChartData pathData;

        #region Construction and data setting
        /// <summary>
        /// The Window where the details of a path can be charted (height, grade, ...)
        /// </summary>
        public PathChartWindow()
        {
            InitializeComponent();

            chartCanvasses = new Dictionary<string, Canvas> {
                {"height", HeightCanvas},
                {"grade", GradeCanvas},
                {"curvature", CurvatureCanvas},
                {"distance", DistanceCanvas},
                {"milemarkers", MileMarkersCanvas},
                {"speedmarkers", SpeedMarkersCanvas}
            };
            legendCanvasses = new Dictionary<string, Canvas> {
                {"height", HeightLegend},
                {"grade", GradeLegend},
                {"curvature", CurvatureLegend},
                {"distance", DistanceLegend},
                {"milemarkers", MileMarkersLegend},
                {"speedmarkers", SpeedMarkersLegend}
            };
            subCharts = new Dictionary<string, ISubChart>();
            ChartScrollbar.Value = ChartScrollbar.Maximum / 2;

            AddHandler(PointerPressedEvent, Window_MouseDown, RoutingStrategies.Tunnel);
            AddHandler(PointerReleasedEvent, Window_MouseUp, RoutingStrategies.Tunnel);
            AddHandler(PointerMovedEvent, Window_MouseMove, RoutingStrategies.Tunnel);
            AddHandler(PointerWheelChangedEvent, OnMouseWheel, RoutingStrategies.Tunnel);
        }

        /// <summary>
        /// Set the data to chart (creates the subcharts when the data object changes)
        /// </summary>
        internal void SetData(PathChartData data)
        {
            if (data == pathData) return;
            pathData = data;
            subCharts.Clear();
            if (data == null) return;
            subCharts["height"] = new HeightChart(data);
            subCharts["grade"] = new GradeChart(data);
            subCharts["curvature"] = new CurvatureChart(data);
            subCharts["distance"] = new DistanceChart(data);
            subCharts["milemarkers"] = new MileMarkersChart(data);
            subCharts["speedmarkers"] = new SpeedlimitsChart(data);
        }
        #endregion

        #region Drawing
        /// <summary>
        /// Draw all the subcharts
        /// </summary>
        public void Draw()
        {
            if (!IsVisible || pathData == null) return;
            double zoomRatioStart = ChartScrollbar.Value;
            double zoomRatioStop = ChartScrollbar.Value + ChartScrollbar.ViewportSize;
            lock (pathData)
            {
                foreach (string chartName in chartCanvasses.Keys)
                {
                    subCharts.TryGetValue(chartName, out ISubChart subChart);
                    if (subChart != null)
                    {
                        subChart.Draw(zoomRatioStart, zoomRatioStop, chartCanvasses[chartName], legendCanvasses[chartName]);
                    }
                }
            }
        }
        #endregion

        #region Window events
        private void Window_SizeChanged(object sender, SizeChangedEventArgs e)
        {
            Draw();
        }

        /// <summary>
        /// We do not want to really close the window, but only hide it
        /// </summary>
        protected override void OnClosing(WindowClosingEventArgs e)
        {
            base.OnClosing(e);
            if (e.CloseReason == WindowCloseReason.ApplicationShutdown) return;
            e.Cancel = true;  // cancels the window close
            this.Hide();      // Programmatically hides the window
            Hidden?.Invoke();
        }

        private void OnMouseWheel(object sender, PointerWheelEventArgs e)
        {
            if (this.IsActive)
            {   // We want to make sure that mouse scrolling is not captured here if already captured in trackViewer.
                double xPositionOfMouse = e.GetPosition(this.HeightCanvas).X;
                double xPositionOfMouseAsRatio = xPositionOfMouse / Math.Max(1, this.HeightCanvas.Bounds.Width);

                // WPF used 120 per notch and exp(0.1 * delta / 40); Avalonia uses 1 per notch.
                ZoomChange(Math.Exp(0.3 * e.Delta.Y), xPositionOfMouseAsRatio);
                e.Handled = true;
            }
        }

        private bool mouseIsMoving = false;
        private double realXatMouse=0;
        private void Window_MouseDown(object sender, PointerPressedEventArgs e)
        {
            if (!e.GetCurrentPoint(this).Properties.IsLeftButtonPressed)
                return;
            if (e.Source is Visual source && (source is ScrollBar || source.FindAncestorOfType<ScrollBar>() != null || source.FindAncestorOfType<Button>() != null || source is Button || source.FindAncestorOfType<GridSplitter>() != null || source is GridSplitter))
                return;
            e.Pointer.Capture(this);
            this.mouseIsMoving = true;
            double mouseXstart = e.GetPosition(this).X;
            double mouseXAsRatio = mouseXstart / Math.Max(1, this.HeightCanvas.Bounds.Width);
            this.realXatMouse = ChartScrollbar.Value + mouseXAsRatio * ChartScrollbar.ViewportSize;
        }

        private void Window_MouseMove(object sender, PointerEventArgs e)
        {
            if (this.mouseIsMoving)
            {
                double mouseXstart = e.GetPosition(this).X;
                double mouseXAsRatio = mouseXstart / Math.Max(1, this.HeightCanvas.Bounds.Width);
                SetScrollValue(this.realXatMouse - mouseXAsRatio * ChartScrollbar.ViewportSize);
            }
        }

        private void Window_MouseUp(object sender, PointerReleasedEventArgs e)
        {
            if (this.mouseIsMoving)
            {
                e.Pointer.Capture(null);
                this.mouseIsMoving = false;
            }
        }
        #endregion

        #region Zooming
        private void SetScrollValue(double value)
        {
            ChartScrollbar.Value = Math.Max(ChartScrollbar.Minimum, Math.Min(ChartScrollbar.Maximum, value));
        }

        /// <summary>
        /// Zoom the charting window
        /// </summary>
        /// <param name="zoomFactor">Factor to zoom with</param>
        public void ZoomChange(double zoomFactor)
        {
            ZoomChange(zoomFactor, ChartScrollbar.Maximum > 0 ? ChartScrollbar.Value / ChartScrollbar.Maximum : 0.5);
        }

        private void ZoomChange(double zoomFactor, double zoomCenterRatio)
        {
            double oldValue = ChartScrollbar.Value;

            double zoomCenter = ChartScrollbar.Value + zoomCenterRatio * ChartScrollbar.ViewportSize;
            ChartScrollbar.ViewportSize = ChartScrollbar.ViewportSize / zoomFactor;
            if (ChartScrollbar.ViewportSize > 0.9999)
            {
                ChartScrollbar.ViewportSize = 0.9999;
            }
            ChartScrollbar.Maximum = 1 - ChartScrollbar.ViewportSize;
            SetScrollValue(zoomCenter - zoomCenterRatio * ChartScrollbar.ViewportSize);
            if (ChartScrollbar.Value == oldValue)
            {
                // Normally, a value change will trigger a draw. But in case value is 0, it stays at 0. So force a draw
                Draw();
            }
        }

        /// <summary>
        /// Shift the chartwindow (when zoomed)
        /// </summary>
        /// <param name="shiftSteps">The number of zoom steps (negative for shifting left)</param>
        public void Shift(int shiftSteps)
        {
            double shiftSize = Math.Min(ChartScrollbar.Maximum / 10, ChartScrollbar.ViewportSize * 0.5);
            SetScrollValue(ChartScrollbar.Value + shiftSteps * shiftSize);
        }

        private void ChartScrollbar_ValueChanged(object sender, RangeBaseValueChangedEventArgs e)
        {
            Draw();
        }
        #endregion

        #region Save JSON
        /// <summary>
        /// Action that can be set to act as a callback when the save .json button is clicked
        /// </summary>
        public Action OnJsonSaveClick { get; set; }

        private void SaveJson_Click(object sender, RoutedEventArgs e)
        {
            OnJsonSaveClick?.Invoke();
        }
        #endregion
    }
}
