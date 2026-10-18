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

// This file is the responsibility of the 3D & Environment Team.

using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using Orts.Formats.Msts;
using Orts.Viewer3D.Popups;
using ORTS.Common;
using ORTS.Menu;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;

namespace Orts.Viewer3D.Processes
{
    /// <summary>
    /// A start menu drawn inside the game itself, as a panel floating in front of the driver, so a run
    /// can be chosen from a headset where the desktop menu cannot be seen. It also works flat.
    ///
    /// Route -> activity (or explore) -> path -> consist -> time, season and weather -> start. Keys:
    /// up/down (and page up/down) move, Enter chooses, Backspace goes back, left/right change a value,
    /// Escape leaves. The run then starts in this process exactly as the desktop menu would start it.
    /// </summary>
    public class GameStateVRMenu : GameState
    {
        enum Screen { Routes, Activities, Paths, Consists, Options }

        // Panel in the seat frame: metres in front of and below the eyes, and its size.
        const float PanelDistanceM = 1.7f;
        const float PanelDropM = 0.08f;
        const float PanelWidthM = 1.5f;
        const int PanelWidthPx = 1400;
        const int PanelHeightPx = 900;
        const int VisibleRows = 18;

        Screen CurrentScreen = Screen.Routes;
        readonly int[] Selection = new int[5];
        readonly int[] Scroll = new int[5];

        // Content, filled on the loader thread.
        readonly object ContentLock = new object();
        List<KeyValuePair<Folder, Route>> Routes;
        List<Activity> Activities;
        List<Path> Paths;
        List<Consist> Consists;
        Route LoadedRoute;      // the route Activities and Paths belong to
        Folder LoadedFolder;    // the folder Consists belong to
        volatile string Status = "Reading the content folders...";
        volatile bool Busy = true;
        Screen? PendingLoad = Screen.Routes;

        // Choices.
        Activity ChosenActivity;
        Path ChosenPath;
        Consist ChosenConsist;
        int StartHour = 10;
        int StartMinute = 0;
        SeasonType Season = SeasonType.Summer;
        WeatherType Weather = WeatherType.Clear;
        int OptionRow;
        const int OptionRows = 5; // hour, minute, season, weather, start

        // Drawing (render thread).
        RenderTarget2D Panel;
        SpriteBatch SpriteBatch;
        WindowTextManager TextManager;
        WindowTextFont TitleFont, ItemFont, SmallFont;
        PanelPrimitive Primitive;
        Matrix PanelMatrix = Matrix.Identity;
        bool Launched;

        internal override void Load()
        {
            Screen? load;
            lock (ContentLock)
            {
                load = PendingLoad;
                PendingLoad = null;
            }
            if (load == null)
                return;
            try
            {
                switch (load.Value)
                {
                    case Screen.Routes:
                        var routes = new List<KeyValuePair<Folder, Route>>();
                        foreach (var folder in Folder.GetFolders(Game.Settings))
                            foreach (var route in Route.GetRoutes(folder))
                                routes.Add(new KeyValuePair<Folder, Route>(folder, route));
                        routes.Sort((a, b) => String.Compare(a.Value.Name, b.Value.Name, StringComparison.CurrentCultureIgnoreCase));
                        lock (ContentLock) Routes = routes;
                        Status = routes.Count > 0 ? "" : "No routes found. Add a content folder in the desktop menu's Options first.";
                        break;
                    case Screen.Activities:
                        {
                            var folder = Routes[Selection[(int)Screen.Routes]].Key;
                            var route = Routes[Selection[(int)Screen.Routes]].Value;
                            // GetActivities puts the two explore entries first; keep them there and sort the rest by name.
                            var all = Activity.GetActivities(folder, route);
                            var activities = all.Where(a => a is ExploreActivity).ToList();
                            activities.AddRange(all.Where(a => !(a is ExploreActivity)).OrderBy(a => a.Name, StringComparer.CurrentCultureIgnoreCase));
                            lock (ContentLock) { Activities = activities; LoadedRoute = route; Paths = null; }
                            Status = "";
                        }
                        break;
                    case Screen.Paths:
                        {
                            var route = Routes[Selection[(int)Screen.Routes]].Value;
                            var paths = Path.GetPaths(route, false).OrderBy(p => p.Name, StringComparer.CurrentCultureIgnoreCase).ToList();
                            lock (ContentLock) { Paths = paths; LoadedRoute = route; }
                            Status = paths.Count > 0 ? "" : "This route has no player paths.";
                        }
                        break;
                    case Screen.Consists:
                        {
                            var folder = Routes[Selection[(int)Screen.Routes]].Key;
                            var consists = Consist.GetConsists(folder).OrderBy(c => c.Name, StringComparer.CurrentCultureIgnoreCase).ToList();
                            lock (ContentLock) { Consists = consists; LoadedFolder = folder; }
                            Status = consists.Count > 0 ? "" : "No consists found in this folder.";
                        }
                        break;
                }
            }
            catch (Exception error)
            {
                Trace.WriteLine(error);
                Status = "Could not read the content: " + error.Message;
            }
            Busy = false;
        }

