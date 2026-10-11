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
using System.IO;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Layout;
using Avalonia.Platform.Storage;
using LibAE.Formats;
using ActivityEditor.ActionProperties;
using Orts.Formats.OR;

namespace ActivityEditor.Preference
{
    public partial class Options : Window
    {
        List<string> routePaths;

        CheckBox checkBox1, ShowSnap, ShowLabelPlat, showTiles, snapTrack, SnapInfo, showRuler, snapLine, trackInfo;
        NumericUpDown snapCircle, PlSiZoomLevel;
        TextBlock snapCircleLabel, PlSiLabel;
        TextBox MSTSPath, AEPath, CommentAction;
        ListBox ListRoutePaths, ListAvailable, ListUsed;
        Button RemoveRoutePaths;

        public Options()
        {
            InitializeComponent();
            Program.aePreference.UpdateConfig();
            this.checkBox1.IsChecked = Program.aePreference.ShowAllSignal;
            this.ShowSnap.IsChecked = Program.aePreference.ShowSnapCircle;
            this.ShowLabelPlat.IsChecked = Program.aePreference.ShowPlSiLabel;
            this.MSTSPath.Text = Program.aePreference.MSTSPath;
            this.AEPath.Text = Program.aePreference.AEPath;
            ListRoutePaths.ItemsSource = Program.aePreference.RoutePaths.ToList();
            routePaths = new List<string> ();
            this.showTiles.IsChecked = Program.aePreference.ShowTiles;
            this.snapTrack.IsChecked = Program.aePreference.ShowSnapLine;
            this.SnapInfo.IsChecked = Program.aePreference.ShowSnapInfo;
            this.showRuler.IsChecked = Program.aePreference.ShowRuler;
            this.snapLine.IsChecked = Program.aePreference.ShowSnapLine;
            this.trackInfo.IsChecked = Program.aePreference.ShowTrackInfo;
            this.ListAvailable.ItemsSource = Program.aePreference.AvailableActions.ToList();
            this.ListUsed.ItemsSource = Program.aePreference.UsedActions.ToList();
            CheckedChanged(null, null);
            PlSiShow(null, null);
            HookEvents();
        }

        static CheckBox Check(string text)
        {
            return new CheckBox { Content = text };
        }

