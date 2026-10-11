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
//

using System;
using System.Collections.ObjectModel;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

using System.Reflection;
using GNU.Gettext;
using ORTS.Menu;
using ORTS.Common;
using Orts.Common;
using ORTS.TrackViewer.Drawing;
using ORTS.TrackViewer.Drawing.Labels;
using ORTS.TrackViewer.UserInterface;
using ORTS.TrackViewer.Editing;
using ORTS.TrackViewer.Editing.Charts;

namespace ORTS.TrackViewer
{

    /// <summary>
    /// Delegate that can be called by routines such that we can draw it to the screen
    /// </summary>
    /// <param name="message">Message to draw</param>
    public delegate void MessageDelegate(string message);

    /// <summary>
    /// This is the main type for your game
    /// </summary>
    public class TrackViewer : Microsoft.Xna.Framework.Game
    {
        #region Public members
        /// <summary>String showing the date of the program</summary>
        public readonly static string TrackViewerVersion = "2018/01/09";
        /// <summary>Path where the content (like .png files) is stored</summary>
        public string ContentPath { get; private set; }
        /// <summary>Directory where the content (like .png files) is stored</summary>
        public static string ContentDirectory => System.IO.Path.Combine(AppContext.BaseDirectory, "Content");
        /// <summary>The context menu (drawn in the game window) used by the path editor and labels</summary>
        public static GameMenu ContextMenu { get; } = new GameMenu { ShowBar = false };
        /// <summary>True when the last mouse/keyboard input was used by the in-window menus</summary>
        public bool InputCapturedByUserInterface { get; private set; }
        /// <summary>Folder where MSTS is installed (or at least, where the files needed for tracks, routes and paths are stored)</summary>
        public Folder InstallFolder { get; private set; }
        /// <summary>List of available routes (in the install directory)</summary>
        public Collection<Route> Routes { get; private set; } // Collection because of FxCop
        /// <summary>List of available paths in the current route</summary>
        public Collection<Path> Paths { get; private set; } // Collection because of FxCop
        /// <summary>Route, ie with a path c:\program files\microsoft games\train simulator\routes\usa1  - may be different on different pc's</summary>
        public Route CurrentRoute { get; private set; }
        /// <summary>Route that was used last time</summary>
        private Route DefaultRoute;
        /// <summary>Width of the drawing screen in pixels</summary>
        public int ScreenW { get; private set; }
        /// <summary>Height of the drawing screen in pixels</summary>
        public int ScreenH { get; private set; }
        /// <summary>The information of the route like trackDB, tsectiondat, ..., loaded from MSTS route files.</summary>
        public RouteData RouteData { get; private set; }
        /// <summary>(Draw)trackDB, that also contains the track data base and the track section data</summary>
        public DrawTrackDB DrawTrackDB { get; private set; }
        /// <summary>Main draw area</summary>
        public DrawArea DrawArea { get; private set; }
        /// <summary>The frame rate</summary>
        public SmoothedData FrameRate { get; private set; }
        /// <summary>The routines to select and draw multiple paths</summary>
        public DrawMultiplePaths DrawMultiplePaths { get; private set; }

        /// <summary>The language manager to deal with various languages.</summary>
        public LanguageManager LanguageManager { get; private set; }

        /// <summary>The Path editor</summary>
        public PathEditor PathEditor { get; private set; }
        /// <summary>The routines to draw the .pat file</summary>
        public DrawPATfile DrawPATfile { get; private set; }

        #endregion

        #region Private members
        GraphicsDeviceManager graphics;
        SpriteBatch spriteBatch;

        /// <summary>Draw area for the inset</summary>
        ShadowDrawArea drawAreaInset;
        /// <summary>The scale ruler to draw on screen</summary>
        DrawScaleRuler drawScaleRuler;
        /// <summary>For drawing real world longitude and latitude</summary>
        DrawLongitudeLatitude drawLongitudeLatitude;
        /// <summary>For drawing the action that the editor might be taking</summary>
        DrawEditorAction drawEditorAction;
        /// <summary>The routines to draw the world tiles</summary>
        DrawWorldTiles drawWorldTiles;
        /// <summary>The routines to draw the grade of a path</summary>
        DrawPathChart drawPathChart;
        /// <summary>The routines to draw the terrain textures</summary>
        public DrawTerrain drawTerrain; //todo, get it private again: statusbar

        DrawLabels drawLabels;

        /// <summary>The menu at the top</summary>
        MenuControl menuControl;
        /// <summary>The status bar at the bottom</summary>
        StatusBarControl statusBarControl;

        /// <summary>when we have lost focus, we do not want to enable shifting with mouse</summary>
        private bool lostFocus;
        /// <summary>number of times we want to skip draw because nothing happened</summary>
        private int skipDrawAmount;
        /// <summary>Maximum number of times we will skipp drawing</summary>
        private const int maxSkipDrawAmount = 10;

        /// <summary>The fontmanager that we use to draw strings</summary>
        private FontManager fontManager;
        /// <summary>The command-line arguments</summary>
        private string[] commandLineArgs;
        #endregion

        #region Constructor and Initialization methods

        /// <summary>
        /// Constructor. This is where it all starts.
        /// </summary>
        public TrackViewer(string[] args)
        {
            if (Properties.Settings.Default.CallUpgrade)
            {
                Properties.Settings.Default.Upgrade();
                Properties.Settings.Default.CallUpgrade = false;
            }

            this.commandLineArgs = args;

            graphics = new GraphicsDeviceManager(this);
            ContentPath = ContentDirectory;

            Content.RootDirectory = "Content";
            graphics.PreferredBackBufferWidth = 1024;
            graphics.PreferredBackBufferHeight = 768;
            ScreenH = graphics.PreferredBackBufferHeight;
            ScreenW = graphics.PreferredBackBufferWidth;
            SetAliasing();
            graphics.IsFullScreen = false;
            Window.AllowUserResizing = true;
            Window.ClientSizeChanged += new System.EventHandler<System.EventArgs>(Window_ClientSizeChanged);

            //we do not a very fast behaviour, but we do need to get all key presses
            IsFixedTimeStep = true;
            TargetElapsedTime = TimeSpan.FromSeconds(0.05);
            FrameRate = new SmoothedData(0.5f);
            InitLogging();

            LanguageManager = new LanguageManager();
            LanguageManager.LoadLanguage(); // need this before all menus and stuff are initialized.

        }

