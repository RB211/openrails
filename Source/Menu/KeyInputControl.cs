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
using System.Collections.Generic;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Layout;
using Avalonia.Media;
using ORTS.Settings;
using Xna = Microsoft.Xna.Framework.Input;
using KeyModifiers = Avalonia.Input.KeyModifiers;

namespace Menu
{
    /// <summary>
    /// A control for viewing and altering keyboard input settings (it replaces the WinForms KeyInputControl and KeyInputEditControl).
    /// </summary>
    /// <remarks>
    /// <para>This control will modify the <see cref="UserCommandInput"/> it is given (but not the default input).</para>
    /// <para>The control displays the currently assigned keyboard shortcut and highlights the text if it is not the default.
    /// Clicking on the text starts editing: the next key pressed (with any modifiers) is captured, translated into the
    /// appropriate form for the setting being modified (i.e. modifier only, or key + modifiers) and shown. The buttons
    /// accept (✔), cancel (✘) or reset to the default (↺). Only scan codes are used, never virtual keys.</para>
    /// <para>Unlike the Windows-only low-level keyboard hook used before, keys which the window manager or desktop
    /// keep for themselves (e.g. Alt+Tab) cannot be captured.</para>
    /// </remarks>
    public class KeyInputControl : UserControl
    {
        public UserCommandInput UserInput { get; private set; }
        public UserCommandInput DefaultInput { get; private set; }

        static KeyInputControl CurrentlyEditing;

        readonly Button TextButton;
        readonly TextBlock Text;
        readonly StackPanel EditButtons;
        readonly bool IsModifier;

        string OriginalPersistentDescriptor;

        int ScanCode;
        Xna.Keys VirtualKey;
        bool Shift;
        bool Control;
        bool Alt;
        bool CurrentShift, CurrentControl, CurrentAlt;

        static readonly IBrush ChangedBackground = new SolidColorBrush(Color.FromRgb(0, 120, 215));
        static readonly IBrush EditingBorder = new SolidColorBrush(Color.FromRgb(0, 120, 215));

        public KeyInputControl(UserCommandInput userInput, UserCommandInput defaultInput)
        {
            UserInput = userInput;
            DefaultInput = defaultInput;
            IsModifier = UserInput.IsModifier;

            Text = new TextBlock { VerticalAlignment = VerticalAlignment.Center, TextTrimming = TextTrimming.CharacterEllipsis };
            TextButton = new Button
            {
                Content = Text,
                HorizontalAlignment = HorizontalAlignment.Stretch,
                HorizontalContentAlignment = HorizontalAlignment.Left,
                Padding = new Thickness(6, 2),
                MinHeight = 24,
                BorderThickness = new Thickness(1),
            };
            ToolTip.SetTip(TextButton, Localization.GetString("Click to change this key"));
            TextButton.Click += (sender, e) =>
            {
                if (CurrentlyEditing != this)
                    BeginEdit();
            };

            EditButtons = new StackPanel { Orientation = Orientation.Horizontal, IsVisible = false, Margin = new Thickness(2, 0, 0, 0) };
            EditButtons.Children.Add(CreateEditButton("✔", "Accept changes", (sender, e) => EndEdit(accept: true)));
            EditButtons.Children.Add(CreateEditButton("✘", "Cancel changes", (sender, e) => EndEdit(accept: false)));
            EditButtons.Children.Add(CreateEditButton("↺", "Reset to default", (sender, e) =>
            {
                UserInput.PersistentDescriptor = DefaultInput.PersistentDescriptor;
                EndEdit(accept: true);
            }));

            var panel = new DockPanel();
            DockPanel.SetDock(EditButtons, Dock.Right);
            panel.Children.Add(EditButtons);
            panel.Children.Add(TextButton);
            Content = panel;

            // Capture all keys while editing, before anything else (e.g. Tab navigation or the button's own Space/Enter) sees them.
            TextButton.AddHandler(KeyDownEvent, OnEditKeyDown, RoutingStrategies.Tunnel);
            TextButton.AddHandler(KeyUpEvent, OnEditKeyUp, RoutingStrategies.Tunnel);
            LostFocus += (sender, e) =>
            {
                // Leaving the control (e.g. clicking elsewhere) keeps what was entered.
                if (CurrentlyEditing == this && !IsKeyboardFocusWithin)
                    EndEdit(accept: true);
            };

            UpdateText();
        }

        Button CreateEditButton(string text, string toolTip, EventHandler<RoutedEventArgs> click)
        {
            var button = new Button { Content = text, Width = 26, MinHeight = 24, Padding = new Thickness(0), HorizontalContentAlignment = HorizontalAlignment.Center, Focusable = false };
            ToolTip.SetTip(button, Localization.GetString(toolTip));
            button.Click += click;
            return button;
        }

