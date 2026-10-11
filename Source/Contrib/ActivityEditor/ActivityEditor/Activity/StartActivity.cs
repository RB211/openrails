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
using ActivityEditor.Engine;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Layout;

namespace ActivityEditor.Activity
{
    public partial class StartActivity : Window
    {
        AEConfig config;
        public TextBox ActName;
        public TextBox ActDescr;
        public TextBox ActConsist;

        public StartActivity(AEConfig aeConfig)
        {
            InitializeComponent();
            config = aeConfig;
            ActName.Text = aeConfig.getActivityName();
            ActDescr.Text = aeConfig.getActivityDescr();
        }

        void InitializeComponent()
        {
            Title = "StartActivity";
            Width = 698;
            Height = 375;
            WindowStartupLocation = WindowStartupLocation.CenterOwner;

            ActName = new TextBox { Width = 300 };
            ActDescr = new TextBox { AcceptsReturn = true, TextWrapping = Avalonia.Media.TextWrapping.Wrap, Height = 90 };
            ActConsist = new TextBox { Width = 300 };
            var descriptionGroup = new GroupBox
            {
                Text = "Description",
                Content = new StackPanel
                {
                    Spacing = 4,
                    Children =
                    {
                        Forms.Row("Activity Name : ", ActName, 110),
                        new TextBlock { Text = "Description :" },
                        ActDescr,
                        Forms.Row("Consist :", ActConsist, 110),
                    },
                },
            };
            // "Path Events" (Edit/Remove) was a placeholder without any functionality in the WinForms version.
            var pathEventsGroup = new GroupBox
            {
                Text = "Path Events",
                Content = new StackPanel
                {
                    Orientation = Orientation.Horizontal,
                    Spacing = 4,
                    Children = { new Button { Content = "Edit", IsEnabled = false }, new Button { Content = "Remove", IsEnabled = false } },
                },
            };
            var ok = new Button { Content = "OK", Width = 75 };
            ok.Click += SA_OK_Click;

            var tabs = new TabControl();
            tabs.Items.Add(new TabItem
            {
                Header = "Activity Description",
                Content = new StackPanel
                {
                    Margin = new Thickness(8),
                    Spacing = 6,
                    Children = { new TextBlock { Text = "Activity Description", FontSize = 16 }, descriptionGroup, pathEventsGroup, Forms.Buttons(ok) },
                },
            });
            tabs.Items.Add(new TabItem
            {
                Header = "Traffic Description",
                Content = new TextBlock { Text = "Traffic Description", FontSize = 16, Margin = new Thickness(8) },
            });
            Content = tabs;
        }

        private void SA_OK_Click(object sender, RoutedEventArgs e)
        {
            if (string.IsNullOrEmpty(ActName.Text))
                return;
            config.setActivityName(ActName.Text);
            config.setActivityDescr(ActDescr.Text);
            Close();
        }
    }
}