        /// <summary>
        /// Allows the game to perform any initialization it needs to before starting to run.
        /// This is where it can query for any required services and load any non-graphic
        /// relation ontent.  Calling base.Initialize will enumerate through any components
        /// and initialize them as well.
        /// </summary>
        protected override void Initialize()
        {
            GameThread.SetCurrentThreadAsGameThread();
            TVInputSettings.SetDefaults();

            statusBarControl = new StatusBarControl(this);
            menuControl = new MenuControl(this);
            menuControl.PopulateLanguages();
            DrawColors.Initialize(menuControl);

            drawWorldTiles = new DrawWorldTiles();
            drawScaleRuler = new DrawScaleRuler();
            DrawArea = new DrawArea(drawScaleRuler);
            drawAreaInset = new ShadowDrawArea(null)
            {
                StrictChecking = true
            };

            fontManager = FontManager.Instance;
            SetSubwindowSizes();

            this.IsMouseVisible = true;

            // install folder
            if (String.IsNullOrEmpty(Properties.Settings.Default.installDirectory))
            {
                try
                {
                    Properties.Settings.Default.installDirectory = MSTS.MSTSPath.Base();
                }
                catch {}
            }
            InstallFolder = new Folder("default", Properties.Settings.Default.installDirectory);

            FindRoutes(InstallFolder);

            drawPathChart = new DrawPathChart();

            Properties.Settings.Default.CurrentActivityName = ""; 

            base.Initialize();
        }

        /// <summary>
        /// Set the sizes of the various subwindows that they can use to draw upon.
        /// </summary>
        void SetSubwindowSizes()
        {
            int insetRatio = 10;

            //We need to give enough room for menu and status bar (both drawn in the game window)
            int menuHeight = menuControl.MenuHeight;
            int statusbarHeight = statusBarControl.StatusbarHeight;
            menuControl.SetScreenSize(ScreenW, ScreenH);
            ContextMenu.SetScreenSize(ScreenW, ScreenH);
            statusBarControl.SetScreenSize(ScreenW, statusbarHeight, ScreenH);

            //The rest of the available pixes are for the draw-area's
            DrawArea.SetScreenSize(0, menuHeight, ScreenW, ScreenH - statusbarHeight - menuHeight);
            drawAreaInset.SetScreenSize(ScreenW - ScreenW / insetRatio, menuHeight + 1, ScreenW / insetRatio, ScreenH / insetRatio);

            //Some on-screen features depend on the actual font-height
            int halfHeight = (int)(fontManager.DefaultFont.Height / 2);
            drawScaleRuler.SetLocationAndSize(halfHeight, ScreenH - statusbarHeight - halfHeight, 2*halfHeight);
            drawLongitudeLatitude = new DrawLongitudeLatitude(halfHeight, menuHeight);
            drawEditorAction = new DrawEditorAction(halfHeight, menuHeight + 2 * halfHeight);
        }

        /// <summary>
        /// LoadContent will be called once per game and is the place to load
        /// all of your content.
        /// </summary>
        protected override void LoadContent()
        {
            // Create a new SpriteBatch, which can be used to draw textures.
            spriteBatch = new SpriteBatch(GraphicsDevice);
            BasicShapes.LoadContent(GraphicsDevice, spriteBatch, ContentPath);
            drawAreaInset.LoadContent(GraphicsDevice, spriteBatch, 2, 2, 2);
            //drawTerrain.LoadContent(GraphicsDevice); // can only be done when route is known!
        }

        /// <summary>
        /// UnloadContent will be called once per game and is the place to unload
        /// all content.
        /// </summary>
        protected override void UnloadContent()
        {
            // Unload any non ContentManager content here
        }

        /// <summary>
        /// Simplified Draw routine that only shows background and a message.
        /// </summary>
        /// <param name="message">The message you want to show</param>
        private void DrawLoadingMessage(string message)
        {
            // This is not really a game State, because it is not used interactively. In fact, Draw itself is
            // probably not called because the program is doing other things
            BeginDraw();
            GraphicsDevice.Clear(DrawColors.colorsNormal.ClearWindow);
            spriteBatch.Begin();
            // it is better to have integer locations, otherwise text is difficult to read
            Vector2 messageLocation = new Vector2((float)Math.Round(ScreenW / 2f), (float)Math.Round(ScreenH / 2f));
            BasicShapes.DrawStringCentered(messageLocation, DrawColors.colorsNormal.Text, message);

            // we have to redo the string drawing, because we now first have to load the characters into textures.
            fontManager.Update(GraphicsDevice);
            BasicShapes.DrawStringCentered(messageLocation, DrawColors.colorsNormal.Text, message);

            spriteBatch.End();
            EndDraw();
        }

        #endregion

