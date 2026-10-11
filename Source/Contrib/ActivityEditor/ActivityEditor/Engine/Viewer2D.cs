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


using ActivityEditor.Activity;
using LibAE;
using LibAE.Formats;
using Microsoft.Xna.Framework;
using Orts.Formats.Msts;
using Orts.Formats.OR;
using Orts.Parsers.Msts;
using ORTS;
using ORTS.Common;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using Brushes = ActivityEditor.Drawing.Brushes;
using Pens = ActivityEditor.Drawing.Pens;
using Image = ActivityEditor.Drawing.Image;
using Matrix = ActivityEditor.Drawing.Matrix;
using Cursor = Avalonia.Input.Cursor;
using Graphics = ActivityEditor.Drawing.Graphics;
using Font = ActivityEditor.Drawing.Font;
using FontStyle = ActivityEditor.Drawing.FontStyle;
using Pen = ActivityEditor.Drawing.Pen;
using SolidBrush = ActivityEditor.Drawing.SolidBrush;
using Icon = ActivityEditor.Drawing.Icon;
using Size = System.Drawing.Size;
using Point = System.Drawing.Point;
using KeyEventArgs = Avalonia.Input.KeyEventArgs;
using TextBox = Avalonia.Controls.TextBox;
using Button = Avalonia.Controls.Button;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using ActivityEditor.Drawing;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Media;
using Avalonia.Threading;
using System.Threading.Tasks;
using System.Xml.Serialization;
using Color = System.Drawing.Color;

namespace ActivityEditor.Engine
{
    public partial class Viewer2D : UserControl
    {
        public TypeEditor ViewerMode;
        public string ActivityPath;
        Pen redPen = new Pen(Color.Red);
        Pen greenPen = new Pen(Color.Green);
        Pen orangePen = new Pen(Color.Orange);
        Pen darkGrayPen = new Pen(Color.DarkGray);
        Pen bluePen = new Pen(Color.Blue);
        Icon StationIcon;
        Icon TagIcon;
        Icon SignalIco;
        Icon ShuntIco;
        Icon OtherSigIco;
        Icon RepeatIco;
        Icon SpeedIco;
        Icon StationConnector;
        Image Ruler;
        private Font sidingFont;
        private Font stationFont;
        private SolidBrush sidingBrush;

        bool loaded = false;
        PointF lastContextMenuPos;

        public GlobalItem itemToUpdate = null;
        public GlobalItem itemToEdit = null;
        private StationItem stationItem = null;
        public ActEditor actParent;
        public AEConfig aeConfig;
        public PseudoSim Simulator;
        public MSTSItems aeItems { get { return Simulator.mstsItems; } protected set { } }
        Random rnd = new Random();

        private int IM_Width = 720;
        private int IM_Height = 720;
        public AreaRoute areaRoute { get { return Simulator.areaRoute; } protected set { } }

        //  To define the biggest draughtboard for the route
        List<TilesInfo> tilesList = new List<TilesInfo>();
        float sizeX, sizeZ;

        private bool Dragging = false;
        private bool Zooming = false;
        float xScale = 1;
        float yScale = 1;
        public ToolClicks ToolClicked = ToolClicks.NO_TOOL;
        public Image ToolToUse = null;
        public Cursor CursorToUse = Cursor.Default;
        public Cursor reduit = new Cursor(StandardCursorType.Cross);

        private float usedScale = 0;
        private float previousScale = 0;
        private float currentRules = 0;
        private float origRules = 0;
        public double snapSize { get; set; }

        //  Keep the distance between the map origin and the visible section
        private PointF RouteScreenOrig = new PointF(0,0);
        private SizeF Distant2Origin = new SizeF(0, 0);
        //  Son équivalent en int pour le forms
        private System.Drawing.SizeF StartDrawing = new System.Drawing.SizeF(0, 0);
        private SizeF correctY = new SizeF (0,0);

        private Size routePicture = new Size(0, 0);

        private bool LeftClick = false;
        private bool RightClick = false;

        //  Represent the size of the visible part of the route 
        private RectangleF ViewWindow;
        //  LastCursor is used to calculate the current motion when dragging
        private System.Drawing.Point LastCursorPosition = new System.Drawing.Point();
        //  refZoomPoint mark the reference position for the zoom, stored as MSTS coord.
        private System.Drawing.Point refZoomPoint = new System.Drawing.Point(0, 0);
        private MSTSCoord coordZoomPoint;

        private System.Drawing.Point CurrentMousePosition = new System.Drawing.Point(0, 0);

        public DispatcherTimer ActivityTimer;

        //  For Debugging

#region InitViewer

        /// <summary>
        /// Initialise a new instance of the Viewer for ROUTE Configuration,
        /// with the parent windows and the ComponentItem of the route to be used
        /// </summary>
        /// <param name="parent">The parent ActEditor</param>
        /// <param name="routePath">The Route ComponentItem where to get the information</param>
        /// <returns>Nothing</returns>
        public Viewer2D(ActEditor parent, string routePath, PseudoSim simulator)
        {
            ViewerMode = TypeEditor.ROUTECONFIG;
            Program.actEditor.DisplayStatusMessage("Load Route for update: please wait");
            loadIcon();
            ActivityPath = routePath;

            //  Simulator retains all datas from MSTS, Route Metadata and Activity.
            //  It has already loaded all MSTS config, all OR config and synchronized both (in the background).
            Simulator = simulator;

            actParent = parent;

            //  Load the right side panel
            aeConfig = new AEConfig(TypeEditor.ROUTECONFIG, actParent, this);
            aeConfig.LoadPanels("Info");
            aeConfig.LoadRoute();

            InitializeComponent();
            this.Text = "Route MetaData";
            sidingFont = new Font("Arial", 8, ActivityEditor.Drawing.FontStyle.Regular);
            stationFont = new Font("Arial", 12, ActivityEditor.Drawing.FontStyle.Regular);
            sidingBrush = new SolidBrush(Color.Black);

            // initialise the timer used to handle user input
            ActivityTimer = new DispatcherTimer();
            ActivityTimer.Interval = TimeSpan.FromMilliseconds(50);
            ActivityTimer.Tick += new System.EventHandler(ActivityTimer_Tick);
            ActivityTimer.Start();
            // InitData needs the size of the drawing area: it is called once the view has been laid out.
            actParent.SelectTools(ViewerMode);
        }

        /// <summary>
        /// Initialise a new instance of the Viewer for ACTIVITY Edition,
        /// with the parent windows and the ActivityInfo object ready to use
        /// </summary>
        /// <param name="parent">The parent ActEditor</param>
        /// <param name="activityInfo">The pre-loaded activity info object</param>
        /// <returns>Nothing</returns>
        public Viewer2D(ActEditor parent, ActivityInfo activityInfo, PseudoSim simulator)
        {
            ViewerMode = TypeEditor.ACTIVITY;
            Program.actEditor.DisplayStatusMessage("Load Route and Activity for update: please wait");
            loadIcon();
            actParent = parent;

            Simulator = simulator;

            aeConfig = new AEConfig(TypeEditor.ACTIVITY, actParent, this);
            aeConfig.LoadRoute();
            aeConfig.LoadActivity(activityInfo);
            aeConfig.LoadPanels("Info");

            InitializeComponent();
            sidingFont = new Font("Arial", 8, ActivityEditor.Drawing.FontStyle.Regular);
            stationFont = new Font("Arial", 12, ActivityEditor.Drawing.FontStyle.Regular);
            sidingBrush = new SolidBrush(Color.Black);

            // initialise the timer used to handle user input
            ActivityTimer = new DispatcherTimer();
            ActivityTimer.Interval = TimeSpan.FromMilliseconds(50);
            ActivityTimer.Tick += new System.EventHandler(ActivityTimer_Tick);
            ActivityTimer.Start();
            // InitData needs the size of the drawing area: it is called once the view has been laid out.
            actParent.SelectTools(ViewerMode);
        }
#endregion
#region initData

        private void InitData()
        {
            if (!loaded)
            {
                // do this only once
                loaded = true;
                //trackSections.DataSource = new List<InterlockingTrack>(simulator.InterlockingSystem.Tracks.Values).ToArray();
            }
            Program.actEditor.DisplayStatusMessage("Init data for display...");
            //aeConfig.aeRouteConfig.InitORData(Simulator);
            tilesList = areaRoute.tilesList;
            

            sizeX = (Math.Abs(areaRoute.tileMaxX - areaRoute.tileMinX) + 1) * 2048f;
            sizeZ = (Math.Abs(areaRoute.tileMaxZ - areaRoute.tileMinZ) + 1) * 2048f;

            ViewWindow = new RectangleF(0, 0, sizeX, sizeZ);
            routePicture = new Size(RouteWidth, RouteHeight);
            xScale = RouteWidth / ViewWindow.Width;
            yScale = RouteHeight / ViewWindow.Height;
            previousScale = 1 / Math.Min(xScale, yScale);
            origRules = currentRules = (RouteWidth * previousScale);
            if (RouteWidth > (ViewWindow.Width / previousScale))
            {
                Distant2Origin.Width = ((int)(RouteWidth - (ViewWindow.Width / previousScale)) / 2) * previousScale;
            }
            if (RouteHeight > (ViewWindow.Height / previousScale))
            {
                Distant2Origin.Height = ((int)(RouteHeight - (ViewWindow.Height / previousScale)) / 2) * previousScale;
            }
            Distant2Origin.Width += 1024f;  //  Origin is on center of tile
            Distant2Origin.Height += 1024f;  //  Origin is on center of tile
            ComputeUsedScale();
            ComputeStartDrawing();
            return;
        }

        public void InitImage()
        {
            correctY.Height = IM_Height;
        }

        [CallOnThread("Updater")]
        void ActivityTimer_Tick(object sender, EventArgs e)
        {
            GenerateView();
        }

        public List<TilesInfo> getTileList()
        {
            return tilesList;
        }

        public float getUsedScale()
        {
            return usedScale;
        }
    #endregion

    #region Draw

        public bool firstShow = true;
        public bool followTrain = false;
        float subX, subY;
        float oldWidth = 0;
        float oldHeight = 0;

