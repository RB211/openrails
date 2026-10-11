// COPYRIGHT 2009, 2010, 2011, 2012, 2013, 2014 by the Open Rails project.
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
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using ORTS.Common;
using ORTS.Common.Input;
using ORTS.Settings;
using ORTS.Updater;

namespace Menu
{
    public partial class OptionsWindow : Window
    {
        readonly UserSettings Settings;
        readonly UpdateManager UpdateManager;
        readonly TelemetryManager TelemetryManager;
        readonly string BaseDocumentationUrl;

        public class ComboBoxMember
        {
            public string Code { get; set; }
            public string Name { get; set; }
            public override string ToString() => Name;
        }

        public OptionsWindow(UserSettings settings, UpdateManager updateManager, TelemetryManager telemetryManager, string baseDocumentationUrl)
        {
            InitializeComponent();
            Icon = App.LoadIcon();

            Settings = settings;
            UpdateManager = updateManager;
            TelemetryManager = telemetryManager;
            BaseDocumentationUrl = baseDocumentationUrl;

            InitializeHelpIcons();
            WireEvents();

            // Collect all the available language codes by searching for
            // localisation files, but always include English (base language).
            var languageCodes = new List<string> { "en" };
            foreach (var path in Directory.GetDirectories(ApplicationInfo.ProcessDirectory))
                if (Directory.GetFiles(path, "*.Messages.resources.dll").Length > 0)
                    languageCodes.Add(Path.GetFileName(path));

            // Turn the list of codes in to a list of code + name pairs for
            // displaying in the dropdown list.
            SetItems(comboLanguage,
                new[] { new ComboBoxMember { Code = "", Name = "System" } }
                .Union(languageCodes
                    .SelectMany(lc =>
                    {
                        try
                        {
                            return new[] { new ComboBoxMember { Code = lc, Name = CultureInfo.GetCultureInfo(lc).NativeName } };
                        }
                        catch (ArgumentException)
                        {
                            return new ComboBoxMember[0];
                        }
                    })
                    .OrderBy(l => l.Name)
                ));
            SelectByCode(comboLanguage, Settings.Language);
            if (comboLanguage.SelectedItem == null) comboLanguage.SelectedIndex = 0;

            SetItems(comboOtherUnits, new[] {
                new ComboBoxMember { Code = "Route", Name = Localization.GetString("Route") },
                new ComboBoxMember { Code = "Automatic", Name = Localization.GetString("Player's location") },
                new ComboBoxMember { Code = "Metric", Name = Localization.GetString("Metric") },
                new ComboBoxMember { Code = "US", Name = Localization.GetString("Imperial US") },
                new ComboBoxMember { Code = "UK", Name = Localization.GetString("Imperial UK") },
            });
            SelectByCode(comboOtherUnits, Settings.Units);

            SetItems(comboPressureUnit, new[] {
                new ComboBoxMember { Code = "Automatic", Name = Localization.GetString("Automatic") },
                new ComboBoxMember { Code = "bar", Name = Localization.GetString("bar") },
                new ComboBoxMember { Code = "PSI", Name = Localization.GetString("psi") },
                new ComboBoxMember { Code = "inHg", Name = Localization.GetString("inHg") },
                new ComboBoxMember { Code = "kgf/cm^2", Name = Localization.GetString("kgf/cm²") },
            });
            SelectByCode(comboPressureUnit, Settings.PressureUnit);

            // General tab
            checkAlerter.IsChecked = Settings.Alerter;
            checkAlerterExternal.IsEnabled = Settings.Alerter;
            checkAlerterExternal.IsChecked = Settings.Alerter && !Settings.AlerterDisableExternal;
            checkOverspeedMonitor.IsChecked = Settings.SpeedControl;
            checkRetainers.IsChecked = Settings.RetainersOnAllCars;
            checkGraduatedRelease.IsChecked = Settings.GraduatedRelease;
            numericBrakePipeChargingRate.Value = Settings.BrakePipeChargingRate;
            checkEnableTCSScripts.IsChecked = !Settings.DisableTCSScripts;    // Inverted as "Enable scripts" is better UI than "Disable scripts"
            checkAutoSaveActive.IsChecked = Settings.AutoSaveActive;
            ButtonAutoSave15.IsChecked = Settings.AutoSaveActive & Settings.AutoSaveInterval == 15;
            ButtonAutoSave30.IsChecked = Settings.AutoSaveActive & Settings.AutoSaveInterval == 30;
            ButtonAutoSave60.IsChecked = Settings.AutoSaveActive & Settings.AutoSaveInterval == 60;
            ButtonAutoSave15.IsEnabled = ButtonAutoSave30.IsEnabled = ButtonAutoSave60.IsEnabled = Settings.AutoSaveActive;

            // Audio tab
            numericSoundVolumePercent.Value = Settings.SoundVolumePercent;
            numericSoundDetailLevel.Value = Settings.SoundDetailLevel;
            numericExternalSoundPassThruPercent.Value = Settings.ExternalSoundPassThruPercent;

            // Video tab
            checkDynamicShadows.IsChecked = Settings.DynamicShadows;
            checkShadowAllShapes.IsChecked = Settings.ShadowAllShapes;
            checkModelInstancing.IsChecked = Settings.ModelInstancing;
            checkWire.IsChecked = Settings.Wire;
            checkVerticalSync.IsChecked = Settings.VerticalSync;
            numericViewingDistance.Value = Settings.ViewingDistance;
            checkDistantMountains.IsChecked = Settings.DistantMountains;
            labelDistantMountainsViewingDistance.IsEnabled = Settings.DistantMountains;
            numericDistantMountainsViewingDistance.IsEnabled = Settings.DistantMountains;
            numericDistantMountainsViewingDistance.Value = Settings.DistantMountainsViewingDistance / 1000;
            checkLODViewingExtension.IsChecked = Settings.LODViewingExtension;
            numericViewingFOV.Value = Settings.ViewingFOV;
            numericUpDownFOV_ValueChanged(null, null);
            numericWorldObjectDensity.Value = Settings.WorldObjectDensity;
            trackDayAmbientLight.Value = Settings.DayAmbientLight;
            trackDayAmbientLight_ValueChanged(null, null);
            trackAntiAliasing.Value = Settings.AntiAliasing;
            trackAntiAliasing_ValueChanged(null, null);
            checkDoubleWire.IsChecked = Settings.DoubleWire;

            // Simulation tab
            checkSimpleControlsPhysics.IsChecked = Settings.SimpleControlPhysics;
            checkUseAdvancedAdhesion.IsChecked = Settings.UseAdvancedAdhesion;
            checkBreakCouplers.IsChecked = Settings.BreakCouplers;
            checkCurveSpeedDependent.IsChecked = Settings.CurveSpeedDependent;
            checkBoilerPreheated.IsChecked = Settings.HotStart;
            checkForcedRedAtStationStops.IsChecked = !Settings.NoForcedRedAtStationStops;
            checkDoorsAITrains.IsChecked = Settings.OpenDoorsInAITrains;
            checkDieselEnginesStarted.IsChecked = !Settings.NoDieselEngineStart; // Inverted as "EngineStart" is better UI than "NoEngineStart"
            checkElectricPowerConnected.IsChecked = Settings.ElectricHotStart;

            // Keyboard tab
            InitializeKeyboardSettings();

            // Raildriver Tab
            InitializeRailDriverSettings();

            // DataLogger tab
            SetItems(comboDataLoggerSeparator, new[] {
                new ComboBoxMember { Code = "comma", Name = Localization.GetString("comma") },
                new ComboBoxMember { Code = "semicolon", Name = Localization.GetString("semicolon") },
                new ComboBoxMember { Code = "tab", Name = Localization.GetString("tab") },
                new ComboBoxMember { Code = "space", Name = Localization.GetString("space") },
            });
            SelectByCode(comboDataLoggerSeparator, Settings.DataLoggerSeparator);
            checkDataLogUseInternalUnits.IsChecked = Settings.DataLogUseInternalUnits;
            checkDataLogger.IsChecked = Settings.DataLogger;
            checkDataLogPerformance.IsChecked = Settings.DataLogPerformance;
            checkDataLogPhysics.IsChecked = Settings.DataLogPhysics;
            checkDataLogSteamPerformance.IsChecked = Settings.DataLogExclusiveSteamPerformance;
            checkDataLogSteamPowerCurve.IsChecked = Settings.DataLogExclusiveSteamPowerCurve;
            dataLoggerInterval.Value = Settings.DataLoggerInterval;
            checkVerboseConfigurationMessages.IsChecked = Settings.VerboseConfigurationMessages;

            // Evaluation tab
            checkDataLogTrainSpeed.IsChecked = Settings.DataLogTrainSpeed;
            labelDataLogTSInterval.IsEnabled = Settings.DataLogTrainSpeed;
            numericDataLogTSInterval.IsEnabled = Settings.DataLogTrainSpeed;
            checkListDataLogTSContents.IsEnabled = Settings.DataLogTrainSpeed;
            numericDataLogTSInterval.Value = Settings.DataLogTSInterval;
            var contents = new[] {
                Localization.GetString("Time"),
                Localization.GetString("Train Speed"),
                Localization.GetString("Max. Speed"),
                Localization.GetString("Signal State"),
                Localization.GetString("Track Elevation"),
                Localization.GetString("Direction"),
                Localization.GetString("Control Mode"),
                Localization.GetString("Distance Travelled"),
                Localization.GetString("Throttle %"),
                Localization.GetString("Brake Cyl Press"),
                Localization.GetString("Dyn Brake %"),
                Localization.GetString("Gear Setting")
            };
            for (var i = 0; i < contents.Length; i++)
                checkListDataLogTSContents.Children.Add(new CheckBox { Content = contents[i], IsChecked = i < Settings.DataLogTSContents.Length && Settings.DataLogTSContents[i] == 1 });
            checkDataLogStationStops.IsChecked = Settings.DataLogStationStops;

            // System tab
            DrawSystemTab(updateManager);

            checkWindowed.IsChecked = !Settings.FullScreen;
            foreach (var size in new[] { "800x600", "1024x768", "1280x720", "1280x800", "1280x1024", "1360x768", "1366x768", "1440x900", "1536x864", "1600x900", "1680x1050", "1920x1080", "1920x1200", "2560x1440", "3840x2160" })
                comboWindowSize.Items.Add(size);
            comboWindowSize.Text = Settings.WindowSize;
            checkWindowGlass.IsChecked = Settings.WindowGlass;

            // keep values in line with enum Orts.Simulation.ConfirmLevel
            // see also function Message(CabControl control, ConfirmLevel level, string message)
            // in Source\Orts.Simulation\Simulation\Confirmer.cs
            SetItems(comboControlConfirmations, new[] {
                new ComboBoxMember { Code = "None", Name = Localization.GetString("None") },
                new ComboBoxMember { Code = "Information", Name = Localization.GetString("Information") },
                new ComboBoxMember { Code = "Warning", Name = Localization.GetString("Warning") },
                new ComboBoxMember { Code = "Error", Name = Localization.GetString("Error") },
            });
            comboControlConfirmations.SelectedIndex = Settings.SuppressConfirmations;

            numericWebServerPort.Value = Settings.WebServerPort;
            checkPerformanceTuner.IsChecked = Settings.PerformanceTuner;
            labelPerformanceTunerTarget.IsEnabled = Settings.PerformanceTuner;
            numericPerformanceTunerTarget.Value = Settings.PerformanceTunerTarget;
            numericPerformanceTunerTarget.IsEnabled = Settings.PerformanceTuner;

            // Experimental tab
            checkUseSuperElevation.IsChecked = Settings.LegacySuperElevation;
            numericSuperElevationGauge.Value = Settings.SuperElevationGauge;
            trackLODBias.Value = Settings.LODBias;
            trackLODBias_ValueChanged(null, null);
            checkSignalLightGlow.IsChecked = Settings.SignalLightGlow;
            checkUseLocationPassingPaths.IsChecked = Settings.UseLocationPassingPaths;
            checkUseMSTSEnv.IsChecked = Settings.UseMSTSEnv;
            trackAdhesionFactor.Value = Settings.AdhesionFactor;
            trackAdhesionFactorChange.Value = Settings.AdhesionFactorChange;
            trackAdhesionFactor_ValueChanged(null, null);
            checkShapeWarnings.IsChecked = !Settings.SuppressShapeWarnings;   // Inverted as "Show warnings" is better UI than "Suppress warnings"
            checkCorrectQuestionableBrakingParams.IsChecked = Settings.CorrectQuestionableBrakingParams;
            numericActRandomizationLevel.Value = Settings.ActRandomizationLevel;
            numericActWeatherRandomizationLevel.Value = Settings.ActWeatherRandomizationLevel;

            Closed += (sender, e) => railDriverLegend?.Close();
        }