        void InitializeComponent()
        {
            Title = "Options";
            Width = 560;
            Height = 380;
            WindowStartupLocation = WindowStartupLocation.CenterOwner;

            // Editor Options
            checkBox1 = Check("Show all Signal");
            ShowSnap = Check("Show Snap circle");
            ShowLabelPlat = Check("Show Platform Label");
            snapCircleLabel = new TextBlock { Text = "Size of snap circle :", VerticalAlignment = VerticalAlignment.Center };
            snapCircle = new NumericUpDown { Minimum = 2, Maximum = 20, Value = 10, Increment = 1, Width = 120, FormatString = "0" };
            PlSiLabel = new TextBlock { Text = "Zoom level :", VerticalAlignment = VerticalAlignment.Center };
            PlSiZoomLevel = new NumericUpDown { Minimum = 0.25m, Maximum = 1.5m, Value = 1, Increment = 0.05m, Width = 120, FormatString = "0.00" };
            var editorOptions = new StackPanel
            {
                Margin = new Thickness(8),
                Spacing = 6,
                Children =
                {
                    new TextBlock { Text = "Editor Preference", FontSize = 16 },
                    checkBox1,
                    new StackPanel { Orientation = Orientation.Horizontal, Spacing = 12, Children = { ShowSnap, snapCircleLabel, snapCircle } },
                    new StackPanel { Orientation = Orientation.Horizontal, Spacing = 12, Children = { ShowLabelPlat, PlSiLabel, PlSiZoomLevel } },
                },
            };

            // Path Options
            MSTSPath = new TextBox { Width = 300 };
            var browseMSTSPath = new Button { Content = "Browse" };
            browseMSTSPath.Click += browseMSTSPath_Click;
            ListRoutePaths = new ListBox { Height = 110 };
            var AddRoutePaths = new Button { Content = "Add", Width = 75 };
            AddRoutePaths.Click += AddRoutePaths_Click;
            RemoveRoutePaths = new Button { Content = "Remove", Width = 75 };
            RemoveRoutePaths.Click += RemoveRoutePaths_Click;
            AEPath = new TextBox { Width = 300, IsReadOnly = true };
            var pathOptions = new StackPanel
            {
                Margin = new Thickness(8),
                Spacing = 6,
                Children =
                {
                    new TextBlock { Text = "Please, configure your paths", FontSize = 16 },
                    new StackPanel { Orientation = Orientation.Horizontal, Spacing = 6, Children = { new TextBlock { Text = "MSTS Path :", Width = 100, VerticalAlignment = VerticalAlignment.Center }, MSTSPath, browseMSTSPath } },
                    new TextBlock { Text = "Route Paths" },
                    new Grid
                    {
                        ColumnDefinitions = new ColumnDefinitions("*,Auto"),
                        Children =
                        {
                            ListRoutePaths,
                            WithColumn(new StackPanel { Spacing = 4, Margin = new Thickness(6, 0, 0, 0), Children = { AddRoutePaths, RemoveRoutePaths } }, 1),
                        },
                    },
                    new StackPanel { Orientation = Orientation.Horizontal, Spacing = 6, Children = { new TextBlock { Text = "Activity Editor", Width = 100, VerticalAlignment = VerticalAlignment.Center }, AEPath } },
                },
            };

            // Debug Options
            showTiles = Check("Show Tiles");
            trackInfo = Check("Show Track Info");
            showRuler = Check("Show Ruler");
            snapLine = Check("Show Snap Line");
            SnapInfo = Check("Show Snap Info");
            snapTrack = Check("Show Snap Track Info");
            var debugOptions = new StackPanel
            {
                Margin = new Thickness(8),
                Spacing = 6,
                Children = { new TextBlock { Text = "Editor Preference", FontSize = 16 }, showTiles, trackInfo, showRuler, snapLine, SnapInfo, snapTrack },
            };

            // Actions Configuration
            ListAvailable = new ListBox { Height = 160 };
            ListAvailable.Tapped += ShowCommentAvailable;
            ListUsed = new ListBox { Height = 160 };
            ListUsed.Tapped += ShowCommentUsed;
            ListUsed.DoubleTapped += EditProperties;
            ListUsed.PointerReleased += MouseDownUsed;
            var AddAction = new Button { Content = "Add >" };
            AddAction.Click += AddToUsed;
            var RemoveAction = new Button { Content = "< remove" };
            RemoveAction.Click += RemoveFromUsed;
            CommentAction = new TextBox { IsReadOnly = true, TextWrapping = Avalonia.Media.TextWrapping.Wrap, Height = 50 };
            var actionsOptions = new StackPanel
            {
                Margin = new Thickness(8),
                Spacing = 6,
                Children =
                {
                    new TextBlock { Text = "Configuration for Generic Auxiliary Actions", FontSize = 16 },
                    new Grid
                    {
                        ColumnDefinitions = new ColumnDefinitions("*,Auto,*"),
                        Children =
                        {
                            ListAvailable,
                            WithColumn(new StackPanel { Spacing = 4, Margin = new Thickness(6, 0), VerticalAlignment = VerticalAlignment.Center, Children = { AddAction, RemoveAction } }, 1),
                            WithColumn(ListUsed, 2),
                        },
                    },
                    CommentAction,
                },
            };

            var tabControl1 = new TabControl();
            tabControl1.Items.Add(new TabItem { Header = "Editor Options", Content = editorOptions, FontSize = 15 });
            tabControl1.Items.Add(new TabItem { Header = "Path Options", Content = pathOptions, FontSize = 15 });
            tabControl1.Items.Add(new TabItem { Header = "Debug Options", Content = debugOptions, FontSize = 15 });
            tabControl1.Items.Add(new TabItem { Header = "Actions Configuration", Content = actionsOptions, FontSize = 15 });

            var OptionOK = new Button { Content = "OK", Width = 75, HorizontalContentAlignment = HorizontalAlignment.Center, HorizontalAlignment = HorizontalAlignment.Right, Margin = new Thickness(8) };
            OptionOK.Click += optionOK_click;
            DockPanel.SetDock(OptionOK, Dock.Bottom);
            Content = new DockPanel { Children = { OptionOK, tabControl1 } };
        }

