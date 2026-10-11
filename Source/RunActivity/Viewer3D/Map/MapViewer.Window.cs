// COPYRIGHT 2023 by the Open Rails project.
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

// Map window: Avalonia window layout, settings and user input. Everything in this file runs on the
// Avalonia UI thread. Changes to game state are queued with PostToGame and run on the updater thread.

using System;
using System.Diagnostics;
using System.Linq;
using System.Reflection;
using System.Text.RegularExpressions;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Styling;
using Avalonia.Threading;
using Orts.MultiPlayer;
using Orts.Simulation.Physics;
using Orts.Simulation.RollingStocks;
using Orts.Viewer3D.Map;
using AvWindow = Avalonia.Controls.Window;
using Color = Avalonia.Media.Color;

namespace Orts.Viewer3D.Debugging
{
    public partial class MapViewer
    {
        #region Controls
        internal AvWindow Window;
        MapCanvas mapCanvas;
        internal Control playerRolePanel;
        internal Control messagesPanel;
        internal Control multiplayerSettingsPanel;
        internal Control playersPanel;
        internal TextBlock timeLabel;

        CheckBox useAntiAliasingCheckbox;
        CheckBox showTimeCheckbox;
        Button rotateThemesButton;
        CheckBox showPlatformsCheckbox;
        CheckBox showPlatformLabelsCheckbox;
        CheckBox showSidingLabelsCheckbox;
        CheckBox showSwitchesCheckbox;
        CheckBox showSignalsCheckbox;
        CheckBox showSignalStateCheckbox;
        CheckBox showTrainLabelsCheckbox;
        CheckBox showTrainStateCheckbox;
        RadioButton showActiveTrainsRadio;
        RadioButton showAllTrainsRadio;
        Button mapCustomizationButton;
        Border mapCustomizationPanel;

        CheckBox allowJoiningCheckbox;
        CheckBox preferGreenCheckbox;
        CheckBox penaltyCheckbox;

        CheckBox drawPathCheckbox;
        CheckBox allowChangingSignalsCheckbox;
        CheckBox allowThrowingSwitchesCheckbox;
        NumericUpDown mapResolutionUpDown;

        ListBox playersView;
        ListBox messages;
        TextBox messageInput;

        ContextMenu setSwitchMenu;
        ContextMenu setSignalMenu;
        MenuItem allowCallOnMenuItem;
        ContextMenu trainActionsMenu;
        ContextMenu playerActionsMenu;
        MenuItem playerMenuItem;
        MenuItem makeThisPlayerAnAssistantMenuItem;
        MenuItem jumpToThisPlayerInGameMenuItem;
        MenuItem followPlayerMenuItem;
        ContextMenu messageActionsMenu;

        DispatcherTimer UITimer;
        bool AllowClose;
        bool BoundsChanged;
        #endregion

        static bool IsChecked(ToggleButton button) => button?.IsChecked == true;

        static string T(string text) => Viewer.Catalog.GetString(text);

        #region Window construction
        void CreateWindow()
        {
            if (Disposed) return;

            Window = new AvWindow
            {
                Title = T("Map window"),
                Width = 784,
                Height = 561,
                MinWidth = 600,
                MinHeight = 400,
                // Like the WinForms version, which returned the focus to the game window: don't steal the focus.
                ShowActivated = false,
                WindowStartupLocation = WindowStartupLocation.Manual,
                FontSize = 12,
            };
            AddCompactStyles(Window);

            var root = new DockPanel();

            // Player role banner (multiplayer dispatcher only)
            var playerRoleLink = new TextBlock
            {
                Text = T("Learn more"),
                Foreground = Brushes.Blue,
                TextDecorations = TextDecorations.Underline,
                Cursor = new Cursor(StandardCursorType.Hand),
                VerticalAlignment = VerticalAlignment.Center,
                Margin = new Thickness(0, 0, 10, 0),
            };
            playerRoleLink.PointerReleased += (s, e) => playerRoleLink_LinkClicked();
            DockPanel.SetDock(playerRoleLink, Dock.Right);
            var playerRoleExplanation = new TextBlock
            {
                Text = T("You are the dispatcher in this multiplayer session"),
                FontWeight = FontWeight.Bold,
                Foreground = new SolidColorBrush(Color.FromRgb(40, 116, 166)),
                VerticalAlignment = VerticalAlignment.Center,
                Margin = new Thickness(12, 0, 0, 0),
            };
            playerRolePanel = new Border
            {
                Background = new SolidColorBrush(Color.FromRgb(214, 234, 248)),
                Height = 30,
                IsVisible = false,
                Child = new DockPanel { Children = { playerRoleLink, playerRoleExplanation } },
            };
            DockPanel.SetDock(playerRolePanel, Dock.Top);
            root.Children.Add(playerRolePanel);

            // Side panel
            var sidePanel = CreateSidePanel();
            DockPanel.SetDock(sidePanel, Dock.Right);
            root.Children.Add(sidePanel);

            // Messages (multiplayer dispatcher only)
            messagesPanel = CreateMessagesPanel();
            DockPanel.SetDock(messagesPanel, Dock.Top);
            root.Children.Add(messagesPanel);

            // Map canvas with its overlays
            root.Children.Add(CreateCanvasPanel());

            Window.Content = root;

            CreateContextMenus();

            InitializeForm();

            Window.Closing += (s, e) =>
            {
                if (AllowClose) return;
                // Prevent the window from closing; instead, hide it
                e.Cancel = true;
                Viewer.MapViewerEnabled = false;
                Window.Hide();
            };
            Window.PositionChanged += (s, e) => BoundsChanged = true;
            Window.PropertyChanged += (s, e) =>
            {
                if (e.Property == TopLevel.ClientSizeProperty || e.Property == AvWindow.WindowStateProperty)
                    BoundsChanged = true;
            };
            UiThread.Track(Window);

            // Initialise the timer used to handle user input
            UITimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(100) };
            UITimer.Tick += UITimer_Tick;
            UITimer.Start();
        }