        void WireEvents()
        {
            buttonOK.Click += buttonOK_Click;
            buttonCancel.Click += (sender, e) => Close(DialogResult.Cancel);
            buttonTelemetry.Click += buttonTelemetry_Click;
            checkAlerter.IsCheckedChanged += checkAlerter_CheckedChanged;
            checkAutoSaveActive.IsCheckedChanged += checkAutoSave_checkchanged;
            ButtonAutoSave15.IsCheckedChanged += buttonAutoSaveInterval_checkchanged;
            ButtonAutoSave30.IsCheckedChanged += buttonAutoSaveInterval_checkchanged;
            ButtonAutoSave60.IsCheckedChanged += buttonAutoSaveInterval_checkchanged;
            checkDistantMountains.IsCheckedChanged += checkDistantMountains_Click;
            numericViewingFOV.ValueChanged += numericUpDownFOV_ValueChanged;
            trackDayAmbientLight.ValueChanged += trackDayAmbientLight_ValueChanged;
            trackAntiAliasing.ValueChanged += trackAntiAliasing_ValueChanged;
            trackLODBias.ValueChanged += trackLODBias_ValueChanged;
            trackAdhesionFactor.ValueChanged += trackAdhesionFactor_ValueChanged;
            trackAdhesionFactorChange.ValueChanged += trackAdhesionFactor_ValueChanged;
            checkDataLogTrainSpeed.IsCheckedChanged += checkDataLogTrainSpeed_Click;
            checkPerformanceTuner.IsCheckedChanged += checkPerformanceTuner_Click;
            buttonCheckKeys.Click += buttonCheckKeys_Click;
            buttonDefaultKeys.Click += buttonDefaultKeys_Click;
            buttonExport.Click += buttonExport_Click;
            btnShowRDLegend.Click += BtnShowRDLegend_Click;
            btnStartRDCalibration.Click += StartRDCalibration_Click;
            btnRDReset.Click += BtnRDReset_Click;
            btnCheck.Click += BtnCheck_Click;
            btnRDSettingsExport.Click += BtnRDSettingsExport_Click;
        }