        public void needRedim()
        {
            if (oldHeight != RouteHeight || oldWidth != RouteWidth)
            {
                oldWidth = RouteWidth;
                oldHeight = RouteHeight;
                IM_Width = RouteWidth;
                IM_Height = RouteHeight;
                correctY.Height = IM_Height;
                routePicture = new Size(RouteWidth, RouteHeight);
                firstShow = true;
            }
        }

        private void ComputeUsedScale()
        {
            previousScale = usedScale;  //  Keep the last usedScale
            usedScale = currentRules / RouteWidth;
            usedScale = (float)Math.Round((double)usedScale, 2, MidpointRounding.AwayFromZero);
        }

        /// <summary>
        /// Request a redraw of the route view (the drawing itself is done in <see cref="DrawView"/>, called while rendering).
        /// </summary>
        public void GenerateView()
        {
            routeDrawing?.InvalidateVisual();
        }

        /// <summary>
        /// Draw the route view. This used to draw into a bitmap shown by a PictureBox; it is now called from
        /// <see cref="RouteCanvas.Render"/> with a GDI+-like wrapper over Avalonia's DrawingContext.
        /// </summary>
        public void DrawView(Graphics g)
        {
            if (!loaded)
                return;
            needRedim();
            {
                Graphics gMod = g;
                subX = 0; // areaRoute.getMinX() + ViewWindow.X; 
                subY = 0; // areaRoute.getMinY() + ViewWindow.Y;
                g.Clear(Color.White);
                ComputeStartDrawing();
                
                usedScale = (currentRules / RouteWidth);
                usedScale = (float)Math.Round((double)usedScale, 2, MidpointRounding.AwayFromZero);
                if (usedScale == 0)
                    usedScale = 0.1f;
                if (Program.aePreference.ShowRuler)
                    showRules(g, usedScale);
                snapSize = (double)(Program.aePreference.getSnapCircle() * usedScale);
                
                PointF[] points = new PointF[3];
                Pen p = darkGrayPen;
                Pen t = bluePen;
                Pen r = redPen;
                Pen pen = bluePen;

                p.Width = 1;
                if (p.Width < 1) p.Width = 1;
                else if (p.Width > 3) p.Width = 3;

                PointF scaledA = new PointF(0, 0);
                PointF scaledB = new PointF(0, 0);
                SizeF decal = (correctY - StartDrawing); 
                PointF scaledC = new PointF(0, 0);
                PointF centerCurve = new PointF(0, 0);
                #region showItem

                pen = bluePen;
                if (Program.aePreference.ShowTiles)
                {   //  X,Y tile are on center of tile
                    foreach (var tile in tilesList)
                    {
                        scaledA.X = decal.Width + (((tile.TileX * 2048f) - subX -1024f) / usedScale);
                        scaledA.Y = decal.Height - (((tile.TileZ *  2048f) - subY + 1024f) / usedScale);
                        int width = (int)(2048 / usedScale);
                        int height = (int)(2048 / usedScale);
                        g.DrawRectangle(pen, (int)scaledA.X, (int)scaledA.Y, width, height);
                        g.DrawString(" " + tile.TileX + "," + tile.TileZ, sidingFont, sidingBrush, scaledA.X, scaledA.Y);
                    }
                }
                if (Program.aePreference.ShowTrackInfo && Program.aePreference.PlSiZoom >= usedScale)
                {
                    foreach (var buffer in Simulator.mstsItems.getBuffers())
                    {
                        buffer.SynchroLocation();
                        scaledA.X = decal.Width + (((float)buffer.Location.X - subX) / usedScale);
                        scaledA.Y = decal.Height - (((float)buffer.Location.Y - subY) / usedScale);
                        g.DrawRectangle(pen, (int)scaledA.X, (int)scaledA.Y, 5, 5);
                        g.DrawString((string)buffer.associateNode.Index.ToString(), sidingFont, sidingBrush, scaledA.X - 20, scaledA.Y);
                        if (Program.aePreference.ShowPlSiLabel && buffer.isItSeen())
                        {
                            Matrix matrixSA = new Matrix();
                            matrixSA.RotateAt((float)0.0f, new System.Drawing.Point((int)scaledA.X, (int)scaledA.Y), MatrixOrder.Append);
                            g.Transform = matrixSA;
                            g.DrawString(buffer.NameBuffer, sidingFont, sidingBrush, scaledA.X + 15, scaledA.Y + 10);
                        }

                    }
                     foreach (var junction in aeItems.getSwitches())
                    {
                        junction.SynchroLocation();
                        scaledA.X = decal.Width + (((float)junction.Location.X - subX) / usedScale);
                        scaledA.Y = decal.Height - (((float)junction.Location.Y - subY) / usedScale);
                        g.DrawRectangle(pen, (int)scaledA.X, (int)scaledA.Y, 5, 5);
                        g.DrawString((string)junction.associateNode.Index.ToString(), sidingFont, sidingBrush, scaledA.X - 20, scaledA.Y);
                    }
                }

                foreach (var line in aeItems.getSegments())
                //foreach (var line in shape.getTrItems())
                {
                    scaledA.X = decal.Width + (((float)line.getStart().X - subX) / usedScale);
                    scaledA.Y = decal.Height - (((float)line.getStart().Y - subY) / usedScale);
                    scaledB.X = decal.Width + ((float)line.getEnd().X - subX) / usedScale;
                    scaledB.Y = decal.Height - (((float)line.getEnd().Y - subY) / usedScale);

                    if ((scaledA.X < 0 && scaledB.X < 0) || (scaledA.X > IM_Width && scaledB.X > IM_Width) || (scaledA.Y > IM_Height && scaledB.Y > IM_Height) || (scaledA.Y < 0 && scaledB.Y < 0))
                        continue;
                    if (line.isCurved == true && line.curve != null)
                    {
                        centerCurve.X = decal.Width + (((float)line.curve.Centre.ConvertVector().X - subX) / usedScale);
                        centerCurve.Y = decal.Height - (((float)line.curve.Centre.ConvertVector().Y - subY) / usedScale);
                        scaledC.X = decal.Width + ((float)line.curve.C.ConvertVector().X - subX) / usedScale;
                        scaledC.Y = decal.Height - ((float)line.curve.C.ConvertVector().Y - subY) / usedScale;
                        points[0] = scaledA;
                        points[1] = scaledC;
                        points[2] = scaledB;
                        if (line.isSnap())
                        {
                            if (Program.aePreference.ShowSnapLine && Program.aePreference.PlSiZoom >= usedScale)
                            {
                                g.DrawRectangle(pen, (int)scaledA.X, (int)scaledA.Y, 5, 5);
                                g.DrawRectangle(pen, (int)scaledB.X, (int)scaledB.Y, 5, 5);
                                g.DrawRectangle(pen, (int)scaledC.X, (int)scaledC.Y, 5, 5);
                                if (Program.aePreference.ShowSnapInfo)
                                {
                                    g.DrawRectangle(pen, (int)centerCurve.X, (int)centerCurve.Y, 5, 5);
                                    g.DrawLine(pen, centerCurve, scaledA);
                                    g.DrawLine(pen, centerCurve, scaledB);
                                    if (line.GetDecal())
                                    {
                                        g.DrawString(line.AsString() + "a", sidingFont, sidingBrush, (int)scaledA.X + 30, (int)scaledA.Y + 10);
                                        g.DrawString(line.AsString() + "b", sidingFont, sidingBrush, (int)scaledB.X + 30, (int)scaledB.Y - 10);
                                    }
                                    else
                                    {
                                        g.DrawString(line.AsString() + "a", sidingFont, sidingBrush, (int)scaledA.X, (int)scaledA.Y + 10);
                                        g.DrawString(line.AsString() + "b", sidingFont, sidingBrush, (int)scaledB.X, (int)scaledB.Y - 10);
                                    }
                                    if (line.curve.checkedPoint != null)
                                    {
                                        foreach (var checkP in line.curve.checkedPoint)
                                        {
                                            scaledC.X = decal.Width + ((float)checkP.ConvertVector().X - subX) / usedScale;
                                            scaledC.Y = decal.Height - ((float)checkP.ConvertVector().Y - subY) / usedScale;
                                            g.DrawRectangle(pen, (int)scaledC.X, (int)scaledC.Y, 5, 5);
                                        }
                                    }
                                }
                            }
                            g.DrawCurve(r, points);
                        }
                        else
                            g.DrawCurve(p, points);
                    }
                    else
                    {
                        if (line.isSnap())
                        {
                            if (Program.aePreference.ShowSnapLine && Program.aePreference.PlSiZoom >= usedScale)
                            {
                                g.DrawRectangle(pen, (int)scaledA.X, (int)scaledA.Y, 5, 5);
                                g.DrawRectangle(pen, (int)scaledB.X, (int)scaledB.Y, 5, 5);
                                if (Program.aePreference.ShowSnapInfo)
                                {
                                    if (line.GetDecal())
                                    {
                                        g.DrawString(line.AsString() + "a", sidingFont, sidingBrush, (int)scaledA.X + 30, (int)scaledA.Y + 20);
                                        g.DrawString(line.AsString() + "b", sidingFont, sidingBrush, (int)scaledB.X + 30, (int)scaledB.Y - 20);
                                    }
                                    else
                                    {
                                        g.DrawString(line.AsString() + "a", sidingFont, sidingBrush, (int)scaledA.X, (int)scaledA.Y + 10);
                                        g.DrawString(line.AsString() + "b", sidingFont, sidingBrush, (int)scaledB.X, (int)scaledB.Y - 10);
                                    }
                                }
                            }
                            g.DrawLine(r, scaledA, scaledB);
                        }
                        else
                            g.DrawLine(p, scaledA, scaledB);
                    }
                }

                foreach (var item in aeConfig.getTagWidgets())
                {
                    var tag = item.tagWidget;
                    scaledA.X = decal.Width + (tag.Location.X - subX) / usedScale;
                    scaledA.Y = (decal.Height - ((tag.Location.Y - subY) / usedScale)) - 16;
                    tag.SynchroLocation();
                    tag.Location2D.X = scaledA.X; tag.Location2D.Y = scaledA.Y;
                    if ((scaledA.X < 0) || (scaledA.X > IM_Width) || (scaledA.Y > IM_Height) || (scaledA.Y < 0))
                        continue;

                    if (item.NameDisplay.IsChecked == true)
                    {
                        g.DrawString(item.TagName.Text, sidingFont, sidingBrush, scaledA);
                    }
                    else
                    {
                        System.Drawing.Rectangle rect = new System.Drawing.Rectangle((int)scaledA.X, (int)scaledA.Y, 16, 16);
                        g.DrawIcon(TagIcon, rect);
                    }
                }
                foreach (var item in aeConfig.getStationWidgets())
                {
                    Matrix myMatrix = new Matrix();
                    var station = item.stationWidget;
                    station.SynchroLocation();
                    scaledA.X = (decal.Width + (station.Location.X - subX) / usedScale);
                    scaledA.Y = (decal.Height - ((station.Location.Y - subY) / usedScale));
                    station.Location2D.X = scaledA.X; station.Location2D.Y = scaledA.Y;

                    System.Drawing.Rectangle rect = new System.Drawing.Rectangle((int)scaledA.X,
                                                        (int)scaledA.Y, 
                                                        (int)(10/usedScale), (int)(10/usedScale));
                    if (item.stationWidget.icoAngle != 0f)
                    {
                        myMatrix.RotateAt(item.stationWidget.icoAngle, scaledA, MatrixOrder.Append);
                        gMod.Transform = myMatrix;
                    }
                    gMod.DrawIcon(StationIcon, rect);
                    gMod.ResetTransform();
                    if (station.isItSeen())
                    {
                        g.DrawEllipse(r, scaledA.X - 4, scaledA.Y - 4, 8, 8);
                    }
                    else
                        g.DrawEllipse(t, scaledA.X - 4, scaledA.Y - 4, 8, 8);

                    if (station.stationArea.Count > 1)
                    {
                        bool canDraw = false;
                        foreach (StationAreaItem SAWidget in station.stationArea)
                        {
                            int X = (int)(decal.Width + ((SAWidget.Coord.TileX * 2048f + SAWidget.Coord.X - subX) / usedScale));
                            int Y = (int)(decal.Height - ((SAWidget.Coord.TileY * 2048f + SAWidget.Coord.Y - subY) / usedScale));
                            if (!((X < 0) || (X > IM_Width) || (Y > IM_Height) || (Y < 0)))
                            {
                                canDraw = true;
                                break;
                            }
                        }
                        if (((scaledA.X < 0) || (scaledA.X > IM_Width) || (scaledA.Y > IM_Height) || (scaledA.Y < 0)) && !canDraw)
                            continue;

                        List<System.Drawing.Point> polyPoints = new List<System.Drawing.Point>();
                        foreach (StationAreaItem SAWidget in station.stationArea)
                        {
                            int X = (int)(decal.Width + ((SAWidget.Coord.TileX * 2048f + SAWidget.Coord.X - subX) / usedScale));
                            int Y = (int)(decal.Height - ((SAWidget.Coord.TileY * 2048f + SAWidget.Coord.Y - subY) / usedScale));
                            polyPoints.Add(new System.Drawing.Point(X, Y));
                            if (SAWidget.IsInterface())
                            {
                                Matrix matrixSA = new Matrix();

                                rect = new System.Drawing.Rectangle((int)X, 
                                    (int)(Y - (2 / usedScale)), 
                                    (int)(2/usedScale), (int)(2/usedScale));
                                if (SAWidget.getStationConnector().getDirConnector() == AllowedDir.IN)
                                {
                                    matrixSA.RotateAt((float)SAWidget.getStationConnector().angle, new System.Drawing.Point(X, Y), MatrixOrder.Append);
                                    gMod.Transform = matrixSA;
                                    gMod.DrawIcon(StationConnector, rect);
                                }
                                else if (SAWidget.getStationConnector().getDirConnector() == AllowedDir.OUT)
                                {
                                    matrixSA.RotateAt((float)SAWidget.getStationConnector().angle + 180f, new System.Drawing.Point(X, Y), MatrixOrder.Append);
                                    gMod.Transform = matrixSA;
                                    gMod.DrawIcon(StationConnector, rect);
                                }
                                else if (SAWidget.getStationConnector().getDirConnector() == AllowedDir.InOut)
                                {
                                    matrixSA.RotateAt((float)SAWidget.getStationConnector().angle, new System.Drawing.Point(X, Y), MatrixOrder.Append);
                                    gMod.Transform = matrixSA;
                                    gMod.DrawIcon(StationConnector, rect);
                                    matrixSA.RotateAt(180f, new System.Drawing.Point(X, Y), MatrixOrder.Append);
                                    gMod.Transform = matrixSA;
                                    gMod.DrawIcon(StationConnector, rect);
                                }
                                gMod.ResetTransform();
                                //else
                                {
                                    if (SAWidget.getStationConnector().isConfigured())
                                    {
                                        SolidBrush myBrush = new SolidBrush(Color.FromArgb(100, Color.Red));
                                        g.FillEllipse(myBrush, (float)(X - 4), (float)(Y - 4), (float)8, (float)8);
                                        myBrush.Dispose();
                                    }
                                    else
                                    {
                                        g.DrawEllipse(Pens.Red, X - 4, Y - 4, 8, 8);
                                    }
                                    if (SAWidget.getStationConnector().getLabel().Length > 0 && 
                                        Program.aePreference.ShowPlSiLabel && Program.aePreference.PlSiZoom >= usedScale &&
                                        SAWidget.isItSeen())
                                        g.DrawString(SAWidget.getStationConnector().getLabel(), stationFont, sidingBrush, new System.Drawing.Point(X+15, Y+10));
                                }
                            }
                            else
                                g.DrawEllipse(Pens.DarkBlue, X - 1, Y - 1, 3, 3);
                        }

                        using (SolidBrush br = new SolidBrush(Color.FromArgb(100, Color.Yellow)))
                        {
                            g.FillPolygon(br, polyPoints.ToArray());
                        }
                        g.DrawPolygon(Pens.DarkBlue, polyPoints.ToArray());
                    }
                    if (item.NameDisplay.IsChecked == true)
                    {
                        if ((scaledA.X < 0) || (scaledA.X > IM_Width) || (scaledA.Y > IM_Height) || (scaledA.Y < 0))
                            continue;
                        g.DrawString(item.StationName.Text, stationFont, sidingBrush, scaledA);
                    }

                }

                foreach (var item in aeConfig.getActItem())
                {
                    if (item is PathEventItem)
                    {
                        PointF coord = ((PathEventItem)item).Coord.ConvertToPointF();
                        scaledA.X = (decal.Width + (coord.X - subX) / usedScale) -8;
                        scaledA.Y = (decal.Height - ((coord.Y - subY) / usedScale)) -8;
                        if ((scaledA.X < 0) || (scaledA.X > IM_Width) || (scaledA.Y > IM_Height) || (scaledA.Y < 0))
                            continue;

                        if (((PathEventItem)item).nameVisible)
                        {
                            g.DrawString(((PathEventItem)item).nameEvent, sidingFont, sidingBrush, scaledA);
                        }
                        else
                        {
                            System.Drawing.Rectangle rect = new System.Drawing.Rectangle((int)scaledA.X, (int)scaledA.Y, 16, 16);
                            g.DrawIcon(GetEventIcon((PathEventItem)item), rect);
                        }
                    }
                }
    #endregion

                #region showCursor

                if (findItemFromMouse(CurrentMousePosition) != null)
                {
                    pen = r;
                }
                else
                {
                    pen = t;
                }

                if (Program.aePreference.ShowSnapCircle && (ToolClicked == ToolClicks.NO_TOOL))
                {
                    PointF point = convertScreen2ViewCoord(CurrentMousePosition.X, CurrentMousePosition.Y);
                    MSTSCoord coord = Simulator.mstsDataConfig.TileBase.getMstsCoord(point);
                    int sizeEllipse = (int)(Program.aePreference.getSnapCircle() / usedScale);
                    g.DrawEllipse(pen, (int)(CurrentMousePosition.X - (sizeEllipse / 2)),
                                        (int)(CurrentMousePosition.Y - sizeEllipse / 2),
                                        (int)(sizeEllipse),
                                        (int)(sizeEllipse));
                    g.DrawString(coord.asString(), sidingFont, sidingBrush, CurrentMousePosition.X + 20, CurrentMousePosition.Y - 10);
                }
                else if (ToolClicked == ToolClicks.NO_TOOL)
                {

                }
                else if (ToolClicked != ToolClicks.NO_TOOL)
                {
                    var tool = ToolToUse;
                    System.Drawing.Rectangle rect = new System.Drawing.Rectangle((int)(CurrentMousePosition.X),
                                        (int)(CurrentMousePosition.Y - (20)),
                                        (int)20,
                                        (int)20);

                    if (tool != null)
                    {
                        g.DrawImage(tool, rect);
                    }
                }
    #endregion

                float x, y;
                PointF scaledItem = new PointF(0f, 0f);
                var widthDraw = 12f * p.Width;
                if (widthDraw > 20)
                    widthDraw = 20;//not to make it too large
                if (usedScale < Program.aePreference.getPlSiZoom())
                {
#if !DRAW_ALL
#region drawSignal
                    foreach (var s in aeItems.getSignals())
                    {
                        s.SynchroLocation();
                        if (float.IsNaN(s.Location.X) || float.IsNaN(s.Location.Y))
                            continue;
                        x = decal.Width + (s.Location.X - subX) / usedScale;
                        y = decal.Height - (s.Location.Y - subY) / usedScale;

                        if (x < 0 || x > IM_Width || y > IM_Height || y < 0)
                            continue;
                        scaledItem.X = x;
                        scaledItem.Y = y;
                        s.Location2D.X = scaledItem.X;
                        s.Location2D.Y = scaledItem.Y;
                        //if (s.Signal.isSignalNormal())//only show nor
                        {
                            var color = Brushes.Green;
                            pen = greenPen;
                            if (s.IsProceed == 0)
                            {
                            }
                            else if (s.IsProceed == 1)
                            {
                                color = Brushes.Orange;
                                pen = orangePen;
                            }
                            else
                            {
                                color = Brushes.Red;
                                pen = redPen;
                            }
                            System.Drawing.Rectangle rect = new System.Drawing.Rectangle((int)scaledItem.X-12, (int)scaledItem.Y-5, (int)widthDraw, (int)widthDraw);
                            g.DrawEllipse(Pens.DarkBlue, (int)scaledItem.X - 1, (int)scaledItem.Y - 1, 2, 2);
                            if (s.hasDir)
                            {
                                System.Drawing.Point rotationLoc = new System.Drawing.Point((int)scaledItem.X-5, (int)scaledItem.Y+5);
                                Matrix myMatrix = new Matrix();
                                scaledB.X = decal.Width + (s.Dir.X - subX) / usedScale;
                                scaledB.Y = decal.Height - (s.Dir.Y - subY) / usedScale;
                                float a = ((float)(Math.Atan2(scaledB.Y - scaledItem.Y, scaledB.X - scaledItem.X) * 180.0d / Math.PI)) - 90.0f;
                                //a = ((float)(Math.Atan2(scaledItem.Y - scaledB.Y, scaledItem.X - scaledB.X) * 180.0d / Math.PI)) - 90.0f;
                                //a = ((float)(Math.Atan2(scaledB.X - scaledItem.X, scaledB.Y - scaledItem.Y) * 180.0d / Math.PI)) + 90.0f;
                                //a = -90;
                                myMatrix.RotateAt(a, rotationLoc, MatrixOrder.Append);

                                // Draw the rectangle to the screen again after applying the

                                // transform.
                                g.Transform = myMatrix;
                            }

                            if (Program.aePreference.ShowAllSignal)
                            {
                                switch (s.SigFonction)
                                {
                                    case MstsSignalFunction.NORMAL:
                                    case MstsSignalFunction.DISTANCE:
                                        g.DrawIcon(SignalIco, rect);
                                        break;
                                    case MstsSignalFunction.REPEATER:
                                        g.DrawIcon(RepeatIco, rect);
                                        break;
                                    case MstsSignalFunction.SHUNTING:
                                        g.DrawIcon(ShuntIco, rect);
                                        break;
                                    case MstsSignalFunction.ALERT:
                                        g.DrawIcon(OtherSigIco, rect);
                                        break;
                                    case MstsSignalFunction.SPEED:
                                        g.DrawIcon(SpeedIco, rect);
                                        break;
                                }
                            }
                            else
                            {
                                switch (s.SigFonction)
                                {
                                    case MstsSignalFunction.NORMAL:
                                    case MstsSignalFunction.DISTANCE:
                                        g.DrawIcon(SignalIco, rect);
                                        break;
                                }
                            }
                            g.ResetTransform();
                        }
                    }
#endregion
#region crossOver
                    foreach (AECrossOver crossOver in aeItems.getCrossOver())
                    {
                        Matrix matrixSA = new Matrix();
                        if (float.IsNaN(crossOver.Location.X) || float.IsNaN(crossOver.Location.Y))
                            continue;
                        x = decal.Width + (crossOver.Location.X - subX) / usedScale;
                        y = decal.Height - (crossOver.Location.Y - subY) / usedScale;

                        if (x < 0 || x > IM_Width || y > IM_Height || y < 0)
                            continue;
                        double tempo = 0;

                        g.DrawRectangle(pen, (int)x, (int)y, 5, 5);
                        if (Program.aePreference.ShowPlSiLabel && crossOver.isItSeen())
                        {
                            matrixSA.RotateAt((float)tempo, new System.Drawing.Point((int)x, (int)y), MatrixOrder.Append);
                            g.Transform = matrixSA;
                        }
                        g.ResetTransform();
                    }
                    #endregion

#region drawSiding
                    foreach (SideItem siding in aeItems.getSidings())
                    {
                        Matrix matrixSA = new Matrix();
                        if (float.IsNaN(siding.Location.X) || float.IsNaN(siding.Location.Y))
                            continue;
                        x = decal.Width + (siding.Location.X - subX) / usedScale;
                        y = decal.Height - (siding.Location.Y - subY) / usedScale;

                        if (x < 0 || x > IM_Width || y > IM_Height || y < 0)
                            continue;
                        //x2 = decal.Width + (siding.Location2.X - subX) / usedScale;
                        //y2 = decal.Height - (siding.Location2.Y - subY) / usedScale;
                        //siding.Location2D.X = x;
                        //siding.Location2D.Y = y;
                        double tempo = 0;
                        //double tempo = Math.Atan2(y2 - y, x2 - x);
                        //tempo = (tempo * 180.0d) / Math.PI;

                        if (siding.type == TrItem.trItemType.trSIDING)
                        {
                            g.DrawEllipse(Pens.Blue, x - 1, y - 1, 3, 3);
                            //g.DrawEllipse(Pens.Blue, x2 - 1, y2 - 1, 3, 3);
                            if (Program.aePreference.ShowPlSiLabel && siding.isItSeen())
                            {
                                matrixSA.RotateAt((float)tempo, new System.Drawing.Point((int)x, (int)y), MatrixOrder.Append);
                                g.Transform = matrixSA;
                                g.DrawString(siding.Name, sidingFont, sidingBrush, x+15, y+10);
                            }
                        }
                        else
                        {
                            g.DrawEllipse(Pens.Brown, x - 1, y - 1, 3, 3);
                            //g.DrawEllipse(Pens.Brown, x2 - 1, y2 - 1, 3, 3);
                            if (Program.aePreference.ShowPlSiLabel && siding.isItSeen())
                            {
                                matrixSA.RotateAt((float)tempo, new System.Drawing.Point((int)x, (int)y), MatrixOrder.Append);
                                g.Transform = matrixSA;
                                g.DrawString(siding.Name, sidingFont, sidingBrush, x+15, y+10);
                            }
                        }
                        g.ResetTransform();
                    }
                }
#endregion
#endif
            }
        }

