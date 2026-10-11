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

// The map window ("dispatcher window"). The window itself is an Avalonia window living on the
// Avalonia UI thread (see UiThread.cs); this class is created on the render thread and is the
// object the rest of the game talks to (Program.MapForm).
// The window layout and input handling is in MapViewer.Window.cs, the drawing code is here.

using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.Linq;
using Avalonia;
using Avalonia.Media;
using Avalonia.Media.Immutable;
using Microsoft.Xna.Framework;
using Orts.Formats.Msts;
using Orts.MultiPlayer;
using Orts.Simulation;
using Orts.Simulation.AIs;
using Orts.Simulation.Physics;
using Orts.Simulation.RollingStocks;
using Orts.Simulation.Signalling;
using Orts.Simulation.Timetables;
using Orts.Viewer3D.Map;
using ORTS.Common;
using Color = Avalonia.Media.Color;
using PointF = System.Drawing.PointF;
using RectangleF = System.Drawing.RectangleF;
using SignallingDebugWindow = Orts.Viewer3D.Popups.SignallingDebugWindow;

namespace Orts.Viewer3D.Debugging
{
    public partial class MapViewer : IDisposable
    {
        #region Variables
        /// <summary>
        /// Reference to the main simulator object.
        /// </summary>
        public readonly Simulator simulator;
        private readonly MapDataProvider MapDataProvider;
        private readonly MapThemeProvider MapThemeProvider;
        private string ThemeName = "light";
        private ThemeStyle Theme;
        public Viewer Viewer;

        /// <summary>
        /// Always true (like the WinForms <c>Form.Enabled</c> it replaces); visibility is controlled by <see cref="Viewer.MapViewerEnabled"/>.
        /// </summary>
        public bool Enabled { get; set; } = true;

        /// <summary>
        /// True when the user is dragging the route view
        /// </summary>
        public bool Dragging;
        private WorldPosition worldPos;
        public float xScale = 1; // pixels / metre
        public float yScale = 1; // pixels / metre

        public List<SwitchWidget> switchItemsDrawn;
        public List<SignalWidget> signalItemsDrawn;
        // Read by Viewer.cs on the updater thread
        public volatile SwitchWidget switchPickedItem;
        public volatile SignalWidget signalPickedItem;
        public TrainWidget trainPickedItem;
        public bool switchPickedItemHandled;
        public double switchPickedTime;
        public bool signalPickedItemHandled;
        public double signalPickedTime;
        public bool DrawPath = true; // Whether the train path should be drawn
        readonly TrackNode[] nodes;

        public List<Train> selectedTrainList;
        /// <summary>
        /// Contains the last position of the mouse
        /// </summary>
        private System.Drawing.Point LastCursorPosition = new System.Drawing.Point();

        static readonly double[] DashPattern = { 4, 2 };
        static readonly Color RedColor = Color.FromRgb(244, 67, 54);
        static readonly Color GreenColor = Color.FromRgb(76, 175, 80);
        static readonly Color OrangeColor = Color.FromRgb(255, 235, 59);
        static readonly Color PathColor = Color.FromRgb(52, 152, 219);
        static readonly Color ZoomTargetColor = Color.FromRgb(46, 64, 83);
        Color TrackColor = Color.FromRgb(46, 64, 83);
        Color PlatformColor = Colors.Blue;

        // Pen widths, as in the WinForms version
        float grayPenWidth = 1;
        float signalPenWidth = 1; // greenPen, orangePen, redPen
        float pathPenWidth = 2;
        float trainPenWidth = 6;
        Color trainPenColor = Colors.DarkGreen;

        // Fonts: "Segoe UI Semibold" 10pt in the WinForms version
        public const float LabelFontSizePt = 10; // Also used as the approximate width of a character of a label
        const double LabelFontSizePx = LabelFontSizePt * 96.0 / 72.0;
        static readonly Typeface trainFont = new Typeface(FontFamily.Default, FontStyle.Normal, FontWeight.Bold);
        static readonly Typeface labelFont = new Typeface(FontFamily.Default, FontStyle.Normal, FontWeight.SemiBold);
        static readonly IBrush trainBrush = new ImmutableSolidColorBrush(Colors.Red);
        static readonly IBrush sidingBrush = new ImmutableSolidColorBrush(Colors.Blue);
        static readonly IBrush PlatformBrush = new ImmutableSolidColorBrush(Colors.DarkBlue);
        static readonly IBrush SignalBrush = new ImmutableSolidColorBrush(Colors.DarkRed);
        static readonly IBrush InactiveTrainBrush = new ImmutableSolidColorBrush(Colors.DarkRed);
        private Color MapCanvasColor = Colors.White;

        // The train selected by clicking on it on the map or indirectly via the "follow" or "jump to" train options.
        // Read by Viewer.cs on the updater thread.
        public volatile Train PickedTrain = Program.Simulator.PlayerLocomotive.Train;
        /// <summary>
        /// Defines the area to view, in meters. The left edge is meters from the leftmost extent of the route.
        /// </summary>
        public RectangleF ViewWindow;

        // Extents of the route in meters measured from the World origin
        public float minX = float.MaxValue;
        public float minY = float.MaxValue;
        public float maxX = float.MinValue;
        public float maxY = float.MinValue;

        public int RedrawCount;
        private double lastUpdateTime;

        private bool MapCustomizationVisible = false;

        /// <summary>Maximum of the map resolution control, limited to the size of the route</summary>
        decimal MapResolutionMaximum = 200000;
        const decimal MapResolutionMinimum = 80;

        /// <summary>Size of the map canvas in pixels, as of the last draw</summary>
        public int CanvasWidth, CanvasHeight;

        /// <summary>Actions that change game state, executed on the updater thread by <see cref="RunPendingGameActions"/></summary>
        readonly ConcurrentQueue<Action> GameActions = new ConcurrentQueue<Action>();
        bool DrawErrorReported;
        #endregion

        public MapViewer(Simulator simulator, Viewer viewer)
        {
            if (simulator == null)
                throw new ArgumentNullException("simulator", "Simulator object cannot be null.");

            this.simulator = simulator;
            Viewer = viewer;
            MapDataProvider = new MapDataProvider(this);
            MapThemeProvider = new MapThemeProvider();
            MapThemeProvider.InitializeThemes();
            Theme = MapThemeProvider.GetTheme(ThemeName);
            nodes = simulator.TDB.TrackDB.TrackNodes;

            ViewWindow = new RectangleF(0, 0, 5000f, 5000f);
            selectedTrainList = new List<Train>();

            InitializeData();

            MPManager.Instance().MessageReceived += MPManager_MessageReceived;

            // The window is created on the Avalonia UI thread
            UiThread.Post(CreateWindow);
        }