        [CallOnThread("Updater")]
        internal override void Update(RenderFrame frame, double totalRealSeconds)
        {
            if (!Launched && !Busy)
                HandleInput();

            // The driver sits at the origin looking along -Z; the panel floats ahead.
            var display = Game.RenderProcess.DisplaySize;
            var projection = Matrix.CreatePerspectiveFieldOfView(MathHelper.ToRadians(50), (float)display.X / display.Y, 0.1f, 100f);
            frame.SetView(Matrix.Identity, projection);
            if (Primitive != null)
                frame.AddPrimitive(Primitive.Material, Primitive, RenderPrimitiveGroup.World, ref PanelMatrix);

            base.Update(frame, totalRealSeconds);
        }

        void RequestLoad(Screen screen)
        {
            Busy = true;
            Status = "Reading...";
            lock (ContentLock) PendingLoad = screen;
        }

        int CurrentCount()
        {
            lock (ContentLock)
            {
                switch (CurrentScreen)
                {
                    case Screen.Routes: return Routes != null ? Routes.Count : 0;
                    case Screen.Activities: return Activities != null ? Activities.Count : 0;
                    case Screen.Paths: return Paths != null ? Paths.Count : 0;
                    case Screen.Consists: return Consists != null ? Consists.Count : 0;
                    default: return OptionRows;
                }
            }
        }

        void HandleInput()
        {
            var screen = (int)CurrentScreen;
            var count = CurrentCount();
            var step = 0;
            if (UserInput.IsKeyPressed(Keys.Down)) step = 1;
            if (UserInput.IsKeyPressed(Keys.Up)) step = -1;
            if (UserInput.IsKeyPressed(Keys.PageDown)) step = VisibleRows - 1;
            if (UserInput.IsKeyPressed(Keys.PageUp)) step = -(VisibleRows - 1);
            if (UserInput.IsKeyPressed(Keys.Home)) Selection[screen] = 0;
            if (UserInput.IsKeyPressed(Keys.End)) Selection[screen] = Math.Max(0, count - 1);
            if (step != 0 && count > 0)
                Selection[screen] = MathHelper.Clamp(Selection[screen] + step, 0, count - 1);
            if (CurrentScreen == Screen.Options)
                OptionRow = Selection[screen];

            if (CurrentScreen == Screen.Options)
            {
                var delta = UserInput.IsKeyPressed(Keys.Right) ? 1 : UserInput.IsKeyPressed(Keys.Left) ? -1 : 0;
                if (delta != 0)
                {
                    switch (OptionRow)
                    {
                        case 0: StartHour = (StartHour + delta + 24) % 24; break;
                        case 1: StartMinute = (StartMinute + delta * 15 + 60) % 60; break;
                        case 2: Season = (SeasonType)(((int)Season + delta + 4) % 4); break;
                        case 3: Weather = (WeatherType)(((int)Weather + delta + 3) % 3); break;
                    }
                }
            }

            if (UserInput.IsKeyPressed(Keys.Enter) && count > 0)
                Choose();
            if (UserInput.IsKeyPressed(Keys.Back))
                Back();
            if (UserInput.IsKeyPressed(Keys.Escape))
                Game.Exit();
        }