        void UpdateText()
        {
            string text;
            try
            {
                text = UserInput.ToString();
            }
            catch (Exception)
            {
                // Key names come from the platform; never let a missing name break the options.
                text = UserInput.PersistentDescriptor;
            }
            Text.Text = text;

            string defaultText;
            try
            {
                defaultText = DefaultInput.ToString();
            }
            catch (Exception)
            {
                defaultText = DefaultInput.PersistentDescriptor;
            }

            if (text == defaultText)
            {
                TextButton.ClearValue(BackgroundProperty);
                TextButton.ClearValue(ForegroundProperty);
            }
            else
            {
                TextButton.Background = ChangedBackground;
                TextButton.Foreground = Brushes.White;
            }
        }

        void BeginEdit()
        {
            // Only one key may be edited at a time.
            CurrentlyEditing?.EndEdit(accept: true);
            CurrentlyEditing = this;

            OriginalPersistentDescriptor = UserInput.PersistentDescriptor;
            var parts = OriginalPersistentDescriptor.Split(',');
            if (parts.Length >= 5)
            {
                ScanCode = int.Parse(parts[0]);
                VirtualKey = (Xna.Keys)int.Parse(parts[1]);
                Shift = parts[2] != "0";
                Control = parts[3] != "0";
                Alt = parts[4] != "0";
            }
            CurrentShift = CurrentControl = CurrentAlt = false;

            EditButtons.IsVisible = true;
            TextButton.BorderBrush = EditingBorder;
            TextButton.BorderThickness = new Thickness(2);
            ToolTip.SetTip(TextButton, Localization.GetString("Press any key"));
            TextButton.Focus();
        }

        void EndEdit(bool accept)
        {
            if (CurrentlyEditing != this)
                return;
            CurrentlyEditing = null;

            // Undo user's editing (Cancel); reset to default has already been applied.
            if (!accept)
                UserInput.PersistentDescriptor = OriginalPersistentDescriptor;

            // Ensure the modifiable inputs are kept in sync.
            if (UserInput is UserCommandModifiableKeyInput modifiableKeyInput)
                modifiableKeyInput.SynchronizeCombine();

            EditButtons.IsVisible = false;
            TextButton.ClearValue(BorderBrushProperty);
            TextButton.BorderThickness = new Thickness(1);
            ToolTip.SetTip(TextButton, Localization.GetString("Click to change this key"));
            UpdateText();
        }

        string PersistentDescriptor => String.Format("{0},{1},{2},{3},{4}", ScanCode, (int)VirtualKey, Shift ? 1 : 0, Control ? 1 : 0, Alt ? 1 : 0);

        void OnEditKeyDown(object sender, KeyEventArgs e)
        {
            if (CurrentlyEditing != this)
                return;
            e.Handled = true;

            var scanCode = GetScanCode(e.PhysicalKey);

            // True if the key is a modifier (shift, control, alt).
            var isModifier = false;
            switch (scanCode)
            {
                case 0x2A:
                case 0x36: CurrentShift = true; isModifier = true; break;
                case 0x1D: CurrentControl = true; isModifier = true; break;
                case 0x38: CurrentAlt = true; isModifier = true; break;
                default: break;
            }
            // Modifiers pressed before editing started are only known from the event.
            CurrentShift |= (e.KeyModifiers & KeyModifiers.Shift) != 0;
            CurrentControl |= (e.KeyModifiers & KeyModifiers.Control) != 0;
            CurrentAlt |= (e.KeyModifiers & KeyModifiers.Alt) != 0;

            if (scanCode == 0)
                return; // A key we can't represent.

            if (!(IsModifier ^ isModifier))
            {
                ScanCode = IsModifier ? 0 : scanCode;
                VirtualKey = Xna.Keys.None;
                Shift = CurrentShift;
                Control = CurrentControl;
                Alt = CurrentAlt;
                UserInput.PersistentDescriptor = PersistentDescriptor;
                UpdateText();
            }
        }

        void OnEditKeyUp(object sender, KeyEventArgs e)
        {
            if (CurrentlyEditing != this)
                return;
            e.Handled = true;

            switch (GetScanCode(e.PhysicalKey))
            {
                case 0x2A:
                case 0x36: CurrentShift = false; break;
                case 0x1D: CurrentControl = false; break;
                case 0x38: CurrentAlt = false; break;
                default: break;
            }
        }

        /// <summary>
        /// Translates a physical key into the IBM PC (set 1) scan code used by the Open Rails key settings.
        /// Like the Windows low-level keyboard hook, extended keys (e.g. the cursor keys) use the code of their
        /// non-extended twin, except for Print Screen (0x137) and Pause (0x11D) which are special.
        /// </summary>
        public static int GetScanCode(PhysicalKey key)
        {
            return ScanCodes.TryGetValue(key, out var scanCode) ? scanCode : 0;
        }