        void MPManager_MessageReceived(object sender, MPManager.MessageReceivedEventArgs e)
        {
            UiThread.Post(() => AddNewMessage(e.Time, e.Message));
        }

        /// <summary>
        /// Queue an action that changes game state, to be run on the updater thread.
        /// </summary>
        void PostToGame(Action action)
        {
            GameActions.Enqueue(action);
        }

        /// <summary>
        /// Execute the game state changes requested from the map window. Called by the viewer on the updater thread.
        /// </summary>
        public void RunPendingGameActions()
        {
            var any = false;
            while (GameActions.TryDequeue(out var action))
            {
                any = true;
                try
                {
                    action();
                }
                catch (Exception error)
                {
                    Trace.TraceWarning("Map window action failed: {0}", error);
                }
            }
            // Show the result as soon as possible
            if (any)
                UiThread.Post(GenerateView);
        }

        #region initData
        private void InitializeData()
        {
            switchItemsDrawn = new List<SwitchWidget>();
            signalItemsDrawn = new List<SignalWidget>();
            switches = new List<SwitchWidget>();
            for (int i = 0; i < nodes.Length; i++)
            {
                TrackNode currNode = nodes[i];

                if (currNode != null)
                {
                    if (currNode.TrEndNode)
                    {
                        //buffers.Add(new PointF(currNode.UiD.TileX * 2048 + currNode.UiD.X, currNode.UiD.TileZ * 2048 + currNode.UiD.Z));
                    }
                    else if (currNode.TrVectorNode != null)
                    {

                        if (currNode.TrVectorNode.TrVectorSections.Length > 1)
                        {
                            AddSegments(segments, currNode, currNode.TrVectorNode.TrVectorSections, ref minX, ref minY, ref maxX, ref maxY, simulator);
                        }
                        else
                        {
                            TrVectorSection s = currNode.TrVectorNode.TrVectorSections[0];

                            foreach (TrPin pin in currNode.TrPins)
                            {

                                TrackNode connectedNode = nodes[pin.Link];

                                dVector A = new dVector(s.TileX, s.X, s.TileZ, +s.Z);
                                dVector B = new dVector(connectedNode.UiD.TileX, connectedNode.UiD.X, connectedNode.UiD.TileZ, connectedNode.UiD.Z);
                                segments.Add(new LineSegment(A, B, /*s.InterlockingTrack.IsOccupied*/ false, null));
                            }
                        }
                    }
                    else if (currNode.TrJunctionNode != null)
                    {
                        foreach (TrPin pin in currNode.TrPins)
                        {
                            var vectorSections = nodes[pin.Link]?.TrVectorNode?.TrVectorSections;
                            if (vectorSections == null || vectorSections.Length < 1)
                                continue;
                            TrVectorSection item = pin.Direction == 1 ? vectorSections.First() : vectorSections.Last();
                            dVector A = new dVector(currNode.UiD.TileX, currNode.UiD.X, currNode.UiD.TileZ, +currNode.UiD.Z);
                            dVector B = new dVector(item.TileX, +item.X, item.TileZ, +item.Z);
                            var x = dVector.DistanceSqr(A, B);
                            if (x < 0.1) continue;
                            segments.Add(new LineSegment(B, A, /*s.InterlockingTrack.IsOccupied*/ false, item));
                        }
                        switches.Add(new SwitchWidget(currNode));
                    }
                }
            }

            var maxsize = maxX - minX > maxY - minY ? maxX - minX : maxY - minY;
            // Take up to next 500
            maxsize = (int)((maxsize / 100) + 1) * 500;
            if ((decimal)maxsize < MapResolutionMaximum)
            {
                // do not make maximum larger then the maximum defined in the Designer
                MapResolutionMaximum = (decimal)maxsize;
            }
            Inited = true;

            if (simulator.TDB == null || simulator.TDB.TrackDB == null || simulator.TDB.TrackDB.TrItemTable == null)
                return;

            MapDataProvider.PopulateItemLists();
        }

        bool Inited;
        public List<LineSegment> segments = new List<LineSegment>();
        public List<SwitchWidget> switches;
        public List<SignalWidget> signals = new List<SignalWidget>();
        public List<SidingWidget> sidings = new List<SidingWidget>();
        public List<PlatformWidget> platforms = new List<PlatformWidget>();
        #endregion

        #region playersList
        readonly List<string> PlayersList = new List<string>();

        public void AddPlayer(string name)
        {
            PlayersList.Add(name);
        }

        int DisconnectedPlayersCount = 0;
        int AssistantPlayersCount = 0;
        public void CheckPlayers()
        {
            if (Dragging || !MPManager.IsMultiPlayer() || MPManager.OnlineTrains == null || MPManager.OnlineTrains.Players == null) return;
            var players = MPManager.OnlineTrains.Players;
            var username = MPManager.GetUserName();
            players = players.Concat(MPManager.Instance().lostPlayer).ToDictionary(x => x.Key, x => x.Value);
            if (playersView.Items.Count == players.Count + 1 && DisconnectedPlayersCount == MPManager.Instance().lostPlayer.Count && AssistantPlayersCount == MPManager.Instance().aiderList.Count) return;

            DisconnectedPlayersCount = MPManager.Instance().lostPlayer.Count;
            AssistantPlayersCount = MPManager.Instance().aiderList.Count;

            // Repopuale `PlayersList`
            PlayersList.Clear();
            AddPlayer(username);
            foreach (var p in players)
            {
                if (PlayersList.Contains(p.Key)) continue;
                AddPlayer(p.Key);
            }

            playersView.Items.Clear();
            foreach (var p in PlayersList)
            {
                if (p == username)
                    playersView.Items.Add(CreatePlayerItem(p, p + " [" + Viewer.Catalog.GetString("You") + "]", FontWeight.Bold, null));
                else if (MPManager.Instance().aiderList.Contains(p))
                    playersView.Items.Add(CreatePlayerItem(p, p + " [" + Viewer.Catalog.GetString("Helper") + "]", FontWeight.Normal, Color.FromRgb(40, 116, 166)));
                else if (MPManager.Instance().lostPlayer.ContainsKey(p))
                    playersView.Items.Add(CreatePlayerItem(p, p + " [" + Viewer.Catalog.GetString("Disconnected") + "]", FontWeight.Normal, Colors.Gray));
                else
                    playersView.Items.Add(CreatePlayerItem(p, p, FontWeight.Normal, null));
            }
        }
        #endregion