        void HookEvents()
        {
            checkBox1.IsCheckedChanged += checkBox1_CheckedChanged;
            ShowSnap.IsCheckedChanged += CheckedChanged;
            ShowLabelPlat.IsCheckedChanged += PlSiShow;
            snapCircle.ValueChanged += snapCircle_ValueChanged;
            PlSiZoomLevel.ValueChanged += PlSiValue;
            showTiles.IsCheckedChanged += showTiles_CheckedChanged;
            snapTrack.IsCheckedChanged += snapTrack_CheckedChanged;
            SnapInfo.IsCheckedChanged += SnapInfo_CheckedChanged;
            showRuler.IsCheckedChanged += showRuler_CheckedChanged;
            snapLine.IsCheckedChanged += snapLine_CheckedChanged;
            trackInfo.IsCheckedChanged += trackInfo_changed;
        }

        static Control WithColumn(Control control, int column)
        {
            Grid.SetColumn(control, column);
            return control;
        }

        async Task<string> BrowseFolder(string title)
        {
            var folders = await StorageProvider.OpenFolderPickerAsync(new FolderPickerOpenOptions { Title = title, AllowMultiple = false });
            return folders.Count > 0 ? folders[0].TryGetLocalPath() : null;
        }

        void RefreshRoutePaths()
        {
            ListRoutePaths.ItemsSource = Program.aePreference.RoutePaths.ToList();
            RemoveRoutePaths.IsEnabled = Program.aePreference.RoutePaths.Count > 0;
        }

        private async void browseMSTSPath_Click(object sender, RoutedEventArgs e)
        {
            string path = await BrowseFolder("Select the MSTS folder");
            if (path != null)
            {
                MSTSPath.Text = path;
                Program.aePreference.MSTSPath = path;
                string completeFileName = LibAE.PathCase.Combine(path, "routes");
                if (Directory.Exists(completeFileName))
                {
                    Program.aePreference.RoutePaths.Add(completeFileName);
                    RefreshRoutePaths();
                }
            }
        }

        private async void AddRoutePaths_Click(object sender, RoutedEventArgs e)
        {
            string path = await BrowseFolder("Select a Routes folder");
            if (path != null)
            {
                Program.aePreference.RoutePaths.Add(path);
                RefreshRoutePaths();
            }
        }

        private void RemoveRoutePaths_Click(object sender, RoutedEventArgs e)
        {
            int selected = ListRoutePaths.SelectedIndex;
            if (selected >= 0)
            {
                Program.aePreference.RoutePaths.RemoveAt(selected);
            }
            RefreshRoutePaths();
        }

        private void checkBox1_CheckedChanged(object sender, RoutedEventArgs e)
        {
            Program.aePreference.ShowAllSignal = checkBox1.IsChecked == true;
        }

        private void CheckedChanged(object sender, RoutedEventArgs e)
        {
            bool show = ShowSnap.IsChecked == true;
            this.snapCircle.IsEnabled = show;
            this.snapCircleLabel.IsEnabled = show;
            this.snapCircle.Value = Program.aePreference.getSnapCircle() > 0 ? Program.aePreference.getSnapCircle() : 2;
            Program.aePreference.ShowSnapCircle = show;
        }

        private void PlSiShow(object sender, RoutedEventArgs e)
        {
            bool show = ShowLabelPlat.IsChecked == true;
            this.PlSiZoomLevel.IsEnabled = show;
            this.PlSiLabel.IsEnabled = show;
            this.PlSiZoomLevel.Value = (decimal)(Program.aePreference.getPlSiZoom() > 0 ? Program.aePreference.getPlSiZoom() : 1);
            Program.aePreference.ShowPlSiLabel = show;
            Program.aePreference.PlSiZoom = (float)(this.PlSiZoomLevel.Value ?? 1);
        }

        private void snapCircle_ValueChanged(object sender, NumericUpDownValueChangedEventArgs e)
        {
            Program.aePreference.setSnapCircle((int)(snapCircle.Value ?? 2));
        }

        private void PlSiValue(object sender, NumericUpDownValueChangedEventArgs e)
        {
            Program.aePreference.setPlSiZoom((float)(PlSiZoomLevel.Value ?? 1));
        }

        private void showTiles_CheckedChanged(object sender, RoutedEventArgs e)
        {
            Program.aePreference.ShowTiles = showTiles.IsChecked == true;
        }

        private void snapTrack_CheckedChanged(object sender, RoutedEventArgs e)
        {
            Program.aePreference.ShowSnapLine = snapTrack.IsChecked == true;
        }

        private void SnapInfo_CheckedChanged(object sender, RoutedEventArgs e)
        {
            Program.aePreference.ShowSnapInfo = SnapInfo.IsChecked == true;
        }

        private void showRuler_CheckedChanged(object sender, RoutedEventArgs e)
        {
            Program.aePreference.ShowRuler = showRuler.IsChecked == true;
        }

