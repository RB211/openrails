// COPYRIGHT 2018 by the Open Rails project.
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
using System.IO;
using System.Linq;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Shapes;
using Avalonia.Input;
using Avalonia.Layout;
using Avalonia.Media;
using ORTS.Common;
using ORTS.Common.Input;
using ORTS.Settings;
using Path = System.IO.Path;

namespace Menu
{
    public partial class OptionsWindow
    {
        private RailDriverDevice instance;
        private Window railDriverLegend;
        private Canvas railDriverLegendGuidance;
        private RailDriverCalibrationSetting currentCalibrationStep = RailDriverCalibrationSetting.CutOffDelta;

        private byte[] calibrationSettings = new byte[Enum.GetNames(typeof(UserCommand)).Length];
        private bool isCalibrationSet;

        private readonly List<RDButtonInputControl> RDButtonInputControls = new List<RDButtonInputControl>();

        private static int[,] startingPoints = {
            { 170, 110 }, { 170, 150 }, { 170, 60 }, //Reverser
            { 230, 120 },  { 230, 150 }, { 230, 60 }, { 230, 90 }, //Throttle
            { 340, 150 }, { 340, 90 }, { 340, 60 }, // Auto Brake
            { 440, 150 }, { 440, 60 }, { 440, 150 }, { 470, 150 }, { 440, 60 }, { 470, 60 }, // Independent Brake
            { 520, 80 }, { 540, 80 }, { 560, 80 }, // Rotary Switch 1
            { 520, 150 }, { 535, 150 }, { 550, 150 }, // Rotary Switch 2
        };

        /// <summary>
        /// The RailDriver device, if there is one. The device is driven through the Windows HID API (PIEHid), so it is only available on Windows.
        /// </summary>
        static RailDriverDevice GetRailDriverDevice(out string unavailableReason)
        {
            unavailableReason = null;
            if (!OperatingSystem.IsWindows())
            {
                unavailableReason = Localization.GetString("RailDriver hardware is only supported on Windows. The button assignments below are shown for reference only.");
                return null;
            }
            try
            {
                var device = RailDriverDevice.Instance;
                if (device.Enabled)
                    return device;
                unavailableReason = Localization.GetString("No RailDriver device was found. Connect the RailDriver and open the options again to change its settings.");
            }
            catch (Exception error)
            {
                unavailableReason = Localization.GetString("The RailDriver device could not be accessed:") + " " + error.Message;
            }
            return null;
        }

        private Window GetRailDriverLegend()
        {
            if (null == railDriverLegend)
            {
                var image = App.LoadBitmap("RailDriverLegend.png");
                railDriverLegendGuidance = new Canvas { Width = image.Size.Width, Height = image.Size.Height, IsHitTestVisible = false };
                var content = new Panel
                {
                    Children =
                    {
                        new Image { Source = image, Width = image.Size.Width, Height = image.Size.Height },
                        railDriverLegendGuidance,
                    },
                };

                railDriverLegend = new Window()
                {
                    Title = Localization.GetString("RailDriver"),
                    Icon = App.LoadIcon(),
                    ShowInTaskbar = false,
                    CanResize = false,
                    SizeToContent = SizeToContent.WidthAndHeight,
                    Content = content,
                };
                railDriverLegend.Closed += (sender, e) =>
                {
                    railDriverLegend = null;
                };
                railDriverLegend.KeyDown += (sender, e) =>
                {
                    if (e.Key == Key.Escape)
                        railDriverLegend.Close();
                };
                // Drag the window by the picture, as there is little title bar to grab.
                content.PointerPressed += (sender, e) => railDriverLegend.BeginMoveDrag(e);
            }
            DrawRailDriverGuidance();
            return railDriverLegend;
        }