        #region Draw
        public bool FirstShow = true;
        public bool FollowTrain;
        public float subX, subY;
        public float oldWidth;
        public float oldHeight;

        /// <summary>
        /// Regenerates the 2D view. The actual drawing happens when Avalonia renders the canvas (<see cref="Draw"/>),
        /// which examines the track network each time the view is drawn.
        /// </summary>
        public void GenerateView()
        {
            if (!Inited || Window == null) return;

            timeLabel.IsVisible = IsChecked(showTimeCheckbox);
            if (IsChecked(showTimeCheckbox))
                MapDataProvider.ShowSimulationTime();

            if (FirstShow || FollowTrain)
            {
                WorldPosition pos = PickedTrain?.Cars?.FirstOrDefault()?.WorldPosition;

                if (pos != null)
                {
                    var ploc = new PointF((pos.TileX * 2048) + pos.Location.X, (pos.TileZ * 2048) + pos.Location.Z);
                    ViewWindow.X = ploc.X - minX - (ViewWindow.Width / 2); ViewWindow.Y = ploc.Y - minY - (ViewWindow.Width / 2);
                    FirstShow = false;
                }
            }

            try
            {
                CheckPlayers();
            }
            catch (InvalidOperationException) { } // Player list modified by the multiplayer code, try again next time

            mapCanvas.InvalidateVisual(); // Triggers a re-paint
        }

        /// <summary>
        /// Draws the map. Called from <see cref="MapCanvas.Render"/> on the UI thread.
        /// </summary>
        internal void Draw(DrawingContext g, Avalonia.Size size)
        {
            CanvasWidth = (int)size.Width;
            CanvasHeight = (int)size.Height;
            g.FillRectangle(new ImmutableSolidColorBrush(MapCanvasColor), new Rect(size));
            if (!Inited || CanvasWidth <= 0 || CanvasHeight <= 0) return;

            try
            {
                DrawMap(g);
            }
            catch (Exception error)
            {
                // The simulator runs concurrently on the updater thread, so collections may change under our feet.
                // Just skip this frame; the next one will most likely succeed.
                if (!DrawErrorReported)
                {
                    DrawErrorReported = true;
                    Trace.TraceInformation("Map window: skipped drawing a frame: {0}", error.Message);
                }
            }
        }

        void DrawMap(DrawingContext g)
        {
            subX = minX + ViewWindow.X; subY = minY + ViewWindow.Y;

            xScale = CanvasWidth / ViewWindow.Width;
            yScale = CanvasHeight / ViewWindow.Height;
            xScale = yScale = Math.Max(xScale, yScale); // Make X and Y scales the same to maintain correct angles

            // Set the default pen to represent 1 meter
            var scale = (float)Math.Round(xScale); // Round to nearest pixels/meter
            var penWidth = (int)MathHelper.Clamp(scale, 1, 4); // Keep 1 <= width <= 4 pixels

            grayPenWidth = signalPenWidth = penWidth;
            pathPenWidth = penWidth * 2;
            trainPenWidth = grayPenWidth * 6;

            var forwardDist = 100 / xScale; if (forwardDist < 5) forwardDist = 5;

            // Draw platforms first because track is drawn over the thicker platform line
            DrawPlatforms(g, penWidth);

            PointF scaledA, scaledB;
            DrawTrack(g, out scaledA, out scaledB);

            // Draw trains and path
            DrawTrains(g, scaledA, scaledB);

            // Keep widgetWidth <= 15 pixels
            var widgetWidth = Math.Min(penWidth * 6, 15);

            // Draw signals on top of path so they are easier to see.
            signalItemsDrawn.Clear();
            ShowSignals(g, scaledB, widgetWidth);

            // Draw switches
            switchItemsDrawn.Clear();
            ShowSwitches(g, widgetWidth);

            // Draw labels for sidings and platforms last so they go on top for readability
            MapDataProvider.CleanTextCells(); // Empty the listing of labels ready for adding labels again
            ShowPlatformLabels(g); // Platforms take priority over sidings and signal states
            ShowSidingLabels(g);

            DrawZoomTarget(g);
        }

        #region Drawing primitives (GDI+ equivalents)
        static IPen MakePen(Color color, double width, bool dashed = false)
        {
            return new Pen(new ImmutableSolidColorBrush(color), width, dashed ? new DashStyle(DashPattern, 0) : null);
        }

        static Avalonia.Point P(PointF p) => new Avalonia.Point(p.X, p.Y);

        static void DrawLine(DrawingContext g, IPen pen, PointF a, PointF b)
        {
            g.DrawLine(pen, P(a), P(b));
        }

        /// <summary>
        /// Equivalent of GDI+ Graphics.DrawCurve(pen, points) for 3 points: a cardinal spline with tension 0.5
        /// </summary>
        static void DrawCurve(DrawingContext g, IPen pen, PointF p0, PointF p1, PointF p2)
        {
            const float t = 0.5f / 3f;
            var c1 = new Avalonia.Point(p0.X + (t * (p1.X - p0.X)), p0.Y + (t * (p1.Y - p0.Y)));
            var c2 = new Avalonia.Point(p1.X - (t * (p2.X - p0.X)), p1.Y - (t * (p2.Y - p0.Y)));
            var c3 = new Avalonia.Point(p1.X + (t * (p2.X - p0.X)), p1.Y + (t * (p2.Y - p0.Y)));
            var c4 = new Avalonia.Point(p2.X - (t * (p2.X - p1.X)), p2.Y - (t * (p2.Y - p1.Y)));
            var geometry = new StreamGeometry();
            using (var context = geometry.Open())
            {
                context.BeginFigure(P(p0), false);
                context.CubicBezierTo(c1, c2, P(p1), true);
                context.CubicBezierTo(c3, c4, P(p2), true);
                context.EndFigure(false);
            }
            g.DrawGeometry(null, pen, geometry);
        }

        static void FillEllipse(DrawingContext g, IBrush brush, RectangleF rect)
        {
            g.DrawEllipse(brush, null, new Rect(rect.X, rect.Y, rect.Width, rect.Height));
        }

        static void DrawString(DrawingContext g, string text, Typeface font, IBrush brush, PointF point)
        {
            var formattedText = new FormattedText(text, CultureInfo.CurrentCulture, FlowDirection.LeftToRight, font, LabelFontSizePx, brush);
            g.DrawText(formattedText, P(point));
        }
        #endregion