        void showRules(Graphics g, float usedScale)
        {
            PointF scaledA = new PointF(0, 0);
            PointF scaledB = new PointF(0, 0);
            SizeF decal = (correctY - StartDrawing);
            Matrix myMatrix = new Matrix();

            scaledA.X = 10;
            scaledA.Y = 10;
            scaledB.X = scaledA.X + (100 / usedScale);
            scaledB.Y = scaledA.Y;
            g.DrawLine(greenPen, scaledA, scaledB);

            scaledB.X = scaledA.X;
            scaledB.Y = scaledA.Y + (100 / usedScale);
            g.DrawLine(greenPen, scaledA, scaledB);
            System.Drawing.Rectangle rect = new System.Drawing.Rectangle((int)10,
                                    (int)10,
                                    (int)(100 / usedScale), (int)(100 / usedScale));
            //g.DrawIcon(Ruler, rect);
            g.DrawImage(Ruler, rect);
            scaledA.X = 30;
            scaledA.Y = 30;
            g.DrawString("100m", stationFont, sidingBrush, scaledA);
#if false
		            scaledA.X = 0;
            scaledA.Y = 0;
            
            myMatrix.RotateAt(90f, scaledA, MatrixOrder.Append);
            g.Transform = myMatrix;
            g.DrawIcon(Ruler, rect);
            g.ResetTransform();
  
	#endif        
        }
#endregion

