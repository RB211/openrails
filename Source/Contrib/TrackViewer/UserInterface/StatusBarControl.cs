// COPYRIGHT 2014, 2015 by the Open Rails project.
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
// The status bar at the bottom of the window. Originally a WPF StatusBar docked in the WinForms game window;
// it is now drawn with MonoGame (on the game thread) so it can be updated every frame without any marshalling.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Microsoft.Xna.Framework;

using Orts.Formats.Msts;
using ORTS.Common;
using ORTS.TrackViewer.Editing;
using ORTS.TrackViewer.Drawing;


namespace ORTS.TrackViewer.UserInterface
{
    /// <summary>
    /// The statusbar at the bottom of the screen shows informational things like location, track items and possibly
    /// details on the track section, the path, ....
    /// Most of the items have a dedicated place in the statusbar. For flexibility the last item is simply a string
    /// that can contain various items (depending on the users setting/choice as well as for debug during development
    /// </summary>
    public sealed class StatusBarControl
    {
        /// <summary>Height of the statusbar in pixels</summary>
        public int StatusbarHeight => Font.Height + 8;

        private TextFont Font => FontManager.Instance.UIFont;

        private int screenWidth;
        private int yBottom;

        // The texts of the various fields
        private string tileXZ = "";
        private string locationX = "";
        private string locationZ = "";
        private string statusTrIndex = "";
        private string statusTrItemType = "";
        private string statusTrItemIndex = "";
        private string statusTrItemLocationX = "";
        private string statusTrItemLocationZ = "";
        private readonly StringBuilder statusAdditional = new StringBuilder();

        private static readonly Color background = new Color(240, 240, 240);
        private static readonly Color separatorColor = new Color(190, 190, 190);
        private static readonly Color fieldBorder = new Color(160, 160, 160);
        private static readonly Color textColor = new Color(20, 20, 20);

        /// <summary>
        /// Constructor for the statusbar
        /// </summary>
        /// <param name="trackViewer">Track viewer object that contains all the information we want to show the status for</param>
        public StatusBarControl(TrackViewer trackViewer)
        {
        }

        /// <summary>
        /// set the size of the statusbar control (also after rescaling)
        /// </summary>
        /// <param name="width">Width of the statusbar</param>
        /// <param name="height">Height of the statusbar</param>
        /// <param name="yBottom">Y-value in screen pixels at the bottom of the statusbar</param>
        public void SetScreenSize(int width, int height, int yBottom)
        {
            this.screenWidth = width;
            this.yBottom = yBottom;
        }

        /// <summary>
        /// Update the various elements in the statusbar
        /// </summary>
        /// <param name="trackViewer">trackViewer object that contains all relevant data</param>
        /// <param name="mouseLocation">The Worldlocation of the mouse pointer</param>
        public void Update(TrackViewer trackViewer, WorldLocation mouseLocation)
        {
            ResetAdditionalText();

            SetMouseLocationStatus(mouseLocation);

            if (trackViewer.DrawTrackDB == null) return;

            SetTrackIndexStatus(trackViewer);

            SetTrackItemStatus(trackViewer);

            AddFPS(trackViewer);
            AddVectorSectionStatus(trackViewer);
            AddPATfileStatus(trackViewer);
            AddTrainpathStatus(trackViewer);
            AddTerrainStatus(trackViewer);
        }

        /// <summary>
        /// Draw the status bar. Must be called between SpriteBatch Begin and End.
        /// </summary>
        public void Draw()
        {
            int height = StatusbarHeight;
            int top = yBottom - height;
            BasicShapes.FillRectangle(new Rectangle(0, top, screenWidth, height), background);
            BasicShapes.FillRectangle(new Rectangle(0, top, screenWidth, 1), separatorColor);

            int textY = top + (height - Font.Height) / 2;
            int x = 6;
            int charWidth = Math.Max(1, Font.MeasureString("0"));

            void Field(string text, int minChars, bool rightAlign, bool boxed)
            {
                int width = Math.Max(minChars * charWidth, Font.MeasureString(text));
                if (boxed)
                {
                    BasicShapes.DrawRectangleOutline(new Rectangle(x - 2, top + 3, width + 6, height - 6), fieldBorder);
                }
                int textX = rightAlign ? x + width - Font.MeasureString(text) : x;
                BasicShapes.DrawString(Font, new Point(textX, textY), textColor, text);
                x += width + 8;
            }
            void Separator()
            {
                BasicShapes.FillRectangle(new Rectangle(x, top + 4, 1, height - 8), separatorColor);
                x += 8;
            }

            Field(tileXZ, 12, false, false);
            Separator();
            Field(locationX, 9, true, false);
            Field(locationZ, 9, true, false);
            Separator();
            Field(TrackViewer.catalog.GetString("Trackindex"), 0, false, false);
            Field(statusTrIndex, 5, true, true);
            Separator();
            Field(TrackViewer.catalog.GetString("Item"), 0, false, false);
            Field(statusTrItemType, 10, false, true);
            Field(statusTrItemIndex, 5, true, true);
            Field(statusTrItemLocationX, 8, true, true);
            Field(statusTrItemLocationZ, 8, true, true);
            Separator();
            Field(statusAdditional.ToString(), 0, false, false);
        }