        private void optionOK_click(object sender, RoutedEventArgs e)
        {
            Close();
            Program.aePreference.ShowAllSignal = this.checkBox1.IsChecked == true;
            Program.aePreference.ShowSnapCircle = this.ShowSnap.IsChecked == true;
            Program.aePreference.ShowPlSiLabel = this.ShowLabelPlat.IsChecked == true;
            Program.aePreference.MSTSPath = this.MSTSPath.Text ?? "";
            Program.aePreference.ShowTiles = this.showTiles.IsChecked == true;
            Program.aePreference.ShowSnapLine = this.snapTrack.IsChecked == true;
            Program.aePreference.ShowSnapInfo = this.SnapInfo.IsChecked == true;
            Program.aePreference.ShowRuler = this.showRuler.IsChecked == true;
            Program.aePreference.ShowSnapLine = snapLine.IsChecked == true;
            Program.aePreference.ShowTrackInfo = this.trackInfo.IsChecked == true;
            Program.aePreference.saveXml();
        }

        private void snapLine_CheckedChanged(object sender, RoutedEventArgs e)
        {
            Program.aePreference.ShowSnapLine = snapLine.IsChecked == true;
        }

        private void trackInfo_changed(object sender, RoutedEventArgs e)
        {
            Program.aePreference.ShowTrackInfo = trackInfo.IsChecked == true;
        }

        private void AddToUsed(object sender, RoutedEventArgs e)
        {
            int selected = ListAvailable.SelectedIndex;
            if (selected >= 0)
            {
                Program.aePreference.AddGenAction(Program.aePreference.AvailableActions[selected]);
                ListUsed.ItemsSource = Program.aePreference.UsedActions.ToList();
            }
        }

        private void RemoveFromUsed(object sender, RoutedEventArgs e)
        {
            int selected = ListUsed.SelectedIndex;
            if (selected >= 0)
            {
                Program.aePreference.RemoveGenAction(selected);
                ListUsed.ItemsSource = Program.aePreference.UsedActions.ToList();
            }
        }

        public async void EditProperties(object sender, RoutedEventArgs e)
        {
            int selected = ListUsed.SelectedIndex;
            if (selected >= 0)
            {
                AuxActionRef action = Program.aePreference.GetAction(selected);
                if (action != null)
                {
                    await EditActionProperties(action);
                }
            }
        }

        async Task EditActionProperties(AuxActionRef action)
        {
            if (action.GetType() == typeof(AuxActionHorn))
                await EditHornProperties(action);
            else if (action.GetType() == typeof(AuxControlStart))
                await EditControlStartProperties(action);
        }

        public async Task EditHornProperties(AuxActionRef action)
        {
            HornProperties hornProperties = new HornProperties(action);
            await Dialogs.ShowDialog(hornProperties);
            ((AuxActionHorn)action).SaveProperties(hornProperties.Action);
        }

        public async Task EditControlStartProperties(AuxActionRef action)
        {
            ControlStartProperties controlStartProperties = new ControlStartProperties(action);
            await Dialogs.ShowDialog(controlStartProperties);
            ((AuxControlStart)action).SaveProperties(controlStartProperties.Action);
        }

        public void ShowCommentUsed(object sender, RoutedEventArgs e)
        {
            int selected = ListUsed.SelectedIndex;
            if (selected >= 0 && selected < Program.aePreference.AvailableActions.Count)
            {
                CommentAction.Text = Program.aePreference.GetComment(Program.aePreference.AvailableActions[selected]);
            }
        }

        public void ShowCommentAvailable(object sender, RoutedEventArgs e)
        {
            int selected = ListAvailable.SelectedIndex;
            if (selected >= 0)
            {
                CommentAction.Text = Program.aePreference.GetComment(Program.aePreference.AvailableActions[selected]);
            }
        }

        private async void MouseDownUsed(object sender, PointerReleasedEventArgs e)
        {
            // Right click on a used action edits its properties.
            if (e.InitialPressMouseButton != MouseButton.Right)
                return;
            int index = ListUsed.SelectedIndex;
            if (index < 0)
                return;
            if (index < Program.aePreference.AvailableActions.Count)
                CommentAction.Text = Program.aePreference.GetComment(Program.aePreference.AvailableActions[index]);
            AuxActionRef action = Program.aePreference.GetAction(index);
            if (action != null)
            {
                await EditActionProperties(action);
            }
        }

    }
}