        static void AddCompactStyles(StyledElement element)
        {
            // The Fluent theme is made for touch; make the controls about as dense as the WinForms ones.
            element.Styles.Add(new Style(x => x.OfType<CheckBox>())
            {
                Setters =
                {
                    new Setter(Layoutable.MinHeightProperty, 0d),
                    new Setter(TemplatedControl.PaddingProperty, new Thickness(4, 0, 0, 0)),
                    new Setter(TemplatedControl.FontSizeProperty, 12d),
                },
            });
            element.Styles.Add(new Style(x => x.OfType<RadioButton>())
            {
                Setters =
                {
                    new Setter(Layoutable.MinHeightProperty, 0d),
                    new Setter(TemplatedControl.PaddingProperty, new Thickness(4, 0, 0, 0)),
                    new Setter(TemplatedControl.FontSizeProperty, 12d),
                },
            });
            element.Styles.Add(new Style(x => x.OfType<Button>())
            {
                Setters =
                {
                    new Setter(TemplatedControl.PaddingProperty, new Thickness(6, 3)),
                    new Setter(TemplatedControl.FontSizeProperty, 12d),
                },
            });
            element.Styles.Add(new Style(x => x.OfType<ListBoxItem>())
            {
                Setters =
                {
                    new Setter(Layoutable.MinHeightProperty, 0d),
                    new Setter(TemplatedControl.PaddingProperty, new Thickness(4, 1)),
                },
            });
        }

        static Control GroupBox(string header, Control content)
        {
            var title = new TextBlock { Text = header, FontWeight = FontWeight.SemiBold, Margin = new Thickness(0, 0, 0, 4) };
            DockPanel.SetDock(title, Dock.Top);
            return new Border
            {
                BorderBrush = Brushes.Gray,
                BorderThickness = new Thickness(1),
                CornerRadius = new CornerRadius(3),
                Padding = new Thickness(6),
                Child = new DockPanel { Children = { title, content } },
            };
        }

        static CheckBox NewCheckBox(string text, bool isChecked) => new CheckBox { Content = T(text), IsChecked = isChecked };

        static Button NewButton(string text) => new Button { Content = T(text), HorizontalAlignment = HorizontalAlignment.Stretch, HorizontalContentAlignment = HorizontalAlignment.Center, Margin = new Thickness(0, 2) };

        Control CreateSidePanel()
        {
            // Multiplayer settings (dispatcher only)
            allowJoiningCheckbox = NewCheckBox("Allow new players to join", true);
            preferGreenCheckbox = NewCheckBox("Prefer green signals", true);
            penaltyCheckbox = new CheckBox { Content = T("Penalty for speeding && SPAD").Replace("&&", "&"), IsChecked = true };
            allowJoiningCheckbox.IsCheckedChanged += (s, e) => allowJoiningCheckbox_CheckedChanged();
            preferGreenCheckbox.IsCheckedChanged += (s, e) => preferGreenCheckbox_CheckedChanged();
            penaltyCheckbox.IsCheckedChanged += (s, e) => penaltyCheckbox_CheckedChanged();
            multiplayerSettingsPanel = new Border
            {
                IsVisible = false,
                Padding = new Thickness(10, 0, 10, 6),
                Child = GroupBox(T("Multiplayer settings"), new StackPanel { Children = { allowJoiningCheckbox, preferGreenCheckbox, penaltyCheckbox } }),
            };

            // Map settings and controls
            drawPathCheckbox = NewCheckBox("Draw next path section", true);
            drawPathCheckbox.IsCheckedChanged += (s, e) => drawPathCheckbox_CheckedChanged();
            allowChangingSignalsCheckbox = NewCheckBox("Allow changing signals", true);
            allowThrowingSwitchesCheckbox = NewCheckBox("Allow throwing switches", true);
            mapResolutionUpDown = new NumericUpDown
            {
                Minimum = MapResolutionMinimum,
                Maximum = MapResolutionMaximum,
                Increment = 50,
                Value = Math.Min(5000, MapResolutionMaximum),
                FormatString = "N0",
                ParsingNumberStyle = System.Globalization.NumberStyles.Number,
                ClipValueToMinMax = true,
                Margin = new Thickness(0, 2, 0, 4),
            };
            mapResolutionUpDown.ValueChanged += (s, e) => mapResolutionUpDown_ValueChanged();
            var seeTrainInGameButton = NewButton("Jump to my train in game");
            seeTrainInGameButton.Click += (s, e) => seeTrainInGameButton_Click();
            var centerOnMyTrainButton = NewButton("Jump to my train on the map");
            centerOnMyTrainButton.Click += (s, e) => centerOnMyTrainButton_Click();
            var followMyTrainOnMap = NewButton("Follow my train on the map");
            followMyTrainOnMap.Click += (s, e) => followMyTrainOnMap_Click();
            var mapSettingsPanel = new Border
            {
                Padding = new Thickness(10, 0, 10, 6),
                Child = GroupBox(T("Map settings and controls"), new StackPanel
                {
                    Children =
                    {
                        drawPathCheckbox,
                        allowChangingSignalsCheckbox,
                        allowThrowingSwitchesCheckbox,
                        new TextBlock { Text = T("Map resolution (m):"), Margin = new Thickness(0, 4, 0, 0) },
                        mapResolutionUpDown,
                        seeTrainInGameButton,
                        centerOnMyTrainButton,
                        followMyTrainOnMap,
                    }
                }),
            };

            // Players (multiplayer only)
            playersView = new ListBox { SelectionMode = SelectionMode.Multiple };
            playersPanel = new Border
            {
                IsVisible = false,
                Padding = new Thickness(10, 0, 10, 10),
                Child = GroupBox(T("Players"), playersView),
            };

            var grid = new Grid
            {
                Width = 200,
                RowDefinitions = new RowDefinitions("20,Auto,Auto,*"),
            };
            Grid.SetRow(multiplayerSettingsPanel, 1);
            Grid.SetRow(mapSettingsPanel, 2);
            Grid.SetRow(playersPanel, 3);
            grid.Children.Add(multiplayerSettingsPanel);
            grid.Children.Add(mapSettingsPanel);
            grid.Children.Add(playersPanel);
            return grid;
        }

        Control CreateMessagesPanel()
        {
            messages = new ListBox { Height = 97, Margin = new Thickness(0, 4) };
            messageInput = new TextBox { Margin = new Thickness(0, 0, 4, 0), VerticalAlignment = VerticalAlignment.Center };
            messageInput.GotFocus += (s, e) => messageInput_Enter();
            messageInput.LostFocus += (s, e) => messageInput_Leave();
            var messageAllButton = new Button { Content = T("Message all players"), VerticalAlignment = VerticalAlignment.Center };
            messageAllButton.Click += (s, e) => messageAllButton_Click();
            var moreReplyOptionsButton = new Button { Content = ">", VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(2, 0, 0, 0) };
            moreReplyOptionsButton.Click += (s, e) => moreReplyOptionsButton_Click(moreReplyOptionsButton);

            var inputRow = new Grid { ColumnDefinitions = new ColumnDefinitions("*,Auto,Auto") };
            Grid.SetColumn(messageAllButton, 1);
            Grid.SetColumn(moreReplyOptionsButton, 2);
            inputRow.Children.Add(messageInput);
            inputRow.Children.Add(messageAllButton);
            inputRow.Children.Add(moreReplyOptionsButton);

            return new StackPanel
            {
                IsVisible = false,
                Margin = new Thickness(13, 8, 13, 4),
                Children =
                {
                    new TextBlock { Text = T("Messages:") },
                    messages,
                    inputRow,
                }
            };
        }