        static void SetItems(ComboBox comboBox, IEnumerable<ComboBoxMember> members)
        {
            comboBox.Items.Clear();
            foreach (var member in members)
                comboBox.Items.Add(member);
        }

        static void SelectByCode(ComboBox comboBox, string code)
        {
            comboBox.SelectedItem = comboBox.Items.OfType<ComboBoxMember>().FirstOrDefault(m => m.Code == code);
        }

        static string SelectedCode(ComboBox comboBox)
        {
            return (comboBox.SelectedItem as ComboBoxMember)?.Code ?? "";
        }

        static int GetValue(NumericUpDown numeric)
        {
            return (int)(numeric.Value ?? numeric.Minimum);
        }

        static int GetValue(Slider slider)
        {
            return (int)Math.Round(slider.Value);
        }

        private void DrawSystemTab(UpdateManager updateManager)
        {
            var updateChannelNames = new Dictionary<string, string> {
                { "stable", Localization.GetString("Stable (recommended)") },
                { "testing", Localization.GetString("Testing") },
                { "unstable", Localization.GetString("Unstable") },
                { "", Localization.GetString("None") },
            };
            var updateChannelDescriptions = new Dictionary<string, string> {
                { "stable", Localization.GetString("Infrequent updates to official, hand-picked versions. Recommended for most users.") },
                { "testing", Localization.GetString("Weekly updates which may contain noticable defects. For project supporters.") },
                { "unstable", Localization.GetString("Daily updates which may contain serious defects. For developers only.") },
                { "", Localization.GetString("No updates.") },
            };
            var channels = UpdateManager.GetChannels();
            foreach (var channel in channels)
            {
                var radio = new RadioButton()
                {
                    Content = updateChannelNames.TryGetValue(channel.ToLowerInvariant(), out var name) ? name : channel,
                    GroupName = "UpdateChannel",
                    IsChecked = (updateManager.ChannelName ?? "").Equals(channel, StringComparison.InvariantCultureIgnoreCase),
                    Tag = channel,
                };
                panelUpdateChannels.Children.Add(radio);
                var label = new TextBlock()
                {
                    Text = updateChannelDescriptions.TryGetValue(channel.ToLowerInvariant(), out var description) ? description : "",
                    Margin = new Thickness(28, 0, 0, 6),
                    TextWrapping = TextWrapping.Wrap,
                };
                panelUpdateChannels.Children.Add(label);
            }
            if (channels.Length == 0)
            {
                panelUpdateChannels.Children.Add(new TextBlock
                {
                    Text = OperatingSystem.IsWindows()
                        ? Localization.GetString("No update channels are configured (Updater.ini is missing).")
                        : Localization.GetString("Open Rails does not update itself on this platform. Please update it the same way you installed it."),
                    TextWrapping = TextWrapping.Wrap,
                });
            }
        }

        static string ParseCategoryFrom(string name)
        {
            var len = name.IndexOf(' ');
            if (len == -1)
                return "";
            else
                return name.Substring(0, len);
        }


        static string ParseDescriptorFrom(string name)
        {
            var len = name.IndexOf(' ');
            if (len == -1)
                return name;
            else
                return name.Substring(len + 1);
        }

        readonly List<(UserCommand Command, KeyInputControl Control)> KeyInputControls = new List<(UserCommand, KeyInputControl)>();

