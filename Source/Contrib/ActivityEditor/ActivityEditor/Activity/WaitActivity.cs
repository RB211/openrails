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
using Avalonia;
using Avalonia.Controls;
using Avalonia.Interactivity;

namespace ActivityEditor.Activity
{
    public partial class WaitActivity : Window
    {
        public TextBox SW_PlaceValue;
        public TextBox SW_GDescr;

        public WaitActivity()
        {
            Title = "WaitActivity";
            Width = 687;
            WindowStartupLocation = WindowStartupLocation.CenterOwner;
            SizeToContent = SizeToContent.Height;

            SW_PlaceValue = new TextBox { Width = 300 };
            SW_GDescr = new TextBox { AcceptsReturn = true, TextWrapping = Avalonia.Media.TextWrapping.Wrap, Height = 100 };
            var ok = new Button { Content = "OK", Width = 75 };
            ok.Click += SW_OK_Click;
            Content = Forms.Layout(
                new TextBlock { Text = "WAIT Activity Description", FontSize = 16 },
                new GroupBox
                {
                    Text = "Description",
                    Content = new StackPanel
                    {
                        Spacing = 4,
                        Children = { Forms.Row("Place Name : ", SW_PlaceValue, 110), new TextBlock { Text = "Give your description :" }, SW_GDescr },
                    },
                },
                new GroupBox { Text = "Environment", Content = new Panel { Height = 40 } },
                Forms.Buttons(ok));
        }

        private void SW_OK_Click(object sender, RoutedEventArgs e)
        {
            Close();
        }
    }
}