        Control CreateCanvasPanel()
        {
            mapCanvas = new MapCanvas(this) { Margin = new Thickness(13), ClipToBounds = true, Focusable = true };
            mapCanvas.PointerPressed += (s, e) => HandlePointer(e.GetCurrentPoint(mapCanvas), e.KeyModifiers);
            mapCanvas.PointerMoved += (s, e) => HandlePointer(e.GetCurrentPoint(mapCanvas), e.KeyModifiers);
            mapCanvas.PointerReleased += (s, e) => HandlePointer(e.GetCurrentPoint(mapCanvas), e.KeyModifiers);
            mapCanvas.PointerWheelChanged += (s, e) => OnMouseWheel(e.Delta.Y);
            mapCanvas.SizeChanged += (s, e) => GenerateView();

            timeLabel = new TextBlock
            {
                Text = T("Simulation time"),
                FontWeight = FontWeight.Bold,
                HorizontalAlignment = HorizontalAlignment.Left,
                VerticalAlignment = VerticalAlignment.Bottom,
                Margin = new Thickness(25, 0, 0, 22),
                IsHitTestVisible = false,
            };

            mapCustomizationButton = new Button
            {
                Content = T("Map customization"),
                HorizontalAlignment = HorizontalAlignment.Right,
                VerticalAlignment = VerticalAlignment.Bottom,
                Margin = new Thickness(0, 0, 21, 21),
            };
            mapCustomizationButton.Click += (s, e) => mapCustomizationButton_Click();

            useAntiAliasingCheckbox = NewCheckBox("Use anti-aliasing", false);
            showTimeCheckbox = NewCheckBox("Show simulation time", true);
            rotateThemesButton = new Button { Content = T("Rotate between themes"), Margin = new Thickness(0, 4) };
            rotateThemesButton.Click += (s, e) => rotateThemesButton_Click();
            showPlatformsCheckbox = NewCheckBox("Platforms", false);
            showPlatformLabelsCheckbox = NewCheckBox("Platform labels", false);
            showSidingLabelsCheckbox = NewCheckBox("Siding labels", false);
            showSwitchesCheckbox = NewCheckBox("Switches", false);
            showSignalsCheckbox = NewCheckBox("Signals", false);
            showSignalStateCheckbox = NewCheckBox("Signal state", false);
            showSignalStateCheckbox.Margin = new Thickness(18, 0, 0, 0);
            showTrainLabelsCheckbox = NewCheckBox("Train labels", false);
            showTrainStateCheckbox = NewCheckBox("Train state", false);
            showTrainStateCheckbox.Margin = new Thickness(18, 0, 0, 0);
            showActiveTrainsRadio = new RadioButton { Content = T("Only active trains"), GroupName = "MapTrains", IsChecked = true };
            showAllTrainsRadio = new RadioButton { Content = T("All trains"), GroupName = "MapTrains" };
            // Any map feature change is shown immediately
            foreach (var toggle in new ToggleButton[] { useAntiAliasingCheckbox, showTimeCheckbox, showPlatformsCheckbox, showPlatformLabelsCheckbox, showSidingLabelsCheckbox,
                showSwitchesCheckbox, showSignalsCheckbox, showSignalStateCheckbox, showTrainLabelsCheckbox, showTrainStateCheckbox, showActiveTrainsRadio, showAllTrainsRadio })
                toggle.IsCheckedChanged += (s, e) => GenerateView();
            useAntiAliasingCheckbox.IsCheckedChanged += (s, e) => ApplyAntiAliasing();

            mapCustomizationPanel = new Border
            {
                IsVisible = false,
                Width = 190,
                HorizontalAlignment = HorizontalAlignment.Right,
                VerticalAlignment = VerticalAlignment.Top,
                Margin = new Thickness(0, 16, 21, 0),
                Padding = new Thickness(10),
                BorderBrush = Brushes.Gray,
                BorderThickness = new Thickness(1),
                Child = new StackPanel
                {
                    Children =
                    {
                        useAntiAliasingCheckbox,
                        showTimeCheckbox,
                        rotateThemesButton,
                        new TextBlock { Text = T("Map features"), FontWeight = FontWeight.SemiBold, Margin = new Thickness(0, 6, 0, 2) },
                        showPlatformsCheckbox,
                        showPlatformLabelsCheckbox,
                        showSidingLabelsCheckbox,
                        showSwitchesCheckbox,
                        showSignalsCheckbox,
                        showSignalStateCheckbox,
                        showTrainLabelsCheckbox,
                        showTrainStateCheckbox,
                        showActiveTrainsRadio,
                        showAllTrainsRadio,
                    }
                },
            };

            return new Grid { Children = { mapCanvas, timeLabel, mapCustomizationButton, mapCustomizationPanel } };
        }

        static MenuItem NewMenuItem(string text, Action click, Color? background = null, bool white = false)
        {
            var item = new MenuItem { Header = T(text) };
            if (background != null)
                item.Background = new SolidColorBrush(background.Value);
            if (white)
                item.Foreground = Brushes.White;
            if (click != null)
                item.Click += (s, e) => click();
            return item;
        }

        static MenuItem NewMenuTitle(string text) => new MenuItem { Header = T(text), IsEnabled = false, FontWeight = FontWeight.Bold };

