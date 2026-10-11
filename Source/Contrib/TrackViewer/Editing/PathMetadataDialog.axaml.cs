// COPYRIGHT 2014, 2018 by the Open Rails project.
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
using Avalonia.Input;
using Avalonia.Interactivity;

namespace ORTS.TrackViewer.Editing
{
    /// <summary>
    /// Interaction logic for PathMetadataDialog.axaml. Lives on the Avalonia UI thread.
    /// </summary>
    public sealed partial class PathMetadataDialog : Window
    {
        /// <summary>True if the user closed the dialog with OK</summary>
        public bool DialogResult { get; private set; }

        /// <summary>Parameterless constructor for the XAML loader only</summary>
        public PathMetadataDialog() { InitializeComponent(); }

        /// <summary>
        /// Create the dialog to edit the path metadata, and fill it with current values
        /// </summary>
        /// <param name="metadata">Array of string, containing ID, name, start and end</param>
        /// <param name="isPlayerPath">Is the path currently a player path?</param>
        public PathMetadataDialog(string[] metadata, bool isPlayerPath)
        {
            InitializeComponent();
            pathID.Text    = metadata[0];
            pathName.Text  = metadata[1];
            pathStart.Text = metadata[2];
            pathEnd.Text   = metadata[3];
            pathIsPlayerPath.IsChecked = isPlayerPath;
            Opened += (s, e) =>
            {
                pathID.Focus();
                pathID.SelectAll();
            };
        }

        private void ButtonOK_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = true;
            Close();
        }

        private void ButtonCancel_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = false;
            Close();
        }

        /// <summary>
        /// Return the meta data of the path (ID, name, start and end) as edited by the user.
        /// </summary>
        /// <returns>string array containing the meta data</returns>
        public string[] GetMetadata()
        {
            string[] metadata = { pathID.Text ?? "", pathName.Text ?? "", pathStart.Text ?? "", pathEnd.Text ?? "", (pathIsPlayerPath.IsChecked == true).ToString() };
            return metadata;
        }

        private void PathX_GotFocus(object sender, FocusChangedEventArgs e)
        {
            ((TextBox)sender).SelectAll();
        }
    }
}