        void InitializeKeyboardSettings()
        {
            panelKeys.Children.Clear();
            panelKeys.RowDefinitions.Clear();
            KeyInputControls.Clear();

            var lastCategory = "";
            var i = 0;
            foreach (UserCommand command in Enum.GetValues(typeof(UserCommand)))
            {
                var name = InputSettings.GetPrettyLocalizedName(command);
                var category = ParseCategoryFrom(name);
                var descriptor = ParseDescriptorFrom(name);

                if (category != lastCategory)
                {
                    var catlabel = new TextBlock
                    {
                        Text = category,
                        FontWeight = FontWeight.Bold,
                        HorizontalAlignment = HorizontalAlignment.Center,
                        Margin = new Thickness(0, 8, 0, 2),
                    };
                    panelKeys.RowDefinitions.Add(new RowDefinition(GridLength.Auto));
                    Grid.SetRow(catlabel, i);
                    panelKeys.Children.Add(catlabel);

                    lastCategory = category;
                    ++i;
                }

                var label = new TextBlock
                {
                    Text = descriptor,
                    HorizontalAlignment = HorizontalAlignment.Right,
                    VerticalAlignment = VerticalAlignment.Center,
                    Margin = new Thickness(0, 0, 8, 0),
                };
                panelKeys.RowDefinitions.Add(new RowDefinition(GridLength.Auto));
                Grid.SetRow(label, i);
                panelKeys.Children.Add(label);

                var keyInputControl = new KeyInputControl(Settings.Input.Commands[(int)command], InputSettings.DefaultCommands[(int)command])
                {
                    Margin = new Thickness(0, 1),
                };
                Grid.SetRow(keyInputControl, i);
                Grid.SetColumn(keyInputControl, 1);
                panelKeys.Children.Add(keyInputControl);
                KeyInputControls.Add((command, keyInputControl));

                ++i;
            }
        }

        void SaveKeyboardSettings()
        {
            foreach (var (command, control) in KeyInputControls)
                Settings.Input.Commands[(int)command].PersistentDescriptor = control.UserInput.PersistentDescriptor;
        }

        /// <summary>
        /// Key names come from the platform; report rather than crash if that fails.
        /// </summary>
        string CheckKeysForErrors()
        {
            try
            {
                return Settings.Input.CheckForErrors();
            }
            catch (Exception error)
            {
                return Localization.GetString("Unable to check the key assignments:") + "\n" + error.Message;
            }
        }

        async void buttonOK_Click(object sender, EventArgs e)
        {
            var result = CheckKeysForErrors();
            if (result != "" && MessageBoxResult.Yes != await MessageBox.Show(this, Localization.GetString("Continue with conflicting key assignments?\n\n") + result, App.ProductName, MessageBoxButtons.YesNo))
                return;

            var dialogResult = DialogResult.OK;
            if (Settings.Language != SelectedCode(comboLanguage))
                dialogResult = DialogResult.Retry;

            // General tab
            Settings.Alerter = checkAlerter.IsChecked == true;
            Settings.AlerterDisableExternal = checkAlerterExternal.IsChecked != true;
            Settings.SpeedControl = checkOverspeedMonitor.IsChecked == true;
            Settings.RetainersOnAllCars = checkRetainers.IsChecked == true;
            Settings.GraduatedRelease = checkGraduatedRelease.IsChecked == true;
            Settings.BrakePipeChargingRate = GetValue(numericBrakePipeChargingRate);
            Settings.PressureUnit = SelectedCode(comboPressureUnit);
            Settings.Units = SelectedCode(comboOtherUnits);
            Settings.DisableTCSScripts = checkEnableTCSScripts.IsChecked != true; // Inverted as "Enable scripts" is better UI than "Disable scripts"
            Settings.AutoSaveActive = checkAutoSaveActive.IsChecked == true;
            Settings.AutoSaveInterval = ButtonAutoSave15.IsChecked == true ? 15 : ButtonAutoSave30.IsChecked == true ? 30 : 60;

            // Audio tab
            Settings.SoundVolumePercent = GetValue(numericSoundVolumePercent);
            Settings.SoundDetailLevel = GetValue(numericSoundDetailLevel);
            Settings.ExternalSoundPassThruPercent = GetValue(numericExternalSoundPassThruPercent);

            // Video tab
            Settings.DynamicShadows = checkDynamicShadows.IsChecked == true;
            Settings.ShadowAllShapes = checkShadowAllShapes.IsChecked == true;
            Settings.ModelInstancing = checkModelInstancing.IsChecked == true;
            Settings.Wire = checkWire.IsChecked == true;
            Settings.VerticalSync = checkVerticalSync.IsChecked == true;
            Settings.ViewingDistance = GetValue(numericViewingDistance);
            Settings.DistantMountains = checkDistantMountains.IsChecked == true;
            Settings.DistantMountainsViewingDistance = GetValue(numericDistantMountainsViewingDistance) * 1000;
            Settings.LODViewingExtension = checkLODViewingExtension.IsChecked == true;
            Settings.ViewingFOV = GetValue(numericViewingFOV);
            Settings.WorldObjectDensity = GetValue(numericWorldObjectDensity);

            Settings.DayAmbientLight = GetValue(trackDayAmbientLight);
            Settings.DoubleWire = checkDoubleWire.IsChecked == true;
            Settings.AntiAliasing = GetValue(trackAntiAliasing);

            // Simulation tab
            Settings.SimpleControlPhysics = checkSimpleControlsPhysics.IsChecked == true;
            Settings.UseAdvancedAdhesion = checkUseAdvancedAdhesion.IsChecked == true;
            Settings.BreakCouplers = checkBreakCouplers.IsChecked == true;
            Settings.CurveSpeedDependent = checkCurveSpeedDependent.IsChecked == true;
            Settings.HotStart = checkBoilerPreheated.IsChecked == true;
            Settings.NoForcedRedAtStationStops = checkForcedRedAtStationStops.IsChecked != true;
            Settings.OpenDoorsInAITrains = checkDoorsAITrains.IsChecked == true;
            Settings.NoDieselEngineStart = checkDieselEnginesStarted.IsChecked != true; // Inverted as "EngineStart" is better UI than "NoEngineStart"
            Settings.ElectricHotStart = checkElectricPowerConnected.IsChecked == true;

            // Keyboard tab
            // These are edited live.
            SaveKeyboardSettings();

            // Raildriver Tab
            SaveRailDriverSettings();

            // DataLogger tab
            Settings.DataLoggerSeparator = SelectedCode(comboDataLoggerSeparator);
            Settings.DataLogUseInternalUnits = checkDataLogUseInternalUnits.IsChecked == true;
            Settings.DataLogger = checkDataLogger.IsChecked == true;
            Settings.DataLogPerformance = checkDataLogPerformance.IsChecked == true;
            Settings.DataLogPhysics = checkDataLogPhysics.IsChecked == true;
            Settings.DataLogExclusiveSteamPerformance = checkDataLogSteamPerformance.IsChecked == true;
            Settings.DataLogExclusiveSteamPowerCurve = checkDataLogSteamPowerCurve.IsChecked == true;
            Settings.DataLoggerInterval = GetValue(dataLoggerInterval);
            Settings.VerboseConfigurationMessages = checkVerboseConfigurationMessages.IsChecked == true;

            // Evaluation tab
            Settings.DataLogTrainSpeed = checkDataLogTrainSpeed.IsChecked == true;
            Settings.DataLogTSInterval = GetValue(numericDataLogTSInterval);
            var contentChecks = checkListDataLogTSContents.Children.OfType<CheckBox>().ToList();
            for (var i = 0; i < contentChecks.Count && i < Settings.DataLogTSContents.Length; i++)
                Settings.DataLogTSContents[i] = contentChecks[i].IsChecked == true ? 1 : 0;
            Settings.DataLogStationStops = checkDataLogStationStops.IsChecked == true;

            // System tab
            Settings.Language = SelectedCode(comboLanguage);
            foreach (var radio in panelUpdateChannels.Children.OfType<RadioButton>())
                if (radio.IsChecked == true)
                    UpdateManager.SetChannel((string)radio.Tag);
            Settings.FullScreen = checkWindowed.IsChecked != true;
            Settings.WindowSize = GetValidWindowSize(comboWindowSize.Text ?? "");
            Settings.WindowGlass = checkWindowGlass.IsChecked == true;
            Settings.SuppressConfirmations = Math.Max(0, comboControlConfirmations.SelectedIndex);
            Settings.WebServerPort = GetValue(numericWebServerPort);
            Settings.PerformanceTuner = checkPerformanceTuner.IsChecked == true;
            Settings.PerformanceTunerTarget = GetValue(numericPerformanceTunerTarget);

            // Experimental tab
            Settings.LegacySuperElevation = checkUseSuperElevation.IsChecked == true;
            Settings.SuperElevationGauge = GetValue(numericSuperElevationGauge);
            Settings.LODBias = GetValue(trackLODBias);
            Settings.SignalLightGlow = checkSignalLightGlow.IsChecked == true;
            Settings.UseLocationPassingPaths = checkUseLocationPassingPaths.IsChecked == true;
            Settings.UseMSTSEnv = checkUseMSTSEnv.IsChecked == true;
            Settings.AdhesionFactor = GetValue(trackAdhesionFactor);
            Settings.AdhesionFactorChange = GetValue(trackAdhesionFactorChange);
            Settings.SuppressShapeWarnings = checkShapeWarnings.IsChecked != true;
            Settings.CorrectQuestionableBrakingParams = checkCorrectQuestionableBrakingParams.IsChecked == true;
            Settings.ActRandomizationLevel = GetValue(numericActRandomizationLevel);
            Settings.ActWeatherRandomizationLevel = GetValue(numericActWeatherRandomizationLevel);

            Settings.Save();

            Close(dialogResult);
        }