        void CreateContextMenus()
        {
            setSwitchMenu = new ContextMenu
            {
                Items =
                {
                    NewMenuTitle("Set switch to..."),
                    NewMenuItem("Main route", () => setSwitchMenu_ItemClicked("mainRoute"), Color.FromRgb(93, 64, 55), true),
                    NewMenuItem("Side route", () => setSwitchMenu_ItemClicked("sideRoute"), Color.FromRgb(161, 136, 127)),
                }
            };

            allowCallOnMenuItem = NewMenuItem("Allow call on", () => setSignalMenu_ItemClicked("callOn"));
            allowCallOnMenuItem.IsEnabled = false;
            setSignalMenu = new ContextMenu
            {
                Items =
                {
                    NewMenuTitle("Set signal aspect to..."),
                    NewMenuItem("System controlled", () => setSignalMenu_ItemClicked("system")),
                    NewMenuItem("Stop", () => setSignalMenu_ItemClicked("stop"), Color.FromRgb(203, 67, 53), true),
                    NewMenuItem("Approach", () => setSignalMenu_ItemClicked("approach"), Color.FromRgb(241, 196, 15)),
                    NewMenuItem("Proceed", () => setSignalMenu_ItemClicked("proceed"), Color.FromRgb(34, 153, 84), true),
                    new Separator(),
                    allowCallOnMenuItem,
                }
            };

            trainActionsMenu = new ContextMenu
            {
                Items =
                {
                    NewMenuItem("Jump to this train in game", () => trainActionsMenu_ItemClicked("seeInGame")),
                    NewMenuItem("Follow this train on the map", () => trainActionsMenu_ItemClicked("followOnMap")),
                }
            };

            playerMenuItem = NewMenuTitle("Player");
            makeThisPlayerAnAssistantMenuItem = NewMenuItem("Make this player an assistant", () => playerActionsMenu_ItemClicked("assistant"));
            jumpToThisPlayerInGameMenuItem = NewMenuItem("Jump to this player in game", () => playerActionsMenu_ItemClicked("seeInGame"));
            followPlayerMenuItem = NewMenuItem("Follow on the map", () => playerActionsMenu_ItemClicked("followOnMap"));
            playerActionsMenu = new ContextMenu
            {
                Items =
                {
                    playerMenuItem,
                    makeThisPlayerAnAssistantMenuItem,
                    jumpToThisPlayerInGameMenuItem,
                    followPlayerMenuItem,
                    NewMenuItem("Kick from multiplayer session", () => playerActionsMenu_ItemClicked("kick")),
                }
            };

            messageActionsMenu = new ContextMenu
            {
                Items =
                {
                    NewMenuItem("Message the selected player", () => messageActionsMenu_ItemClicked("message")),
                    NewMenuItem("Reply to the selected player", () => messageActionsMenu_ItemClicked("reply")),
                }
            };
        }

        void InitializeForm()
        {
            MapDataProvider.SetControls();
            Theme = MapThemeProvider.GetTheme(ThemeName);

            setDefaults();
            ApplyTheme();
            ApplyAntiAliasing();
        }

        void CloseWindow()
        {
            UITimer?.Stop();
            if (Window == null) return;
            AllowClose = true;
            Window.Close();
        }
        #endregion

        #region Settings
        // The controls which have a property "Map_<control name>" in UserSettings.cs.
        // Properties are stored in the registry or in a .ini file.

        object GetSetting(string name)
        {
            return Viewer.Settings.GetProperty("Map_" + name)?.GetValue(Viewer.Settings, null);
        }

        void SaveSetting(string name, object value)
        {
            var property = Viewer.Settings.GetProperty("Map_" + name);
            if (property == null) return;
            property.SetValue(Viewer.Settings, value, null);
            Viewer.Settings.Save("Map_" + name);
        }

        void BindSetting(ToggleButton toggle, string name)
        {
            if (GetSetting(name) is bool value)
                toggle.IsChecked = value;
            toggle.IsCheckedChanged += (s, e) => SaveSetting(name, toggle.IsChecked == true);
        }

        /// <summary>
        /// Set the defaults for all the map controls which have a property in UserSettings.cs
        /// </summary>
        private void setDefaults()
        {
            BindSetting(showTrainStateCheckbox, nameof(showTrainStateCheckbox));
            BindSetting(showTrainLabelsCheckbox, nameof(showTrainLabelsCheckbox));
            BindSetting(showSignalStateCheckbox, nameof(showSignalStateCheckbox));
            BindSetting(showSignalsCheckbox, nameof(showSignalsCheckbox));
            BindSetting(showSwitchesCheckbox, nameof(showSwitchesCheckbox));
            BindSetting(showSidingLabelsCheckbox, nameof(showSidingLabelsCheckbox));
            BindSetting(showPlatformLabelsCheckbox, nameof(showPlatformLabelsCheckbox));
            BindSetting(showPlatformsCheckbox, nameof(showPlatformsCheckbox));
            BindSetting(showTimeCheckbox, nameof(showTimeCheckbox));
            BindSetting(useAntiAliasingCheckbox, nameof(useAntiAliasingCheckbox));
            BindSetting(penaltyCheckbox, nameof(penaltyCheckbox));
            BindSetting(preferGreenCheckbox, nameof(preferGreenCheckbox));
            BindSetting(allowJoiningCheckbox, nameof(allowJoiningCheckbox));
            BindSetting(allowThrowingSwitchesCheckbox, nameof(allowThrowingSwitchesCheckbox));
            BindSetting(allowChangingSignalsCheckbox, nameof(allowChangingSignalsCheckbox));
            BindSetting(drawPathCheckbox, nameof(drawPathCheckbox));
            BindSetting(showActiveTrainsRadio, nameof(showActiveTrainsRadio));
            BindSetting(showAllTrainsRadio, nameof(showAllTrainsRadio));

            if (GetSetting(nameof(mapResolutionUpDown)) is int resolution)
                mapResolutionUpDown.Value = Math.Max(MapResolutionMinimum, Math.Min(MapResolutionMaximum, resolution));
            mapResolutionUpDown.ValueChanged += (s, e) =>
            {
                if (mapResolutionUpDown.Value != null)
                    SaveSetting(nameof(mapResolutionUpDown), (int)mapResolutionUpDown.Value.Value);
            };

            if (GetSetting(nameof(rotateThemesButton)) is string themeName && MapThemeProvider.GetTheme(themeName) != null)
            {
                ThemeName = themeName;
                Theme = MapThemeProvider.GetTheme(ThemeName);
            }

            if (GetSetting(nameof(MapViewer)) is int[] bounds && bounds.Length == 4)
            {
                Window.Width = Math.Max(Window.MinWidth, bounds[2]);
                Window.Height = Math.Max(Window.MinHeight, bounds[3]);
                Window.Position = new PixelPoint(bounds[0], bounds[1]);
            }
        }

        /// <summary>
        /// Save the size and position of the window
        /// </summary>
        void SaveBounds()
        {
            if (!BoundsChanged || Window == null || !Window.IsVisible) return;
            BoundsChanged = false;
            // Like the WinForms RestoreBounds: don't remember the size of a minimized or maximized window
            if (Window.WindowState != WindowState.Normal) return;
            var position = Window.Position;
            var size = Window.ClientSize;
            SaveSetting(nameof(MapViewer), new int[] { position.X, position.Y, (int)size.Width, (int)size.Height });
        }
        #endregion

