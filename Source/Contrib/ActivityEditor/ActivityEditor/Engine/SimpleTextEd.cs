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
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Media;

namespace ActivityEditor.Engine
{
    /// <summary>Simple multi-line text editor used for the activity description.</summary>
    public partial class SimpleTextEd : Window
    {
        public TextBox aEditer;

        public SimpleTextEd()
        {
            Title = "Activity Description";
            Width = 500;
            Height = 200;
            WindowStartupLocation = WindowStartupLocation.CenterOwner;
            aEditer = new TextBox { AcceptsReturn = true, TextWrapping = TextWrapping.Wrap };
            var save = new MenuItem { Header = "Save", InputGesture = new KeyGesture(Key.S, KeyModifiers.Control), HotKey = new KeyGesture(Key.S, KeyModifiers.Control) };
            save.Click += Save_Click;
            var fileMenu = new MenuItem { Header = "File" };
            fileMenu.Items.Add(save);
            var bar = new Menu();
            bar.Items.Add(fileMenu);
            DockPanel.SetDock(bar, Dock.Top);
            Content = new DockPanel { Children = { bar, aEditer } };
        }

        protected void Save_Click(Object sender, RoutedEventArgs e)
        {
            Close();
        }
    }
}