        void Choose()
        {
            switch (CurrentScreen)
            {
                case Screen.Routes:
                    CurrentScreen = Screen.Activities;
                    Selection[(int)Screen.Activities] = Scroll[(int)Screen.Activities] = 0;
                    RequestLoad(Screen.Activities);
                    break;
                case Screen.Activities:
                    ChosenActivity = Activities[Selection[(int)Screen.Activities]];
                    if (ChosenActivity is ExploreActivity)
                    {
                        CurrentScreen = Screen.Paths;
                        Selection[(int)Screen.Paths] = Scroll[(int)Screen.Paths] = 0;
                        RequestLoad(Screen.Paths);
                    }
                    else
                        Start();
                    break;
                case Screen.Paths:
                    ChosenPath = Paths[Selection[(int)Screen.Paths]];
                    CurrentScreen = Screen.Consists;
                    if (Consists == null || LoadedFolder != Routes[Selection[(int)Screen.Routes]].Key)
                    {
                        Selection[(int)Screen.Consists] = Scroll[(int)Screen.Consists] = 0;
                        RequestLoad(Screen.Consists);
                    }
                    break;
                case Screen.Consists:
                    ChosenConsist = Consists[Selection[(int)Screen.Consists]];
                    CurrentScreen = Screen.Options;
                    Selection[(int)Screen.Options] = OptionRow = OptionRows - 1; // land on Start
                    break;
                case Screen.Options:
                    if (OptionRow == OptionRows - 1)
                        Start();
                    break;
            }
        }

        void Back()
        {
            switch (CurrentScreen)
            {
                case Screen.Activities: CurrentScreen = Screen.Routes; break;
                case Screen.Paths: CurrentScreen = Screen.Activities; break;
                case Screen.Consists: CurrentScreen = Screen.Paths; break;
                case Screen.Options: CurrentScreen = Screen.Consists; break;
            }
        }

        /// <summary>Starts the chosen run in this process, with the same command line the desktop menu would build.</summary>
        void Start()
        {
            var startTime = String.Format("{0}:{1:00}", StartHour, StartMinute);
            string[] args;
            if (ChosenActivity is DefaultExploreActivity)
                args = new[] { "-start", "-explorer", ChosenPath.FilePath, ChosenConsist.FilePath, startTime, ((int)Season).ToString(), ((int)Weather).ToString() };
            else if (ChosenActivity is ExploreThroughActivity)
                args = new[] { "-start", "-exploreactivity", ChosenPath.FilePath, ChosenConsist.FilePath, startTime, ((int)Season).ToString(), ((int)Weather).ToString() };
            else
                args = new[] { "-start", "-activity", ChosenActivity.FilePath };
            Launched = true;
            Status = "Starting...";
            Trace.TraceInformation("Menu: starting RunActivity {0}", String.Join(" ", args.Select(a => a.Contains(" ") ? "\"" + a + "\"" : a)));
            Game.ReplaceState(new GameStateRunActivity(args));
        }

        [CallOnThread("Render")]
        internal override void BeginRender(RenderFrame frame)
        {
            var graphicsDevice = Game.RenderProcess.GraphicsDevice;
            if (Panel == null)
            {
                Panel = new RenderTarget2D(graphicsDevice, PanelWidthPx, PanelHeightPx, false, SurfaceFormat.Color, DepthFormat.None, 0, RenderTargetUsage.PreserveContents);
                SpriteBatch = new SpriteBatch(graphicsDevice);
                TextManager = new WindowTextManager();
                TitleFont = TextManager.GetExact("Arial", 30, System.Drawing.FontStyle.Bold);
                ItemFont = TextManager.GetExact("Arial", 24, System.Drawing.FontStyle.Regular);
                SmallFont = TextManager.GetExact("Arial", 18, System.Drawing.FontStyle.Regular);
                var height = PanelWidthM * PanelHeightPx / PanelWidthPx;
                Primitive = new PanelPrimitive(graphicsDevice, Panel, PanelWidthM, height, new Vector3(0, -PanelDropM, -PanelDistanceM));
            }
            DrawPanel(graphicsDevice);
            base.BeginRender(frame);
        }