        #region Themes
        void ApplyTheme()
        {
            if (Theme == null)
            {
                ThemeName = "light";
                Theme = MapThemeProvider.GetTheme(ThemeName);
            }
            Window.RequestedThemeVariant = Theme.IsDark ? ThemeVariant.Dark : ThemeVariant.Light;
            var panelBrush = new SolidColorBrush(Theme.PanelBackColor);
            Window.Background = panelBrush;
            Window.Foreground = new SolidColorBrush(Theme.ForeColor);
            mapCustomizationPanel.Background = panelBrush;
            MapCanvasColor = Theme.MapCanvasColor;
            TrackColor = Theme.TrackColor;
            mapCanvas.InvalidateVisual();
        }

        void ApplyAntiAliasing()
        {
            // Optional anti-aliasing
            RenderOptions.SetEdgeMode(mapCanvas, IsChecked(useAntiAliasingCheckbox) ? EdgeMode.Antialias : EdgeMode.Aliased);
            mapCanvas.InvalidateVisual();
        }

        private void rotateThemesButton_Click()
        {
            // Cycles through the array of available themes
            string[] themes = MapThemeProvider.GetThemes();
            int i = Array.IndexOf(themes, ThemeName);
            ThemeName = i >= 0 && i < themes.Length - 1 ? themes[i + 1] : themes[0];

            Theme = MapThemeProvider.GetTheme(ThemeName);
            ApplyTheme();
            SaveSetting(nameof(rotateThemesButton), ThemeName);
        }

        private void mapCustomizationButton_Click()
        {
            MapCustomizationVisible = !MapCustomizationVisible;
            mapCustomizationPanel.IsVisible = MapCustomizationVisible;

            if (MapCustomizationVisible == true)
            {
                mapCustomizationButton.Background = new SolidColorBrush(Color.FromRgb(214, 234, 248));
                mapCustomizationButton.Foreground = new SolidColorBrush(Color.FromRgb(40, 116, 166));
            }
            else
            {
                mapCustomizationButton.ClearValue(TemplatedControl.BackgroundProperty);
                mapCustomizationButton.ClearValue(TemplatedControl.ForegroundProperty);
            }
        }
        #endregion

        void UITimer_Tick(object sender, EventArgs e)
        {
            if (Disposed) return;

            if (Viewer.MapViewerEnabled == false) // Ctrl+9 sets this true to initialise the window and make it visible
            {
                if (Window.IsVisible)
                {
                    SaveBounds();
                    Window.Hide();
                }
                return;
            }
            if (!Window.IsVisible)
                Window.Show();

            if (Viewer.MapViewerEnabledSetToTrue)
            {
                GenerateView();
                Viewer.MapViewerEnabledSetToTrue = false;
            }

            SaveBounds();

            if (simulator.GameTime - lastUpdateTime < 1)
                return;

            lastUpdateTime = simulator.GameTime;

            GenerateView();
        }

        #region Multiplayer settings
        private void allowJoiningCheckbox_CheckedChanged()
        {
            var allow = IsChecked(allowJoiningCheckbox);
            PostToGame(() => MPManager.Instance().AllowNewPlayer = allow);
        }

        private void penaltyCheckbox_CheckedChanged()
        {
            var penalty = IsChecked(penaltyCheckbox);
            PostToGame(() =>
            {
                MPManager.Instance().CheckSpad = penalty;
                if (penalty == false) { MPManager.BroadCast(new MSGMessage("All", "OverSpeedOK", "OK to go overspeed and pass stop light").ToString()); }
                else { MPManager.BroadCast(new MSGMessage("All", "NoOverSpeed", "Penalty for overspeed and passing stop light").ToString()); }
            });
        }

        private void preferGreenCheckbox_CheckedChanged()
        {
            var preferGreen = IsChecked(preferGreenCheckbox);
            PostToGame(() => MPManager.PreferGreen = preferGreen);
        }
        #endregion

        #region Map settings and controls
        private void drawPathCheckbox_CheckedChanged()
        {
            DrawPath = IsChecked(drawPathCheckbox);
            GenerateView();
        }

        private void mapResolutionUpDown_ValueChanged()
        {
            if (mapResolutionUpDown.Value == null) return;

            // Center point of the map viewport before the change in resolution
            var center = new System.Drawing.PointF(ViewWindow.X + (ViewWindow.Width / 2f), ViewWindow.Y + (ViewWindow.Height / 2f));

            float newSizeH = (float)mapResolutionUpDown.Value.Value;
            float verticalByHorizontal = ViewWindow.Height / ViewWindow.Width;
            float newSizeV = newSizeH * verticalByHorizontal;

            ViewWindow = new System.Drawing.RectangleF(center.X - (newSizeH / 2f), center.Y - (newSizeV / 2f), newSizeH, newSizeV);

            GenerateView();
        }

        decimal MapResolution
        {
            get => mapResolutionUpDown.Value ?? 5000;
            set => mapResolutionUpDown.Value = Math.Max(MapResolutionMinimum, Math.Min(MapResolutionMaximum, value));
        }

        private void seeTrainInGameButton_Click()
        {
            PickedTrain = Program.Simulator.PlayerLocomotive.Train;
            ClickedTrain = true;
        }

        private void centerOnMyTrainButton_Click()
        {
            PickedTrain = Program.Simulator.PlayerLocomotive.Train;
            FollowTrain = false;
            FirstShow = true;
            GenerateView();
        }

        private void followMyTrainOnMap_Click()
        {
            PickedTrain = Program.Simulator.PlayerLocomotive.Train;
            FollowTrain = true;
            GenerateView();
        }

        private void playerRoleLink_LinkClicked()
        {
            try
            {
                Process.Start(new ProcessStartInfo { FileName = "https://open-rails.readthedocs.io/en/latest/multiplayer.html#in-game-controls", UseShellExecute = true });
            }
            catch (Exception error)
            {
                Trace.TraceWarning("Could not open the web browser: {0}", error.Message);
            }
        }
        #endregion

        #region Mouse input on the map
        private void OnMouseWheel(double delta)
        {
            decimal tempValue = MapResolution;
            if (delta < 0) tempValue /= 0.95m;
            else if (delta > 0) tempValue *= 0.95m;
            else return;

            MapResolution = tempValue;
        }

        private bool Zooming;
        private bool LeftClick;
        private bool RightClick;

        /// <summary>
        /// Avalonia reports a press of a second mouse button as a pointer move, so all pointer events are
        /// dispatched on the kind of update instead of on the kind of event.
        /// </summary>
        void HandlePointer(PointerPoint point, KeyModifiers modifiers)
        {
            var x = (int)Math.Round(point.Position.X);
            var y = (int)Math.Round(point.Position.Y);
            switch (point.Properties.PointerUpdateKind)
            {
                case PointerUpdateKind.LeftButtonPressed:
                    mapCanvas_MouseDown(MouseButton.Left, x, y);
                    break;
                case PointerUpdateKind.RightButtonPressed:
                    mapCanvas_MouseDown(MouseButton.Right, x, y);
                    break;
                case PointerUpdateKind.LeftButtonReleased:
                    mapCanvas_MouseUp(MouseButton.Left, x, y, modifiers);
                    break;
                case PointerUpdateKind.RightButtonReleased:
                    mapCanvas_MouseUp(MouseButton.Right, x, y, modifiers);
                    break;
                case PointerUpdateKind.Other:
                    mapCanvas_MouseMove(x, y);
                    break;
            }
        }

