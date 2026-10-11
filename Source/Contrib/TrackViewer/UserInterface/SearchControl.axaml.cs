// COPYRIGHT 2014 by the Open Rails project.
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
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;

namespace ORTS.TrackViewer.UserInterface
{
    /// <summary>
    /// The kind of items that can be searched from the search control
    /// </summary>
    public enum SearchableItem {
        /// <summary>Search for (rail) track node</summary>
        TrackNode,
        /// <summary>Search for (rail) track item</summary>
        TrackItem,
        /// <summary>Search for road track node</summary>
        TrackNodeRoad,
        /// <summary>Search for road track item</summary>
        TrackItemRoad,
    }

    /// <summary>
    /// Interaction logic for SearchControl.axaml
    /// This contains the callbacks needed for the (small) searchwindow/searchcontrol.
    /// Lives on the Avalonia UI thread; the actual search is done on the game thread.
    /// </summary>
    public sealed partial class SearchControl : Window
    {
        readonly TrackViewer trackViewer;
        readonly SearchableItem searchItem;

        /// <summary>Parameterless constructor for the XAML loader only</summary>
        public SearchControl() { InitializeComponent(); }

        /// <summary>
        /// Constructor
        /// </summary>
        /// <param name="trackViewer">Trackviewer so we can perform a callback</param>
        /// <param name="searchItem">Type of item that needs to be searched</param>
        public SearchControl(TrackViewer trackViewer, SearchableItem searchItem)
        {
            InitializeComponent();
            this.trackViewer = trackViewer;
            this.searchItem = searchItem;
            Dialogs.DigitsOnly(textboxIndex);
            Opened += (s, e) => textboxIndex.Focus();
            switch (searchItem)
            {
                case SearchableItem.TrackNode:
                    labelType.Text = "trackNode index = ";
                    break;
                case SearchableItem.TrackItem:
                    labelType.Text = "trackItem index = ";
                    break;
                case SearchableItem.TrackNodeRoad:
                    labelType.Text = "road trackNode index = ";
                    break;
                case SearchableItem.TrackItemRoad:
                    labelType.Text = "road trackItem index = ";
                    break;
            }
        }

        /// <summary>
        /// If the search button has been clicked, validate the input and call the trackViewewer callback
        /// </summary>
        private void ButtonSearch_Click(object sender, RoutedEventArgs e)
        {
            if (!int.TryParse(textboxIndex.Text, System.Globalization.NumberStyles.Integer, System.Globalization.CultureInfo.CurrentCulture, out int index))
            {   // clear text if not an integer. Should not happen because we limit possible input
                textboxIndex.Text = "";
                return;
            }
            SearchableItem item = searchItem;
            TrackViewer viewer = trackViewer;
            GameThread.Post(() =>
            {
                if (viewer.DrawTrackDB == null) return;
                switch (item)
                {
                    case SearchableItem.TrackNode:
                        viewer.CenterAroundTrackNode(index);
                        break;
                    case SearchableItem.TrackItem:
                        viewer.CenterAroundTrackItem(index);
                        break;
                    case SearchableItem.TrackNodeRoad:
                        viewer.CenterAroundTrackNodeRoad(index);
                        break;
                    case SearchableItem.TrackItemRoad:
                        viewer.CenterAroundTrackItemRoad(index);
                        break;
                }
            });
        }

        private void ButtonCancel_Click(object sender, RoutedEventArgs e)
        {
            this.Close();
        }

        /// <summary>
        /// Handle an enter/return press on the textbox. If return is pressed, just do the same as the search button.
        /// </summary>
        private void OnKeyDownHandler(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Return || e.Key == Key.Enter)
            {
                ButtonSearch_Click(sender, e);
            }
        }
    }
}
