// COPYRIGHT 2009, 2010, 2011, 2012, 2013, 2014, 2015 by the Open Rails project.
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
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using ORTS.Updater;

namespace Menu.Notifications
{
    /// <summary>
    /// Holds presentation details of the current notification displayed on the panel.
    /// </summary>
    public class NotificationPage
    {
        private StackPanel Panel; // Needed so that controls can be added
        private MainWindow MainForm; // Needed so the Retry button can raise an event which the form can catch.

        // Holds the controls for each item, so they can be added to the panel in order.
        public List<NDetail> NDetailList = new List<NDetail>();

        // Holds the index of each button on the page and the button, so the correct action can be taken depending on the type of button and its index.
        public Dictionary<int, NButtonControl> ButtonDictionary = new Dictionary<int, NButtonControl>();

        public NotificationPage(MainWindow mainForm, StackPanel panel, NotificationManager manager)
        {
            MainForm = mainForm;
            Panel = panel;
            NButtonControl.ButtonCount = 0;
            AddPageCountAndArrows(manager);
        }

        private void AddPageCountAndArrows(NotificationManager manager)
        {
            var pageCount = manager.Notifications?.NotificationList.Count ?? 0; // If no Internet connection, then Notifications == null
            var pageLabel = $"{manager.CurrentPageIndex + 1}/{pageCount}";

            var row = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Right, Spacing = 4, Margin = new Thickness(0, 0, 48, 0) };

            // Swap clickable arrows and disabled ones at each end of range.
            var previousVisibility = (manager.CurrentPageIndex > 0);
            row.Children.Add(previousVisibility
                ? new Arrow(manager.PreviousImage, true, MainForm.Previous_Click)
                : new Arrow(manager.FirstImage, false, null));

            row.Children.Add(new TextBlock { Text = pageLabel, FontWeight = FontWeight.Bold, VerticalAlignment = VerticalAlignment.Center, MinWidth = 30, TextAlignment = TextAlignment.Center });

            var nextVisibility = (manager.CurrentPageIndex < pageCount - 1);
            row.Children.Add(nextVisibility
                ? new Arrow(manager.NextImage, true, MainForm.Next_Click)
                : new Arrow(manager.LastImage, false, null));

            Panel.Children.Add(row);
        }

        public class Arrow : Button
        {
            protected override Type StyleKeyOverride => typeof(Button);

            public Arrow(Bitmap image, bool enabled, EventHandler<EventArgs> click)
            {
                Classes.Add("flat");
                Content = new Image { Source = image, Width = 9, Height = 9 };
                Width = 20;
                Height = 20;
                IsEnabled = enabled;
                if (click != null)
                    Click += (sender, e) => click(sender, e);
            }
        }

        #region NDetails
        // An NDetail creates a control (a row) for the page's panel.

        public class NDetail
        {
            public Control Control;

            public const int VerticalSpacing = 6;
            public const int LeftPadding = 10;
            public const int LeftPaddingIndented = 20;

            protected static IBrush GetBrush(string colorName, IBrush defaultBrush)
            {
                // "black" text would be invisible on a dark theme, so let the theme decide.
                if (String.IsNullOrEmpty(colorName) || colorName.Equals("black", StringComparison.OrdinalIgnoreCase))
                    return defaultBrush;
                return Color.TryParse(colorName, out var color) ? new SolidColorBrush(color) : defaultBrush;
            }
        }