        private void mapCanvas_MouseDown(MouseButton button, int x, int y)
        {
            if (button == MouseButton.Left) LeftClick = true;
            if (button == MouseButton.Right) RightClick = true;

            if (LeftClick == true && RightClick == false)
            {
                if (Dragging == false)
                {
                    Dragging = true;
                    FollowTrain = false;
                    mapCanvas.Cursor = new Cursor(StandardCursorType.SizeAll);
                }
            }
            else if (LeftClick == true && RightClick == true)
            {
                if (Zooming == false) Zooming = true;
            }
            LastCursorPosition.X = x;
            LastCursorPosition.Y = y;
            MPManager.Instance().ComposingText = false;
        }

        private void mapCanvas_MouseUp(MouseButton button, int x, int y, KeyModifiers modifiers)
        {
            if (button == MouseButton.Left) LeftClick = false;
            if (button == MouseButton.Right) RightClick = false;

            if (LeftClick == false)
            {
                if (Dragging)
                {
                    mapCanvas.Cursor = null;
                    mapCanvas.InvalidateVisual(); // Remove the zoom target
                }
                Dragging = false;
                Zooming = false;
            }

            if ((modifiers & KeyModifiers.Shift) == KeyModifiers.Shift)
            {
                CanvasMoveAndZoomInOut(x, y, 1200);
            }
            else if ((modifiers & KeyModifiers.Alt) == KeyModifiers.Alt)
            {
                CanvasMoveAndZoomInOut(x, y, 30000);
            }
            else if ((modifiers & KeyModifiers.Control) == KeyModifiers.Control)
            {
                CanvasMoveAndZoomInOut(x, y, MapResolutionMaximum);
            }
            else if (LeftClick == false)
            {
                if (LastCursorPosition.X == x && LastCursorPosition.Y == y)
                {
                    var range = 5 * (int)xScale; if (range > 10) range = 10;
                    var temp = findItemFromMouse(x, y, range);
                    if (temp != null)
                    {
                        if (temp is SwitchWidget widget)
                        {
                            switchPickedItem = widget;
                            signalPickedItem = null;
                            trainPickedItem = null;
                            HandlePickedSwitch();
                        }
                        if (temp is SignalWidget widget1)
                        {
                            signalPickedItem = widget1;
                            switchPickedItem = null;
                            trainPickedItem = null;
                            HandlePickedSignal();
                        }
                        if (temp is TrainWidget widget2)
                        {
                            trainPickedItem = widget2;
                            signalPickedItem = null;
                            switchPickedItem = null;
                            HandlePickedTrain();
                        }
                    }
                    else
                    {
                        switchPickedItem = null;
                        signalPickedItem = null;
                        UnHandleItemPick();
                        PickedTrain = null;
                    }
                }

            }
        }

        private void mapCanvas_MouseMove(int x, int y)
        {
            if (Dragging && !Zooming)
            {
                int diffX = LastCursorPosition.X - x;
                int diffY = LastCursorPosition.Y - y;

                ViewWindow.Offset(diffX * ScrollSpeedX / 10, -diffY * ScrollSpeedY / 10);
                GenerateView();
            }
            else if (Zooming)
            {
                decimal tempValue = MapResolution;
                if (LastCursorPosition.Y - y < 0) tempValue /= 0.95m;
                else if (LastCursorPosition.Y - y > 0) tempValue *= 0.95m;

                MapResolution = tempValue;
                GenerateView();
            }
            LastCursorPosition.X = x;
            LastCursorPosition.Y = y;
        }

        private float ScrollSpeedX
        {
            get
            {
                return ViewWindow.Width * 0.10f;
            }
        }

        private float ScrollSpeedY
        {
            get
            {
                return ViewWindow.Width * 0.10f;
            }
        }

        private void CanvasMoveAndZoomInOut(int x, int y, decimal scale)
        {
            int diffX = x - (CanvasWidth / 2);
            int diffY = y - (CanvasHeight / 2);
            ViewWindow.Offset(diffX / xScale, -diffY / yScale);
            MapResolution = scale;
            GenerateView();
        }

        private ItemWidget findItemFromMouse(int x, int y, int range)
        {
            if (range < 5) range = 5;
            double closest = float.NaN;
            ItemWidget closestItem = null;
            if (IsChecked(allowThrowingSwitchesCheckbox))
            {
                foreach (var item in switchItemsDrawn)
                {
                    // If out of range, continue
                    if (item.Location2D.X < x - range || item.Location2D.X > x + range
                       || item.Location2D.Y < y - range || item.Location2D.Y > y + range)
                        continue;

                    if (closestItem != null)
                    {
                        var dist = Math.Pow(item.Location2D.X - closestItem.Location2D.X, 2) + Math.Pow(item.Location2D.Y - closestItem.Location2D.Y, 2);
                        if (dist < closest)
                        {
                            closest = dist; closestItem = item;
                        }
                    }
                    else closestItem = item;
                }
                if (closestItem != null)
                {
                    switchPickedTime = simulator.GameTime;
                    return closestItem;
                }
            }
            if (IsChecked(allowChangingSignalsCheckbox))
            {
                foreach (var item in signalItemsDrawn)
                {
                    // If out of range, continue
                    if (item.Location2D.X < x - range || item.Location2D.X > x + range
                       || item.Location2D.Y < y - range || item.Location2D.Y > y + range)
                        continue;

                    if (closestItem != null)
                    {
                        var dist = Math.Pow(item.Location2D.X - closestItem.Location2D.X, 2) + Math.Pow(item.Location2D.Y - closestItem.Location2D.Y, 2);
                        if (dist < closest)
                        {
                            closest = dist; closestItem = item;
                        }
                    }
                    else closestItem = item;
                }
                if (closestItem != null)
                {
                    switchPickedTime = simulator.GameTime;
                    return closestItem;
                }
            }

            // Now check for trains (first car only)
            TrainCar firstCar;
            Train pickedTrain = null; float tX, tY;
            closest = 100f;

            foreach (var t in Program.Simulator.Trains.ToArray())
            {
                firstCar = null;
                if (t.LeadLocomotive != null)
                {
                    worldPos = t.LeadLocomotive.WorldPosition;
                    firstCar = t.LeadLocomotive;
                }
                else if (t.Cars != null && t.Cars.Count > 0)
                {
                    worldPos = t.Cars[0].WorldPosition;
                    firstCar = t.Cars[0];

                }
                else
                    continue;

                worldPos = firstCar.WorldPosition;
                tX = ((worldPos.TileX * 2048) - subX + worldPos.Location.X) * xScale;
                tY = CanvasHeight - (((worldPos.TileZ * 2048) - subY + worldPos.Location.Z) * yScale);
                float xSpeedCorr = Math.Abs(t.SpeedMpS) * xScale * 1.5f;
                float ySpeedCorr = Math.Abs(t.SpeedMpS) * yScale * 1.5f;

                if (tX < x - range - xSpeedCorr || tX > x + range + xSpeedCorr || tY < y - range - ySpeedCorr || tY > y + range + ySpeedCorr)
                    continue;
                if (pickedTrain == null)
                    pickedTrain = t;
            }
            PickedTrain = pickedTrain;
            // If a train is picked, will clear the player list selection
            if (PickedTrain != null)
            {
                return new TrainWidget(PickedTrain);
            }
            return null;
        }
        #endregion

