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

using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;

namespace ActivityEditor
{
    /// <summary>
    /// Layout helpers for the simple dialogs which replaced the WinForms designer forms.
    /// </summary>
    static class Forms
    {
        public static StackPanel Layout(params Control[] rows)
        {
            var panel = new StackPanel { Margin = new Thickness(12), Spacing = 6 };
            panel.Children.AddRange(rows);
            return panel;
        }

        public static Grid Row(string label, Control control, double labelWidth = 150)
        {
            var grid = new Grid { ColumnDefinitions = new ColumnDefinitions(labelWidth + ",*") };
            var text = new TextBlock { Text = label, VerticalAlignment = VerticalAlignment.Center };
            Grid.SetColumn(control, 1);
            grid.Children.Add(text);
            grid.Children.Add(control);
            return grid;
        }

        public static StackPanel Buttons(params Button[] buttons)
        {
            var panel = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Right, Spacing = 8, Margin = new Thickness(0, 8, 0, 0) };
            foreach (var button in buttons)
            {
                button.HorizontalContentAlignment = HorizontalAlignment.Center;
                panel.Children.Add(button);
            }
            return panel;
        }

        public static TextBlock Title(string text)
        {
            return new TextBlock { Text = text, FontSize = 20, HorizontalAlignment = HorizontalAlignment.Center, Margin = new Thickness(0, 0, 0, 8) };
        }

        public static void SetupDialog(Window window)
        {
            window.SizeToContent = SizeToContent.WidthAndHeight;
            window.CanResize = false;
            window.ShowInTaskbar = false;
            window.WindowStartupLocation = WindowStartupLocation.CenterOwner;
        }
    }
}
