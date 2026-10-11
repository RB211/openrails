// COPYRIGHT 2009 - 2024 by the Open Rails project.
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
using Avalonia.Controls;
using ORTS.Settings;

namespace Menu
{
    public partial class TelemetryWindow : Window
    {
        readonly TelemetryManager Manager;

        public TelemetryWindow(TelemetryManager manager)
        {
            InitializeComponent();
            Icon = App.LoadIcon();

            var catalog = Localization.Catalog;
            comboBoxSystem.Items.Add(catalog.GetParticularString("Telemetry system state combobox", "Undecided (off)"));
            comboBoxSystem.Items.Add(catalog.GetParticularString("Telemetry system state combobox", "Off"));
            comboBoxSystem.Items.Add(catalog.GetParticularString("Telemetry system state combobox", "On"));
            label2.Text = catalog.GetString("We receive lots of feedback via our forums and website, but that is still a minority of Open Rails users - about 25,000 of you. We would like to expand feedback to a wider range of our users so that we can have more confidence making changes and minimise the chance of you having a bad experience. Anonymous data collection is how we're approaching this for technical feedback.");
            label5.Text = catalog.GetString("If enabled, anonymous data will be collected and uploaded to our telemetry server software (Open Source just like Open Rails). Although your IP address will unavoidably be captured during upload, the telemetry server software takes special steps to prevent your data being associated with your IP address afterwards. The telemetry server software will then analyse the data and make public reports from it. These reports are carefully designed to avoid singling out any user or small group of users.");

            Manager = manager;
            comboBoxSystem.SelectedIndex = (int)Manager.GetState(TelemetryType.System);
            comboBoxSystem.SelectionChanged += comboBoxSystem_SelectedIndexChanged;

            linkLabelPreviewSystem.Click += linkLabelPreviewSystem_LinkClicked;
            linkLabelServer.Click += linkLabelServer_LinkClicked;
            buttonClose.Click += (sender, e) => Close();
        }

        private void comboBoxSystem_SelectedIndexChanged(object sender, EventArgs e)
        {
            Manager.SetState(TelemetryType.System, (TelemetryState)comboBoxSystem.SelectedIndex);
        }

        private async void linkLabelPreviewSystem_LinkClicked(object sender, EventArgs e)
        {
            // Show what would be collected, by running the data collector in a terminal.
            var collector = ExternalProcess.ExecutablePath("Contrib.DataCollector");
            try
            {
                if (!ExternalProcess.StartInTerminal(collector, new[] { "/system" }))
                    await MessageBox.Show(this, Localization.GetStringFmt("No terminal emulator was found to run {0}. Please run it from a terminal.", collector + " /system"), App.ProductName);
            }
            catch (Exception error)
            {
                await MessageBox.Show(this, error.Message, App.ProductName);
            }
        }

        private void linkLabelServer_LinkClicked(object sender, EventArgs e)
        {
            ExternalProcess.Open(Manager.Settings.ServerURL);
        }
    }
}