        public class NTitleControl : NDetail
        {
            public NTitleControl(StackPanel panel, int current, int total, string date, string text)
            {
                var title = $"Notification {current}/{total}: {date} - {text}";
                Control = new TextBlock
                {
                    Text = title,
                    FontWeight = FontWeight.Bold,
                    TextWrapping = TextWrapping.Wrap,
                    Margin = new Thickness(LeftPadding, 0, 0, 0),
                };
            }
        }
        public class NHeadingControl : NDetail
        {
            public NHeadingControl(StackPanel panel, string text, string colorName = "blue")
            {
                Control = new TextBlock
                {
                    Foreground = GetBrush(colorName, Brushes.Blue),
                    Text = text,
                    FontWeight = FontWeight.Bold,
                    TextWrapping = TextWrapping.Wrap,
                    Margin = new Thickness(LeftPadding, 10, 0, 0),
                };
            }
        }
        public class NTextControl : NDetail
        {
            public NTextControl(StackPanel panel, string text, string colorName = "black")
            {
                var textBlock = new TextBlock
                {
                    Text = text,
                    TextWrapping = TextWrapping.Wrap,
                    Margin = new Thickness(LeftPaddingIndented, 0, 0, 0),
                };
                var brush = GetBrush(colorName, null);
                if (brush != null)
                    textBlock.Foreground = brush;
                Control = textBlock;
            }
        }
        public class NButtonControl : NDetail
        {
            public static int ButtonCount = 0;
            public Button Button;
            protected MainWindow MainForm;
            public NButtonControl(StackPanel panel, string legend, int width, string description, MainWindow mainForm)
            {
                MainForm = mainForm;
                Button = new Button
                {
                    Content = legend,
                    HorizontalContentAlignment = HorizontalAlignment.Center,
                    Width = width,
                    VerticalAlignment = VerticalAlignment.Center,
                };
                var index = ButtonCount;
                Button.Click += (sender, e) => mainForm.DoNotificationButton(index);

                var label = new TextBlock
                {
                    Text = description,
                    TextWrapping = TextWrapping.Wrap,
                    VerticalAlignment = VerticalAlignment.Center,
                    Margin = new Thickness(LeftPaddingIndented, 0, 0, 0),
                };
                var row = new DockPanel { Margin = new Thickness(LeftPaddingIndented, 0, 0, 0) };
                DockPanel.SetDock(Button, Dock.Left);
                row.Children.Add(Button);
                row.Children.Add(label);
                Control = row;
            }
        }
        public class NLinkControl : NButtonControl
        {
            public string Url;
            public NLinkControl(NotificationPage page, string legend, int width, string description, MainWindow mainForm, string url)
            : base(page.Panel, legend, width, description, mainForm)
            {
                Url = url;
                page.ButtonDictionary.Add(ButtonCount, this);
                ButtonCount++;
            }
        }
        public class NDialogControl : NButtonControl
        {
            public string Form;
            public NDialogControl(NotificationPage page, string legend, int width, string description, MainWindow mainForm, string form)
            : base(page.Panel, legend, width, description, mainForm)
            {
                Form = form;
                page.ButtonDictionary.Add(ButtonCount, this);
                ButtonCount++;
            }

            public void Show()
            {
                switch (Form)
                {
                    case "OptionsForm":
                        MainForm.ShowOptionsDialog();
                        break;
                    case "TelemetryForm":
                        MainForm.ShowTelemetryDialog();
                        break;
                }
            }
        }
        public class NUpdateControl : NButtonControl
        {
            public NUpdateControl(NotificationPage page, string legend, int width, string description, MainWindow mainForm)
            : base(page.Panel, legend, width, description, mainForm)
            {
                page.ButtonDictionary.Add(ButtonCount, this);
                ButtonCount++;
            }
        }
        public class NRefreshControl : NButtonControl
        {
            public NRefreshControl(NotificationPage page, string legend, int width, string description, MainWindow mainForm)
            : base(page.Panel, legend, width, description, mainForm)
            {
                page.ButtonDictionary.Add(ButtonCount, this);
                ButtonCount++;
            }
        }
        public class NRetryControl : NButtonControl
        {
            public NRetryControl(NotificationPage page, string legend, int width, string description, MainWindow mainForm)
            : base(page.Panel, legend, width, description, mainForm)
            {
                page.ButtonDictionary.Add(ButtonCount, this);
                ButtonCount++;
            }
        }
        public void DoButton(UpdateManager updateManager, int key)
        {
            var button = ButtonDictionary[key];
            if (button is NLinkControl)
                ExternalProcess.Open(((NLinkControl)button).Url);
            else if (button is NDialogControl dialogControl)
                dialogControl.Show();
            else if (button is NUpdateControl)
            {
                // In-place updates are only supported on Windows; elsewhere there is no update to apply.
                if (updateManager.LastUpdate != null)
                    updateManager.Update();
            }
            else if (button is NRefreshControl)
                MainForm.OnRefreshUpdates(EventArgs.Empty);
            else if (button is NRetryControl)
                MainForm.OnCheckUpdatesAgain(EventArgs.Empty);
            else throw new Exception("Unknown button type");
        }

        public class NRecordControl : NDetail
        {
            public TextBlock Field;

            public NRecordControl(StackPanel panel, string label, int legendWidth, string field)
            {
                // The legend, and the field to the right of it
                var legend = new TextBlock
                {
                    Text = label + ":",
                    FontWeight = FontWeight.Bold,
                    TextAlignment = TextAlignment.Right,
                    Width = legendWidth,
                };
                Field = new TextBlock
                {
                    Text = field,
                    TextWrapping = TextWrapping.Wrap,
                    Margin = new Thickness(6, 0, 0, 0),
                };
                var row = new DockPanel { Margin = new Thickness(LeftPadding, 0, 0, 0) };
                DockPanel.SetDock(legend, Dock.Left);
                row.Children.Add(legend);
                row.Children.Add(Field);
                Control = row;
            }
        }

        public void FlowNDetails()
        {
            foreach (var nDetail in NDetailList)
            {
                nDetail.Control.Margin = new Thickness(nDetail.Control.Margin.Left, nDetail.Control.Margin.Top, nDetail.Control.Margin.Right, NDetail.VerticalSpacing);
                this.Panel.Children.Add(nDetail.Control);
            }
        }
        #endregion NDetails
    }
}
