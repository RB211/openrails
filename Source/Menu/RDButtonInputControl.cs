// COPYRIGHT 2012, 2014 by the Open Rails project.
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
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Layout;
using Avalonia.Media;
using ORTS.Common.Input;

namespace Menu
{
    /// <summary>
    /// A control for viewing and altering RailDriver button assignments.
    /// </summary>
    /// <remarks>
    /// <para>The control displays the currently assigned button and highlights the text if it is not the default.
    /// Clicking on the text starts reading the RailDriver: pressing one of its buttons assigns it, Backspace removes the assignment.</para>
    /// </remarks>
    public class RDButtonInputControl : UserControl
    {
        public byte UserButton { get; private set; }
        public byte DefaultButton { get; private set; }
        public UserCommand Command { get; }

        private static RailDriverDevice railDriver;
        private static byte[] readBuffer;
        private static readonly byte[] buttonData = new byte[8];

        private static RDButtonInputControl editing;

        readonly TextBox textBox;
        readonly StackPanel editButtons;

        static readonly IBrush ChangedBackground = new SolidColorBrush(Color.FromRgb(0, 120, 215));

        public RDButtonInputControl(UserCommand command, byte userButton, byte defaultButton, RailDriverDevice device)
        {
            Command = command;
            if (railDriver == null && device != null && device.Enabled)
                railDriver = device;
            if (null == readBuffer && railDriver != null)
                readBuffer = railDriver.GetReadBuffer();
            UserButton = userButton;
            DefaultButton = defaultButton;

            textBox = new TextBox { IsReadOnly = true, MinHeight = 24, Padding = new Thickness(4, 2), VerticalContentAlignment = VerticalAlignment.Center };
            ToolTip.SetTip(textBox, Localization.GetString("Press any RailDriver button to Assign. Press Backspace to remove an assignment."));
            textBox.GotFocus += TextBox_Enter;
            textBox.AddHandler(KeyDownEvent, TextBox_KeyPress, RoutingStrategies.Tunnel);

            editButtons = new StackPanel { Orientation = Orientation.Horizontal, IsVisible = false, Margin = new Thickness(2, 0, 0, 0) };
            editButtons.Children.Add(CreateEditButton("✔", "Accept changes", ButtonOK_Click));
            editButtons.Children.Add(CreateEditButton("✘", "Cancel changes", ButtonCancel_Click));
            editButtons.Children.Add(CreateEditButton("↺", "Reset to default", ButtonDefault_Click));

            var panel = new DockPanel();
            DockPanel.SetDock(editButtons, Dock.Right);
            panel.Children.Add(editButtons);
            panel.Children.Add(textBox);
            Content = panel;

            UpdateText();
        }

        Button CreateEditButton(string text, string toolTip, EventHandler<RoutedEventArgs> click)
        {
            var button = new Button { Content = text, Width = 26, MinHeight = 24, Padding = new Thickness(0), HorizontalContentAlignment = HorizontalAlignment.Center, Focusable = false };
            ToolTip.SetTip(button, Localization.GetString(toolTip));
            button.Click += click;
            return button;
        }

        private void UpdateText()
        {
            textBox.Text = UserButton == byte.MaxValue ? string.Empty : UserButton.ToString();
            if (UserButton == DefaultButton)
            {
                textBox.ClearValue(BackgroundProperty);
                textBox.ClearValue(ForegroundProperty);
            }
            else
            {
                textBox.Background = ChangedBackground;
                textBox.Foreground = Brushes.White;
            }
        }

        private async void TextBox_Enter(object sender, EventArgs e)
        {
            if (railDriver == null || editing == this)
                return;
            editing?.ButtonClick();
            editing = this;
            editButtons.IsVisible = true;
            while (editing == this)
            {
                railDriver.ReadCurrentData(ref readBuffer);
                sbyte data = ValidateButtonIndex();
                if (data > -1)
                {
                    textBox.Text = ((byte)data).ToString();
                    railDriver.SetLedsNumeric((byte)data);
                }
                await System.Threading.Tasks.Task.Delay(100);
            }
        }

        private static sbyte ValidateButtonIndex()
        {
            if (readBuffer.Length > 0)
            {
                Buffer.BlockCopy(readBuffer, 8, buttonData, 0, 6);
                ulong buttons = BitConverter.ToUInt64(buttonData, 0);
                //check that only 1 button is pressed
                if (buttons > 0 && ((buttons & (buttons - 1)) == 0))
                {
                    // loop which bit is set
                    for (sbyte i = 0; i < 48; i++)
                    {
                        buttons = buttons >> 1;
                        if (buttons == 0)
                            return i;
                    }
                }
            }
            return -1;
        }

        private void ButtonOK_Click(object sender, RoutedEventArgs e)
        {
            if (byte.TryParse(textBox.Text, out byte result))
                UserButton = result;
            else
                UserButton = 255;
            ButtonClick();
        }

        private void ButtonCancel_Click(object sender, RoutedEventArgs e)
        {
            ButtonClick();
        }

        private void ButtonDefault_Click(object sender, RoutedEventArgs e)
        {
            UserButton = DefaultButton;
            ButtonClick();
        }

        private void ButtonClick()
        {
            if (editing == this)
                editing = null;
            editButtons.IsVisible = false;
            railDriver?.ClearDisplay();
            UpdateText();
        }

        private void TextBox_KeyPress(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Back && editing == this)
            {
                textBox.Text = string.Empty;
                e.Handled = true;
            }
        }
    }
}