        /// <summary>
        /// Returns user's [width]x[height] if expression is valid and values are sane, else returns previous value of setting.
        /// </summary>
        private string GetValidWindowSize(string text)
        {
            var match = Regex.Match(text, @"^\s*([1-9]\d{2,3})\s*[Xx]\s*([1-9]\d{2,3})\s*$");//capturing 2 groups of 3-4digits, separated by X or x, ignoring whitespace in beginning/end and in between
            if (match.Success)
            {
                return $"{match.Groups[1]}x{match.Groups[2]}";
            }
            return Settings.WindowSize; // i.e. no change or message. Just ignore non-numeric entries
        }

        async void buttonDefaultKeys_Click(object sender, EventArgs e)
        {
            if (MessageBoxResult.Yes == await MessageBox.Show(this, Localization.GetString("Remove all custom key assignments?"), App.ProductName, MessageBoxButtons.YesNo))
            {
                Settings.Input.Reset();
                InitializeKeyboardSettings();
            }
        }

        /// <summary>
        /// Where exported listings go: the desktop, like on Windows, or the home folder if there is no desktop folder.
        /// </summary>
        static string GetExportFolder()
        {
            var desktop = Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory);
            if (!String.IsNullOrEmpty(desktop) && Directory.Exists(desktop))
                return desktop;
            return Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        }

        async void buttonExport_Click(object sender, EventArgs e)
        {
            var outputPath = Path.Combine(GetExportFolder(), "Open Rails Keyboard.txt");
            try
            {
                Settings.Input.DumpToText(outputPath);
                await MessageBox.Show(this, Localization.GetString("A listing of all keyboard commands and keys has been placed here:\n\n") + outputPath, App.ProductName);
            }
            catch (Exception error)
            {
                await MessageBox.Show(this, error.Message, App.ProductName);
            }
        }

        async void buttonCheckKeys_Click(object sender, EventArgs e)
        {
            var errors = CheckKeysForErrors();
            if (errors != "")
                await MessageBox.Show(this, errors, App.ProductName);
            else
                await MessageBox.Show(this, Localization.GetString("No errors found."), App.ProductName);
        }

        private void numericUpDownFOV_ValueChanged(object sender, EventArgs e)
        {
            var fov = numericViewingFOV.Value ?? 0;
            labelFOVHelp.Text = Localization.GetStringFmt("{0:F0}° vertical FOV is the same as:\n{1:F0}° horizontal FOV on 4:3\n{2:F0}° horizontal FOV on 16:9", fov, fov * 4 / 3, fov * 16 / 9);
        }