        private void ComputeStartDrawing()
        {
            var d2o = Distant2Origin;
            StartDrawing = Size.Round(d2o);
            StartDrawing.Width = -(d2o.Width / usedScale);
            StartDrawing.Width = (float)Math.Round((double)StartDrawing.Width, 2, MidpointRounding.AwayFromZero);
            StartDrawing.Height = (d2o.Height / usedScale);
            StartDrawing.Height = (float)Math.Round((double)StartDrawing.Height, 2, MidpointRounding.AwayFromZero);
        }


        #region MouseEvent

        private bool zoomed  = false;
        private void routeDrawingMouseDown(object sender, MouseArgs e)
        {
            bool shiftKey = false;
            if (e.Button == MouseButtons.Left)
            {
                LeftClick = true;
            }
            if (e.Button == MouseButtons.Right)
            {
                RightClick = true;
            }
            if (ModifierKeys.HasFlag(KeyModifiers.Shift))
            {
                shiftKey = true;
            }

            if (LeftClick == true && RightClick == false)
            {
                PointF point = convertScreen2ViewCoord(e.X, e.Y);
                MSTSCoord coord = Simulator.mstsDataConfig.TileBase.getMstsCoord(point);

                LeftMouseDown(sender, e, shiftKey);
            }
            else if (LeftClick == false && RightClick == true)
            {
                RightMouseDown(sender, e, shiftKey);
            }
            LastCursorPosition.X = e.X;
            LastCursorPosition.Y = e.Y;
        }

        private void LeftMouseDown(object sender, MouseArgs e, bool shiftKey)
        {
            //this.routeDrawing.Focus();
            Dragging = false;
            if (ToolClicked == ToolClicks.AREA_ADD &&
                itemToUpdate != null &&
                (itemToUpdate.GetType() == typeof(StationItem)))
            {
                PointF point = convertScreen2ViewCoord(e.X, e.Y);
                MSTSCoord coord = Simulator.mstsDataConfig.TileBase.getMstsCoord(point);
                StationAreaItem info = aeConfig.AddPointArea((StationItem)itemToUpdate, coord);
                if (info != null)
                {
                    aeConfig.AddORItem(info);
                    stationItem = (StationItem)itemToUpdate;
                    itemToUpdate = info;
                    
                }
                else
                {
                    SetToolClicked(ToolClicks.NO_TOOL);
                }
            }
            else
            {
                var item = findItemFromEvent(e);
                if (ToolClicked == ToolClicks.NO_TOOL &&
                    item != null && item.IsMovable())
                {
                    SetToolClicked(ToolClicks.MOVE);
                }
                if ((ToolClicked == ToolClicks.MOVE
                    || ToolClicked == ToolClicks.ROTATE) &&
                    item != null)
                {
                    //CancelOperation();
                    if (itemToUpdate != item)
                    {
                        itemToUpdate = null;
                        if (ToolClicked == ToolClicks.MOVE && item.IsMovable())
                        {
                            itemToUpdate = item;
                        }
                        else if (ToolClicked == ToolClicks.ROTATE && item.IsRotable())
                        {
                            itemToUpdate = item;
                        }
                    }
                }
                else if (ToolClicked == ToolClicks.AREA && item != null)
                {
                    //CancelOperation();      // First, cancel all operation
                    itemToUpdate = item;
                }
                else if (Dragging == false && ToolClicked == ToolClicks.NO_TOOL)
                {
                    Dragging = true;
                    SetToolClicked(ToolClicks.DRAG);
                }
            }

        }

