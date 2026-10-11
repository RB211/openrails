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

/// This module ...
/// 
/// Author: Stéfan Paitoni
/// Updates : 
/// 


using System;
using System.IO;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using ActivityEditor;
using Avalonia.Controls;
using Avalonia.Layout;
using System.Drawing;
using LibAE;
using LibAE.Formats;
using Orts.Formats.Msts;
using Orts.Parsers.Msts;
using ORTS;
using Orts.Formats.OR;

namespace ActivityEditor.Engine
{
    public class AERouteConfig
    {
        public Grid MainPanel;

        public ORRouteConfig orRouteConfig { get { return Viewer.Simulator.orRouteConfig; } protected set { } }
        public StackPanel TagPanel;
        public StackPanel StationPanel;
        //  MSTS data
        public TrackNode[] nodes { get { return simulator.nodes; } set { } }
        AEConfig Parent;
                
        public TrackSectionsFile TSectionDat { get { return Viewer.Simulator.TSectionDat; } protected set { } }
        public Viewer2D Viewer { get { return Parent.Viewer; } protected set { } }
        public PseudoSim simulator { get { return Viewer.Simulator; } protected set { } }

        public AERouteConfig(AEConfig parent)
        {
            Parent = parent;
        }

        public string getRouteName()
        {
            return orRouteConfig.RouteName;
        }

        public void LoadPanels(string ident, GroupBox routeData)
        {
            // Two scrolling lists (tags and stations), one above the other, in the "routeData" group box.
            routeData.Text = ident;
            TagPanel = new StackPanel { Orientation = Orientation.Vertical };
            StationPanel = new StackPanel { Orientation = Orientation.Vertical };
            MainPanel = new Grid { RowDefinitions = new RowDefinitions("*,*") };
            var tagScroller = new ScrollViewer { Content = TagPanel, Margin = new Avalonia.Thickness(0, 0, 0, 4) };
            var stationScroller = new ScrollViewer { Content = StationPanel };
            Grid.SetRow(stationScroller, 1);
            MainPanel.Children.Add(tagScroller);
            MainPanel.Children.Add(stationScroller);
            routeData.Content = MainPanel;
            // Tags and stations may have been loaded before the panels were created.
            if (Parent.tagGBList != null)
                AddTagPanel(Parent.tagGBList.ToArray());
            if (Parent.stationGBList != null)
                AddStationPanel(Parent.stationGBList.ToArray());
        }

        /// <summary>Show this route's panels in the (shared) "routeData" group box.</summary>
        public void ShowPanels(GroupBox routeData)
        {
            if (MainPanel != null && routeData.Content != MainPanel)
            {
                (MainPanel.Parent as Panel)?.Children.Remove(MainPanel);
                routeData.Content = MainPanel;
            }
        }

        public void AddTagPanel(GroupBox[] groupBoxList)
        {
            if (this.TagPanel == null)
                return;
            this.TagPanel.Children.Clear();
            this.TagPanel.Children.AddRange(groupBoxList);
        }

        public void AddStationPanel(GroupBox[] groupBoxList)
        {
            if (this.StationPanel == null)
                return;
            this.StationPanel.Children.Clear();
            this.StationPanel.Children.AddRange(groupBoxList);
        }

        public System.Drawing.Point GetTagPanelPosition()
        {
            System.Drawing.Point current = new System.Drawing.Point(0, 0);
            return current;
        }

        public System.Drawing.Point GetStationPanelPosition()
        {
            System.Drawing.Point current = new System.Drawing.Point(0, 0);
            return current;
        }

        public void SaveRoute()
        {
            orRouteConfig.SaveConfig();
        }

        public void CloseRoute()
        {
            SaveRoute();
        }

        private GlobalItem findItemFromMouse(int x, int y, int range)
        {
            if (range < 5) range = 5;
            double closest = float.NaN;
            if (orRouteConfig == null)
                return null;
            GlobalItem closestItem = null;
            foreach (var item in orRouteConfig.AllItems)
            {
                if (item.Location2D.X < x - range || item.Location2D.X > x + range
                   || item.Location2D.Y < y - range || item.Location2D.Y > y + range) continue;
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
                return closestItem;
            }
#if ZORRO
            foreach (var item in switchItemsDrawn)
            {
                //if out of range, continue
                if (item.Location2D.X < x - range || item.Location2D.X > x + range
                   || item.Location2D.Y < y - range || item.Location2D.Y > y + range) continue;

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
            foreach (var item in signalItemsDrawn)
            {
                //if out of range, continue
                if (item.Location2D.X < x - range || item.Location2D.X > x + range
                   || item.Location2D.Y < y - range || item.Location2D.Y > y + range) continue;

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
                if (closestItem != null) { switchPickedTime = simulator.GameTime; return closestItem; }
            }

            //now check for trains (first car only)
            TrainCar firstCar;
            PickedTrain = null; float tX, tY;
            closest = 100f;

            foreach (var t in Program.Simulator.Trains)
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
                else continue;

                worldPos = firstCar.WorldPosition;
                tX = (worldPos.TileX * 2048 + worldPos.Location.X - subX) * xScale; tY = pictureBox1.Height - (worldPos.TileZ * 2048 + worldPos.Location.Z - subY) * yScale;

                if (tX < x - range || tX > x + range || tY < y - range || tY > y + range) continue;
                if (PickedTrain == null) PickedTrain = t;
            }
            //if a train is picked, will clear the avatar list selection
            if (PickedTrain != null)
            {
                AvatarView.SelectedItems.Clear();
                return new TrainWidget(PickedTrain);
            }
#endif
            return null;
        }

        public dVector getVectorNextNode(int nodeIdx, TrPin fromPin)
        {
            TrackNode toNext = nodes[fromPin.Link];
            if (nodeIdx > fromPin.Link && toNext.TrJunctionNode == null)
                return null;
            dVector nextVector;
            int direction = fromPin.Direction;
            TrVectorSection item;
            if (toNext.UiD == null)
            {
                if (direction == 1)
                {
                    item = toNext.TrVectorNode.TrVectorSections.First();
                }
                else
                {
                    item = toNext.TrVectorNode.TrVectorSections.Last();
                }
                nextVector = new dVector((item.TileX * 2048f) + item.X, (item.TileZ * 2048f) + item.Z);
            }
            else
            {
                nextVector = new dVector((toNext.UiD.TileX * 2048f) + toNext.UiD.X, (toNext.UiD.TileZ * 2048f) + toNext.UiD.Z);
            }

            return nextVector;
        }

    }
}