        /// <summary>
        /// Draws the calibration guidance (where the lever is and what to do) on top of the legend.
        /// </summary>
        void DrawRailDriverGuidance()
        {
            if (railDriverLegendGuidance == null)
                return;
            railDriverLegendGuidance.Children.Clear();
            if ((int)currentCalibrationStep < (startingPoints.Length / 2))
            {
                var x = startingPoints[(int)currentCalibrationStep, 0];
                var y = startingPoints[(int)currentCalibrationStep, 1];
                var rectangle = new Rectangle { Width = 60, Height = 40, Stroke = Brushes.Red, StrokeThickness = 4 };
                Canvas.SetLeft(rectangle, x);
                Canvas.SetTop(rectangle, y);
                railDriverLegendGuidance.Children.Add(rectangle);

                var end = new Point(x - 5, y + 20);
                railDriverLegendGuidance.Children.Add(new Line { StartPoint = new Point(10, 10), EndPoint = end, Stroke = Brushes.Red, StrokeThickness = 4 });
                // Arrow head
                var direction = end - new Point(10, 10);
                var length = Math.Sqrt(direction.X * direction.X + direction.Y * direction.Y);
                var unit = new Point(direction.X / length, direction.Y / length);
                var normal = new Point(-unit.Y, unit.X);
                railDriverLegendGuidance.Children.Add(new Polygon
                {
                    Fill = Brushes.Red,
                    Points = new List<Point>
                    {
                        end + unit * 6,
                        end - unit * 12 + normal * 8,
                        end - unit * 12 - normal * 8,
                    },
                });

                var text = new TextBlock { Text = Localization.GetString(GetStringAttribute.GetPrettyName(currentCalibrationStep)), Foreground = Brushes.Red, FontSize = 18 };
                Canvas.SetLeft(text, 80);
                Canvas.SetTop(text, 225);
                railDriverLegendGuidance.Children.Add(text);
            }
        }

        private void InitializeRailDriverInputControls()
        {
            panelRDButtons.Children.Clear();
            panelRDButtons.RowDefinitions.Clear();
            RDButtonInputControls.Clear();

            string previousCategory = "";
            var i = 0;

            foreach (UserCommand command in Enum.GetValues(typeof(UserCommand)))
            {
                string name = InputSettings.GetPrettyLocalizedName(command);
                string category, description;
                int index = name.IndexOf(' ');
                if (index == -1)
                {
                    category = string.Empty;
                    description = name;
                }
                else
                {
                    category = name.Substring(0, index);
                    description = name.Substring(index + 1);
                }

                if (category != previousCategory)
                {
                    var categorylabel = new TextBlock
                    {
                        Text = category,
                        FontWeight = FontWeight.Bold,
                        Margin = new Thickness(0, 8, 0, 2),
                    };
                    panelRDButtons.RowDefinitions.Add(new RowDefinition(GridLength.Auto));
                    Grid.SetRow(categorylabel, i);
                    panelRDButtons.Children.Add(categorylabel);

                    previousCategory = category;
                    ++i;
                }

                var label = new TextBlock
                {
                    Text = description,
                    HorizontalAlignment = HorizontalAlignment.Right,
                    VerticalAlignment = VerticalAlignment.Center,
                    Margin = new Thickness(0, 0, 8, 0),
                };
                panelRDButtons.RowDefinitions.Add(new RowDefinition(GridLength.Auto));
                Grid.SetRow(label, i);
                panelRDButtons.Children.Add(label);

                var rdButtonControl = new RDButtonInputControl(command, Settings.RailDriver.UserCommands[(int)command], RailDriverSettings.GetDefaultValue(command), instance)
                {
                    Margin = new Thickness(0, 1),
                };
                ToolTip.SetTip(rdButtonControl, Localization.GetString("Click to change this button"));
                Grid.SetRow(rdButtonControl, i);
                Grid.SetColumn(rdButtonControl, 1);
                panelRDButtons.Children.Add(rdButtonControl);
                RDButtonInputControls.Add(rdButtonControl);

                ++i;
            }
        }

