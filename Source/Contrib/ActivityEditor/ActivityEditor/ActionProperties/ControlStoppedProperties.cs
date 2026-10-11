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
using Avalonia.Interactivity;

namespace ActivityEditor.ActionProperties
{
    public partial class ControlStoppedProperties : Window
    {
        public ControlStoppedProperties()
        {
            Title = "ControlStoppedProperties";
            var ok = new Button { Content = "OK", Width = 75 };
            ok.Click += OK_Click;
            Content = Forms.Layout(new TextBlock { Text = "No Parameter to set" }, Forms.Buttons(ok));
            Forms.SetupDialog(this);
        }

        private void OK_Click(object sender, RoutedEventArgs e)
        {
            Close();
        }
    }
}