        #region Main game methods
        /// <summary>
        /// Allows the game to run logic such as updating the world,
        /// checking for collisions, gathering input, and playing audio.
        /// </summary>
        /// <param name="gameTime">Provides a snapshot of timing values.</param>
        protected override void Update(GameTime gameTime)
        {
            // Actions requested from the Avalonia UI thread (dialogs) are executed here, on the game thread.
            if (GameThread.RunPending())
            {
                skipDrawAmount = 0;
            }

            if (!this.IsActive || Dialogs.ModalIsOpen)
            {
                lostFocus = true;
                FinishUpdate(gameTime);
                return;
            }

            TVUserInput.Update();
            if (lostFocus)
            {
                // if the previous call was in inactive mode, we do want TVUserInput to be updated, but we will only
                // act on it the next round. To make sure moving the mouse to other locations and back is influencing
                // the location visible in trackviewer.
                lostFocus = false;
                FinishUpdate(gameTime);
                return;
            }

            fontManager.Update(GraphicsDevice);
            if (DrawTrackDB != null)
            {   // when update is called, we are not searching via menu
                DrawTrackDB.ClearHighlightOverrides();
            }

            if (TVUserInput.Changed)
            {
                skipDrawAmount = 0;
                TVUserInput.Handled();
            }

            // The in-window menus get the first chance to use the input
            InputCapturedByUserInterface = false;
            if (ContextMenu.IsOpen)
            {
                ContextMenu.HandleInput();
                InputCapturedByUserInterface = true;
            }
            else if (menuControl.Menu.HandleInput())
            {
                InputCapturedByUserInterface = true;
            }
            if (InputCapturedByUserInterface)
            {
                FinishUpdate(gameTime);
                return;
            }

            // First check all the buttons that can be kept down.

            if (this.drawPathChart.IsActived)
            {
                if (TVUserInput.IsDown(TVUserCommands.ShiftLeft)) { drawPathChart.Shift(-1); skipDrawAmount = 0; }
                if (TVUserInput.IsDown(TVUserCommands.ShiftRight)) { drawPathChart.Shift(1); skipDrawAmount = 0; }
                if (TVUserInput.IsDown(TVUserCommands.ZoomIn)) { drawPathChart.Zoom(-1); skipDrawAmount = 0; }
                if (TVUserInput.IsDown(TVUserCommands.ZoomOut)) { drawPathChart.Zoom(1); skipDrawAmount = 0; }
            }
            else
            {
                if (TVUserInput.IsDown(TVUserCommands.ShiftLeft)) { DrawArea.ShiftLeft(); skipDrawAmount = 0; }
                if (TVUserInput.IsDown(TVUserCommands.ShiftRight)) { DrawArea.ShiftRight(); skipDrawAmount = 0; }
                if (TVUserInput.IsDown(TVUserCommands.ShiftUp)) { DrawArea.ShiftUp(); skipDrawAmount = 0; }
                if (TVUserInput.IsDown(TVUserCommands.ShiftDown)) { DrawArea.ShiftDown(); skipDrawAmount = 0; }

                if (TVUserInput.IsDown(TVUserCommands.ZoomIn)) { DrawArea.Zoom(-1); skipDrawAmount = 0; }
                if (TVUserInput.IsDown(TVUserCommands.ZoomOut)) { DrawArea.Zoom(1); skipDrawAmount = 0; }
            }

            if (TVUserInput.IsPressed(TVUserCommands.Quit)) this.Quit();
            if (TVUserInput.IsPressed(TVUserCommands.ReloadRoute)) this.ReloadRoute();

            if (TVUserInput.IsPressed(TVUserCommands.ShiftToMouseLocation)) DrawArea.ShiftToLocation(DrawArea.MouseLocation);
            if (TVUserInput.IsPressed(TVUserCommands.ZoomInSlow)) DrawArea.Zoom(-1);
            if (TVUserInput.IsPressed(TVUserCommands.ZoomOutSlow)) DrawArea.Zoom(1);
            if (TVUserInput.IsPressed(TVUserCommands.ZoomToTile)) DrawArea.ZoomToTile();
            if (TVUserInput.IsPressed(TVUserCommands.ZoomReset))
            {
                DrawArea.ZoomReset(DrawTrackDB);
                drawAreaInset.ZoomReset(DrawTrackDB);  // needed in case window was resized
            }

            if (DrawPATfile != null && Properties.Settings.Default.showPATfile)
            {
                if (TVUserInput.IsPressed(TVUserCommands.ExtendPath))     DrawPATfile.ExtendPath();
                if (TVUserInput.IsPressed(TVUserCommands.ExtendPathFull)) DrawPATfile.ExtendPathFull();
                if (TVUserInput.IsPressed(TVUserCommands.ReducePath))     DrawPATfile.ReducePath();
                if (TVUserInput.IsPressed(TVUserCommands.ReducePathFull)) DrawPATfile.ReducePathFull();
                if (TVUserInput.IsDown(TVUserCommands.ShiftToPathLocation)) DrawArea.ShiftToLocation(DrawPATfile.CurrentLocation);
            }

            if (PathEditor != null && Properties.Settings.Default.showTrainpath)
            {
                if (TVUserInput.IsPressed(TVUserCommands.ExtendPath))     PathEditor.ExtendPath();
                if (TVUserInput.IsPressed(TVUserCommands.ExtendPathFull)) PathEditor.ExtendPathFull();
                if (TVUserInput.IsPressed(TVUserCommands.ReducePath))     PathEditor.ReducePath();
                if (TVUserInput.IsPressed(TVUserCommands.ReducePathFull)) PathEditor.ReducePathFull();
                if (TVUserInput.IsDown(TVUserCommands.ShiftToPathLocation)) DrawArea.ShiftToLocation(PathEditor.CurrentLocation);

                if (TVUserInput.IsPressed(TVUserCommands.EditorUndo)) PathEditor.Undo();
                if (TVUserInput.IsPressed(TVUserCommands.EditorRedo)) PathEditor.Redo();
                if (TVUserInput.IsMouseXButton1Pressed()) PathEditor.Undo();
                if (TVUserInput.IsMouseXButton2Pressed()) PathEditor.Redo();
            }

            // Window-relative location for the in-window context menus, (approximate) screen location for dialogs.
            // On Wayland the screen location of a window is not known, so dialogs are positioned by the compositor.
            var mouseLocationX = TVUserInput.MouseLocationX;
            var mouseLocationY = TVUserInput.MouseLocationY;
            var mouseLocationAbsoluteX = Window.ClientBounds.Left + TVUserInput.MouseLocationX;
            var mouseLocationAbsoluteY = Window.ClientBounds.Top  + TVUserInput.MouseLocationY;
            if (PathEditor != null && PathEditor.EditingIsActive)
            {
                if (TVUserInput.IsMouseRightButtonPressed())
                {
                    PathEditor.OnLeftMouseRelease(); // any action done with left mouse is cancelled now
                    PathEditor.PopupContextMenu(mouseLocationX, mouseLocationY, mouseLocationAbsoluteX, mouseLocationAbsoluteY);
                }

                PathEditor.DeterminePossibleActions(TVUserInput.IsDown(TVUserCommands.EditorTakesMouseClickDrag), TVUserInput.IsDown(TVUserCommands.EditorTakesMouseClickAction),
                    TVUserInput.MouseLocationX, TVUserInput.MouseLocationY);

                if (TVUserInput.IsPressed(TVUserCommands.PlaceEndPoint)) PathEditor.PlaceEndPoint();
                if (TVUserInput.IsPressed(TVUserCommands.PlaceWaitPoint)) PathEditor.PlaceWaitPoint();


                if (TVUserInput.IsMouseLeftButtonPressed())
                {
                    PathEditor.OnLeftMouseClick();
                }
                if (TVUserInput.IsMouseLeftButtonDown())
                {
                    PathEditor.OnLeftMouseMoved(); // to make sure it is reactive enough, don't even care if mouse is really moved
                }
                if (TVUserInput.IsMouseLeftButtonReleased())
                {
                    PathEditor.OnLeftMouseRelease();
                }

                if (TVUserInput.IsReleased(TVUserCommands.EditorTakesMouseClickDrag))
                {
                    PathEditor.OnLeftMouseCancel();
                }
                drawPathChart.DrawDynamics();
            }
            else if (drawLabels != null)
            {
                if (TVUserInput.IsPressed(TVUserCommands.AddLabel))
                {
                    drawLabels.AddLabel(mouseLocationAbsoluteX, mouseLocationAbsoluteY);
                }

                if (TVUserInput.IsDown(TVUserCommands.EditorTakesMouseClickDrag))
                {
                    if (TVUserInput.IsMouseLeftButtonPressed())
                    {
                        drawLabels.OnLeftMouseClick();
                    }
                    if (TVUserInput.IsMouseLeftButtonDown())
                    {
                        drawLabels.OnLeftMouseMoved();
                    }
                    if (TVUserInput.IsMouseLeftButtonReleased())
                    {
                        drawLabels.OnLeftMouseRelease();
                    }
                }
                if (TVUserInput.IsReleased(TVUserCommands.EditorTakesMouseClickDrag))
                {
                    drawLabels.OnLeftMouseCancel();
                }

                if (TVUserInput.IsMouseRightButtonPressed())
                {
                    drawLabels.PopupContextMenu(mouseLocationX, mouseLocationY, mouseLocationAbsoluteX, mouseLocationAbsoluteY);
                }
            }

            bool otherWindowHasMouse = ContextMenu.IsOpen || drawPathChart.IsActived;
            if (!TVUserInput.IsDown(TVUserCommands.EditorTakesMouseClickDrag) && !otherWindowHasMouse)
            {
                if (TVUserInput.IsMouseMoved() && TVUserInput.IsMouseLeftButtonDown())
                {
                    DrawArea.ShiftArea(TVUserInput.MouseMoveX(), TVUserInput.MouseMoveY());
                }
            }

            if (TVUserInput.IsMouseWheelChanged())
            {
                int mouseWheelChange = TVUserInput.MouseWheelChange();
                if (!this.drawPathChart.IsActived)
                {
                    if (TVUserInput.IsDown(TVUserCommands.MouseZoomSlow))
                    {
                        DrawArea.Zoom(mouseWheelChange > 0 ? -1 : 1);
                    }
                    else
                    {
                        DrawArea.Zoom(-mouseWheelChange / 40);
                    }
                }
            }


            DrawArea.Update();
            drawAreaInset.Update();
            drawAreaInset.Follow(DrawArea, 10f);

            if (TVUserInput.IsPressed(TVUserCommands.ToggleZoomAroundMouse)) menuControl.MenuToggleZoomingAroundMouse();

            if (TVUserInput.IsPressed(TVUserCommands.ToggleShowTerrain)) menuControl.MenuToggleShowTerrain();
            if (TVUserInput.IsPressed(TVUserCommands.ToggleShowDMTerrain)) menuControl.MenuToggleShowDMTerrain();
            if (TVUserInput.IsPressed(TVUserCommands.ToggleShowPatchLines)) menuControl.MenuToggleShowPatchLines();
            if (TVUserInput.IsPressed(TVUserCommands.ToggleShowSignals)) menuControl.MenuToggleShowSignals();
            if (TVUserInput.IsPressed(TVUserCommands.ToggleShowSidings)) menuControl.MenuToggleShowSidings();
            if (TVUserInput.IsPressed(TVUserCommands.ToggleShowSidingNames)) menuControl.MenuToggleShowSidingNames();
            if (TVUserInput.IsPressed(TVUserCommands.ToggleShowPlatforms)) menuControl.MenuToggleShowPlatforms();
            if (TVUserInput.IsPressed(TVUserCommands.ToggleShowPlatformNames)) menuControl.MenuCirculatePlatformStationNames();
            if (TVUserInput.IsPressed(TVUserCommands.ToggleShowSpeedLimits)) menuControl.MenuToggleShowSpeedLimits();
            if (TVUserInput.IsPressed(TVUserCommands.ToggleShowMilePosts)) menuControl.MenuToggleShowMilePosts();
            if (TVUserInput.IsPressed(TVUserCommands.ToggleShowTrainpath)) menuControl.MenuToggleShowTrainpath();
            if (TVUserInput.IsPressed(TVUserCommands.ToggleShowPatFile)) menuControl.MenuToggleShowPatFile();
            if (TVUserInput.IsPressed(TVUserCommands.ToggleHighlightTracks)) menuControl.MenuToggleHighlightTracks();
            if (TVUserInput.IsPressed(TVUserCommands.ToggleHighlightItems)) menuControl.MenuToggleHighlightItems();

            //keyboard shortcuts for menu
            if (TVUserInput.IsPressed(TVUserCommands.MenuFile)) menuControl.Menu.OpenBarMenu(menuControl.menuFile);
            if (TVUserInput.IsPressed(TVUserCommands.MenuView)) menuControl.Menu.OpenBarMenu(menuControl.menuView);
            if (TVUserInput.IsPressed(TVUserCommands.MenuTrackItems)) menuControl.Menu.OpenBarMenu(menuControl.menuTrackItems);
            if (TVUserInput.IsPressed(TVUserCommands.MenuPreferences)) menuControl.Menu.OpenBarMenu(menuControl.menuPreferences);
            if (TVUserInput.IsPressed(TVUserCommands.MenuStatusbar)) menuControl.Menu.OpenBarMenu(menuControl.menuStatusbar);
            if (TVUserInput.IsPressed(TVUserCommands.MenuPathEditor)) menuControl.Menu.OpenBarMenu(menuControl.menuPathEditor);
            if (TVUserInput.IsPressed(TVUserCommands.MenuTerrain)) menuControl.Menu.OpenBarMenu(menuControl.menuTerrain);
            if (TVUserInput.IsPressed(TVUserCommands.MenuHelp)) menuControl.Menu.OpenBarMenu(menuControl.menuHelp);


            if (TVUserInput.IsPressed(TVUserCommands.Debug)) RunDebug();

            FinishUpdate(gameTime);
        }