        private void DrawPlatforms(DrawingContext g, int penWidth)
        {
            if (!IsChecked(showPlatformsCheckbox))
                return;

            // Platforms can be obtrusive, so draw in solid blue only when zoomed in and fade them as we zoom out
            switch (penWidth)
            {
                case 1:
                    PlatformColor = Color.FromRgb(174, 214, 241); break;
                case 2:
                    PlatformColor = Color.FromRgb(93, 173, 226); break;
                default:
                    PlatformColor = Color.FromRgb(46, 134, 193); break;
            }

            var width = grayPenWidth * 3;
            var platformPen = MakePen(PlatformColor, width);
            foreach (var p in platforms)
            {
                var scaledA = new PointF((p.Extent1.X - subX) * xScale, CanvasHeight - ((p.Extent1.Y - subY) * yScale));
                var scaledB = new PointF((p.Extent2.X - subX) * xScale, CanvasHeight - ((p.Extent2.Y - subY) * yScale));

                MapDataProvider.FixForBadData(width, ref scaledA, ref scaledB, p.Extent1, p.Extent2);
                DrawLine(g, platformPen, scaledA, scaledB);
            }
        }

        private void DrawTrack(DrawingContext g, out PointF scaledA, out PointF scaledB)
        {
            var trackPen = MakePen(TrackColor, 1);
            scaledA = new PointF(0, 0);
            scaledB = new PointF(0, 0);
            PointF scaledC = new PointF(0, 0);
            foreach (var line in segments)
            {
                scaledA.X = ((line.A.TileX * 2048) - subX + (float)line.A.X) * xScale;
                scaledA.Y = CanvasHeight - (((line.A.TileZ * 2048) - subY + (float)line.A.Z) * yScale);
                scaledB.X = ((line.B.TileX * 2048) - subX + (float)line.B.X) * xScale;
                scaledB.Y = CanvasHeight - (((line.B.TileZ * 2048) - subY + (float)line.B.Z) * yScale);

                if ((scaledA.X < 0 && scaledB.X < 0)
                    || (scaledA.Y < 0 && scaledB.Y < 0))
                    continue;

                if (line.isCurved == true)
                {
                    scaledC.X = ((float)line.C.X - subX) * xScale; scaledC.Y = CanvasHeight - (((float)line.C.Z - subY) * yScale);
                    DrawCurve(g, trackPen, scaledA, scaledC, scaledB);
                }
                else DrawLine(g, trackPen, scaledA, scaledB);
            }
        }

        private void DrawTrains(DrawingContext g, PointF scaledA, PointF scaledB)
        {
            var margin = 30 * xScale; // Margins to determine if we want to draw a train
            var margin2 = 5000 * xScale;

            // Variable for drawing train path
            var mDist = 5000f; var pDist = 50; // Segment length when drawing path

            selectedTrainList.Clear();

            // Copy, as the list is modified by the updater thread
            selectedTrainList.AddRange(simulator.Trains.ToArray());

            var pathPen = MakePen(PathColor, pathPenWidth, true);

            foreach (var train in selectedTrainList)
            {
                string trainName;
                WorldPosition worldPos;
                TrainCar locoCar = null;
                if (train.LeadLocomotive != null)
                {
                    trainName = train.GetTrainName(train.LeadLocomotive.CarID);
                    locoCar = train.LeadLocomotive;
                }
                else if (train.Cars != null && train.Cars.Count > 0)
                {
                    trainName = train.GetTrainName(train.Cars[0].CarID);
                    if (train.TrainType == Train.TRAINTYPE.AI || train.TrainType == Train.TRAINTYPE.STATIC)
                        trainName = train.Number.ToString() + ":" + train.Name;

                    locoCar = train.Cars.Where(r => r is MSTSLocomotive).FirstOrDefault();

                    // Skip trains with no loco
                    if (locoCar == null)
                        locoCar = train.Cars[0];
                }
                else
                    continue;

                // Draw the path, then each car of the train, then maybe the name
                var loc = train.FrontTDBTraveller.WorldLocation;
                float x = ((loc.TileX * 2048) + loc.Location.X - subX) * xScale;
                float y = CanvasHeight - (((loc.TileZ * 2048) + loc.Location.Z - subY) * yScale);

                // If train out of view then skip it.
                if (x < -margin2 || y < -margin2)
                    continue;

                DrawTrainPath(train, subX, subY, pathPen, g, scaledA, scaledB, pDist, mDist);

                // If zoomed out, so train occupies less than 2 * minTrainPx pixels, then
                // draw the train as 2 squares of combined length minTrainPx.
                const int minTrainPx = 24;

                // pen | train | Values for a good presentation
                //  1		10
                //  2       12
                //  3       14
                //  4		16
                trainPenWidth = grayPenWidth * 6;

                var minTrainLengthM = minTrainPx / xScale; // Calculate length equivalent to a set number of pixels
                bool drawEveryCar = IsDrawEveryCar(train, minTrainLengthM);

                foreach (var car in train.Cars.ToArray())
                    DrawCar(g, train, car, locoCar, margin, minTrainPx, drawEveryCar);

                worldPos = locoCar.WorldPosition;
                var scaledTrain = new PointF
                {
                    X = ((worldPos.TileX * 2048) - subX + worldPos.Location.X) * xScale,
                    Y = -25 + CanvasHeight - (((worldPos.TileZ * 2048) - subY + worldPos.Location.Z) * yScale)
                };
                if (IsChecked(showTrainLabelsCheckbox))
                    DrawTrainLabels(g, train, trainName, scaledTrain);
            }
        }

