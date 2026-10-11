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
using Orts.Formats.OR;

namespace ActivityEditor.ActionProperties
{
    public partial class HornProperties : Window
    {
        public AuxActionHorn Action { get; protected set; }
        TextBox textBox1;
        TextBox textBox2;

        public HornProperties(AuxActionRef action)
        {
            Action = (AuxActionHorn)action;
            InitializeComponent();
            textBox1.Text = Action.Delay.ToString();
            textBox2.Text = Action.RequiredDistance.ToString();
            textBox1.TextChanged += textBox1_TextChanged;
            textBox2.TextChanged += textBox2_TextChanged;
        }

        void InitializeComponent()
        {
            Title = "HornProperties";
            textBox1 = new TextBox { Width = 80 };
            textBox2 = new TextBox { Width = 80 };
            var ok = new Button { Content = "OK", Width = 75 };
            ok.Click += HornOK_Click;
            Content = Forms.Layout(
                Forms.Row("Delay :", textBox1, 170),
                Forms.Row("Required Trigger Distance :", textBox2, 170),
                Forms.Buttons(ok));
            Forms.SetupDialog(this);
        }

        private void HornOK_Click(object sender, RoutedEventArgs e)
        {
            Close();
        }

        private void textBox1_TextChanged(object sender, EventArgs e)
        {
            // int.TryParse: Convert.ToInt32 threw on partial input such as an empty box.
            if (int.TryParse(textBox1.Text, out int delay) && delay > 1 && delay < 5)
                Action.Delay = delay;
        }

        private void textBox2_TextChanged(object sender, EventArgs e)
        {
            if (int.TryParse(textBox2.Text, out int distance) && distance > 10 && distance < 500)
                Action.RequiredDistance = distance;
        }
    }
}
