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
using System.Linq;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Layout;
using Avalonia.Media;
using LibAE.Formats;
using Orts.Formats.OR;

namespace ActivityEditor.Route_Metadata
{
    public partial class StationDisplay : Window
    {
        static readonly string ColumnWidths = "94,94,60,60,60,60,60,*";

        StationItem station;
        TextBox StationName;
        TextBlock label7;
        Grid tablePaths;

        public StationDisplay(StationItem item)
        {
            int countPaths = 0;
            station = item;
            InitializeComponent();
            StationName.Text = station.nameStation;
            ClearTable();
            foreach (var pointArea in item.stationArea)
            {
                if (pointArea.IsInterface())
                {
                    countPaths += pointArea.getStationConnector().stationPaths.getPaths().Count;
                }
            }
            if (countPaths > 0 && countPaths != item.StationPathsHelper.DefinedPath.Count)
            {
                this.label7.Text = "Please Load Paths:";
            }
            else
            {
                label7.Text = "List of Paths seems OK:";
            }
            foreach (var originPoint in station.StationPathsHelper.DefinedPath)
            {
                foreach (var destinPoint in originPoint.Value)
                {
                    foreach (var possibility in destinPoint.Value)
                    {
                        AddPathData(originPoint.Key, possibility.Value);
                    }
                }
            }
        }

        void InitializeComponent()
        {
            Title = "Station Configuration";
            Width = 949;
            Height = 516;
            WindowStartupLocation = WindowStartupLocation.CenterOwner;

            StationName = new TextBox { Width = 180 };
            var quit = new Button { Content = "Quit", Width = 89 };
            quit.Click += QuitStationConfig;
            var loadPaths = new Button { Content = "Load Paths", Width = 85 };
            loadPaths.Click += LoadPaths;
            label7 = new TextBlock { Text = "label7", VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(12, 0) };

            var top = new StackPanel
            {
                Orientation = Orientation.Horizontal,
                Spacing = 8,
                Margin = new Thickness(8),
                Children = { new TextBlock { Text = "Station :", VerticalAlignment = VerticalAlignment.Center }, StationName, label7, loadPaths, quit },
            };
            DockPanel.SetDock(top, Dock.Top);

            var header = new Grid { ColumnDefinitions = new ColumnDefinitions(ColumnWidths), Background = Brushes.Gainsboro, Margin = new Thickness(8, 0) };
            string[] titles = { "From", " To ", "Max. Platform Length", "Max. Siding Length", "Number of Platform", "Number of Siding", "Max. Passing Length" };
            for (int i = 0; i < titles.Length; i++)
            {
                var text = new TextBlock { Text = titles[i], TextWrapping = TextWrapping.Wrap, Margin = new Thickness(3), FontWeight = FontWeight.SemiBold };
                Grid.SetColumn(text, i);
                header.Children.Add(text);
            }
            DockPanel.SetDock(header, Dock.Top);

            tablePaths = new Grid { ColumnDefinitions = new ColumnDefinitions(ColumnWidths), Margin = new Thickness(8, 0) };
            var root = new DockPanel();
            root.Children.Add(top);
            root.Children.Add(header);
            root.Children.Add(new ScrollViewer { Content = tablePaths });
            Content = root;
        }

        public void ClearTable()
        {
            tablePaths.Children.Clear();
            tablePaths.RowDefinitions.Clear();
        }

        private void QuitStationConfig(object sender, RoutedEventArgs e)
        {
            Close();
        }

        void AddCell(string text, int column, int row)
        {
            var label = new TextBlock { Text = text, Margin = new Thickness(3, 1) };
            Grid.SetColumn(label, column);
            Grid.SetRow(label, row);
            tablePaths.Children.Add(label);
        }

        public void AddPathData(string label, StationPath path)
        {
            double passingYard = 0;
            if (path == null)
                return;
            string cinBoth;
            if (path.MainPath)
                cinBoth = "M - " + label;
            else
                cinBoth = label;
            int nbrGlobalItem = path.ComponentItem.Count - 1;
            string coutBoth;
            GlobalItem lastItem = (GlobalItem)(path.ComponentItem[nbrGlobalItem]);
            if (lastItem.GetType() == typeof(AEBufferItem) && lastItem.associateNode.TrEndNode)
            {
                coutBoth = ((AEBufferItem)lastItem).NameBuffer;
            }
            else if (lastItem.GetType() == typeof(TrackSegment) && lastItem.associateNode.TrJunctionNode == null)
            {
                coutBoth = ((TrackSegment)(lastItem)).HasConnector.label;
                if ((path.ComponentItem[0]).GetType() == typeof(TrackSegment))
                    passingYard = Math.Round(path.PassingYard, 1);
            }
            else
                coutBoth = "###";

            int row = tablePaths.RowDefinitions.Count;
            tablePaths.RowDefinitions.Add(new RowDefinition(GridLength.Auto));
            AddCell(cinBoth, 0, row);
            AddCell(coutBoth, 1, row);
            AddCell(Math.Round(path.Platform, 1).ToString(), 2, row);
            AddCell(Math.Round(path.Siding, 1).ToString(), 3, row);
            AddCell(path.NbrPlatform.ToString(), 4, row);
            AddCell(path.NbrSiding.ToString(), 5, row);
            AddCell(passingYard.ToString(), 6, row);
        }

        private void LoadPaths(object sender, RoutedEventArgs e)
        {
            station.StationPathsHelper.Reload();
            foreach (var originPoint in station.StationPathsHelper.DefinedPath)
            {
                foreach (var destinPoint in originPoint.Value)
                {
                    foreach (var possibility in destinPoint.Value)
                    {
                        AddPathData(originPoint.Key, possibility.Value);
                    }
                }
            }
            this.label7.Text = "Loaded:";
        }
    }
}