        static readonly Dictionary<PhysicalKey, int> ScanCodes = new Dictionary<PhysicalKey, int>
        {
            { PhysicalKey.Escape, 0x01 },
            { PhysicalKey.Digit1, 0x02 }, { PhysicalKey.Digit2, 0x03 }, { PhysicalKey.Digit3, 0x04 }, { PhysicalKey.Digit4, 0x05 },
            { PhysicalKey.Digit5, 0x06 }, { PhysicalKey.Digit6, 0x07 }, { PhysicalKey.Digit7, 0x08 }, { PhysicalKey.Digit8, 0x09 },
            { PhysicalKey.Digit9, 0x0A }, { PhysicalKey.Digit0, 0x0B },
            { PhysicalKey.Minus, 0x0C }, { PhysicalKey.Equal, 0x0D }, { PhysicalKey.Backspace, 0x0E }, { PhysicalKey.Tab, 0x0F },
            { PhysicalKey.Q, 0x10 }, { PhysicalKey.W, 0x11 }, { PhysicalKey.E, 0x12 }, { PhysicalKey.R, 0x13 }, { PhysicalKey.T, 0x14 },
            { PhysicalKey.Y, 0x15 }, { PhysicalKey.U, 0x16 }, { PhysicalKey.I, 0x17 }, { PhysicalKey.O, 0x18 }, { PhysicalKey.P, 0x19 },
            { PhysicalKey.BracketLeft, 0x1A }, { PhysicalKey.BracketRight, 0x1B }, { PhysicalKey.Enter, 0x1C },
            { PhysicalKey.ControlLeft, 0x1D },
            { PhysicalKey.A, 0x1E }, { PhysicalKey.S, 0x1F }, { PhysicalKey.D, 0x20 }, { PhysicalKey.F, 0x21 }, { PhysicalKey.G, 0x22 },
            { PhysicalKey.H, 0x23 }, { PhysicalKey.J, 0x24 }, { PhysicalKey.K, 0x25 }, { PhysicalKey.L, 0x26 },
            { PhysicalKey.Semicolon, 0x27 }, { PhysicalKey.Quote, 0x28 }, { PhysicalKey.Backquote, 0x29 },
            { PhysicalKey.ShiftLeft, 0x2A }, { PhysicalKey.Backslash, 0x2B },
            { PhysicalKey.Z, 0x2C }, { PhysicalKey.X, 0x2D }, { PhysicalKey.C, 0x2E }, { PhysicalKey.V, 0x2F }, { PhysicalKey.B, 0x30 },
            { PhysicalKey.N, 0x31 }, { PhysicalKey.M, 0x32 },
            { PhysicalKey.Comma, 0x33 }, { PhysicalKey.Period, 0x34 }, { PhysicalKey.Slash, 0x35 }, { PhysicalKey.ShiftRight, 0x36 },
            { PhysicalKey.NumPadMultiply, 0x37 }, { PhysicalKey.AltLeft, 0x38 }, { PhysicalKey.Space, 0x39 }, { PhysicalKey.CapsLock, 0x3A },
            { PhysicalKey.F1, 0x3B }, { PhysicalKey.F2, 0x3C }, { PhysicalKey.F3, 0x3D }, { PhysicalKey.F4, 0x3E }, { PhysicalKey.F5, 0x3F },
            { PhysicalKey.F6, 0x40 }, { PhysicalKey.F7, 0x41 }, { PhysicalKey.F8, 0x42 }, { PhysicalKey.F9, 0x43 }, { PhysicalKey.F10, 0x44 },
            { PhysicalKey.NumLock, 0x45 }, { PhysicalKey.ScrollLock, 0x46 },
            { PhysicalKey.NumPad7, 0x47 }, { PhysicalKey.NumPad8, 0x48 }, { PhysicalKey.NumPad9, 0x49 }, { PhysicalKey.NumPadSubtract, 0x4A },
            { PhysicalKey.NumPad4, 0x4B }, { PhysicalKey.NumPad5, 0x4C }, { PhysicalKey.NumPad6, 0x4D }, { PhysicalKey.NumPadAdd, 0x4E },
            { PhysicalKey.NumPad1, 0x4F }, { PhysicalKey.NumPad2, 0x50 }, { PhysicalKey.NumPad3, 0x51 },
            { PhysicalKey.NumPad0, 0x52 }, { PhysicalKey.NumPadDecimal, 0x53 },
            { PhysicalKey.IntlBackslash, 0x56 }, { PhysicalKey.F11, 0x57 }, { PhysicalKey.F12, 0x58 },
            // Extended keys
            { PhysicalKey.NumPadEnter, 0x1C }, { PhysicalKey.ControlRight, 0x1D }, { PhysicalKey.NumPadDivide, 0x35 },
            { PhysicalKey.PrintScreen, 0x137 }, { PhysicalKey.AltRight, 0x38 }, { PhysicalKey.Pause, 0x11D },
            { PhysicalKey.Home, 0x47 }, { PhysicalKey.ArrowUp, 0x48 }, { PhysicalKey.PageUp, 0x49 },
            { PhysicalKey.ArrowLeft, 0x4B }, { PhysicalKey.ArrowRight, 0x4D },
            { PhysicalKey.End, 0x4F }, { PhysicalKey.ArrowDown, 0x50 }, { PhysicalKey.PageDown, 0x51 },
            { PhysicalKey.Insert, 0x52 }, { PhysicalKey.Delete, 0x53 },
            { PhysicalKey.MetaLeft, 0x5B }, { PhysicalKey.MetaRight, 0x5C }, { PhysicalKey.ContextMenu, 0x5D },
        };
    }
}