        private void RightMouseDown(object sender, MouseArgs e, bool shiftKey)
        {
            //this.routeDrawing.Focus();
            var item = findItemFromEvent(e);
            itemToEdit = null;
            if (ToolClicked == ToolClicks.NO_TOOL &&
                item != null && item.IsEditable())
            {
                itemToEdit = item;
            }
            else if (Zooming == false)
            {
                Zooming = true;
                refZoomPoint.X = e.X;
                refZoomPoint.Y = e.Y;
                PointF point = convertScreen2ViewCoord(refZoomPoint.X, refZoomPoint.Y);
                coordZoomPoint = Simulator.mstsDataConfig.TileBase.getMstsCoord(point);
                refZoomPoint.Y = RouteHeight - e.Y;
                ToolClicked = ToolClicks.ZOOM;
                System.Drawing.Point refCoord = convertViewCoord2Screen(coordZoomPoint);
            }
        }


        private void routeDrawingMouseMove(object sender, MouseArgs e)
        {
            bool controlKey = false;
            Program.actEditor.DisplayStatusMessage((string)("Mouse move: " + e.X + "," + e.Y));
            if (ModifierKeys.HasFlag(KeyModifiers.Control))
            {
                controlKey = true;
            }


            if (ToolClicked == ToolClicks.AREA_ADD &&
                itemToUpdate != null &&
                (itemToUpdate.GetType() == typeof(StationAreaItem)))
            {
                PointF point = convertScreen2ViewCoord(e.X, e.Y);
                MSTSCoord coord = Simulator.mstsDataConfig.TileBase.getMstsCoord(point);
                aeConfig.UpdateItem (itemToUpdate, coord, controlKey, false);
                //((StationAreaWidget)itemToUpdate).UpdatePointArea(aeConfig.getSegments(), );
            }

            if (!Dragging && !Zooming)
            {
                CurrentMousePosition.X = e.X;
                CurrentMousePosition.Y = e.Y;
                if (ToolClicked == ToolClicks.MOVE 
                        && itemToUpdate != null 
                        && itemToUpdate.IsMovable())
                {
                    if (e.X < 10 || e.Y < 10 || e.X > routePicture.Width - 10 || e.Y > routePicture.Height - 10)
                    {
                        dragWindow(e, -1);
                    }
                    PointF point = convertScreen2ViewCoord (e.X, e.Y);
                    MSTSCoord coord = Simulator.mstsDataConfig.TileBase.getMstsCoord(point);
                    aeConfig.UpdateItem(itemToUpdate, coord, controlKey, false);
                    //itemToUpdate.configCoord(coord, aeConfig.getSegments(), controlKey);
                }
                else if (ToolClicked == ToolClicks.ROTATE
                        && itemToUpdate != null 
                        && itemToUpdate.IsRotable())
                {
                    PointF point = e.Location;
                    double tempo = Math.Atan2(itemToUpdate.Location2D.Y - point.Y, itemToUpdate.Location2D.X - point.X);
                    tempo = (tempo * 180.0d) / Math.PI;
                    tempo = tempo - 90d;
                    itemToUpdate.setAngle((float)tempo);
                    //itemToUpdate.setAngle(((float)(Math.Atan2(itemToUpdate.Location2D.Y - point.Y, itemToUpdate.Location2D.X - point.X) * 180.0d / Math.PI)) - 90.0f);
                }
                GenerateView();
                return;
            }
            if (Dragging && !Zooming)
            {
                dragWindow(e, 1);
                /*
                SizeF diff = new SizeF(LastCursorPosition.X - e.X, e.Y - LastCursorPosition.Y);
                diff.Width *= usedScale;
                diff.Height *= usedScale;
                Distant2Origin -= diff;
                GenerateView();
                 * */
            }
#if SPA_ADD
            else if (Zooming || windowSizeUpDown.Value < 80)
#else
            else if (Zooming && LastCursorPosition.Y != e.Y)
#endif
            {
                float facteur = 0.98F;
                if (ModifierKeys.HasFlag(KeyModifiers.Control))
                    facteur = 0.90F;
                zoomed = true;
                if (LastCursorPosition.Y - e.Y < 0)
                {
                    currentRules /= facteur;
                }
                else if (LastCursorPosition.Y - e.Y > 0)
                {
                    currentRules *= facteur;
                }
                

                ZoomAndAlignWindow(coordZoomPoint, refZoomPoint);
            }
            LastCursorPosition.X = e.X;
            LastCursorPosition.Y = e.Y;
            GenerateView();
        }

        private void dragWindow(MouseArgs e, int sens)
        {
            int eX = e.X;
            int eY = e.Y;
            //  Le test est important pour conserver le drag fonctionnel
            if (eX < 10 || eY < 10 || eX > routePicture.Width - 10 || eY > routePicture.Height - 10)
            {
                if (eY < 10)
                    eY = LastCursorPosition.Y - (5 * sens);
                else if (eY > routePicture.Height - 10)
                    eY = LastCursorPosition.Y + (5 * sens);
                else
                    eY = LastCursorPosition.Y;
                if (eX < 10)
                {
                    eX = LastCursorPosition.X - (5 * sens);
                }
                else if (eX > routePicture.Width - 10)
                {
                    eX = LastCursorPosition.X + (5 * sens);
                }
                else
                {
                    eX = LastCursorPosition.X;
                }
            }

            SizeF diff = new SizeF(LastCursorPosition.X - eX, eY - LastCursorPosition.Y);
            diff.Width *= usedScale;
            diff.Height *= usedScale;
            Distant2Origin -= diff;
            GenerateView();
        }

        private async void routeDrawingMouseUp(object sender, MouseArgs e)
        {
            if ((ToolClicked == ToolClicks.AREA_ADD ||
                ToolClicked == ToolClicks.MOVE) &&
                itemToUpdate != null && itemToUpdate.GetType() == typeof(StationAreaItem))
            {
                PointF point = convertScreen2ViewCoord(e.X, e.Y);
                MSTSCoord coord = Simulator.mstsDataConfig.TileBase.getMstsCoord(point);
                stationItem = (StationItem)aeConfig.UpdateItem(itemToUpdate, coord, false, true);
                itemToUpdate = stationItem;
                if (stationItem != null && !(ToolClicked == ToolClicks.AREA_ADD))
                {
                    stationItem.complete(aeConfig.aeRouteConfig.orRouteConfig,
                        aeConfig.aeItems,
                        Simulator.mstsDataConfig.TileBase);
                }
            }
            else if (ToolClicked == ToolClicks.AREA &&
                itemToUpdate != null && 
                (itemToUpdate.GetType() == typeof(StationItem) ||
                itemToUpdate.GetType() == typeof(StationAreaItem)))
            {
                SetToolClicked(ToolClicks.AREA_ADD);
            }

            if (LeftClick && ToolClicked != ToolClicks.NO_TOOL &&
                ToolClicked != ToolClicks.DRAG &&
                ToolClicked != ToolClicks.ZOOM)
            {
                PointF point = e.Location;
                lastContextMenuPos = point;
                PlaceToolItem();
            }
            if (LeftClick && !Dragging)
            {
                if (LastCursorPosition.X == e.X && LastCursorPosition.Y == e.Y)
                {
#if To_REMOVE
                    var range = 5 * xScale; if (range > 10) range = 10;
                    var temp = findItemFromMouse(e.X, e.Y, (int)range);
                    if (temp != null)
                    {
                        //
                    }
                    else
                    {
                        //switchPickedItem = null; signalPickedItem = null; UnHandleItemPick(); PickedTrain = null; 
                    }
#endif
                }
            }
            if (RightClick && !zoomed && itemToEdit == null)
            {
                PointF point = e.Location;
                lastContextMenuPos = point;
                PathContextMenu.Open(routeDrawing);
            }
            else if (RightClick && !zoomed && itemToEdit != null && itemToEdit.IsEditable())
            {
                GlobalItem edited = await aeConfig.EditItem(itemToEdit);
                if (edited != null && edited.GetType() == typeof(StationItem))
                {
                    ((StationItem)edited).complete(aeConfig.aeRouteConfig.orRouteConfig,
                        aeConfig.aeItems,
                        Simulator.mstsDataConfig.TileBase);
                }
            }
            zoomed = false;
            Dragging = Zooming = false;

            if (e.Button == MouseButtons.Left) LeftClick = false;
            if (e.Button == MouseButtons.Right) RightClick = false;

            if (ToolClicked != ToolClicks.AREA_ADD)
            {
                CancelOperation();
                ToolClicked = ToolClicks.NO_TOOL;
            }
        }

        void routeDrawing_MouseDoubleClick(object sender, MouseArgs e)
        {
            if (e.Button == MouseButtons.Left)
            {
            }
        }

        private void routeDrawingMouseWheel(object sender, MouseArgs e)
        {
        }

        public void Viewer2D_KeyDown(object sender, KeyEventArgs e)
        {
            SizeF diff;
            //this.routeDrawing.Focus();
            if (e.Key == Key.Escape)
            {
                CancelAllOperation();
                return;
            }
            else if (e.Key == Key.Up)
            {
                diff = new SizeF(0, -1);
            }
            else if (e.Key == Key.Down)
            {
                diff = new SizeF(0, 1);
            }
            else if (e.Key == Key.Left)
            {
                diff = new SizeF(1, 0);
            }
            else if (e.Key == Key.Right)
            {
                diff = new SizeF(-1, 0);
            }
            else
                return;
            diff.Width *= usedScale;
            diff.Height *= usedScale;
            Distant2Origin -= diff;
            GenerateView();

        }

    #endregion

#region AlignView
        private void ZoomViewWindow()
        {
            float width = ViewWindow.Width * (currentRules / origRules);
            float height = ViewWindow.Height * (currentRules / origRules);
            width = (float)Math.Round((double)width, 2, MidpointRounding.AwayFromZero);
            height = (float)Math.Round((double)height, 2, MidpointRounding.AwayFromZero);
            ViewWindow = new RectangleF(0, 0, width, height);
            ComputeUsedScale();
        }