        private void DrawCar(DrawingContext g, Train train, TrainCar car, TrainCar locoCar, float margin, int minTrainPx, bool drawEveryCar)
        {
            if (drawEveryCar == false)
                // Skip the intermediate cars
                if (car != train.Cars.First() && car != train.Cars.Last())
                    return;

            var t = new Traveller(train.RearTDBTraveller);
            var worldPos = car.WorldPosition;
            var dist = t.DistanceTo(worldPos.WorldLocation.TileX, worldPos.WorldLocation.TileZ, worldPos.WorldLocation.Location.X, worldPos.WorldLocation.Location.Y, worldPos.WorldLocation.Location.Z);
            if (dist > -1)
            {
                var scaledTrain = new PointF();
                float x;
                float y;
                if (drawEveryCar)
                {
                    t.Move(dist + (car.CarLengthM / 2)); // Move along from centre of car to front of car
                    x = ((t.TileX * 2048) + t.Location.X - subX) * xScale;
                    y = CanvasHeight - (((t.TileZ * 2048) + t.Location.Z - subY) * yScale);

                    // If car out of view then skip it.
                    if (x < -margin || y < -margin)
                        return;

                    t.Move(-car.CarLengthM + (2 / xScale)); // Move from front of car to rear less 1 pixel to create a visible gap // TODO: investigate `(1 / xScale)` ==> `(2 / xScale)` car gap consequences
                    scaledTrain.X = x; scaledTrain.Y = y;
                }
                else // Draw the train as 2 boxes of fixed size
                {
                    trainPenWidth = minTrainPx / 2;
                    if (car == train.Cars.First())
                    {
                        // Draw first half a train back from the front of the first car as abox
                        t.Move(dist + (car.CarLengthM / 2));
                        x = ((t.TileX * 2048) + t.Location.X - subX) * xScale;
                        y = CanvasHeight - (((t.TileZ * 2048) + t.Location.Z - subY) * yScale);

                        // If car out of view then skip it.
                        if (x < -margin || y < -margin)
                            return;

                        t.Move(-(minTrainPx - 2) / xScale / 2); // Move from front of car to rear less 1 pixel to create a visible gap
                    }
                    else // car == t.Cars.Last()
                    {
                        // Draw half a train back from the rear of the first box
                        worldPos = train.Cars.First().WorldPosition;
                        dist = t.DistanceTo(worldPos.WorldLocation.TileX, worldPos.WorldLocation.TileZ, worldPos.WorldLocation.Location.X, worldPos.WorldLocation.Location.Y, worldPos.WorldLocation.Location.Z);
                        t.Move(dist + (train.Cars.First().CarLengthM / 2) - (minTrainPx / xScale / 2));
                        x = ((t.TileX * 2048) + t.Location.X - subX) * xScale;
                        y = CanvasHeight - (((t.TileZ * 2048) + t.Location.Z - subY) * yScale);
                        if (x < -margin || y < -margin)
                            return;
                        t.Move(-minTrainPx / xScale / 2);
                    }
                    scaledTrain.X = x; scaledTrain.Y = y;
                }
                x = ((t.TileX * 2048) + t.Location.X - subX) * xScale;
                y = CanvasHeight - (((t.TileZ * 2048) + t.Location.Z - subY) * yScale);

                // If car out of view then skip it.
                if (x < -margin || y < -margin)
                    return;

                SetTrainColor(train, locoCar, car);
                DrawLine(g, MakePen(trainPenColor, trainPenWidth), new PointF(x, y), scaledTrain);
            }
        }

        private void SetTrainColor(Train t, TrainCar locoCar, TrainCar car)
        {
            // Draw train in green with locos in brown
            // HSL values
            // Saturation: 100/100
            // Hue: if loco then H=50/360 else H=120/360
            // Lightness: if active then L=40/100 else L=30/100
            // RGB values
            // active loco: RGB 204,170,0
            // inactive loco: RGB 153,128,0
            // active car: RGB 0,204,0
            // inactive car: RGB 0,153,0
            trainPenColor = MapDataProvider.IsActiveTrain(t as AITrain)
                ? car is MSTSLocomotive
                    ? (car == locoCar) ? Color.FromRgb(204, 170, 0) : Color.FromRgb(153, 128, 0)
                    : Color.FromRgb(0, 204, 0)
                : car is MSTSLocomotive ? Color.FromRgb(153, 128, 0) : Color.FromRgb(0, 153, 0);

            if (t.TrainType == Train.TRAINTYPE.STATIC || (t.TrainType == Train.TRAINTYPE.AI && t.GetAIMovementState() == AITrain.AI_MOVEMENT_STATE.AI_STATIC))
            {
                trainPenColor = car is MSTSLocomotive ? Color.FromRgb(19, 185, 160) : Color.FromRgb(83, 237, 214);
            }

            // Draw player train with loco in red
            if (t.TrainType == Train.TRAINTYPE.PLAYER && car == locoCar)
                trainPenColor = Colors.Red;
        }

        private void DrawTrainLabels(DrawingContext g, Train t, string trainName, PointF scaledTrain)
        {
            if (IsChecked(showActiveTrainsRadio))
            {
                if (t is AITrain && MapDataProvider.IsActiveTrain(t as AITrain))
                    ShowTrainNameAndState(g, scaledTrain, t, trainName);
            }
            else
            {
                ShowTrainNameAndState(g, scaledTrain, t, trainName);
            }
        }

        private void ShowTrainNameAndState(DrawingContext g, PointF scaledItem, Train t, string trainName)
        {
            if (simulator.TimetableMode)
            {
                if (t is TTTrain tTTrain)
                {
                    // Remove name of timetable, e.g.: ":SCE"
                    var lastPos = trainName.LastIndexOf(":");
                    var shortName = (lastPos > 0) ? trainName.Substring(0, lastPos) : trainName;

                    if (MapDataProvider.IsActiveTrain(tTTrain))
                    {
                        if (IsChecked(showTrainStateCheckbox))
                        {
                            // 4:AI mode, 6:Mode, 7:Auth, 9:Signal, 12:Path
                            var status = tTTrain.GetStatus(Viewer.MilepostUnitsMetric);

                            // Add in fields 4 and 7
                            status = tTTrain.AddMovementState(status, Viewer.MilepostUnitsMetric);

                            var statuses = $"{status[4]} {status[6]} {status[7]} {status[9]}";

                            // Add path if it contains any deadlock information
                            if (MapDataProvider.ContainsDeadlockIndicators(status[12]))
                                statuses += status[12];

                            DrawString(g, $"{shortName} {statuses}", trainFont, trainBrush, scaledItem);
                        }
                        else
                            DrawString(g, shortName, trainFont, trainBrush, scaledItem);
                    }
                    else
                        DrawString(g, shortName, trainFont, InactiveTrainBrush, scaledItem);
                }
            }
            else
                DrawString(g, trainName, trainFont, trainBrush, scaledItem);
        }

        static readonly IBrush SwitchMainBrush = new ImmutableSolidColorBrush(Color.FromRgb(93, 64, 55));
        static readonly IBrush SwitchSideBrush = new ImmutableSolidColorBrush(Color.FromRgb(161, 136, 127));