        /// <summary>
        /// The part of Update that needs to be done always, even when there is no input to act on.
        /// </summary>
        private void FinishUpdate(GameTime gameTime)
        {
            base.Update(gameTime);

            HandleCommandLineArgs();

            SetTitle();

            // Even if there is nothing new to draw for main window, we might still need to draw for the shadow textures.
            // (This uses render targets, so it is done here and not within the main Draw)
            if (DrawTrackDB != null && Properties.Settings.Default.showInset)
            {
                drawAreaInset.DrawShadowTextures(DrawTrackDB.DrawTracks, DrawColors.colorsNormal.ClearWindowInset);
            }

            // if there is nothing to draw, be done. Note that we cannot simply skip drawing inside Draw, since the
            // content of the back buffer is undefined after a present on some backends (e.g. Vulkan).
            if (--skipDrawAmount > 0)
            {
                SuppressDraw();
            }
        }

        /// <summary>
        /// This is called when the game should draw itself.
        /// </summary>
        /// <param name="gameTime">Provides a snapshot of timing values.</param>
        protected override void Draw(GameTime gameTime)
        {
            GraphicsDevice.Clear(DrawColors.colorsNormal.ClearWindow);

            spriteBatch.Begin();
            if (DrawTrackDB != null)
            {
                if (drawTerrain != null) { drawTerrain.Draw(DrawArea); }
                drawWorldTiles.Draw(DrawArea);
                DrawArea.DrawTileGrid();
                if (drawTerrain != null) { drawTerrain.DrawPatchLines(DrawArea); }

                DrawTrackDB.DrawRoads(DrawArea);
                DrawTrackDB.DrawTracks(DrawArea);
                DrawTrackDB.DrawTrackHighlights(DrawArea, true);

                DrawTrackDB.DrawJunctionAndEndNodes(DrawArea);

                if (Properties.Settings.Default.showInset)
                {
                    drawAreaInset.DrawBackground(DrawColors.colorsNormal.ClearWindowInset);
                    //drawTrackDB.DrawTracks(drawAreaInset); //replaced by next line
                    drawAreaInset.DrawShadowedTextures();
                    DrawTrackDB.DrawTrackHighlights(drawAreaInset, false);
                    drawAreaInset.DrawBorder(Color.Red, DrawArea);
                    drawAreaInset.DrawBorder(Color.Black);
                }

                if (DrawMultiplePaths != null) DrawMultiplePaths.Draw(DrawArea);
                if (DrawPATfile != null && Properties.Settings.Default.showPATfile) DrawPATfile.Draw(DrawArea);
                if (PathEditor != null && Properties.Settings.Default.showTrainpath) PathEditor.Draw(DrawArea);
                drawEditorAction.Draw(PathEditor);

                DrawTrackDB.DrawRoadTrackItems(DrawArea);
                DrawTrackDB.DrawTrackItems(DrawArea);
                DrawTrackDB.DrawItemHighlights(DrawArea);

                CalculateFPS(gameTime);

                drawScaleRuler.Draw();
                drawLongitudeLatitude.Draw(DrawArea.MouseLocation);
                drawLabels.Draw(DrawArea);

                DebugWindow.DrawAll();
            }
            else if (Routes == null || Routes.Count == 0)
            {
                Vector2 messageLocation = new Vector2((float)Math.Round(ScreenW / 2f), (float)Math.Round(ScreenH / 2f));
                BasicShapes.DrawStringCentered(messageLocation, DrawColors.colorsNormal.Text,
                    catalog.GetString("No routes found. Use File > Select install folder to select an MSTS/Open Rails install folder."));
            }

            statusBarControl.Update(this, DrawArea.MouseLocation);
            statusBarControl.Draw();
            menuControl.Menu.Draw();
            ContextMenu.Draw();

            spriteBatch.End();

            base.Draw(gameTime);
            skipDrawAmount = maxSkipDrawAmount;
        }