        void DrawPanel(GraphicsDevice graphicsDevice)
        {
            TextManager.Load(graphicsDevice); // uploads the glyphs measured and drawn last frame
            graphicsDevice.SetRenderTarget(Panel);
            graphicsDevice.Clear(new Color(18, 22, 30, 235));
            SpriteBatch.Begin(SpriteSortMode.Immediate, BlendState.NonPremultiplied);

            var margin = 40;
            var y = margin;
            string title, hint;
            var rows = new List<string>();
            var screen = (int)CurrentScreen;
            lock (ContentLock)
            {
                switch (CurrentScreen)
                {
                    case Screen.Routes:
                        title = "Open Rails - choose a route";
                        if (Routes != null) rows.AddRange(Routes.Select(r => r.Value.Name + "   (" + r.Key.Name + ")"));
                        hint = "Up/Down and Enter choose, Escape quits";
                        break;
                    case Screen.Activities:
                        title = (LoadedRoute != null ? LoadedRoute.Name : "") + " - choose an activity";
                        if (Activities != null)
                            rows.AddRange(Activities.Select(a => a is DefaultExploreActivity ? "Explore the route" : a is ExploreThroughActivity ? "Explore in activity mode" : a.Name));
                        hint = "Enter chooses, Backspace goes back";
                        break;
                    case Screen.Paths:
                        title = (LoadedRoute != null ? LoadedRoute.Name : "") + " - choose a path";
                        if (Paths != null) rows.AddRange(Paths.Select(p => p.Name));
                        hint = "Enter chooses, Backspace goes back";
                        break;
                    case Screen.Consists:
                        title = "Choose a consist";
                        if (Consists != null) rows.AddRange(Consists.Select(c => c.Name + "   (" + c.Locomotive.Name + ")"));
                        hint = "Enter chooses, Backspace goes back";
                        break;
                    default:
                        title = "Ready to start";
                        rows.Add("Start hour      " + StartHour.ToString("00"));
                        rows.Add("Start minute    " + StartMinute.ToString("00"));
                        rows.Add("Season        " + Season);
                        rows.Add("Weather       " + Weather);
                        rows.Add("Start  >  " + (ChosenPath != null ? ChosenPath.Name : "") + "  with  " + (ChosenConsist != null ? ChosenConsist.Name : ""));
                        hint = "Left/Right change a value, Enter on Start begins the run, Backspace goes back";
                        break;
                }
            }

            TitleFont.Draw(SpriteBatch, new Point(margin, y), title, Color.White);
            y += TitleFont.Height + 16;

            // Keep the selection visible.
            var selected = Selection[screen];
            if (selected < Scroll[screen]) Scroll[screen] = selected;
            if (selected >= Scroll[screen] + VisibleRows) Scroll[screen] = selected - VisibleRows + 1;
            var rowHeight = ItemFont.Height + 6;
            for (var i = Scroll[screen]; i < rows.Count && i < Scroll[screen] + VisibleRows; i++)
            {
                var text = rows[i];
                var maxWidth = PanelWidthPx - 2 * margin - 30;
                while (text.Length > 4 && ItemFont.MeasureString(text) > maxWidth)
                    text = text.Substring(0, text.Length - 4) + "...";
                if (i == selected)
                {
                    SpriteBatch.Draw(Panel == null ? null : WhitePixel(graphicsDevice), new Rectangle(margin - 10, y - 2, PanelWidthPx - 2 * margin + 20, rowHeight), new Color(70, 110, 170, 255));
                    ItemFont.Draw(SpriteBatch, new Point(margin, y), "> " + text, Color.White);
                }
                else
                    ItemFont.Draw(SpriteBatch, new Point(margin + 28, y), text, new Color(210, 215, 225));
                y += rowHeight;
            }
            if (rows.Count > VisibleRows)
                SmallFont.Draw(SpriteBatch, new Point(margin, y + 4), String.Format("{0} of {1}", selected + 1, rows.Count), new Color(160, 165, 175));

            var status = Status;
            if (!String.IsNullOrEmpty(status))
                SmallFont.Draw(SpriteBatch, new Point(margin, PanelHeightPx - margin - SmallFont.Height * 2 - 8), status, new Color(255, 210, 120));
            SmallFont.Draw(SpriteBatch, new Point(margin, PanelHeightPx - margin - SmallFont.Height), hint, new Color(160, 165, 175));

            SpriteBatch.End();
            graphicsDevice.SetRenderTarget(null);
        }