        private void ShowSwitches(DrawingContext g, float width)
        {
            if (!IsChecked(showSwitchesCheckbox))
                return;

            for (var i = 0; i < switches.Count; i++)
            {
                SwitchWidget sw = switches[i];

                var x = (sw.Location.X - subX) * xScale;
                var y = CanvasHeight - ((sw.Location.Y - subY) * yScale);
                if (x < 0 || y < 0)
                    continue;

                var scaledItem = new PointF() { X = x, Y = y };

                if (sw.Item.TrJunctionNode.SelectedRoute == sw.main)
                    FillEllipse(g, SwitchMainBrush, GetRect(scaledItem, width));
                else
                    FillEllipse(g, SwitchSideBrush, GetRect(scaledItem, width));

                sw.Location2D.X = scaledItem.X; sw.Location2D.Y = scaledItem.Y;
                switchItemsDrawn.Add(sw);
            }
        }

        private void ShowSignals(DrawingContext g, PointF scaledB, float width)
        {
            if (!IsChecked(showSignalsCheckbox))
                return;

            foreach (var s in signals)
            {
                if (float.IsNaN(s.Location.X) || float.IsNaN(s.Location.Y))
                    continue;
                var x = (s.Location.X - subX) * xScale;
                var y = CanvasHeight - ((s.Location.Y - subY) * yScale);
                if (x < 0 || y < 0)
                    continue;

                var scaledItem = new PointF() { X = x, Y = y };
                s.Location2D.X = scaledItem.X; s.Location2D.Y = scaledItem.Y;
                if (s.Signal.isSignalNormal())
                {
                    var color = GreenColor;
                    if (s.IsProceed == 0)
                    {
                    }
                    else if (s.IsProceed == 1)
                    {
                        color = OrangeColor;
                    }
                    else
                    {
                        color = RedColor;
                    }
                    FillEllipse(g, new ImmutableSolidColorBrush(color), GetRect(scaledItem, width));
                    signalItemsDrawn.Add(s);
                    if (s.hasDir)
                    {
                        scaledB.X = (s.Dir.X - subX) * xScale; scaledB.Y = CanvasHeight - ((s.Dir.Y - subY) * yScale);
                        DrawLine(g, MakePen(color, signalPenWidth), scaledItem, scaledB);
                    }
                    ShowSignalState(g, scaledItem, s);
                }
            }
        }

        private void ShowSignalState(DrawingContext g, PointF scaledItem, SignalWidget sw)
        {
            if (!IsChecked(showSignalStateCheckbox))
                return;

            var item = sw.Item as SignalItem;
            var trainNumber = sw.Signal?.enabledTrain?.Train?.Number;
            var trainString = (trainNumber == null) ? "" : $" train: {trainNumber}";
            var offset = 0;
            var position = scaledItem;
            foreach (var signalHead in sw.Signal.SignalHeads)
            {
                offset++;
                position.X += offset * 10;
                position.Y += offset * 15;
                var text = $"  {item?.SigObj} {signalHead.SignalTypeName} {signalHead.state} {trainString}";
                scaledItem.Y = MapDataProvider.GetUnusedYLocation(scaledItem.X, CanvasHeight - ((sw.Location.Y - subY) * yScale), text);
                if (scaledItem.Y >= 0f) // -1 indicates no free slot to draw label
                    DrawString(g, text, labelFont, SignalBrush, scaledItem);
            }

        }

        private void ShowSidingLabels(DrawingContext g)
        {
            if (!IsChecked(showSidingLabelsCheckbox))
                return;

            foreach (var s in sidings)
            {
                var scaledItem = new PointF();

                scaledItem.X = (s.Location.X - subX) * xScale;
                scaledItem.Y = MapDataProvider.GetUnusedYLocation(scaledItem.X, CanvasHeight - ((s.Location.Y - subY) * yScale), s.Name);
                if (scaledItem.Y >= 0f) // -1 indicates no free slot to draw label
                    DrawString(g, s.Name, labelFont, sidingBrush, scaledItem);
            }
        }

        private void ShowPlatformLabels(DrawingContext g)
        {
            var platformMarginPxX = 5;

            if (!IsChecked(showPlatformLabelsCheckbox))
                return;

            foreach (var p in platforms)
            {
                var scaledItem = new PointF();
                scaledItem.X = ((p.Location.X - subX) * xScale) + platformMarginPxX;
                var yPixels = CanvasHeight - ((p.Location.Y - subY) * yScale);

                // If track is close to horizontal, then start label search 1 row down to minimise overwriting platform line.
                if (p.Extent1.X != p.Extent2.X
                    && Math.Abs((p.Extent1.Y - p.Extent2.Y) / (p.Extent1.X - p.Extent2.X)) < 0.1)
                    yPixels += spacing;

                scaledItem.Y = MapDataProvider.GetUnusedYLocation(scaledItem.X, CanvasHeight - ((p.Location.Y - subY) * yScale), p.Name);
                if (scaledItem.Y >= 0f) // -1 indicates no free slot to draw label
                    DrawString(g, p.Name, labelFont, PlatformBrush, scaledItem);
            }
        }

        /// <summary>
        /// If the train is long enough then draw every car else just draw it as one or two blocks
        /// </summary>
        /// <param name="train"></param>
        /// <param name="minTrainLengthM"></param>
        /// <returns></returns>
        private bool IsDrawEveryCar(Train train, float minTrainLengthM)
        {
            float trainLengthM = 0f;
            foreach (var car in train.Cars)
            {
                trainLengthM += car.CarLengthM;
                if (trainLengthM > minTrainLengthM)
                {
                    return true;
                }
            }
            return false;
        }

        /// <summary>
        /// Indicates the location around which the image is zoomed.
        /// If user drags an item of interest into this target box and zooms in, the item will remain in view.
        /// </summary>
        /// <param name="g"></param>
        private void DrawZoomTarget(DrawingContext g)
        {
            if (!Dragging)
                return;

            const int size = 24;
            var top = (CanvasHeight / 2) - (size / 2);
            var left = (CanvasWidth / 2) - (size / 2);
            g.DrawRectangle(null, MakePen(ZoomTargetColor, 1, true), new Rect(left, top, size, size));
        }

        public Vector2[][] alignedTextY;
        public int[] alignedTextNum;
        public const int spacing = 12; // TODO: Rename to clarify the meaning of this variable

        const float SignalErrorDistance = 100;
        const float SignalWarningDistance = 500;
        const float DisplayDistance = 1000;
        const float DisplaySegmentLength = 10;
        const float MaximumSectionDistance = 10000;