        private void trackAdhesionFactor_ValueChanged(object sender, EventArgs e)
        {
            SetAdhesionLevelValue();
            AdhesionFactorValueLabel.Text = GetValue(trackAdhesionFactor).ToString() + "%";
            AdhesionFactorChangeValueLabel.Text = GetValue(trackAdhesionFactorChange).ToString() + "%";
        }

        private void SetAdhesionLevelValue()
        {
            int level = GetValue(trackAdhesionFactor) - GetValue(trackAdhesionFactorChange);

            if (level > 159)
                AdhesionLevelValue.Text = Localization.GetString("Very easy");
            else if (level > 139)
                AdhesionLevelValue.Text = Localization.GetString("Easy");
            else if (level > 119)
                AdhesionLevelValue.Text = Localization.GetString("MSTS Compatible");
            else if (level > 89)
                AdhesionLevelValue.Text = Localization.GetString("Normal");
            else if (level > 69)
                AdhesionLevelValue.Text = Localization.GetString("Hard");
            else if (level > 59)
                AdhesionLevelValue.Text = Localization.GetString("Very Hard");
            else
                AdhesionLevelValue.Text = Localization.GetString("Good luck!");
        }

        private void trackDayAmbientLight_ValueChanged(object sender, EventArgs e)
        {
            labelDayAmbientLight.Text = Localization.GetStringFmt("{0}%", GetValue(trackDayAmbientLight) * 5);
            ToolTip.SetTip(trackDayAmbientLight, (GetValue(trackDayAmbientLight) * 5).ToString() + " %");
        }

        private void trackAntiAliasing_ValueChanged(object sender, EventArgs e)
        {
            string method;
            switch ((UserSettings.AntiAliasingMethod)GetValue(trackAntiAliasing))
            {
                case UserSettings.AntiAliasingMethod.None:
                    method = "Disabled";
                    break;
                case UserSettings.AntiAliasingMethod.MSAA2x:
                    method = "2x MSAA";
                    break;
                case UserSettings.AntiAliasingMethod.MSAA4x:
                    method = "4x MSAA";
                    break;
                case UserSettings.AntiAliasingMethod.MSAA8x:
                    method = "8x MSAA";
                    break;
                case UserSettings.AntiAliasingMethod.MSAA16x:
                    method = "16x MSAA";
                    break;
                case UserSettings.AntiAliasingMethod.MSAA32x:
                    method = "32x MSAA";
                    break;
                default:
                    method = "";
                    break;
            }
            labelAntiAliasingValue.Text = method;
        }

        private void trackLODBias_ValueChanged(object sender, EventArgs e)
        {
            var value = GetValue(trackLODBias);
            if (value == -100)
                labelLODBias.Text = Localization.GetStringFmt("No detail (-{0}%)", -value);
            else if (value < 0)
                labelLODBias.Text = Localization.GetStringFmt("Less detail (-{0}%)", -value);
            else if (value == 0)
                labelLODBias.Text = Localization.GetStringFmt("Default detail (+{0}%)", value);
            else if (value < 100)
                labelLODBias.Text = Localization.GetStringFmt("More detail (+{0}%)", value);
            else
                labelLODBias.Text = Localization.GetStringFmt("All detail (+{0}%)", value);
        }

        private void checkAlerter_CheckedChanged(object sender, EventArgs e)
        {
            //Disable checkAlerterExternal when checkAlerter is not checked
            if (checkAlerter.IsChecked == true)
            {
                checkAlerterExternal.IsEnabled = true;
            }
            else
            {
                checkAlerterExternal.IsEnabled = false;
                checkAlerterExternal.IsChecked = false;
            }
        }

        private void checkDistantMountains_Click(object sender, EventArgs e)
        {
            labelDistantMountainsViewingDistance.IsEnabled = checkDistantMountains.IsChecked == true;
            numericDistantMountainsViewingDistance.IsEnabled = checkDistantMountains.IsChecked == true;
        }

        private void checkDataLogTrainSpeed_Click(object sender, EventArgs e)
        {
            checkListDataLogTSContents.IsEnabled = checkDataLogTrainSpeed.IsChecked == true;
            labelDataLogTSInterval.IsEnabled = checkDataLogTrainSpeed.IsChecked == true;
            numericDataLogTSInterval.IsEnabled = checkDataLogTrainSpeed.IsChecked == true;
        }

        private void checkPerformanceTuner_Click(object sender, EventArgs e)
        {
            numericPerformanceTunerTarget.IsEnabled = checkPerformanceTuner.IsChecked == true;
            labelPerformanceTunerTarget.IsEnabled = checkPerformanceTuner.IsChecked == true;
        }

        private void checkAutoSave_checkchanged(object sender, EventArgs e)
        {
            if (checkAutoSaveActive.IsChecked == true)
            {
                ButtonAutoSave15.IsEnabled = true;
                ButtonAutoSave15.IsChecked = Settings.AutoSaveInterval == 15;
                ButtonAutoSave30.IsEnabled = true;
                ButtonAutoSave30.IsChecked = Settings.AutoSaveInterval == 30;
                ButtonAutoSave60.IsEnabled = true;
                ButtonAutoSave60.IsChecked = Settings.AutoSaveInterval == 60;
            }
            else
            {
                ButtonAutoSave15.IsChecked = false;
                ButtonAutoSave15.IsEnabled = false;
                ButtonAutoSave30.IsChecked = false;
                ButtonAutoSave30.IsEnabled = false;
                ButtonAutoSave60.IsChecked = false;
                ButtonAutoSave60.IsEnabled = false;
            }
        }

        private void buttonAutoSaveInterval_checkchanged(object sender, EventArgs e)
        {
            // The radio buttons share a group, so only the interval needs remembering.
            if (ButtonAutoSave15.IsChecked == true)
                Settings.AutoSaveInterval = 15;
            else if (ButtonAutoSave30.IsChecked == true)
                Settings.AutoSaveInterval = 30;
            else if (ButtonAutoSave60.IsChecked == true)
                Settings.AutoSaveInterval = 60;
        }

