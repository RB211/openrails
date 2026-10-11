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
    public partial class ControlStartProperties : Window
    {
        public AuxControlStart Action { get; protected set; }
        TextBox textBox1;
        TextBox textBox2;

        public ControlStartProperties(AuxActionRef action)
        {
            Action = (AuxControlStart)action;
            InitializeComponent();
            textBox1.Text = Action.ActivationDelay.ToString();
            textBox2.Text = Action.ActionDuration.ToString();
            textBox1.TextChanged += textBox1_TextChanged;
            textBox2.TextChanged += textBox2_TextChanged;
        }

        void InitializeComponent()
        {
            Title = "ControlStartProperties";
            textBox1 = new TextBox { Width = 80 };
            ToolTip.SetTip(textBox1, "Delay before the action starts (1 - 20 s, less than the duration)");
            textBox2 = new TextBox { Width = 80 };
            ToolTip.SetTip(textBox2, "Duration of the action (10 - 25 s)");
            var ok = new Button { Content = "OK", Width = 75 };
            ok.Click += HornOK_Click;
            Content = Forms.Layout(
                Forms.Row("Pre-delay :", textBox1, 120),
                Forms.Row("ActionDuration", textBox2, 120),
                Forms.Buttons(ok));
            Forms.SetupDialog(this);
        }

        private void HornOK_Click(object sender, RoutedEventArgs e)
        {
            Close();
        }

        private void textBox1_TextChanged(object sender, EventArgs e)
        {
            if (int.TryParse(textBox1.Text, out int delay) && delay >= 1 && delay <= 20 && delay < Action.ActionDuration)
            {
                Action.ActivationDelay = delay;
            }
        }

        private void textBox2_TextChanged(object sender, EventArgs e)
        {
            if (int.TryParse(textBox2.Text, out int duration) && duration >= 10 && duration <= 25)
                Action.ActionDuration = duration;
        }
    }
}
