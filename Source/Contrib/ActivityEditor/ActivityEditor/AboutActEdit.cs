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
using System.Reflection;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;

namespace ActivityEditor
{
    partial class AboutActEdit : Window
    {
        public AboutActEdit()
        {
            this.Title = String.Format("About {0}", AssemblyTitle);
            Width = 503;
            Height = 283;
            CanResize = false;
            ShowInTaskbar = false;
            WindowStartupLocation = WindowStartupLocation.CenterOwner;

            var ok = new Button { Content = "OK", Width = 75, IsDefault = true, IsCancel = true, HorizontalAlignment = HorizontalAlignment.Right, HorizontalContentAlignment = HorizontalAlignment.Center };
            ok.Click += (s, e) => Close();
            var text = new StackPanel
            {
                Spacing = 8,
                Margin = new Thickness(9, 0, 0, 0),
                Children =
                {
                    new TextBlock { Text = "Activity Editor" },
                    new TextBlock { Text = "Version 0.1" },
                    new TextBlock { Text = "Copyright by Open Rails project" },
                    new TextBlock { Text = "OR" },
                    new TextBox { Text = "Tool intended for the creation of activities for the Open simulator Rail.", IsReadOnly = true, TextWrapping = TextWrapping.Wrap, AcceptsReturn = true, Height = 80 },
                    ok,
                },
            };
            var logo = new Image { Source = AppResources.Load("AboutActEdit.jpg"), Stretch = Stretch.Uniform, Width = 180, VerticalAlignment = VerticalAlignment.Top };
            Grid.SetColumn(text, 1);
            Content = new Grid
            {
                Margin = new Thickness(9),
                ColumnDefinitions = new ColumnDefinitions("Auto,*"),
                Children = { logo, text },
            };
        }

        public string AssemblyTitle
        {
            get
            {
                var attribute = Assembly.GetExecutingAssembly().GetCustomAttribute<AssemblyTitleAttribute>();
                if (attribute != null && attribute.Title != "")
                    return attribute.Title;
                return Assembly.GetExecutingAssembly().GetName().Name;
            }
        }
    }
}