        /// <summary>
        /// Update the status of the track index
        /// </summary>
        /// <param name="trackViewer"></param>
        private void SetTrackIndexStatus(TrackViewer trackViewer)
        {
            statusTrIndex = "";
            Drawing.CloseToMouseTrack closestTrack = trackViewer.DrawTrackDB.ClosestTrack;
            if (closestTrack == null) return;
            TrackNode tn = closestTrack.TrackNode;
            if (tn == null) return;
            statusTrIndex = string.Format(System.Globalization.CultureInfo.CurrentCulture,
                "{0} ", tn.Index);
        }

        /// <summary>
        /// Reset (is clear) the additionalText line
        /// </summary>
        private void ResetAdditionalText()
        {
            statusAdditional.Clear();
        }

        /// <summary>
        /// Set the status of the closest trackItem (junction or other item)
        /// </summary>
        /// <param name="trackViewer"></param>
        private void SetTrackItemStatus(TrackViewer trackViewer)
        {
            // Track items: clear first
            statusTrItemType = statusTrItemIndex =
                statusTrItemLocationX = statusTrItemLocationZ = string.Empty;

            ORTS.TrackViewer.Drawing.CloseToMouseItem closestItem = trackViewer.DrawTrackDB.ClosestTrackItem;
            ORTS.TrackViewer.Drawing.CloseToMouseJunctionOrEnd closestJunction = trackViewer.DrawTrackDB.ClosestJunctionOrEnd;
            ORTS.TrackViewer.Drawing.CloseToMousePoint closestPoint;
            if (closestItem != null && closestItem.IsCloserThan(closestJunction))
            {
                closestPoint = closestItem;
            }
            else if (closestJunction != null && closestJunction.JunctionOrEndNode != null)
            {
                closestPoint = closestJunction;
            }
            else
            {
                closestPoint = null;
            }

            if (closestPoint != null)
            {
                statusTrItemType = closestPoint.Description;
                statusTrItemIndex = string.Format(System.Globalization.CultureInfo.CurrentCulture,
                "{0} ", closestPoint.Index);
                statusTrItemLocationX = string.Format(System.Globalization.CultureInfo.CurrentCulture,
                "{0,3:F3} ", closestPoint.X);
                statusTrItemLocationZ = string.Format(System.Globalization.CultureInfo.CurrentCulture,
                    "{0,3:F3} ", closestPoint.Z);
                AddSignalStatus(trackViewer, closestPoint.Description, closestPoint.Index);
                AddNamesStatus(trackViewer, closestPoint.Description, closestPoint.Index);
                AddEventNamesStatus(trackViewer, closestPoint.Description, closestPoint.Index);
                CheckEventRightMouseClick(trackViewer, closestPoint.Description, closestPoint.Index);
            }
        }

        /// <summary>
        /// Put the mouse location in the statusbar
        /// </summary>
        /// <param name="mouseLocation"></param>
        private void SetMouseLocationStatus(WorldLocation mouseLocation)
        {
            tileXZ = string.Format(System.Globalization.CultureInfo.CurrentCulture,
                "{0,-7} {1,-7}", mouseLocation.TileX, mouseLocation.TileZ);
            locationX = string.Format(System.Globalization.CultureInfo.CurrentCulture,
                "{0,3:F3} ", mouseLocation.Location.X);
            locationZ = string.Format(System.Globalization.CultureInfo.CurrentCulture,
                "{0,3:F3} ", mouseLocation.Location.Z);
        }