        #endregion

        #region User actions (e.g. from menu)
        /// <summary>
        /// Set aliasing depending on the settings (set in the menu)
        /// </summary>
        public void SetAliasing()
        {
            // Personally, I do not think anti-aliasing looks crisp at all. Poddibly because not enough multi-sampling is used.
            // If someone knows how to get better/best antisampling depending on available hardware, be my guess.
            graphics.PreferMultiSampling = Properties.Settings.Default.doAntiAliasing;
        }

        /// <summary>
        /// Show the window with the chart of the path
        /// </summary>
        public void ShowPathChart()
        {
            this.drawPathChart.Open();
        }

        /// <summary>
        /// Ask the user if we really want to quit or not, and if yes, well, quit.
        /// </summary>
        public void Quit()
        {
            string message = String.Empty;
            if (PathEditor!=null && PathEditor.HasModifiedPath)  {
                message = catalog.GetString("The path you are working on has un-saved changes.\n");
            }
            message += catalog.GetString("Do you really want to Quit?");

            Dialogs.Ask(message, catalog.GetString("Question"), MessageButtons.YesNo, (result) =>
            {
                if (result == MessageResult.Yes)
                {
                    this.Exit();
                }
            });
        }

        void Window_ClientSizeChanged(object sender, EventArgs e)
        {
            ScreenW = Window.ClientBounds.Width;
            ScreenH = Window.ClientBounds.Height;
            if (ScreenW == 0 || ScreenH == 0)
            {   // if something went wrong during fast window switching, let's not continue
                return;
            }
            SetSubwindowSizes();
        }

        /// <summary>
        /// Set the visibility of terrain drawing
        /// </summary>
        /// <param name="isVisible">Normal terrain textures are visible</param>
        /// <param name="isVisibleDM">Distant mountain terrain textures are visible</param>
        /// <returns>true only if there is terrain that can be drawn</returns>
        public bool SetTerrainVisibility(bool isVisible, bool isVisibleDM)
        {
            if (drawTerrain == null)
            {
                return false;
            }

            drawTerrain.SetTerrainVisibility(isVisible, isVisibleDM, DrawArea);
            return true;
        }

        /// <summary>
        /// Set the visibility of the patch lines between terrain
        /// </summary>
        /// <param name="showPatchLines">The value to set the visibility to</param>
        /// <returns>true only if there is terrain that can be drawn</returns>
        public bool SetPatchLineVisibility(bool showPatchLines)
        {
            if (drawTerrain == null)
            {
                return false;
            }

            drawTerrain.SetPatchLineVisibility(showPatchLines);
            return true;
        }

        internal void LoadLabels() => drawLabels?.LoadLabels();
        internal void SaveLabels() => drawLabels?.SaveLabels();
        internal void EditMetaData() => PathEditor?.EditMetaData(Window.ClientBounds.Left + 50, Window.ClientBounds.Top + 20);
        internal void ReversePath() => PathEditor?.ReversePath(Window.ClientBounds.Left + 50, Window.ClientBounds.Top + 20);
        internal void SetTerrainReduction() => drawTerrain?.SetTerrainReduction();
        #endregion