        private void CenterAndZoomWindow ()
        {
            PointF point = convertScreen2ViewCoord(ViewWindow.Width / 2f, ViewWindow.Height / 2f);
            MSTSCoord coord = Simulator.mstsDataConfig.TileBase.getMstsCoord(point);

            ZoomViewWindow();
            GenerateView();
        }

        private void ZoomAndAlignWindow(MSTSCoord coord, PointF point)
        {
            ZoomViewWindow();

            RealignViewWindow(coord, System.Drawing.Point.Truncate (point));
            GenerateView();
        }

        public void CenterViewWindow(MSTSCoord coord)
        {

            PointF point = new PointF(RouteWidth / 2f, RouteHeight / 2f);

            RealignViewWindow(coord, System.Drawing.Point.Truncate(point));
            GenerateView();
        }

        //  RealignViewWindow:
        //  This function try to align the current ViewWindow in order to have the coord under the refPoint
        //  whatever is the zoomFactor
        private void RealignViewWindow(MSTSCoord coord, System.Drawing.Point refPoint)
        {
            System.Drawing.Point refCoord = convertViewCoord2Screen(coord);
            System.Drawing.Point current = refPoint;
            if (current == refCoord)
            {
                //File.AppendAllText(@"C:\temp\AE.txt", "No move needed");
                return;
            }
            var localScale = currentRules / RouteWidth;
            Distant2Origin.Width = (Distant2Origin.Width + ((current.X - refCoord.X) * localScale));
            Distant2Origin.Height = (Distant2Origin.Height) + ((current.Y - refCoord.Y) * localScale);
            return;
        }
        

#endregion

        #region SaveLoad
        public bool Save()
        {
            //aeWidget.saveConfig(wr);
            return false;
        }


        private void CenterView(object sender, EventArgs e)
        {
            Distant2Origin.Width = 0;
            Distant2Origin.Height = 0;
            GenerateView();
        }

        #endregion

        #region Item management
        void AddTag()
        {
            int cnt = rnd.Next(999999);
            PointF point = convertScreen2ViewCoord(lastContextMenuPos.X, lastContextMenuPos.Y);
            MSTSCoord coord = Simulator.mstsDataConfig.TileBase.getMstsCoord(point);
            TagItem tag = new TagItem(ViewerMode);
            tag.configCoord(coord);
            tag.setNameTag(cnt);
            aeConfig.AddORItem(tag);
            System.Drawing.Point current = aeConfig.aeRouteConfig.GetTagPanelPosition();

            if (actParent != null)
            {
                TagWidgetInfo newTagWidget = new TagWidgetInfo(this, tag, cnt);
                aeConfig.AddTagWidgetInfo(newTagWidget);
            }
        }

        private void AddStation()
        {
            int cnt = rnd.Next(999999);
            PointF point = convertScreen2ViewCoord(lastContextMenuPos.X, lastContextMenuPos.Y);
            MSTSCoord coord = Simulator.mstsDataConfig.TileBase.getMstsCoord(point);
            StationItem station = new StationItem(ViewerMode, Simulator.traveller);
            station.configCoord(coord);
            station.setNameStation(cnt);
            aeConfig.AddORItem(station);
            System.Drawing.Point current = aeConfig.aeRouteConfig.GetStationPanelPosition();

            if (actParent != null)
            {
                StationWidgetInfo newStationWidget = new StationWidgetInfo(this, station, cnt);
                aeConfig.AddStationInfo(newStationWidget);
            }
        }

        void AddStationArea()
        {
            int cnt = rnd.Next(999999);
            /*
            PointF point = convertScreen2ViewCoord(lastContextMenuPos.X, lastContextMenuPos.Y);
            MSTSCoord coord = new MSTSCoord();
            coord.Convert(point);
            StationAreaWidget stationArea = new StationAreaWidget();
            stationArea.configCoord(coord);
            System.Drawing.Point current = aeRouteConfig.GetStationPanelPosition();

            if (actParent != null)
            {
                StationWidgetInfo newStationWidget = new StationWidgetInfo(station, cnt);
                stationWidgetInfo.Add(newStationWidget);
                stationGBList.Add(newStationWidget.getStation());
                aeRouteConfig.AddStationPanel(stationGBList.ToArray());
            }
             * */

        }

        private async void AddActStart (TrackSegment segment, System.Drawing.Point CurrentMousePosition)
        {
            int cnt = rnd.Next(999999);
            double dist;
            PointF closest = new PointF(0, 0);

            PointF point = convertScreen2ViewCoord(CurrentMousePosition.X, CurrentMousePosition.Y);
            dist = DrawUtility.FindDistanceToSegment(point, segment, out closest);
            MSTSCoord coord = Simulator.mstsDataConfig.TileBase.getMstsCoord(closest);
            StartActivity newStart = new StartActivity(aeConfig);
            await Dialogs.ShowDialog(newStart);
            ActStartItem actStart = new ActStartItem(ViewerMode);
            actStart.configCoord(coord);
            actStart.setNameStart(cnt);
            aeConfig.AddORItem(actStart);
            aeConfig.AddActItem(actStart);
        }

        private void AddActStop (TrackSegment segment, System.Drawing.Point CurrentMousePosition)
        {
            int cnt = rnd.Next(999999);
            double dist;
            PointF closest = new PointF(0, 0);
            PointF point = convertScreen2ViewCoord(CurrentMousePosition.X, CurrentMousePosition.Y);
            dist = DrawUtility.FindDistanceToSegment(point, segment, out closest);
            MSTSCoord coord = Simulator.mstsDataConfig.TileBase.getMstsCoord(closest);
        }

        private async void AddActWait (TrackSegment segment, System.Drawing.Point CurrentMousePosition)
        {
            int cnt = rnd.Next(999999);
            double dist;
            PointF closest = new PointF(0, 0);

            PointF point = convertScreen2ViewCoord(CurrentMousePosition.X, CurrentMousePosition.Y);
            dist = DrawUtility.FindDistanceToSegment(point, segment, out closest);
            MSTSCoord coord = Simulator.mstsDataConfig.TileBase.getMstsCoord(closest);
            WaitActivity newWait = new WaitActivity();
            newWait.SW_PlaceValue.Text = (string)"wait" + cnt;
            await Dialogs.ShowDialog(newWait);
            ActWaitItem actWait = new ActWaitItem(ViewerMode);
            actWait.configCoord(coord);
            actWait.setNameWait(cnt);
            aeConfig.AddORItem(actWait);
            aeConfig.AddActItem(actWait);
        }


        void PlaceToolItem()
        {
            GlobalItem item;

            switch ((int)ToolClicked)
            {
                case (int)ToolClicks.MOVE:
                    break;
                case (int)ToolClicks.TAG:
                    AddTag();
                    break;
                case (int)ToolClicks.STATION:
                    AddStation();
                    break;
                case (int)ToolClicks.AREA_ADD:
                    AddStationArea();
                    break;
                case (int)ToolClicks.START:
                    item = findSegmentFromMouse(CurrentMousePosition);
                    if (item != null &&
                        item.GetType () == typeof (TrackSegment) &&
                        ((TrackSegment)item).isSnap ())
                    {
                        AddActStart ((TrackSegment)item, CurrentMousePosition);
                    }
                    break;
                case (int)ToolClicks.STOP:
                    item = findSegmentFromMouse(CurrentMousePosition);
                    if (item != null &&
                        item.GetType () == typeof (TrackSegment) &&
                        ((TrackSegment)item).isSnap ())
                    {
                        AddActStop ((TrackSegment)item, CurrentMousePosition);
                    }

                    break;
                case (int)ToolClicks.WAIT:
                    item = findSegmentFromMouse(CurrentMousePosition);
                    if (item != null &&
                        item.GetType () == typeof (TrackSegment) &&
                        ((TrackSegment)item).isSnap())
                    {
                        AddActWait((TrackSegment)item, CurrentMousePosition);
                    }

                    break;
                case (int)ToolClicks.ACTION:
                    break;
                case (int)ToolClicks.CHECK:
                    break;
                case (int)ToolClicks.ROTATE:
                    break;
                default:
                    break;
            }
            SetFocus();
            //this.routeDrawing.Focus();
        }

        public void RemoveTag(int cntStr)
        {
            aeConfig.RemoveTag(cntStr);
            //this.routeDrawing.Focus();

        }

        public void RemoveStation(int cntStr)
        {
            //this.routeDrawing.Focus();
            aeConfig.RemoveStation(cntStr);
        }

        public void TagName_TextChanged(object sender, EventArgs e)
        {
            //this.routeDrawing.Focus();
            var trouve = aeConfig.tagWidgetInfo.Find(place => (((TagWidgetInfo)place).TagName.Name == ((TextBox)sender).Name));
            trouve.tagWidget.nameTag = ((TextBox)sender).Text;

        }

        public void CenterTag_Click(object sender, EventArgs e)
        {
            //this.routeDrawing.Focus();
            TagWidgetInfo trouve = aeConfig.tagWidgetInfo.Find(place => (((TagWidgetInfo)place).TagName.Name == ((Button)sender).Name));
            CenterViewWindow(trouve.tagWidget.Coord);

        }

        public void RemoveTag_Click(object sender, EventArgs e)
        {
            //this.routeDrawing.Focus();
            var trouve = aeConfig.tagWidgetInfo.FindIndex(place => (((TagWidgetInfo)place).TagName.Name == ((Button)sender).Name));
            RemoveTag(trouve);

        }
        public void StationName_TextChanged(object sender, EventArgs e)
        {
            //this.routeDrawing.Focus();
            var trouve = aeConfig.stationWidgetInfo.Find(place => (((StationWidgetInfo)place).StationName.Name == ((TextBox)sender).Name));
            trouve.stationWidget.nameStation = ((TextBox)sender).Text;

        }
        public void CenterStation_Click(object sender, EventArgs e)
        {
            //this.routeDrawing.Focus();
            StationWidgetInfo trouve = aeConfig.stationWidgetInfo.Find(place => (((StationWidgetInfo)place).StationName.Name == ((Button)sender).Name));
            CenterViewWindow(trouve.stationWidget.Coord);

        }