        /// <summary>
        /// Add information about the closest vector section
        /// </summary>
        /// <param name="trackViewer"></param>
        private void AddVectorSectionStatus(TrackViewer trackViewer)
        {
            if (Properties.Settings.Default.statusShowVectorSections)
            {
                TrVectorSection tvs = trackViewer.DrawTrackDB.ClosestTrack?.VectorSection;
                if (tvs == null) return;
                uint shapeIndex = tvs.ShapeIndex;
                string shapeName = "Unknown:" + shapeIndex.ToString(System.Globalization.CultureInfo.CurrentCulture);
                try
                {
                    // Try to find a fixed track
                    TrackShape shape = trackViewer.RouteData.TsectionDat.TrackShapes.Get(shapeIndex);
                    shapeName = shape.FileName;
                }
                catch
                {
                    // try to find a dynamic track
                    try
                    {
                        TrackPath trackPath = trackViewer.RouteData.TsectionDat.TSectionIdx.TrackPaths[tvs.ShapeIndex];
                        shapeName = "<dynamic ?>";
                        foreach (uint trackSection in trackPath.TrackSections)
                        {
                            if (trackSection == tvs.SectionIndex)
                            {
                                shapeName = "<dynamic>";
                            }
                            // For some reason I do not undestand the (route) section.tdb. trackpaths are not consistent tracksections
                            // so this foreach loop will not always find a combination
                        }
                    }
                    catch
                    {
                    }
                }
                statusAdditional.AppendFormat(System.Globalization.CultureInfo.CurrentCulture,
                    " VectorSection ({3}/{4}) filename={2} Index={0} shapeIndex={1}",
                    tvs.SectionIndex, shapeIndex, shapeName,
                        trackViewer.DrawTrackDB.ClosestTrack.TrackVectorSectionIndex + 1,
                        trackViewer.DrawTrackDB.ClosestTrack.TrackNode.TrVectorNode.TrVectorSections.Count());
            }
        }

        /// <summary>
        /// Add information from Trainpaths
        /// </summary>
        /// <param name="trackViewer"></param>
        private void AddTrainpathStatus(TrackViewer trackViewer)
        {
            if (Properties.Settings.Default.statusShowTrainpath && (trackViewer.PathEditor != null))
            {
                if (trackViewer.PathEditor.HasValidPath)
                {
                    //gather some info on path status
                    List<string> statusItems = new List<string>();

                    if (trackViewer.PathEditor.HasEndingPath) statusItems.Add("good end");
                    if (trackViewer.PathEditor.HasBrokenPath) statusItems.Add("broken");
                    if (trackViewer.PathEditor.HasModifiedPath) statusItems.Add("modified");
                    if (trackViewer.PathEditor.HasStoredTail) statusItems.Add("stored tail");

                    string pathStatus = String.Join(", ", statusItems.ToArray());

                    ORTS.TrackViewer.Editing.TrainpathNode curNode = trackViewer.PathEditor.CurrentNode;
                    if (curNode == null) return;

                    statusAdditional.AppendFormat(System.Globalization.CultureInfo.CurrentCulture,
                        " {0} ({4}): TVNs=[{1} {2}] (type={3})",
                        trackViewer.PathEditor.FileName, curNode.NextMainTvnIndex, curNode.NextSidingTvnIndex,
                        curNode.NodeType, pathStatus);

                    if (curNode.IsBroken)
                    {
                        statusAdditional.AppendFormat(System.Globalization.CultureInfo.CurrentCulture,
                            " Broken: {0} ", curNode.BrokenStatusString());
                    }
                    TrainpathVectorNode curVectorNode = curNode as TrainpathVectorNode;
                    if (curVectorNode != null && curNode.NodeType == TrainpathNodeType.Stop)
                    {
                        statusAdditional.AppendFormat(System.Globalization.CultureInfo.CurrentCulture,
                            " (wait-time={0}s)",
                            curVectorNode.WaitTimeS);
                    }

                }
                else
                {
                    statusAdditional.Append("Invalid path");
                }
            }
        }

        /// <summary>
        /// Add information of the basic MSTS PATfile
        /// </summary>
        /// <param name="trackViewer"></param>
        private void AddPATfileStatus(TrackViewer trackViewer)
        {
            if (Properties.Settings.Default.statusShowPATfile && (trackViewer.DrawPATfile != null))
            {
                TrPathNode curNode = trackViewer.DrawPATfile.CurrentNode;
                TrackPDP curPDP = trackViewer.DrawPATfile.CurrentPdp;
                if (curNode == null || curPDP == null) return;
                statusAdditional.AppendFormat(System.Globalization.CultureInfo.CurrentCulture,
                    " {7}: {3}, {4} [{1} {2}] [{5} {6}] <{0}>",
                    curNode.pathFlags, (int)curNode.nextMainNode, (int)curNode.nextSidingNode,
                    curPDP.X, curPDP.Z, curPDP.junctionFlag, curPDP.invalidFlag, trackViewer.DrawPATfile.FileName);
            }
        }