        #region Folder and Route methods
        void HandleCommandLineArgs()
        {
            if (this.commandLineArgs.Length == 0) return;
            string givenPathOrFile = this.commandLineArgs[0];
            this.commandLineArgs = new string[0]; // discard the arguments, no longer needed

            // given_path_or_file should be something like
            // * C:\...\MSTS\Routes\USA2                        , for a directory
            // * C:\...\MSTS\Routes\USA2\usa2.trk               , for a .trk file
            // * C:\...\MSTS\Routes\USA2\marias.tdb             , for a .tdb file
            // * C:\...\MSTS\Routes\USA2\marias.rdb             , for a .rdb file
            // * C:\...\MSTS\Routes\USA2\PATHS\longhale.pat     , for a .pat file

            //Let's first see if it exists and whether it is a file or directory
            string routeFolder = givenPathOrFile;
            bool givenFileIsPat = false;
            if (System.IO.Directory.Exists(givenPathOrFile))
            {
                // It is a directory
            }
            else if (System.IO.File.Exists(givenPathOrFile))
            {
                // It is a file
                var extension = System.IO.Path.GetExtension(givenPathOrFile).ToLower();
                switch (extension)
                {
                    case ".trk":
                        routeFolder = System.IO.Path.GetDirectoryName(givenPathOrFile);
                        break;
                    case ".tdb":
                        routeFolder = System.IO.Path.GetDirectoryName(givenPathOrFile);
                        break;
                    case ".rdb":
                        routeFolder = System.IO.Path.GetDirectoryName(givenPathOrFile);
                        Properties.Settings.Default.drawRoads = true;
                        menuControl.InitUserSettings();
                        break;
                    case ".pat":
                        routeFolder = System.IO.Directory.GetParent(System.IO.Path.GetDirectoryName(givenPathOrFile).ToString()).ToString();
                        givenFileIsPat = true;
                        break;

                    default:
                        Dialogs.ShowMessage(string.Format(catalog.GetString("Route cannot be loaded.\nExtension {0} is not supported"), extension));
                        return;
                }
            }
            else
            {
                //Obviously, this should only happen when ran on the command line, not when a file is opened using
                Dialogs.ShowMessage(string.Format(catalog.GetString("Route cannot be loaded.\n{0} does not exist"), givenPathOrFile));
                return;
            }

            routeFolder = NormalizeDirectory(routeFolder);
            string installFolder = System.IO.Directory.GetParent(System.IO.Directory.GetParent(routeFolder).ToString()).ToString();
            if (!SetSelectedInstallFolder(installFolder)) {
                Dialogs.ShowMessage(string.Format(catalog.GetString("Route cannot be loaded.\nWhile trying to open {0} the folder {1} was inferred as (MSTS or similar) install folder but does not contain expected files"), givenPathOrFile, installFolder));
                return;
            }

            foreach (ORTS.Menu.Route route in this.Routes)
            {
                //MessageBox.Show(route.Path);

                // Comparison is case-insensitive, as on Windows (MSTS content uses arbitrary casing)
                if (String.Equals(NormalizeDirectory(route.Path), routeFolder, StringComparison.OrdinalIgnoreCase))
                {
                    SetRoute(route);

                    if (!givenFileIsPat) { return; }
                    string givenFullPath = System.IO.Path.GetFullPath(givenPathOrFile);
                    foreach (Path availablePath in Paths)
                    {
                        if (String.Equals(System.IO.Path.GetFullPath(availablePath.FilePath), givenFullPath, StringComparison.OrdinalIgnoreCase))
                        {
                            SetPath(availablePath);
                            menuControl.InitUserSettings();
                            return;
                        }
                    }
                }
            }

            Dialogs.ShowMessage(string.Format(catalog.GetString("Route cannot be loaded.\n{0} somehow could not be translated into a loadable route"), givenPathOrFile));
        }

        /// <summary>
        /// Normalize a directory name: full path without trailing directory separator
        /// </summary>
        private static string NormalizeDirectory(string directory)
        {
            return System.IO.Path.GetFullPath(directory).TrimEnd(System.IO.Path.DirectorySeparatorChar, System.IO.Path.AltDirectorySeparatorChar);
        }

        /// <summary>
        /// Return the last part (directory name) of a route path
        /// </summary>
        private static string RouteDirectoryName(string routePath)
        {
            return System.IO.Path.GetFileName(routePath.TrimEnd('\\', '/'));
        }

        /// <summary>
        /// Open up a dialog so the user can select the install directory
        /// (which should contain a sub-directory called ROUTES).
        /// </summary>
        /// <param name="onNewFolder">Called (on the game thread) when indeed a new folder has been selected</param>
        public void SelectInstallFolder(Action onNewFolder)
        {
            IfCanDiscardModifiedPath(() =>
            {
                Dialogs.PickFolder(catalog.GetString("Select install folder"), InstallFolder?.Path, (folderPath) =>
                {
                    if (String.IsNullOrEmpty(folderPath))
                    {
                        return;
                    }

                    if (SetSelectedInstallFolder(folderPath))
                    {
                        onNewFolder?.Invoke();
                    }
                });
            });
        }

        private bool SetSelectedInstallFolder(string folderPath)
        {
            drawTerrain?.Clear();
            Folder newInstallFolder = new Folder("installFolder", folderPath);
            bool foundroutes = FindRoutes(newInstallFolder);
            if (!foundroutes)
            {
                Dialogs.ShowMessage(folderPath + ": " + catalog.GetString("Directory is not a valid install directory.\nThe install directory needs to contain ROUTES, GLOBAL, ..."));
                return false;
            }

            InstallFolder = newInstallFolder;

            // make sure the current route is disabled,
            CurrentRoute = null;
            DrawTrackDB = null;
            PathEditor = null;
            DrawMultiplePaths = null;

            Properties.Settings.Default.installDirectory = folderPath;
            SaveSettings();

            return true;
        }

        /// <summary>
        /// Find the available routes, and if possible load the first one.
        /// </summary>
        /// <returns>True if the route loading was successfull</returns>
        private bool FindRoutes(Folder newInstallFolder)
        {
            if (newInstallFolder == null) return false;
            List<Route> newRoutes = Route.GetRoutes(newInstallFolder).OrderBy(r => r.ToString()).ToList();

            if (newRoutes.Count > 0)
            {
                // set default route
                DefaultRoute = newRoutes[0];
                foreach (Route tryRoute in newRoutes)
                {
                    string dirName = RouteDirectoryName(tryRoute.Path);
                    if (dirName == Properties.Settings.Default.defaultRoute)
                    {
                        DefaultRoute = tryRoute;
                    }
                }

                Routes = new Collection<Route>(newRoutes);
                menuControl.PopulateRoutes();
                return true;
            }
            else
            {
                return false;
            }
        }

        /// <summary>
        /// Load the default route. This would be either the route used last time, the current route, or else the first available route.
        /// </summary>
        /// <param name="afterLoading">Optional action to perform after the route has been loaded</param>
        public void ReloadRoute(Action afterLoading = null)
        {
            if (CurrentRoute != null)
            {
                SetRoute(CurrentRoute, afterLoading);
            }
            else
            {
                SetRoute(DefaultRoute, afterLoading);
            }
        }