        public void RemoveStation_Click(object sender, EventArgs e)
        {
            //this.routeDrawing.Focus();
            var trouve = aeConfig.stationWidgetInfo.FindIndex(place => (((StationWidgetInfo)place).StationName.Name == ((Button)sender).Name));
            RemoveStation(trouve);

        }

        #endregion

        //  ++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++
        #region utility
        public PointF convertScreen2ViewCoord(float x, float y)
        {
            PointF val = new PointF(0, 0);
            float scale = (float)getUsedScale();   //   
            x = x * scale;
            y = (RouteHeight - y) * scale;
            float tempX = x - Distant2Origin.Width;
            val.X = tempX;
            val.Y = y - Distant2Origin.Height;
            //File.AppendAllText(@"F:\temp\AE.txt", "convertScreen2ViewCoord: x:" + x + " y:" + y);
            //File.AppendAllText(@"F:\temp\AE.txt", " donnent valX: " + val.X + " valY:" + val.Y + "\n");
            return val;
        }

        public System.Drawing.Point convertViewCoord2Screen(MSTSCoord coord)
        {
            return System.Drawing.Point.Truncate((PointF)convertViewCoord2ScreenF(coord));
        }

        public PointF convertViewCoord2ScreenF(MSTSCoord coord)
        {
            PointF val = new PointF(0, 0);
            val.X = (((coord.TileX * 2048f) + coord.X));    // - areaRoute.getMinX());
            val.Y = (((coord.TileY * 2048f) + coord.Y));    // - areaRoute.getMinY());
            val.X += Distant2Origin.Width;
            val.Y += Distant2Origin.Height;
            val.X /= usedScale;
            val.Y /= usedScale;
            //val.Y = routeDrawing.Height - val.Y;
            return val;
        }

        #endregion

        public void UnsetFocus()
        {
            aeConfig.UnsetFocus(ViewerMode);
        }

        public void SetFocus()
        {
            aeConfig.SetFocus(ViewerMode);
        }

        /// <summary>Called when the view is closed (was the FormClosing handler of the MDI child).</summary>
        public async Task CloseViewer()
        {
            ActivityTimer.Stop();
            Simulator.StopUpdaterThread();
            await aeConfig.Close(ViewerMode);
            actParent.UnselectTools(ViewerMode);
        }


        private void routeDrawing_Click(object sender, EventArgs e)
        {
            /*
            this.MouseWheel += new System.Windows.Forms.MouseEventHandler(this.routeDrawingMouseWheel);
            if (LastCursorPosition.X == e.X && LastCursorPosition.Y == e.Y)
            {
                var range = 5 * (int)xScale; if (range > 10) range = 10;
                var temp = findItemFromMouse(e.X, e.Y, range);
            }
            */
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

        private void refreshButton_Click(object sender, EventArgs e)
        {
            GenerateView();
        }

        private RectangleF GetRect(PointF p, float size)
        {
            return new RectangleF(p.X - size / 2f, p.Y - size / 2f, size, size);
        }

        private RectangleF GetRect(PointF p, float sizeX, float sizeY)
        {
            return new RectangleF(p.X - sizeX / 2f, p.Y - sizeY / 2f, sizeX, sizeY);
        }

        void CancelAllOperation()
        {
            CancelOperation();
            Program.actEditor.UnsetToolClick();
        }

        void CancelOperation()
        {
            if (itemToUpdate != null)
                itemToUpdate.complete(aeConfig.aeRouteConfig.orRouteConfig, aeConfig.aeItems, Simulator.mstsDataConfig.TileBase);
            itemToUpdate = null;
            Dragging = false;
            Zooming = false;
            LeftClick = false;
            RightClick = false;
            zoomed = false;
        }

        public string getDescr()
        {
            return aeConfig.getActivityDescr();
        }

        public void setDescr(string info)
        {
            aeConfig.setActivityDescr(info);
        }

        protected void loadIcon()
        {
            Stream st;
            Assembly a = Assembly.GetExecutingAssembly();
            string[] resName = a.GetManifestResourceNames();
            st = a.GetManifestResourceStream("ActivityEditor.icon.tag.ico");
            TagIcon = new Icon(st);
            st = a.GetManifestResourceStream("ActivityEditor.icon.SignalBox.ico");
            StationIcon = new Icon(st);
            st = a.GetManifestResourceStream("ActivityEditor.icon.signal.ico");
            SignalIco = new Icon(st);
            st = a.GetManifestResourceStream("ActivityEditor.icon.signalShunt.ico");
            ShuntIco = new Icon(st);
            st = a.GetManifestResourceStream("ActivityEditor.icon.signalOther.ico");
            OtherSigIco = new Icon(st);
            st = a.GetManifestResourceStream("ActivityEditor.icon.speedLimit.ico");
            SpeedIco = new Icon(st);
            st = a.GetManifestResourceStream("ActivityEditor.icon.signalRepeat.ico");
            RepeatIco = new Icon(st);
            st = a.GetManifestResourceStream("ActivityEditor.icon.StationConnector.ico");
            StationConnector = new Icon(st);

            st = a.GetManifestResourceStream("ActivityEditor.icon.Ruler.png");
            Ruler = Image.FromStream(st);

        }

        private GlobalItem findItemFromEvent(MouseArgs e)
        {
            PointF pt = convertScreen2ViewCoord(e.X, e.Y);
            GlobalItem item = findItemFromCoord(pt);
            return item;
        }

        private GlobalItem findSegmentFromMouse(System.Drawing.Point CurrentMousePosition)
        {
            GlobalItem item = null;
            if (aeItems != null)
            {
                PointF pt = convertScreen2ViewCoord((float)CurrentMousePosition.X, (float)CurrentMousePosition.Y);
                item = aeItems.findSegmentFromMouse(pt, (double)(Program.aePreference.getSnapCircle()/2));
                // sideItem = aeItems.findSegmentFromMouse(pt, snapSize);
            }
            return item;
        }

        private GlobalItem findSegmentFromCoord(PointF pt)
        {
            GlobalItem item = null;
            if (aeItems != null)
            {
                item = aeItems.findSegmentFromMouse(pt, snapSize);
            }
            return item;
        }

        public GlobalItem findItemFromMouse(PointF point)
        {
            if (Simulator.orRouteConfig == null || aeItems == null)
            {
                return null;
            }
            findSegmentFromMouse(CurrentMousePosition);
            PointF pt = convertScreen2ViewCoord((float)point.X, (float)point.Y);
            //PointF tf2 = new PointF(0f, 0f);
            //GlobalItem componentItem = Simulator.orRouteConfig.findMetadataItem(pt, snapSize, aeItems);
            GlobalItem item = Simulator.orRouteConfig.FindMetadataItem(pt, (double)(Program.aePreference.getSnapCircle()/2), aeItems);
            return item;
        }

        public GlobalItem findItemFromCoord(PointF pt)
        {
            GlobalItem item;
            GlobalItem segmentItem;
            if (Simulator.orRouteConfig == null || aeItems == null)
            {
                return null;
            }
            segmentItem = findSegmentFromMouse(CurrentMousePosition);
            item = Simulator.orRouteConfig.FindMetadataItem(pt, (double)(Program.aePreference.getSnapCircle() / 2), aeItems);
            if (item == null && segmentItem != null && segmentItem.IsEditable())
            {
                return segmentItem;
            }
            //  GlobalItem componentItem = Simulator.orRouteConfig.findMetadataItem(pt, snapSize, aeItems);
            return item;
        }

        private void FindItem(System.Drawing.Point Last, MouseArgs e)
        {
            if ((Last.X == e.X) && (Last.Y == e.Y))
            {
                Predicate<GlobalItem> match = null;
                PointF pt = convertScreen2ViewCoord(e.X, e.Y);
                GlobalItem item = findItemFromCoord(pt);
                if ((item != null) && (item.GetType() == typeof(TagItem)))
                {
                    Predicate<TagWidgetInfo> predicate = null;
                    if (match == null)
                    {
                        match = place => ((place.GetType() == typeof(TagItem)) && (((TagItem)place).Location2D.X == ((TagItem)item).Location2D.X)) && (((TagItem)place).Location2D.Y == ((TagItem)item).Location2D.Y);
                    }
                    TagItem info = (TagItem)Simulator.orRouteConfig.AllItems.Find(match);
                    if (info != null)
                    {
                        if (predicate == null)
                        {
                            predicate = place => place.TagName.Name == info.nameTag;
                        }
                        TagWidgetInfo infoTag = aeConfig.findTagWidget(predicate);
                    }
                }
            }
        }

        public void SetToolClicked(ToolClicks info)
        {
            ToolClicked = info;

            routeDrawing.Cursor = Cursor.Default;
            if (info == ToolClicks.AREA_ADD)
            {   //  To prevent bad setting
                ToolToUse = AppResources.LoadImage("Resources/64.png");
                CursorToUse = reduit;
            }
            else if (info == ToolClicks.AREA)
            {
                ToolToUse = AppResources.LoadImage("Resources/32.png");
                CursorToUse = reduit;
            }
            else if (info == ToolClicks.DRAG)
            {
                ToolToUse = null;
                CursorToUse = new Cursor(StandardCursorType.Hand);
            }
            else if (info == ToolClicks.ZOOM)
            {
                ToolToUse = null;
                CursorToUse = new Cursor(StandardCursorType.SizeAll);
            }
            else if (info == ToolClicks.MOVE)
            {
                ToolToUse = AppResources.LoadImage("icon/object_move.png");
                CursorToUse = Cursor.Default;
            }
            else if (info == ToolClicks.ROTATE)
            {
                ToolToUse = AppResources.LoadImage("icon/object_rotate.png");
                CursorToUse = reduit;
            }
            else if (info == ToolClicks.TAG)
            {
                ToolToUse = AppResources.LoadImage("icon/tag.gif");
                CursorToUse = reduit;
            }
            else if (info == ToolClicks.METASEGMENT)
            {
                ToolToUse = AppResources.LoadImage("Resources/metasegment.png");
                CursorToUse = reduit;
            }
            else if (info == ToolClicks.STATION)
            {
                ToolToUse = AppResources.LoadImage("icon/SignalBox.ico");
                CursorToUse = reduit;
            }
            else
            {
                ToolClicked = ToolClicks.NO_TOOL;
                CursorToUse = Cursor.Default;
                ToolToUse = null;
            }
            routeDrawing.Cursor = CursorToUse;
        }


#if TOREMOVE
        private void rmvButton_Click(object sender, EventArgs e)
        {
        }

        private void leftButton_MouseDown(object sender, MouseEventArgs e)
        {
            LeftButtonDown = true;
        }

        private void leftButton_MouseLeave(object sender, EventArgs e)
        {
            LeftButtonDown = false;
        }

        private void leftButton_MouseUp(object sender, MouseEventArgs e)
        {
            LeftButtonDown = false;
        }

        private void rightButton_MouseUp(object sender, MouseEventArgs e)
        {
            RightButtonDown = false;
        }

        private void rightButton_MouseDown(object sender, MouseEventArgs e)
        {
            RightButtonDown = true;
        }

        private void rightButton_MouseLeave(object sender, EventArgs e)
        {
            RightButtonDown = false;
        }

        private void upButton_MouseUp(object sender, MouseEventArgs e)
        {
            UpButtonDown = false;
        }

        private void upButton_MouseDown(object sender, MouseEventArgs e)
        {
            UpButtonDown = true;
        }

        private void upButton_MouseLeave(object sender, EventArgs e)
        {
            UpButtonDown = false;
        }

        private void downButton_MouseUp(object sender, MouseEventArgs e)
        {
            DownButtonDown = false;
        }

        private void downButton_MouseDown(object sender, MouseEventArgs e)
        {
            DownButtonDown = true;
        }

        private void downButton_MouseLeave(object sender, EventArgs e)
        {
            DownButtonDown = false;
        }
#endif

    }