        readonly Dictionary<int, SignallingDebugWindow.TrackSectionCacheEntry> Cache = new Dictionary<int, SignallingDebugWindow.TrackSectionCacheEntry>();
        SignallingDebugWindow.TrackSectionCacheEntry GetCacheEntry(Traveller position)
        {
            SignallingDebugWindow.TrackSectionCacheEntry rv;
            if (Cache.TryGetValue(position.TrackNodeIndex, out rv) && (rv.Direction == position.Direction))
                return rv;
            Cache[position.TrackNodeIndex] = rv = new SignallingDebugWindow.TrackSectionCacheEntry()
            {
                Direction = position.Direction,
                Length = 0,
                Objects = new List<SignallingDebugWindow.TrackSectionObject>(),
            };
            var nodeIndex = position.TrackNodeIndex;
            var trackNode = new Traveller(position);
            while (true)
            {
                rv.Length += MaximumSectionDistance - trackNode.MoveInSection(MaximumSectionDistance);
                if (!trackNode.NextSection())
                    break;
                if (trackNode.IsEnd)
                    rv.Objects.Add(new SignallingDebugWindow.TrackSectionEndOfLine() { Distance = rv.Length });
                else if (trackNode.IsJunction)
                    rv.Objects.Add(new SignallingDebugWindow.TrackSectionSwitch() { Distance = rv.Length, TrackNode = trackNode.TN, NodeIndex = nodeIndex });
                else
                    rv.Objects.Add(new SignallingDebugWindow.TrackSectionObject() { Distance = rv.Length }); // Always have an object at the end.
                if (trackNode.TrackNodeIndex != nodeIndex)
                    break;
            }
            trackNode = new Traveller(position);

            rv.Objects = rv.Objects.OrderBy(tso => tso.Distance).ToList();
            return rv;
        }

        // Draw the train path if it is within the window
        public void DrawTrainPath(Train train, float subX, float subY, IPen pathPen, DrawingContext g, PointF scaledA, PointF scaledB, float stepDist, float MaximumSectionDistance)
        {
            if (DrawPath != true) return;
            bool ok = false;
            if (train == Program.Simulator.PlayerLocomotive.Train) ok = true;
            if (MPManager.IsMultiPlayer())
            {
                if (MPManager.OnlineTrains.findTrain(train))
                    ok = true;
            }
            if (train.FirstCar != null & train.FirstCar.CarID.Contains("AI")) // AI train
                ok = true;
            if (Math.Abs(train.SpeedMpS) > 0.001)
                ok = true;

            if (ok == false) return;

            var DisplayDistance = MaximumSectionDistance;
            var position = train.MUDirection != Direction.Reverse ? new Traveller(train.FrontTDBTraveller) : new Traveller(train.RearTDBTraveller, Traveller.TravellerDirection.Backward);
            var caches = new List<SignallingDebugWindow.TrackSectionCacheEntry>();
            // Work backwards until we end up on a different track section.
            var cacheNode = new Traveller(position);
            cacheNode.ReverseDirection();
            var initialNodeOffsetCount = 0;
            while (cacheNode.TrackNodeIndex == position.TrackNodeIndex && cacheNode.NextSection())
                initialNodeOffsetCount++;
            // Now do it again, but don't go the last track section (because it is from a different track node).
            cacheNode = new Traveller(position);
            cacheNode.ReverseDirection();
            for (var i = 1; i < initialNodeOffsetCount; i++)
                cacheNode.NextSection();
            // Push the location right up to the end of the section.
            cacheNode.MoveInSection(MaximumSectionDistance);
            // Now back facing the right way, calculate the distance to the train location.
            cacheNode.ReverseDirection();
            var initialNodeOffset = cacheNode.DistanceTo(position.TileX, position.TileZ, position.X, position.Y, position.Z);
            // Go and collect all the cache entries for the visible range of vector nodes (straights, curves).
            var totalDistance = 0f;
            while (!cacheNode.IsEnd && totalDistance - initialNodeOffset < DisplayDistance)
            {
                if (cacheNode.IsTrack)
                {
                    var cache = GetCacheEntry(cacheNode);
                    cache.Age = 0;
                    caches.Add(cache);
                    totalDistance += cache.Length;
                }
                var nodeIndex = cacheNode.TrackNodeIndex;
                while (cacheNode.TrackNodeIndex == nodeIndex && cacheNode.NextSection()) ;
            }

            var switchErrorDistance = initialNodeOffset + DisplayDistance + SignalWarningDistance;
            var signalErrorDistance = initialNodeOffset + DisplayDistance + SignalWarningDistance;
            var currentDistance = 0f;
            foreach (var cache in caches)
            {
                foreach (var obj in cache.Objects)
                {
                    var objDistance = currentDistance + obj.Distance;
                    if (objDistance < initialNodeOffset)
                        continue;

                    if (obj is SignallingDebugWindow.TrackSectionSwitch switchObj)
                    {
                        for (var pin = switchObj.TrackNode.Inpins; pin < switchObj.TrackNode.Inpins + switchObj.TrackNode.Outpins; pin++)
                        {
                            if (switchObj.TrackNode.TrPins[pin].Link == switchObj.NodeIndex)
                            {
                                if (pin - switchObj.TrackNode.Inpins != switchObj.TrackNode.TrJunctionNode.SelectedRoute)
                                    switchErrorDistance = objDistance;
                                break;
                            }
                        }
                        if (switchErrorDistance < DisplayDistance)
                            break;
                    }

                }
                if (switchErrorDistance < DisplayDistance || signalErrorDistance < DisplayDistance)
                    break;
                currentDistance += cache.Length;
            }

            var currentPosition = new Traveller(position);
            currentPosition.Move(-initialNodeOffset);
            currentDistance = 0;

            foreach (var cache in caches)
            {
                var lastObjDistance = 0f;
                foreach (var obj in cache.Objects)
                {
                    var objDistance = currentDistance + obj.Distance;

                    for (var step = lastObjDistance; step < obj.Distance; step += DisplaySegmentLength)
                    {
                        var stepDistance = currentDistance + step;
                        var stepLength = DisplaySegmentLength > obj.Distance - step ? obj.Distance - step : DisplaySegmentLength;
                        var previousLocation = currentPosition.WorldLocation;
                        currentPosition.Move(stepLength);
                        if (stepDistance + stepLength >= initialNodeOffset && stepDistance <= initialNodeOffset + DisplayDistance)
                        {
                            var currentLocation = currentPosition.WorldLocation;
                            scaledA.X = (float)(((previousLocation.TileX * WorldLocation.TileSize) + previousLocation.Location.X - subX) * xScale);
                            scaledA.Y = (float)(CanvasHeight - (((previousLocation.TileZ * WorldLocation.TileSize) + previousLocation.Location.Z - subY) * yScale));
                            scaledB.X = (float)(((currentLocation.TileX * WorldLocation.TileSize) + currentLocation.Location.X - subX) * xScale);
                            scaledB.Y = (float)(CanvasHeight - (((currentPosition.TileZ * WorldLocation.TileSize) + currentPosition.Location.Z - subY) * yScale)); DrawLine(g, pathPen, scaledA, scaledB);
                        }
                    }
                    lastObjDistance = obj.Distance;

                    if (objDistance >= switchErrorDistance)
                        break;
                }
                currentDistance += cache.Length;
                if (currentDistance >= switchErrorDistance)
                    break;

            }

            currentPosition = new Traveller(position);
            currentPosition.Move(-initialNodeOffset);
            currentDistance = 0;
            foreach (var cache in caches)
            {
                var lastObjDistance = 0f;
                foreach (var obj in cache.Objects)
                {
                    currentPosition.Move(obj.Distance - lastObjDistance);
                    lastObjDistance = obj.Distance;

                    var objDistance = currentDistance + obj.Distance;
                    if (objDistance < initialNodeOffset || objDistance > initialNodeOffset + DisplayDistance)
                        continue;

                    if (obj is SignallingDebugWindow.TrackSectionSwitch switchObj)
                    {
                        for (var pin = switchObj.TrackNode.Inpins; pin < switchObj.TrackNode.Inpins + switchObj.TrackNode.Outpins; pin++)
                        {
                            if (switchObj.TrackNode.TrPins[pin].Link == switchObj.NodeIndex && pin - switchObj.TrackNode.Inpins != switchObj.TrackNode.TrJunctionNode.SelectedRoute)
                            {
                                foreach (var sw in switchItemsDrawn)
                                {
                                    if (sw.Item.TrJunctionNode == switchObj.TrackNode.TrJunctionNode)
                                    {
                                        var r = 6 * signalPenWidth;
                                        DrawLine(g, pathPen, new PointF(sw.Location2D.X - r, sw.Location2D.Y - r), new PointF(sw.Location2D.X + r, sw.Location2D.Y + r));
                                        DrawLine(g, pathPen, new PointF(sw.Location2D.X - r, sw.Location2D.Y + r), new PointF(sw.Location2D.X + r, sw.Location2D.Y - r));
                                        break;
                                    }
                                }
                            }
                        }
                    }

                    if (objDistance >= switchErrorDistance)
                        break;
                }
                currentDistance += cache.Length;
                if (currentDistance >= switchErrorDistance)
                    break;
            }
            // Clean up any cache entries who haven't been using for 30 seconds.
            var oldCaches = Cache.Where(kvp => kvp.Value.Age > 30 * 4).ToArray();
            foreach (var oldCache in oldCaches)
                Cache.Remove(oldCache.Key);

        }
        #endregion

