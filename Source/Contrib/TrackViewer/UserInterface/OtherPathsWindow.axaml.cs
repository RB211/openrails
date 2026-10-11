// COPYRIGHT 2026 by the Open Rails project.
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
//
using System;
using System.Collections.Generic;
using System.Linq;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Media;
using ORTS.TrackViewer.Drawing;

namespace ORTS.TrackViewer.UserInterface
{
    /// <summary>
    /// Interaction logic for OtherPathsWindow.axaml
    /// The window allows to select an arbitrary number of paths that will be drawn on the tracks.
    /// The window lives on the Avalonia UI thread, DrawMultiplePaths on the game thread.
    /// </summary>
    public sealed partial class OtherPathsWindow : Window
    {
        /// <summary>The DrawMultiplePaths that contains the multiple paths from which a user can make a selection</summary>
        private readonly DrawMultiplePaths multiPaths;
        private readonly string[] pathNames;
        /// <summary>While setting IsChecked programmatically, prevent callbacks on changed to do something</summary>
        private bool NoClickAction;

        /// <summary>Parameterless constructor for the XAML loader only</summary>
        public OtherPathsWindow() { InitializeComponent(); }

        /// <summary>
        /// Constructor (on the UI thread)
        /// </summary>
        /// <param name="drawMultiPaths">The object containing the information on the available and selected paths</param>
        /// <param name="pathNames">The names of the paths (determined on the game thread)</param>
        public OtherPathsWindow(DrawMultiplePaths drawMultiPaths, string[] pathNames)
        {
            InitializeComponent();
            this.multiPaths = drawMultiPaths;
            this.pathNames = pathNames;
            this.listOfPaths.Children.Clear();

            foreach (string pathName in pathNames)
            {
                CheckBox checkBox = new CheckBox
                {
                    Content = pathName
                };
                checkBox.Click += CheckBox_Click;
                listOfPaths.Children.Add(checkBox);
            }

            UpdateFromGame(null);
        }

        /// <summary>
        /// Perform an (optional) action on the game thread, and then update the colors of all checkboxes
        /// </summary>
        private void UpdateFromGame(Action<DrawMultiplePaths> action)
        {
            DrawMultiplePaths paths = multiPaths;
            string[] names = pathNames;
            GameThread.Post(() =>
            {
                action?.Invoke(paths);
                var colors = names.ToDictionary(n => n, n => paths.ColorOf(n));
                UiThread.Post(() => RecolorAll(colors));
            });
        }

        /// <summary>
        /// User has selected to clear all paths.
        /// </summary>
        private void ClearOtherPathsButton_Click(object sender, RoutedEventArgs e)
        {
            UpdateFromGame(paths => paths.ClearAll());
        }

        /// <summary>
        /// For all available paths, set the color as well as the checkmark, based on the
        /// state in the DrawMultiPaths. Basically should be called whenever the state changes
        /// </summary>
        private void RecolorAll(Dictionary<string, Microsoft.Xna.Framework.Color?> colors)
        {
            NoClickAction = true;
            foreach (var item in listOfPaths.Children)
            {
                if (item is CheckBox checkBox)
                {
                    string pathName = (string)checkBox.Content;
                    colors.TryGetValue(pathName, out Microsoft.Xna.Framework.Color? xnaColor);
                    if (xnaColor == null)
                    {
                        checkBox.IsChecked = false;
                        checkBox.Foreground = new SolidColorBrush(Color.FromArgb(255, 0, 0, 0));
                    }
                    else
                    {
                        checkBox.IsChecked = true;
                        checkBox.Foreground = new SolidColorBrush(Color.FromArgb(xnaColor.Value.A, xnaColor.Value.R, xnaColor.Value.G, xnaColor.Value.B));
                    }
                }
            }
            NoClickAction = false;
        }

        private void CheckBox_Click(object sender, RoutedEventArgs e)
        {
            if (NoClickAction) return;
            CheckBox selectedCheckBox = (CheckBox)sender;
            string value = (string)selectedCheckBox.Content;
            bool enable = selectedCheckBox.IsChecked == true;
            UpdateFromGame(paths => paths.SetSelection(value, enable));
        }

        private void AlwaysOnTop_Click(object sender, RoutedEventArgs e)
        {
            this.Topmost = AlwaysOnTop.IsChecked == true;
        }
    }
}