        /// <summary>
        /// Set and load a new route
        /// </summary>
        /// <param name="newRoute">The route to load, containing amongst other the directory name of the route</param>
        /// <param name="afterLoading">Optional action to perform after the route has been loaded</param>
        public void SetRoute(Route newRoute, Action afterLoading = null)
        {
            if (newRoute == null) return;
            IfCanDiscardModifiedPath(() =>
            {
                LoadRoute(newRoute);
                afterLoading?.Invoke();
            });
        }

        private void LoadRoute(Route newRoute)
        {
            DrawLoadingMessage(catalog.GetString("Loading route..."));
            MessageDelegate messageHandler = new MessageDelegate(DrawLoadingMessage);

            drawTerrain?.Clear();

            try
            {
                RouteData = new RouteData(newRoute.Path, messageHandler);
                DrawTrackDB = new DrawTrackDB(this.RouteData, messageHandler);
                drawLabels = new DrawLabels(fontManager.DefaultFont.Height);
                CurrentRoute = newRoute;

                Properties.Settings.Default.defaultRoute = RouteDirectoryName(CurrentRoute.Path);
                if (Properties.Settings.Default.zoomRoutePath != CurrentRoute.Path)
                {
                    Properties.Settings.Default.zoomScale = -1; // To disable the use of zoom reset
                }
                SaveSettings();
                DrawArea.ZoomReset(DrawTrackDB);
                drawAreaInset.ZoomReset(DrawTrackDB);
                SetTitle();

            }
            catch (Exception error)
            {
                Console.Error.WriteLine("TrackViewer: route cannot be loaded: " + error);
                Dialogs.ShowMessage(catalog.GetString("Route cannot be loaded. Sorry") + "\n\n" + error.Message);
            }

            if (CurrentRoute == null) return;

            PathEditor = null;
            DrawMultiplePaths = null;
            try
            {
                FindPaths();
            }
            catch { }

            try
            {
                drawWorldTiles.SetRoute(CurrentRoute.Path);
                drawTerrain = new DrawTerrain(CurrentRoute.Path, messageHandler, drawWorldTiles);
                drawTerrain.LoadContent(GraphicsDevice);
                menuControl.MenuSetShowTerrain(false);
                menuControl.MenuSetShowDMTerrain(false);

            }
            catch { }


            menuControl.PopulatePlatforms();
            menuControl.PopulateStations();
            menuControl.PopulateSidings();
            menuControl.PopulateActivitiesMenu();
        }

        /// <summary>
        /// Set the title of the window itself
        /// </summary>
        void SetTitle()
        {
            Assembly assembly = Assembly.GetExecutingAssembly();
            AssemblyTitleAttribute assemblyTitle = assembly.GetCustomAttributes(typeof(AssemblyTitleAttribute), false)[0] as AssemblyTitleAttribute;
            Window.Title = assemblyTitle.Title;
            if ((RouteData != null) && (RouteData.RouteName != null))
            {
                Window.Title += ": " + RouteData.RouteName;
            }
            if (!String.IsNullOrEmpty(Properties.Settings.Default.CurrentActivityName))
            { 
                Window.Title += "  Activity: " + Properties.Settings.Default.CurrentActivityName;
            }
        }

        #endregion

        #region Path methods
        /// <summary>
        /// Find the paths (.pat files) belonging to the current route, and update the menu
        /// </summary>
        private void FindPaths()
        {
            List<Path> newPaths = Path.GetPaths(CurrentRoute, true).OrderBy(r => r.Name).ToList();
            Paths = new Collection<Path>(newPaths);
            menuControl.PopulatePaths();
            SetPath(null);
            DrawMultiplePaths = new DrawMultiplePaths(this.RouteData, Paths);
        }

        /// <summary>
        /// Once a path has been selected, do the necessary loading.
        /// </summary>
        /// <param name="path">Path (with FilePath) that has to be loaded</param>
        /// <param name="afterLoading">Optional action to perform after the path has been loaded</param>
        internal void SetPath(Path path, Action afterLoading = null)
        {
            IfCanDiscardModifiedPath(() =>
            {
                LoadPath(path);
                afterLoading?.Invoke();
            });
        }

        private void LoadPath(Path path)
        {
            if (path == null)
            {
                DrawPATfile = null;
                PathEditor = null;
                drawPathChart.Close();
            }
            else
            {
                DrawLoadingMessage(catalog.GetString("Loading .pat file ..."));
                DrawPATfile = new DrawPATfile(path);

                DrawLoadingMessage(catalog.GetString("Processing .pat file ..."));
                PathEditor = new PathEditor(this.RouteData, this.DrawTrackDB, path);
                drawPathChart.SetPathEditor(this.RouteData, this.PathEditor);

                DrawLoadingMessage(" ...");
            }
        }

        internal void NewPath(Action afterCreation = null)
        {
            IfCanDiscardModifiedPath(() =>
            {
                string pathsDirectory = System.IO.Path.Combine(CurrentRoute.Path, "PATHS");
                PathEditor = new PathEditor(this.RouteData, this.DrawTrackDB, pathsDirectory);
                drawPathChart.SetPathEditor(this.RouteData, this.PathEditor);
                DrawPATfile = null;
                menuControl.SetEnableEditing();
                afterCreation?.Invoke();
                EditMetaData();
            });
        }

        /// <summary>
        /// If the path has been modified, ask the user if he really wants to discard it. Only if there is no modified path, or
        /// the user agrees to discard it, the action is performed (immediately if there was no question, otherwise later on the game thread).
        /// </summary>
        /// <param name="action">The action to perform when the path can be discarded</param>
        void IfCanDiscardModifiedPath(Action action)
        {
            if (PathEditor == null || !PathEditor.HasModifiedPath)
            {
                action();
                return;
            }
            Dialogs.Ask(
                        catalog.GetString("Path has been modified. Loading a new path will discard changes.") + "\n" +
                        catalog.GetString("Do you want to continue?"),
                        catalog.GetString("Trackviewer Path Editor"), MessageButtons.OKCancel,
                        (result) =>
                        {
                            if (result == MessageResult.OK)
                            {
                                action();
                            }
                        });
        }

        #endregion

        #region Centering methods
        /// <summary>
        /// Find a track node, center around it and highlight it
        /// </summary>
        /// <param name="trackNumberIndex">Index of the track node</param>
        public void CenterAroundTrackNode(int trackNumberIndex)
        {
            CenterAround(DrawTrackDB.TrackNodeHighlightOverride(trackNumberIndex));
        }