        private void InitializeRailDriverSettings()
        {
            instance = GetRailDriverDevice(out var unavailableReason);

            checkReverseReverser.IsChecked = Settings.RailDriver.CalibrationSettings[(int)RailDriverCalibrationSetting.ReverseReverser] != 0;
            checkReverseThrottle.IsChecked = Settings.RailDriver.CalibrationSettings[(int)RailDriverCalibrationSetting.ReverseThrottle] != 0;
            checkReverseAutoBrake.IsChecked = Settings.RailDriver.CalibrationSettings[(int)RailDriverCalibrationSetting.ReverseAutoBrake] != 0;
            checkReverseIndependentBrake.IsChecked = Settings.RailDriver.CalibrationSettings[(int)RailDriverCalibrationSetting.ReverseIndependentBrake] != 0;
            checkFullRangeThrottle.IsChecked = Settings.RailDriver.CalibrationSettings[(int)RailDriverCalibrationSetting.FullRangeThrottle] != 0;
            InitializeRailDriverInputControls();

            // Without the device, the tab is shown (so that the settings can be seen) but can't be changed.
            var available = instance != null;
            labelRailDriverUnavailable.IsVisible = !available;
            labelRailDriverUnavailable.Text = unavailableReason;
            panelRDButtons.IsEnabled = available;
            groupBoxReverseRDLevers.IsEnabled = available;
            btnStartRDCalibration.IsEnabled = available;
            btnRDReset.IsEnabled = available;
            btnCheck.IsEnabled = available;
        }

        private async System.Threading.Tasks.Task RunCalibration()
        {
            byte[] readData = instance.GetReadBuffer();
            instance.SetLeds(RailDriverDisplaySign.Char_C, RailDriverDisplaySign.Char_A, RailDriverDisplaySign.Char_L);
            RailDriverCalibrationSetting nextStep = RailDriverCalibrationSetting.ReverserNeutral;
            var result = MessageBoxResult.OK;
            while (result == MessageBoxResult.OK && nextStep < RailDriverCalibrationSetting.ReverseReverser)
            {
                currentCalibrationStep = nextStep;
                DrawRailDriverGuidance();  //enforce redraw legend to show guidance
                result = await MessageBox.Show(railDriverLegend, $"Now calibrating \"{Localization.GetString(GetStringAttribute.GetPrettyName(currentCalibrationStep))}\". Move the Lever as indicated through guidance. \n\nClick OK to read the position and continue. Click Cancel anytime to abort the calibration process.", "RailDriver Calibration", MessageBoxButtons.OKCancel);
                // Read Setting
                if (result == MessageBoxResult.OK)
                {
                    if (0 == instance.ReadCurrentData(ref readData))
                    {
                        int index = 0;
                        if ((int)currentCalibrationStep < 3)        //Reverser
                            index = 1;
                        else if ((int)currentCalibrationStep < 7)   //Throttle/Dynamic Brake
                            index = 2;
                        else if ((int)currentCalibrationStep < 10)  //Auto Brake
                            index = 3;
                        else if ((int)currentCalibrationStep < 12)  //Independent Brake
                            index = 4;
                        else if ((int)currentCalibrationStep < 16)  //Independent Brake
                            index = 5;
                        else if ((int)currentCalibrationStep < 19)  //Rotary 1
                            index = 6;
                        else if ((int)currentCalibrationStep < 22)  //Rotary 2
                            index = 7;

                        calibrationSettings[(int)currentCalibrationStep] = readData[index];
                        instance.SetLedsNumeric(readData[index]);
                    }
                }
                nextStep++;
            }
            currentCalibrationStep = RailDriverCalibrationSetting.CutOffDelta;
            DrawRailDriverGuidance();
            if (nextStep == RailDriverCalibrationSetting.ReverseReverser)
            {
                isCalibrationSet = (await MessageBox.Show(railDriverLegend, "Calibration Completed. Do you want to keep the results?", "Calibration Done", MessageBoxButtons.YesNo) == MessageBoxResult.Yes);
            }
            instance.SetLeds(RailDriverDisplaySign.Blank, RailDriverDisplaySign.Blank, RailDriverDisplaySign.Blank);
        }