        #region Help for Options
        // The icons all share the same code which assumes they are named according to a simple scheme as follows:
        //   1. To add a new Help Icon, copy an existing one (an Image with Classes="help") and paste it into the tab.
        //   2. Give it the same name as the associated control but change the prefix to "pb" (for Picture Box, as it was).
        //   3. Add an entry to InitializeHelpIcons() which links the icon to the control and, if there is one, the label.
        //      This link will highlight the icon when the user hovers (mouses over) the control or the label.
        //   4. Add an entry to HelpIconUrls which opens the user's browser with the correct help page.
        //      The URL can be found from visiting the page and hovering over the title of the section.

        /// <summary>
        /// Allows multiple controls to change a single help icon with their hover events.
        /// </summary>
        private class HelpIconHover
        {
            static readonly Bitmap Normal = App.LoadBitmap("info-18.png");
            static readonly Bitmap Hover = App.LoadBitmap("info-18-hover.png");

            private readonly Image Icon;
            private int HoverCount = 0;

            public HelpIconHover(Image pb)
            {
                Icon = pb;
            }

            public void Enter()
            {
                HoverCount++;
                SetImage();
            }

            public void Leave()
            {
                HoverCount = Math.Max(0, HoverCount - 1);
                SetImage();
            }

            private void SetImage()
            {
                Icon.Source = HoverCount > 0 ? Hover : Normal;
            }
        }

        private void InitializeHelpIcons()
        {
            // static mapping of picture boxes to controls
            var helpIconControls = new (Image, Control[])[]
            {
                // General
                (pbAlerter, new[] { checkAlerter }),
                (pbRetainers, new[] { checkRetainers }),
                (pbGraduatedRelease, new[] { checkGraduatedRelease }),
                (pbBrakePipeChargingRate, new Control[] { lBrakePipeChargingRate, numericBrakePipeChargingRate }),
                (pbPressureUnit, new Control[] { labelPressureUnit, comboPressureUnit }),
                (pbOtherUnits, new Control[] { labelOtherUnits, comboOtherUnits }),
                (pbEnableTcsScripts, new[] { checkEnableTCSScripts }),
                (pbAutoSave, new[] { checkAutoSaveActive }),
                (pbOverspeedMonitor, new[] { checkOverspeedMonitor }),
                (pbTelemetry, new[] { buttonTelemetry }),

                // Audio tab
                (pbSoundVolumePercent, new Control[] { labelSoundVolume, numericSoundVolumePercent }),
                (pbSoundDetailLevel, new Control[]  { labelSoundDetailLevel, numericSoundDetailLevel }),
                (pbExternalSoundPassThruPercent, new Control[]  { labelExternalSound, numericExternalSoundPassThruPercent }),

                // Video tab
                (pbViewingDistance, new Control[] { labelViewingDistance, numericViewingDistance }),
                (pbDistantMountains, new Control[] { checkDistantMountains, numericDistantMountainsViewingDistance, labelDistantMountainsViewingDistance }),
                (pbLODViewingExtension, new[] { checkLODViewingExtension }),
                (pbDynamicShadows, new[] { checkDynamicShadows }),
                (pbShadowAllShapes, new[] { checkShadowAllShapes }),
                (pbWire, new[] { checkWire }),
                (pbDoubleWire, new[] { checkDoubleWire }),
                (pbSignalLightGlow, new[] { checkSignalLightGlow }),
                (pbDayAmbientLight, new Control[] { labelAmbientDaylightBrightness, trackDayAmbientLight }),
                (pbModelInstancing, new[] { checkModelInstancing }),
                (pbVerticalSync, new[] { checkVerticalSync }),
                (pbAntiAliasing, new Control[] { labelAntiAliasingValue, trackAntiAliasing }),
                (pbWorldObjectDensity, new Control[] { labelWorldObjectDensity, numericWorldObjectDensity }),
                (pbLODBias, new Control[] { labelLODBias, trackLODBias }),
                (pbViewingFOV, new Control[] { labelViewingVerticalFOV, numericViewingFOV }),

                // System
                (pbLanguage, new Control[] { labelLanguage, comboLanguage }),
                (pbUpdateMode, new Control[] { labelUpdateMode }),
                (pbWindowed, new Control[] { checkWindowed, labelWindowSize, comboWindowSize }),
                (pbWindowGlass, new[] { checkWindowGlass }),
                (pbControlConfirmations, new Control[] { labelControlConfirmations, comboControlConfirmations }),
                (pbWebServerPort, new Control[] { labelWebServerPort, numericWebServerPort }),
                (pbPerformanceTuner, new Control[] { checkPerformanceTuner, labelPerformanceTunerTarget }),

                // Simulation tab
                (pbAdvancedAdhesionModel, new[] { checkUseAdvancedAdhesion }),
                (pbBreakCouplers, new[] { checkBreakCouplers }),
                (pbCurveDependentSpeedLimit, new[] { checkCurveSpeedDependent }),
                (pbAtGameStartSteamPreHeatBoiler, new[] { checkBoilerPreheated }),
                (pbAtGameStartDieselRunEngines, new[] { checkDieselEnginesStarted }),
                (pbAtGameStartElectricPowerConnected, new[] { checkElectricPowerConnected }),
                (pbSimpleControlAndPhysics, new[] { checkSimpleControlsPhysics }),
                (pbForcedRedAtStationStops, new[] { checkForcedRedAtStationStops }),
                (pbOpenCloseDoorsOnAiTrains, new[] { checkDoorsAITrains }),
                (pbLocationLinkedPassingPathProcessing, new[] { checkUseLocationPassingPaths }),

                // Keyboard, RailDriver and Data logger tabs
                (pbKeyboardOptions, new Control[0]),
                (pbRailDriverOptions, new Control[0]),
                (pbDataLoggerOptions, new Control[0]),

                // Experimental tab
                (pbSuperElevation, new Control [] { ElevationText, checkUseSuperElevation, label8}),
                (pbShowShapeWarnings, new[] { checkShapeWarnings }),
                (pbCorrectQuestionableBrakingParameters, new[] { checkCorrectQuestionableBrakingParams }),
                (pbActivityRandomization, new Control [] { label13, label12 }),
                (pbActivityWeatherRandomization, new Control [] { label26, label27 }),
                (pbMstsEnvironments, new[] { checkUseMSTSEnv }),
                (pbAdhesionFactorCorrection, new Control [] { label9,  trackAdhesionFactor}),
                (pbAdhesionFactorRandomChange, new Control [] { label16, trackAdhesionFactorChange}),
            };
            var urls = HelpIconUrls();
            foreach ((Image pb, Control[] controls) in helpIconControls)
            {
                var hover = new HelpIconHover(pb);
                foreach (var control in controls.Append(pb))
                {
                    control.PointerEntered += (sender, e) => hover.Enter();
                    control.PointerExited += (sender, e) => hover.Leave();
                }
                if (urls.TryGetValue(pb.Name, out var url))
                {
                    pb.PointerPressed += (sender, e) => ExternalProcess.Open(BaseDocumentationUrl + url);
                    ToolTip.SetTip(pb, Localization.GetString("Help"));
                }
            }
        }