        /// <summary>
        /// Generates a rectangle representing a dot being drawn.
        /// </summary>
        /// <param name="p">Center point of the dot, in pixels.</param>
        /// <param name="size">Size of the dot's diameter, in pixels</param>
        /// <returns></returns>
        public static RectangleF GetRect(PointF p, float size)
        {
            return new RectangleF(p.X - (size / 2f), p.Y - (size / 2f), size, size);
        }

        /// <summary>
        /// Generates line segments from an array of TrVectorSection. Also computes
        /// the bounds of the entire route being drawn.
        /// </summary>
        /// <param name="segments"></param>
        /// <param name="items"></param>
        /// <param name="minX"></param>
        /// <param name="minY"></param>
        /// <param name="maxX"></param>
        /// <param name="maxY"></param>
        /// <param name="simulator"></param>
        private static void AddSegments(List<LineSegment> segments, TrackNode node, TrVectorSection[] items, ref float minX, ref float minY, ref float maxX, ref float maxY, Simulator simulator)
        {

            bool occupied = false;

            double tempX1, tempX2, tempZ1, tempZ2;

            for (int i = 0; i < items.Length - 1; i++)
            {
                dVector A = new dVector(items[i].TileX, items[i].X, items[i].TileZ, items[i].Z);
                dVector B = new dVector(items[i + 1].TileX, items[i + 1].X, items[i + 1].TileZ, items[i + 1].Z);

                tempX1 = (A.TileX * 2048) + A.X; tempX2 = (B.TileX * 2048) + B.X;
                tempZ1 = (A.TileZ * 2048) + A.Z; tempZ2 = (B.TileZ * 2048) + B.Z;
                CalcBounds(ref maxX, tempX1, true);
                CalcBounds(ref maxY, tempZ1, true);
                CalcBounds(ref maxX, tempX2, true);
                CalcBounds(ref maxY, tempZ2, true);

                CalcBounds(ref minX, tempX1, false);
                CalcBounds(ref minY, tempZ1, false);
                CalcBounds(ref minX, tempX2, false);
                CalcBounds(ref minY, tempZ2, false);

                segments.Add(new LineSegment(A, B, occupied, items[i]));
            }
        }

        /// <summary>
        /// Given a value representing a limit, evaluate if the given value exceeds the current limit.
        /// If so, expand the limit.
        /// </summary>
        /// <param name="limit">The current limit.</param>
        /// <param name="value">The value to compare the limit to.</param>
        /// <param name="gt">True when comparison is greater-than. False if less-than.</param>
        private static void CalcBounds(ref float limit, double v, bool gt)
        {
            float value = (float)v;
            if (gt)
            {
                if (value > limit)
                {
                    limit = value;
                }
            }
            else
            {
                if (value < limit)
                {
                    limit = value;
                }
            }
        }

        /// <summary>
        /// Kept for compatibility with the WinForms version; visibility is controlled by <see cref="Viewer.MapViewerEnabled"/>.
        /// </summary>
        public void Hide()
        {
        }

        bool Disposed;

        /// <summary>
        /// Closes the window. Called on the render thread when the game ends; never blocks.
        /// </summary>
        public void Dispose()
        {
            if (Disposed) return;
            Disposed = true;
            MPManager.Instance().MessageReceived -= MPManager_MessageReceived;
            UiThread.Post(CloseWindow);
        }
    }
}