        #region Switch, signal and train actions
        private void UnHandleItemPick()
        {
            setSignalMenu.Close();
            setSwitchMenu.Close();
        }

        private void HandlePickedSignal()
        {
            if (MPManager.IsClient() && !MPManager.Instance().AmAider) // Normal client (not server nor aider)
                return;

            setSwitchMenu.Close();
            var picked = signalPickedItem;
            if (picked == null) return;

            allowCallOnMenuItem.IsEnabled = false;
            if (picked.Signal.enabledTrain != null && picked.Signal.CallOnEnabled && !picked.Signal.CallOnManuallyAllowed)
                allowCallOnMenuItem.IsEnabled = true;

            setSignalMenu.Open(mapCanvas);
        }

        private void HandlePickedSwitch()
        {
            if (MPManager.IsClient() && !MPManager.Instance().AmAider) // Normal client (not server nor aider)
                return;

            setSignalMenu.Close();
            if (switchPickedItem == null) return;
            setSwitchMenu.Open(mapCanvas);
        }

        private void HandlePickedTrain()
        {
            trainActionsMenu.Close();
            if (trainPickedItem == null) return;
            PickedTrain = trainPickedItem.Train;
            trainActionsMenu.Open(mapCanvas);
        }

        private void setSignalMenu_ItemClicked(string type)
        {
            var picked = signalPickedItem;
            if (picked == null)
            {
                UnHandleItemPick();
                return;
            }

            var signal = picked.Signal;

            string[] signalAspects = { "system", "stop", "approach", "proceed", "callOn" };
            int numericSignalAspect = Array.IndexOf(signalAspects, type);

            if (MPManager.Instance().AmAider)
            {
                PostToGame(() => MPManager.Notify(new MSGSignalChange(signal, numericSignalAspect).ToString()));
                UnHandleItemPick();
                return;
            }

            PostToGame(() =>
            {
                switch (type)
                {
                    case "system":
                        signal.ClearHoldSignalDispatcher();
                        break;

                    case "stop":
                        signal.RequestHoldSignalDispatcher(true);
                        break;

                    case "approach":
                        signal.RequestApproachAspect();
                        break;

                    case "proceed":
                        signal.RequestLeastRestrictiveAspect();
                        break;

                    case "callOn":
                        signal.SetManualCallOn(true);
                        break;
                }
            });

            mapCanvas.InvalidateVisual(); // Triggers a re-paint
            UnHandleItemPick();
        }

        private void setSwitchMenu_ItemClicked(string type)
        {
            var picked = switchPickedItem;
            if (picked == null)
            {
                UnHandleItemPick(); return;
            }
            var sw = picked.Item.TrJunctionNode;
            var main = (int)picked.main;

            // Aider can send message to the server for a switch
            if (MPManager.IsMultiPlayer() && MPManager.Instance().AmAider)
            {
                var nextSwitchTrack = sw;
                var Selected = 0;
                switch (type)
                {
                    case "mainRoute":
                        Selected = main;
                        break;
                    case "sideRoute":
                        Selected = 1 - main;
                        break;
                }
                // Aider selects and throws the switch, but need to confirm by the dispatcher
                PostToGame(() =>
                {
                    MPManager.Notify(new MSGSwitch(MPManager.GetUserName(),
                        nextSwitchTrack.TN.UiD.WorldTileX, nextSwitchTrack.TN.UiD.WorldTileZ, nextSwitchTrack.TN.UiD.WorldId, Selected, true).ToString());
                    Program.Simulator.Confirmer.Information(Viewer.Catalog.GetString("Switching Request Sent to the Server"));
                });
            }
            else // Server throws the switch immediately
            {
                PostToGame(() =>
                {
                    switch (type)
                    {
                        case "mainRoute":
                            Program.Simulator.Signals.RequestSetSwitch(sw.TN, main);
                            break;
                        case "sideRoute":
                            Program.Simulator.Signals.RequestSetSwitch(sw.TN, 1 - main);
                            break;
                    }
                });
            }
            mapCanvas.InvalidateVisual(); // Triggers a re-paint
            UnHandleItemPick();
        }

        public bool ClickedTrain
        {
            get => clickedTrain;
            set => clickedTrain = value;
        }
        volatile bool clickedTrain;

        /// <summary>
        /// Ask the viewer to jump to <see cref="PickedTrain"/>
        /// </summary>
        void JumpToPickedTrain()
        {
            // The viewer does not cope with a null train
            if (PickedTrain != null)
                ClickedTrain = true;
        }

        private void trainActionsMenu_ItemClicked(string type)
        {
            switch (type)
            {
                case "seeInGame":
                    JumpToPickedTrain();
                    break;

                case "followOnMap":
                    FollowTrain = true;
                    GenerateView();
                    break;
            }
        }
        #endregion

        #region Multiplayer players and messages
        ListBoxItem CreatePlayerItem(string name, string text, FontWeight weight, Color? color)
        {
            var item = new ListBoxItem { Content = text, Tag = name, FontWeight = weight };
            if (color != null)
                item.Foreground = new SolidColorBrush(color.Value);
            item.AddHandler(InputElement.PointerReleasedEvent, (s, e) =>
            {
                if (e.InitialPressMouseButton == MouseButton.Right)
                    playersView_RightClick(item);
            }, RoutingStrategies.Bubble, handledEventsToo: true);
            return item;
        }

        static string TrimBracketsFromEnd(string input)
        {
            string pattern = @"\s\[[^\]]*\]\s*$";
            string result = Regex.Replace(input, pattern, "");

            return result;
        }