        /// <summary>
        /// Add the FPS to the statusbar (Frames Per Second)
        /// </summary>
        private void AddFPS(TrackViewer trackViewer)
        {
            if (Properties.Settings.Default.statusShowFPS)
            {
                statusAdditional.AppendFormat(System.Globalization.CultureInfo.CurrentCulture,
                    " FPS={0:F1} ", trackViewer.FrameRate.SmoothedValue);
            }
        }

        /// <summary>
        /// Add information from terrain
        /// </summary>
        /// <param name="trackViewer"></param>
        private void AddTerrainStatus(TrackViewer trackViewer)
        {
            if (Properties.Settings.Default.statusShowTerrain && (trackViewer.drawTerrain != null))
            {
                statusAdditional.Append(trackViewer.drawTerrain.StatusInformation);
            }
        }

        /// <summary>
        /// Add information from signal
        /// </summary>
        /// <param name="trackViewer">The trackviewer we need to find the trackDB</param>
        /// <param name="description">The description of the item we might want to show, needed to make sure it is a proper item</param>
        /// <param name="index">The index of the item to show</param>
        private void AddSignalStatus(TrackViewer trackViewer, string description, uint index)
        {
            if (!Properties.Settings.Default.statusShowSignal) return;
            if (!String.Equals(description, "signal")) return;
            statusAdditional.Append("signal shape = ");
            statusAdditional.Append(trackViewer.RouteData.GetSignalFilename(index));
        }

        /// <summary>
        /// Add information from platform and station name
        /// </summary>
        /// <param name="trackViewer">The trackviewer we need to find the trackDB</param>
        /// <param name="description">The description of the item we might want to show, needed to make sure it is a proper item</param>
        /// <param name="index">The index of the item to show</param>
        private void AddNamesStatus(TrackViewer trackViewer, string description, uint index)
        {
            if (!Properties.Settings.Default.statusShowNames) return;
            if (!String.Equals(description, "platform")) return;

            TrItem item = trackViewer.RouteData.TrackDB.TrItemTable[index];
            PlatformItem platform = item as PlatformItem;
            if (platform == null) return;
            statusAdditional.AppendFormat(System.Globalization.CultureInfo.CurrentCulture,
                "{0} ({1})", platform.Station, platform.ItemName);
        }

        /// <summary>
        /// Add event information to status line
        /// </summary>
        /// <param name="trackViewer">The trackviewer we need to find the trackDB</param>
        /// <param name="description">The description of the item we might want to show, needed to make sure it is a proper item</param>
        /// <param name="index">The index of the item to show</param>
        ///
        private void AddEventNamesStatus(TrackViewer trackViewer, string description, uint index)
        {
            if (!String.Equals(description, "event")) return;

            TrItem item = trackViewer.RouteData.TrackDB.TrItemTable[index];
            EventItem eventItem = item as EventItem;
            if (eventItem == null) return;

            if (!Properties.Settings.Default.statusShowEventNames) return;

            statusAdditional.AppendFormat(System.Globalization.CultureInfo.CurrentCulture,
                "{0}", eventItem.ItemName);
        }

        /// <summary>
        /// When right mouse click on event a messagebox with event name and description pops up
        /// </summary>
        /// <param name="trackViewer">The trackviewer we need to find the trackDB</param>
        /// <param name="description">The description of the item we might want to show, needed to make sure it is a proper item</param>
        /// <param name="index">The index of the item to show</param>
        ///
        private void CheckEventRightMouseClick(TrackViewer trackViewer, string description, uint index)
        {
            if (!String.Equals(description, "event")) return;

            TrItem item = trackViewer.RouteData.TrackDB.TrItemTable[index];
            EventItem eventItem = item as EventItem;
            if (eventItem == null) return;

            if (!(trackViewer.PathEditor != null && trackViewer.PathEditor.EditingIsActive))
            {
                if (TVUserInput.IsMouseRightButtonPressed() && !trackViewer.InputCapturedByUserInterface)
                {
                    Dialogs.ShowMessage(eventItem.SData2, eventItem.ItemName);
                }
            }
        }
    }
}
