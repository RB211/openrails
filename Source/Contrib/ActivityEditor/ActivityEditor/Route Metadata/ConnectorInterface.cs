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
using Avalonia.Controls;
using Avalonia.Interactivity;
using LibAE;
using LibAE.Formats;
using Orts.Formats.OR;

namespace ActivityEditor.Engine
{
    public partial class StationInterface : Window
    {
        public List<string> AllowedDirections;
        public bool ToRemove = false;
        StationConnector stationConnector;
        ComboBox AllowCB;
        TextBox ConnectionLabel;
        TextBox TCSectionBox;

        public StationInterface(StationConnector info)
        {
            stationConnector = info;
            List<string> availValues = stationConnector.getAllowedDirections();
            InitializeComponent();
            AllowCB.ItemsSource = availValues;
            AllowCB.SelectedItem = availValues[(int)stationConnector.getDirConnector()];
            this.ConnectionLabel.Text = stationConnector.getLabel();
            this.TCSectionBox.Text = "Not yet implemented";
        }

        void InitializeComponent()
        {
            Title = "Station Route Interface";
            AllowCB = new ComboBox { Width = 140 };
            ConnectionLabel = new TextBox { Width = 205 };
            TCSectionBox = new TextBox { Width = 205 };
            var ok = new Button { Content = "OK", Width = 75 };
            ok.Click += OKButton_Click;
            var remove = new Button { Content = "Remove", Width = 75 };
            remove.Click += RemButton_Click;
            Content = Forms.Layout(
                Forms.Row("Connection Label : ", ConnectionLabel),
                Forms.Row("Connection Direction : ", AllowCB),
                Forms.Row("TC Section : ", TCSectionBox),
                Forms.Buttons(remove, ok));
            Forms.SetupDialog(this);
        }

        private void OKButton_Click(object sender, RoutedEventArgs e)
        {
            {
                if (this.ConnectionLabel.Text == "" || this.ConnectionLabel.Text == "Please, give a name")
                {
                    this.ConnectionLabel.Text = "Please, give a name";
                }
                else
                {
                    stationConnector.setLabel(this.ConnectionLabel.Text);
                    stationConnector.setDirConnector(this.AllowCB.SelectedItem as string ?? "");
                    Close();
                }
            }
        }

        private void RemButton_Click(object sender, RoutedEventArgs e)
        {
            ToRemove = true;
            Close();
        }
    }
}