        private void playersView_RightClick(ListBoxItem item)
        {
            var player = item.Tag as string ?? TrimBracketsFromEnd(item.Content as string ?? "");
            playersView.SelectedItems.Clear();
            playersView.SelectedItem = item;

            if (player != MPManager.GetUserName())
            {
                makeThisPlayerAnAssistantMenuItem.Header = MPManager.Instance().aiderList.Contains(player) ? Viewer.Catalog.GetString("Demote this player") : Viewer.Catalog.GetString("Make this player an assistant");
                var isDisconnected = MPManager.Instance().lostPlayer.ContainsKey(player);
                makeThisPlayerAnAssistantMenuItem.IsEnabled = !isDisconnected;
                jumpToThisPlayerInGameMenuItem.IsEnabled = !isDisconnected;
                followPlayerMenuItem.IsEnabled = !isDisconnected;
                // TODO: Figure out a way to allow removing disconnected players

                playerMenuItem.Header = player;
                playerActionsMenu.Open(item);
            }
        }

        private void playerActionsMenu_ItemClicked(string type)
        {
            var player = playerMenuItem.Header as string;
            if (player == null) return;

            switch (type)
            {
                case "assistant":
                    PostToGame(() =>
                    {
                        if (!MPManager.OnlineTrains.Players.ContainsKey(player)) return;

                        if (MPManager.Instance().aiderList.Contains(player))
                        {
                            // Selected player is already an assistant, so the intent is to demote them
                            MPManager.BroadCast(new MSGAider(player, false).ToString());
                            MPManager.Instance().aiderList.Remove(player);
                        }
                        else
                        {
                            // Promote player to be an assistant
                            MPManager.BroadCast(new MSGAider(player, true).ToString());
                            MPManager.Instance().aiderList.Add(player);
                        }
                    });
                    break;

                case "seeInGame":
                    MPManager.OnlineTrains.Players.TryGetValue(player, out OnlinePlayer p);
                    PickedTrain = p?.Train;
                    JumpToPickedTrain();
                    break;

                case "followOnMap":
                    MPManager.OnlineTrains.Players.TryGetValue(player, out OnlinePlayer p1);
                    PickedTrain = p1?.Train;
                    FollowTrain = true;
                    GenerateView();
                    break;

                case "kick":
                    if (!MPManager.IsServer()) return;
                    PostToGame(() =>
                    {
                        if (MPManager.OnlineTrains.Players.ContainsKey(player))
                        {
                            MPManager.OnlineTrains.Players[player].status = OnlinePlayer.Status.Removed;
                            MPManager.BroadCast(new MSGMessage(player, "Error", "Sorry the server has removed you").ToString());
                            return;
                        }
                        lock (MPManager.Instance().lostPlayer)
                        {
                            if (MPManager.Instance().lostPlayer != null && MPManager.Instance().lostPlayer.ContainsKey(player))
                            {
                                MPManager.Instance().lostPlayer[player].quitTime = MPManager.Simulator.GameTime - 700;
                            }
                        }
                    });
                    break;
            }
        }

        public bool AddNewMessage(double _, string msg)
        {
            if (messages == null) return false;
            if (messages.Items.Count > 30) messages.Items.RemoveAt(0);

            messages.Items.Add(msg);
            messages.ScrollIntoView(messages.Items.Count - 1);

            return true;
        }

        static string CleanMessage(string message)
        {
            return (message ?? "").Replace("\r", "").Replace("\t", "");
        }

        private void messageAllButton_Click()
        {
            if (!MPManager.IsMultiPlayer()) return;

            var message = CleanMessage(messageInput.Text);
            MPManager.Instance().ComposingText = false;

            if (message == "") return;
            messageInput.Text = "";

            PostToGame(() =>
            {
                if (MPManager.IsServer())
                {
                    var users = MPManager.OnlineTrains.Players.Keys
                        .Select((string u) => $"{u}\r");
                    string user = string.Join("", users) + "0END";
                    string msgText = new MSGText(MPManager.GetUserName(), user, message).ToString();
                    try
                    {
                        MPManager.Notify(msgText);
                    }
                    catch { }
                }
                else
                {
                    var user = "0Server\r+0END";
                    MPManager.Notify(new MSGText(MPManager.GetUserName(), user, message).ToString());
                }
            });
        }

        private void messageInput_Enter()
        {
            MPManager.Instance().ComposingText = true;
        }

        private void messageInput_Leave()
        {
            MPManager.Instance().ComposingText = false;
        }

        private void moreReplyOptionsButton_Click(Control button)
        {
            messageActionsMenu.Open(button);
        }

        private void messageActionsMenu_ItemClicked(string type)
        {
            switch (type)
            {
                case "message":
                    {
                        if (!MPManager.IsMultiPlayer()) return;

                        var message = CleanMessage(messageInput.Text);
                        messageInput.Text = "";
                        if (message == "") return;
                        var users = "";

                        if (playersView.SelectedItems.Count > 0)
                        {
                            foreach (var chosen in playersView.SelectedItems.OfType<ListBoxItem>())
                            {
                                var name = chosen.Tag as string;
                                if (name == null || name == MPManager.GetUserName())
                                    continue;
                                users += name + "\r";
                            }
                            users += "0END";
                        }
                        else { return; }

                        PostToGame(() => MPManager.Notify(new MSGText(MPManager.GetUserName(), users, message).ToString()));
                        break;
                    }

                case "reply":
                    {
                        if (!MPManager.IsMultiPlayer()) return;

                        var message = CleanMessage(messageInput.Text);
                        MPManager.Instance().ComposingText = false;
                        messageInput.Text = "";
                        if (message == "") return;
                        var users = "";

                        if (messages.SelectedItems.Count > 0)
                        {
                            foreach (var chosen in messages.SelectedItems.OfType<string>())
                            {
                                var tmp = chosen;
                                var index = tmp.IndexOf(':');
                                if (index < 0) continue;
                                tmp = tmp.Substring(0, index) + "\r";
                                if (users.Contains(tmp)) continue;
                                users += tmp;
                            }
                            users += "0END";
                        }
                        else { return; }

                        PostToGame(() => MPManager.Notify(new MSGText(MPManager.GetUserName(), users, message).ToString()));
                        break;
                    }
            }
        }
        #endregion
    }

    /// <summary>
    /// The map drawing surface
    /// </summary>
    public class MapCanvas : Control
    {
        readonly MapViewer Viewer;

        public MapCanvas(MapViewer viewer)
        {
            Viewer = viewer;
        }

        public override void Render(DrawingContext context)
        {
            Viewer.Draw(context, Bounds.Size);
        }
    }
}