        /// <summary>
        /// Find a Road track node, center around it and highlight it
        /// </summary>
        /// <param name="trackNumberIndex">Index of the track node</param>
        public void CenterAroundTrackNodeRoad(int trackNumberIndex)
        {
            CenterAround(DrawTrackDB.TrackNodeHighlightOverrideRoad(trackNumberIndex));
        }

        /// <summary>
        /// Find a trackItem and center around it and highlight it
        /// </summary>
        /// <param name="trackItemIndex">Index of the track item</param>
        public void CenterAroundTrackItem(int trackItemIndex)
        {
            WorldLocation itemLocation = DrawTrackDB.TrackItemHighlightOverride(trackItemIndex);
            if (itemLocation == WorldLocation.None) return;
            CenterAround(itemLocation);
        }

        /// <summary>
        /// Find a road trackItem and center around it and highlight it
        /// </summary>
        /// <param name="trackItemIndex">Index of the track item</param>
        public void CenterAroundTrackItemRoad(int trackItemIndex)
        {
            WorldLocation itemLocation = DrawTrackDB.TrackItemHighlightOverrideRoad(trackItemIndex);
            if (itemLocation == WorldLocation.None) return;
            CenterAround(itemLocation);
        }

        /// <summary>
        /// Center around a certain world-location. In particular, outside the normal Draw/Update loop. So it does a draw itself
        /// To be used from additional windows (like search).
        /// </summary>
        /// <param name="centerLocation">Location to center the view window around</param>
        public void CenterAround(WorldLocation centerLocation)
        {
            if (centerLocation == WorldLocation.None) return;

            DrawArea.ShiftToLocation(centerLocation);
            DrawArea.Update();
            DrawArea.MouseLocation = centerLocation;
            drawAreaInset.Follow(DrawArea, 10f);
            BeginDraw();
            skipDrawAmount = 0; // make sure the draw is really done.
            Draw(new GameTime());
            EndDraw();

        }

        #endregion

        #region RestoreBrokenPaths
        /// <summary>
        /// Attempt to Auto Restore all broken paths
        /// </summary>
        public void AutoRestorePaths()
        {
            if (CurrentRoute == null || Paths == null) return;
            Dialogs.Ask(
                        catalog.GetString("This will open every single .pat file for this route, (try to) fix all broken nodes, and save the modified path. ") +
                        catalog.GetString("Potentially it will therefore change all .pat files on disc.") + "\n" +
                        catalog.GetString("This can be useful when a route has been changed and you want all paths to be corrected.") + "\n\n" +
                        catalog.GetString("Do you want to continue?"),
                        catalog.GetString("Trackviewer Path Editor"), MessageButtons.OKCancel,
                        (dialogResult) =>
                        {
                            if (dialogResult != MessageResult.OK) { return; }

                            //Close all paths that are drawn.
                            IfCanDiscardModifiedPath(() =>
                            {
                                PathEditor = null;
                                DrawMultiplePaths?.ClearAll();

                                AutoFixAllPaths.FixallAndShowResults(this.RouteData, this.DrawTrackDB, Paths, (message) => DrawLoadingMessage(message));
                            });
                        });
        }
        #endregion

        #region Debug methods
        void RunDebug()
        {
            //Properties.Settings.Default.statusShowFPS = true;
            //ReloadRoute();
            //SetPath(Paths[21]);
            //drawPathChart.Open();
            //NewPath();
            //menuControl.SetEnableEditing(true);
            //DrawArea.ZoomToTile();
            //DrawArea.Zoom(-18);
            //CenterAroundTrackNode(30);
            //ReversePath();

        }

        static void InitLogging()
        {   // debug only
            //string logFileName = "C:\\tvlog.txt";
            //System.IO.File.Delete(logFileName);
            //Console.SetOut(new FileTeeLogger(logFileName, Console.Out));
            //// Make Console.Error go to the new Console.Out.
            //Console.SetError(Console.Out);
            //Console.WriteLine("started logging");
        }

        void CalculateFPS(GameTime gameTime)
        {
            float elapsedTime = (float)gameTime.ElapsedGameTime.TotalSeconds;
            FrameRate.Update(elapsedTime, 1f / elapsedTime);
        }

        #endregion

        #region Language and localization
        /// <summary>
        /// This is the 'catalog' needed for localization of TrackViewer (meaning translating it to different languages)
        /// </summary>
        public static GettextResourceManager catalog = new GettextResourceManager("Contrib");

        /// <summary>
        /// Routine to localize (make languague-dependent) an Avalonia element, like a window. Must be called on the Avalonia UI thread.
        /// </summary>
        /// <param name="element">The element that is checked for localizable parameters</param>
        public static void Localize(Avalonia.StyledElement element)
        {
            foreach (var child in ((Avalonia.LogicalTree.ILogical)element).LogicalChildren)
            {
                if (child is Avalonia.StyledElement childAsElement)
                {
                    Localize(childAsElement);
                }
            }

            var objType = element.GetType();
            PropertyInfo property;
            string[] propertyTags = { "Content", "Header", "Text", "Title" };

            foreach (var tag in propertyTags)
            {
                property = objType.GetProperty(tag);
                if (property != null && property.CanRead && property.CanWrite && property.GetValue(element, null) is String)
                    property.SetValue(element, catalog.GetString(property.GetValue(element, null) as string), null);
            }

            if (element is Avalonia.Controls.Control control && Avalonia.Controls.ToolTip.GetTip(control) is string tip)
            {
                Avalonia.Controls.ToolTip.SetTip(control, catalog.GetString(tip));
            }
        }

        /// <summary>
        /// Save the user settings, without crashing when that is not possible.
        /// </summary>
        public static void SaveSettings()
        {
            try
            {
                Properties.Settings.Default.Save();
            }
            catch (Exception error)
            {
                Console.Error.WriteLine("TrackViewer: settings could not be saved: " + error.Message);
            }
        }

        /// <summary>
        /// Change/select the language. Store the preference to be used after a restart
        /// Give message to use that TrackViewer needs to be restarted
        /// </summary>
        /// <param name="languageCode">Two-letter code for the new language</param>
        public void SelectLanguage(string languageCode)
        {
            LanguageManager.SelectLanguage(languageCode);
            //The lines below work from system/english to another language, but not from another language to system/english
            //languageManager.LoadLanguage();
            //Localize(menuControl);
            //Localize(statusBarControl);
        }
        #endregion
    }
}
