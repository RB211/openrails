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
using Orts.Formats.OR;

namespace ActivityEditor.Engine
{
    public partial class BufferInterface : Window
    {
        public List<string> AllowedDirections;
        public bool ToRemove = false;
        public AEBufferItem buffer;
        TextBox bufferLab;
        ComboBox BufDirCB;
        TextBox TCSectionBox;

        public BufferInterface(AEBufferItem givenBuffer)
        {
            buffer = givenBuffer;
            List<string> availValues = givenBuffer.getAllowedDirections();
            InitializeComponent();
            BufDirCB.ItemsSource = availValues;
            BufDirCB.SelectedItem = availValues[(int)givenBuffer.getDirBuffer()];
            bufferLab.Text = givenBuffer.NameBuffer;
        }

        void InitializeComponent()
        {
            Title = "BufferInterface";
            bufferLab = new TextBox { Width = 205 };
            BufDirCB = new ComboBox { Width = 140 };
            TCSectionBox = new TextBox { Width = 205 };
            var ok = new Button { Content = "OK", Width = 75 };
            ok.Click += OKButton_Click;
            var remove = new Button { Content = "Remove", Width = 75 };
            remove.Click += RemButton_Click;
            Content = Forms.Layout(
                Forms.Row("Buffer Label : ", bufferLab),
                Forms.Row("Buffer Direction : ", BufDirCB),
                Forms.Row("TC Section : ", TCSectionBox),
                Forms.Buttons(remove, ok));
            Forms.SetupDialog(this);
        }

        private void OKButton_Click(object sender, RoutedEventArgs e)
        {
            if (bufferLab.Text == "" || bufferLab.Text == "Please, give a name")
            {
                this.bufferLab.Text = "Please, give a name";
            }
            else
            {
                buffer.NameBuffer = bufferLab.Text;
                buffer.setDirBuffer(BufDirCB.SelectedItem as string ?? "");
                Close();
            }
        }

        private void RemButton_Click(object sender, RoutedEventArgs e)
        {
            ToRemove = true;
            Close();
        }
    }
}