        static Texture2D whitePixel;
        static Texture2D WhitePixel(GraphicsDevice graphicsDevice)
        {
            if (whitePixel == null)
            {
                whitePixel = new Texture2D(graphicsDevice, 1, 1);
                whitePixel.SetData(new[] { Color.White });
            }
            return whitePixel;
        }

        internal override void Dispose()
        {
            // The render targets and fonts are small; the run that follows keeps using the device, so leave them to the GC.
            base.Dispose();
        }

        /// <summary>A textured rectangle at a fixed place in the seat frame, showing the menu panel.</summary>
        class PanelPrimitive : RenderPrimitive
        {
            public readonly PanelMaterial Material;
            readonly VertexBuffer VertexBuffer;

            public PanelPrimitive(GraphicsDevice graphicsDevice, Texture2D texture, float width, float height, Vector3 centre)
            {
                Material = new PanelMaterial(graphicsDevice, texture);
                var w = width / 2;
                var h = height / 2;
                var vertices = new[] {
                    new VertexPositionTexture(centre + new Vector3(-w, +h, 0), new Vector2(0, 0)),
                    new VertexPositionTexture(centre + new Vector3(+w, +h, 0), new Vector2(1, 0)),
                    new VertexPositionTexture(centre + new Vector3(-w, -h, 0), new Vector2(0, 1)),
                    new VertexPositionTexture(centre + new Vector3(+w, -h, 0), new Vector2(1, 1)),
                };
                VertexBuffer = new VertexBuffer(graphicsDevice, typeof(VertexPositionTexture), vertices.Length, BufferUsage.WriteOnly);
                VertexBuffer.SetData(vertices);
            }

            public override void Draw(GraphicsDevice graphicsDevice)
            {
                graphicsDevice.SetVertexBuffer(VertexBuffer);
                graphicsDevice.DrawPrimitives(PrimitiveType.TriangleStrip, 0, 2);
            }
        }

        class PanelMaterial : Material
        {
            readonly PanelShader Shader;
            readonly Texture2D Texture;

            public PanelMaterial(GraphicsDevice graphicsDevice, Texture2D texture)
                : base(null, null)
            {
                Shader = new PanelShader(graphicsDevice);
                Texture = texture;
            }

            public override void SetState(GraphicsDevice graphicsDevice, Material previousMaterial)
            {
                Shader.CurrentTechnique = Shader.Techniques["Loading"];
                Shader.Texture = Texture;
                graphicsDevice.BlendState = BlendState.NonPremultiplied;
                graphicsDevice.RasterizerState = RasterizerState.CullNone;
                graphicsDevice.DepthStencilState = DepthStencilState.Default;
            }

            public override void Render(GraphicsDevice graphicsDevice, IEnumerable<RenderItem> renderItems, ref Matrix XNAViewMatrix, ref Matrix XNAProjectionMatrix)
            {
                foreach (var item in renderItems)
                {
                    Shader.WorldViewProjection = item.XNAMatrix * XNAViewMatrix * XNAProjectionMatrix;
                    Shader.CurrentTechnique.Passes[0].Apply();
                    item.RenderPrimitive.Draw(graphicsDevice);
                }
            }

            public override void ResetState(GraphicsDevice graphicsDevice)
            {
                graphicsDevice.BlendState = BlendState.Opaque;
                graphicsDevice.RasterizerState = RasterizerState.CullCounterClockwise;
            }
        }

        /// <summary>The loading screen's shader: a texture through a world-view-projection, which is all a panel needs.</summary>
        class PanelShader : Shader
        {
            readonly EffectParameter worldViewProjection;
            readonly EffectParameter loadingTexture;

            public Matrix WorldViewProjection { set { worldViewProjection.SetValue(value); } }
            public Texture2D Texture { set { loadingTexture.SetValue(value); } }

            public PanelShader(GraphicsDevice graphicsDevice)
                : base(graphicsDevice, "Loading")
            {
                worldViewProjection = Parameters["WorldViewProjection"];
                loadingTexture = Parameters["LoadingTexture"];
            }
        }
    }
}