        private async void StartRDCalibration_Click(object sender, EventArgs e)
        {
            var legend = GetRailDriverLegend();
            legend.Show(this);
            await RunCalibration();
        }

        private async void BtnRDReset_Click(object sender, EventArgs e)
        {
            if (MessageBoxResult.Yes == await MessageBox.Show(this, Localization.GetString("Remove all custom button assignments?"), App.ProductName, MessageBoxButtons.YesNo))
            {
                Settings.RailDriver.Reset();
                InitializeRailDriverSettings();
            }
        }

        private void BtnShowRDLegend_Click(object sender, EventArgs e)
        {
            var legend = GetRailDriverLegend();
            if (!legend.IsVisible)
                legend.Show(this);
            else
                legend.Activate();
        }

        private async void BtnCheck_Click(object sender, EventArgs e)
        {
            string result = CheckButtonAssignments();
            if (!string.IsNullOrEmpty(result))
                await MessageBox.Show(this, result, App.ProductName);
            else
                await MessageBox.Show(this, Localization.GetString("No errors found."), App.ProductName);
        }

        private async void BtnRDSettingsExport_Click(object sender, EventArgs e)
        {
            string outputPath = Path.Combine(GetExportFolder(), "Open Rails RailDriver.txt");
            try
            {
                Settings.RailDriver.DumpToText(outputPath);
                await MessageBox.Show(this, Localization.GetString("A listing of all Raildriver button assignments has been placed here:\n\n") + outputPath, App.ProductName);
            }
            catch (Exception error)
            {
                await MessageBox.Show(this, error.Message, App.ProductName);
            }
        }

        private string CheckButtonAssignments()
        {
            if (instance == null)
                return string.Empty;
            byte[] buttons = new byte[Enum.GetNames(typeof(UserCommand)).Length];
            foreach (var control in RDButtonInputControls)
                buttons[(int)control.Command] = control.UserButton;
            return Settings.RailDriver.CheckForErrors(buttons);
        }

        private void SaveRailDriverSettings()
        {
            foreach (var control in RDButtonInputControls)
                Settings.RailDriver.UserCommands[(int)control.Command] = control.UserButton;

            if (isCalibrationSet)
            {
                currentCalibrationStep = RailDriverCalibrationSetting.ReverserNeutral;
                while (currentCalibrationStep < RailDriverCalibrationSetting.ReverseReverser)
                {

                    Settings.RailDriver.CalibrationSettings[(int)currentCalibrationStep] = calibrationSettings[(int)currentCalibrationStep];
                    currentCalibrationStep++;
                }
            }

            Settings.RailDriver.CalibrationSettings[(int)RailDriverCalibrationSetting.ReverseReverser] = Convert.ToByte(checkReverseReverser.IsChecked == true);
            Settings.RailDriver.CalibrationSettings[(int)RailDriverCalibrationSetting.ReverseThrottle] = Convert.ToByte(checkReverseThrottle.IsChecked == true);
            Settings.RailDriver.CalibrationSettings[(int)RailDriverCalibrationSetting.ReverseAutoBrake] = Convert.ToByte(checkReverseAutoBrake.IsChecked == true);
            Settings.RailDriver.CalibrationSettings[(int)RailDriverCalibrationSetting.ReverseIndependentBrake] = Convert.ToByte(checkReverseIndependentBrake.IsChecked == true);
            Settings.RailDriver.CalibrationSettings[(int)RailDriverCalibrationSetting.FullRangeThrottle] = Convert.ToByte(checkFullRangeThrottle.IsChecked == true);

            currentCalibrationStep = RailDriverCalibrationSetting.CutOffDelta;
        }
    }
}