        /// <summary>
        /// The relevant pages from the manual maintained by James Ross's automatic build, by help icon name.
        /// </summary>
        static Dictionary<string, string> HelpIconUrls()
        {
            return new Dictionary<string, string>
            {
                // General tab
                { "pbAlerter", "/options.html#alerter-in-cab" },
                { "pbRetainers", "/options.html#retainer-valve-on-all-cars" },
                { "pbGraduatedRelease", "/options.html#graduated-release-air-brakes" },
                { "pbBrakePipeChargingRate", "/options.html#brake-pipe-charging-rate" },
                { "pbPressureUnit", "/options.html#pressure-unit" },
                { "pbOtherUnits", "/options.html#other-units" },
                { "pbEnableTcsScripts", "/options.html#disable-tcs-scripts" },
                { "pbAutoSave", "/options.html#auto-save" },
                { "pbOverspeedMonitor", "/options.html#overspeed-monitor" },
                { "pbTelemetry", "/options.html#telemetry" },

                // Audio tab
                { "pbSoundVolumePercent", "/options.html#audio-options" },
                { "pbSoundDetailLevel", "/options.html#audio-options" },
                { "pbExternalSoundPassThruPercent", "/options.html#audio-options" },

                // Video tab
                { "pbViewingDistance", "/options.html#viewing-distance" },
                { "pbDistantMountains", "/options.html#distant-mountains" },
                { "pbLODViewingExtension", "/options.html#extend-object-maximum-viewing-distance-to-horizon" },
                { "pbDynamicShadows", "/options.html#dynamic-shadows" },
                { "pbShadowAllShapes", "/options.html#shadow-for-all-shapes" },
                { "pbWire", "/options.html#overhead-wire" },
                { "pbDoubleWire", "/options.html#double-overhead-wires" },
                { "pbSignalLightGlow", "/options.html#signal-light-glow" },
                { "pbDayAmbientLight", "/options.html#ambient-daylight-brightness" },
                { "pbModelInstancing", "/options.html#model-instancing" },
                { "pbVerticalSync", "/options.html#vertical-sync" },
                { "pbAntiAliasing", "/options.html#anti-aliasing" },
                { "pbWorldObjectDensity", "/options.html#world-object-density" },
                { "pbLODBias", "/options.html#level-of-detail-bias" },
                { "pbViewingFOV", "/options.html#viewing-vertical-fov" },

                // System tab
                { "pbLanguage", "/options.html#language" },
                { "pbUpdateMode", "/options.html#updater-options" },
                { "pbWindowed", "/options.html#windowed" },
                { "pbWindowGlass", "/options.html#window-glass" },
                { "pbControlConfirmations", "/options.html#control-confirmations" },
                { "pbWebServerPort", "/options.html#web-server-port" },
                { "pbPerformanceTuner", "/options.html#performance-tuner" },

                // Simulation tab
                { "pbAdvancedAdhesionModel", "/options.html#advanced-adhesion-model" },
                { "pbBreakCouplers", "/options.html#break-couplers" },
                { "pbCurveDependentSpeedLimit", "/options.html#curve-dependent-speed-limit" },
                { "pbAtGameStartSteamPreHeatBoiler", "/options.html#at-game-start-steam-pre-heat-boiler" },
                { "pbAtGameStartDieselRunEngines", "/options.html#at-game-start-diesel-run-engines" },
                { "pbAtGameStartElectricPowerConnected", "/options.html#at-game-start-electric-power-connected" },
                { "pbSimpleControlAndPhysics", "/options.html#simple-control-and-physics" },
                { "pbForcedRedAtStationStops", "/options.html#forced-red-at-station-stops" },
                { "pbOpenCloseDoorsOnAiTrains", "/options.html#open-close-doors-on-ai-trains" },
                { "pbLocationLinkedPassingPathProcessing", "/options.html#location-linked-passing-path-processing" },

                // Keyboard tab
                { "pbKeyboardOptions", "/options.html#keyboard-options" },

                // Raildriver tab
                { "pbRailDriverOptions", "/options.html#raildriver-options" },

                // Data Logger Options
                { "pbDataLoggerOptions", "/options.html#data-logger-options" },

                // Experimental tab
                { "pbSuperElevation", "/options.html#superelevation" },
                { "pbShowShapeWarnings", "/options.html#show-shape-warnings" },
                { "pbCorrectQuestionableBrakingParameters", "/options.html#correct-questionable-braking-parameters" },
                { "pbActivityRandomization", "/options.html#activity-randomization" },
                { "pbActivityWeatherRandomization", "/options.html#activity-weather-randomization" },
                { "pbMstsEnvironments", "/options.html#msts-environments" },
                { "pbAdhesionFactorCorrection", "/options.html#adhesion-factor-correction" },
                { "pbAdhesionFactorRandomChange", "/options.html#adhesion-factor-random-change" },
            };
        }
        #endregion

        private async void buttonTelemetry_Click(object sender, EventArgs e)
        {
            await new TelemetryWindow(TelemetryManager).ShowDialog(this);
        }
    }
}