    /// <summary>Mouse button, as System.Windows.Forms.MouseButtons.</summary>
    public enum MouseButtons { None, Left, Right, Middle }

    /// <summary>The parts of System.Windows.Forms.MouseEventArgs used by the viewer.</summary>
    public class MouseArgs : EventArgs
    {
        public int X { get; set; }
        public int Y { get; set; }
        public MouseButtons Button { get; set; }
        public PointF Location { get { return new PointF(X, Y); } }
    }

    /// <summary>
    /// The drawing area of a <see cref="Viewer2D"/> (was a PictureBox showing a bitmap).
    /// </summary>
    public class RouteCanvas : Control
    {
        readonly Viewer2D Viewer;

        public RouteCanvas(Viewer2D viewer)
        {
            Viewer = viewer;
            Focusable = true;
            ClipToBounds = true;
        }

        public override void Render(DrawingContext context)
        {
            using (var g = new Graphics(context, Bounds.Size))
            {
                g.Clear(Color.White);
                try
                {
                    Viewer.DrawView(g);
                }
                catch (Exception error)
                {
                    System.Diagnostics.Trace.TraceError("Error drawing the route view: {0}", error);
                }
            }
        }
    }

    public partial class Viewer2D
    {
        public RouteCanvas routeDrawing;
        public ContextMenu PathContextMenu;
        MenuItem CenterCMItem1;
        KeyModifiers ModifierKeys;

        /// <summary>Title of the view (shown in the tab header).</summary>
        public string Text { get; set; } = "Activity";

        int RouteWidth { get { return routeDrawing == null ? IM_Width : Math.Max(1, (int)routeDrawing.Bounds.Width); } }
        int RouteHeight { get { return routeDrawing == null ? IM_Height : Math.Max(1, (int)routeDrawing.Bounds.Height); } }

        private void InitializeComponent()
        {
            routeDrawing = new RouteCanvas(this);
            CenterCMItem1 = new MenuItem { Header = "Center view" };
            CenterCMItem1.Click += CenterView;
            PathContextMenu = new ContextMenu();
            PathContextMenu.Items.Add(CenterCMItem1);

            routeDrawing.PointerPressed += (s, e) =>
            {
                routeDrawing.Focus();
                ModifierKeys = e.KeyModifiers;
                var point = e.GetCurrentPoint(routeDrawing);
                var button = point.Properties.PointerUpdateKind == PointerUpdateKind.RightButtonPressed ? MouseButtons.Right
                    : point.Properties.PointerUpdateKind == PointerUpdateKind.MiddleButtonPressed ? MouseButtons.Middle : MouseButtons.Left;
                var args = new MouseArgs { X = (int)point.Position.X, Y = (int)point.Position.Y, Button = button };
                if (e.ClickCount == 2)
                    routeDrawing_MouseDoubleClick(s, args);
                routeDrawingMouseDown(s, args);
                e.Handled = true;
            };
            routeDrawing.PointerMoved += (s, e) =>
            {
                ModifierKeys = e.KeyModifiers;
                var point = e.GetPosition(routeDrawing);
                routeDrawingMouseMove(s, new MouseArgs { X = (int)point.X, Y = (int)point.Y });
            };
            routeDrawing.PointerReleased += (s, e) =>
            {
                ModifierKeys = e.KeyModifiers;
                var point = e.GetPosition(routeDrawing);
                var button = e.InitialPressMouseButton == MouseButton.Right ? MouseButtons.Right
                    : e.InitialPressMouseButton == MouseButton.Middle ? MouseButtons.Middle : MouseButtons.Left;
                routeDrawingMouseUp(s, new MouseArgs { X = (int)point.X, Y = (int)point.Y, Button = button });
                e.Handled = true;
            };
            routeDrawing.PointerWheelChanged += (s, e) =>
            {
                var point = e.GetPosition(routeDrawing);
                routeDrawingMouseWheel(s, new MouseArgs { X = (int)point.X, Y = (int)point.Y });
            };
            routeDrawing.KeyDown += Viewer2D_KeyDown;
            routeDrawing.SizeChanged += (s, e) =>
            {
                // The WinForms PictureBox had its size from the start; here the size is only known after layout.
                if (!loaded && routeDrawing.Bounds.Width > 0 && routeDrawing.Bounds.Height > 0)
                {
                    IM_Width = RouteWidth;
                    IM_Height = RouteHeight;
                    InitData();
                }
                GenerateView();
            };
            Content = routeDrawing;
        }

        readonly Dictionary<string, Icon> EventIcons = new Dictionary<string, Icon>();

        /// <summary>Icon of an activity event (start/stop/wait), loaded from LibAE's resources and cached.</summary>
        Icon GetEventIcon(PathEventItem item)
        {
            string name = item.getIconName();
            if (name == null)
                return null;
            if (!EventIcons.TryGetValue(name, out var icon))
            {
                using (var stream = item.getIconStream())
                    icon = stream == null ? null : new Icon(stream);
                EventIcons[name] = icon;
            }
            return icon;
        }

        /// <summary>Give the keyboard focus to the drawing area.</summary>
        public void FocusView()
        {
            routeDrawing?.Focus();
        }
    }

    public class TagWidgetInfo
    {
        public GroupBox OneTagGB;
        public TextBox TagName;
        public CheckBox NameDisplay;
        public Button CenterTag;
        public Button RemoveTag;
        public TagItem tagWidget;
        public bool selectedTag = false;
        private Viewer2D Parent;

        public TagWidgetInfo(Viewer2D parent, TagItem tag, int cnt)
        {
            Parent = parent;
            string tagName = "tag" + cnt;
            TagName = new TextBox { Name = tagName, Width = 130, Text = tag.nameTag };
            TagName.TextChanged += Parent.TagName_TextChanged;
            TagName.KeyDown += Parent.Viewer2D_KeyDown;
            NameDisplay = new CheckBox { Content = "Display", IsChecked = false };
            RemoveTag = new Button { Name = tagName, Content = "x", FontSize = 9, Padding = new Avalonia.Thickness(4, 0) };
            RemoveTag.Click += Parent.RemoveTag_Click;
            CenterTag = new Button { Name = tagName, Content = "v", FontSize = 9, Padding = new Avalonia.Thickness(4, 0) };
            CenterTag.Click += Parent.CenterTag_Click;
            OneTagGB = new GroupBox
            {
                Text = "Tag",
                Width = 175,
                Content = new StackPanel
                {
                    Spacing = 2,
                    Children =
                    {
                        TagName,
                        new StackPanel { Orientation = Avalonia.Layout.Orientation.Horizontal, Spacing = 4, Children = { NameDisplay, RemoveTag, CenterTag } },
                    },
                },
            };

            tagWidget = tag;
        }

        public GroupBox getTag()
        {
            return OneTagGB;
        }

    }

    public class StationWidgetInfo
    {
        public GroupBox OneStationGB;
        public TextBox StationName;
        public CheckBox NameDisplay;
        public Button CenterStation;
        public Button RemoveStation;
        public StationItem stationWidget;
        public bool selectedStation = false;
        Viewer2D Parent;

        public StationWidgetInfo(Viewer2D parent, StationItem station, int cnt)
        {
            Parent = parent;
            string stationName = "st" + cnt;
            StationName = new TextBox { Name = stationName, Width = 130, Text = station.nameStation };
            StationName.TextChanged += Parent.StationName_TextChanged;
            StationName.KeyDown += Parent.Viewer2D_KeyDown;
            NameDisplay = new CheckBox { Content = "Display", IsChecked = false };
            RemoveStation = new Button { Name = stationName, Content = "x", FontSize = 9, Padding = new Avalonia.Thickness(4, 0) };
            RemoveStation.Click += Parent.RemoveStation_Click;
            CenterStation = new Button { Name = stationName, Content = "v", FontSize = 9, Padding = new Avalonia.Thickness(4, 0) };
            CenterStation.Click += Parent.CenterStation_Click;
            OneStationGB = new GroupBox
            {
                Text = "Station",
                Width = 175,
                Content = new StackPanel
                {
                    Spacing = 2,
                    Children =
                    {
                        StationName,
                        new StackPanel { Orientation = Avalonia.Layout.Orientation.Horizontal, Spacing = 4, Children = { NameDisplay, RemoveStation, CenterStation } },
                    },
                },
            };

            stationWidget = station;
        }
        public GroupBox getStation()
        {
            return OneStationGB;
        }

    }
}
