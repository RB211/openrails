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
// The main menu of TrackViewer. Originally a WPF menu docked into the WinForms game window; it is now drawn by
// MonoGame inside the game window (see GameMenu), so that it stays where users expect it and all menu actions run
// on the game thread. The structure and the names of the items follow the original MenuControl.xaml.
//
using System;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.Diagnostics;
using System.Linq;
using System.Text;

using ORTS.TrackViewer.Drawing; // for colors
namespace ORTS.TrackViewer.UserInterface
{
    /// <summary>
    /// The main menu.
    /// Many parts of the menu are checkable items. These get initialized from stored property settings.
    /// When updated (by the user) also the stored property settings are updated.
    ///
    /// A few menu items contain lists (e.g. of routes, platforms, sidings, paths). The list of menuitems
    /// are created by dedicated methods that receive the list of items from the rest of the program.
    ///
    /// All methods must be called on the game thread.
    /// </summary>
    public sealed class MenuControl : IPreferenceChanger
    {
        /// <summary>Height of the menu in pixels</summary>
        public int MenuHeight => Menu.BarHeight;

        /// <summary>The menu bar and its popups</summary>
        public GameMenu Menu { get; } = new GameMenu();

        private readonly TrackViewer trackViewer;
        private readonly SaveableSettingsDictionary settingsDictionary = new SaveableSettingsDictionary();
        private OtherPathsWindow otherPathsWindow;

        #region Menu items
#pragma warning disable 1591
        public GameMenuItem menuFile, menuView, menuTrackItems, menuStatusbar, menuPreferences, menuPathEditor, menuTerrain, menuHelp;

        GameMenuItem menuSelectRoute, menuSelectActivity;
        GameMenuItem menuStationList, menuPlatformList, menuSidingList;
        GameMenuItem menuShowTrainpath, menuShowPATfile, menuColorTracks, menuHighlightTracks;
        GameMenuItem menuShowInset, menuShowWorldTiles, menuShowGridLines, menuShowScaleRuler, menuShowLonLat;
        GameMenuItem menuShowLabels;
        GameMenuItem menuDoAntiAliasing;
        GameMenuItem menuHighlightItems, menuShowJunctionNodes, menuShowEndNodes, menuShowCrossovers;
        GameMenuItem menuShowSidingMarkers, menuShowSidingNames, menuShowPlatformMarkers, menuShowPlatformNames, menuShowStationNames;
        GameMenuItem menuShowSignals, menuShowAllSignals, menuShowCrossings, menuShowHazards, menuShowPickups, menuShowSoundRegions, menuShowEvents, menuShowEventNames;
        GameMenuItem menuShowSpeedLimits, menuShowMileposts, menuDrawRoads, menuShowCarSpawners, menuShowRoadCrossings;
        GameMenuItem menuStatusShowVectorSection, menuStatusShowPATfile, menuStatusShowTrainpath, menuStatusShowTerrain, menuStatusShowSignal, menuStatusShowNames, menuStatusShowEventNames;
        GameMenuItem menuZoomIsCenteredOnMouse, menuUseMilesNotMeters, menuLanguage;
        GameMenuItem menuHighlightLastPathSection, menuEnableEditing, menuShowCurrentEditorAction, menuPgupExtendsPath;
        GameMenuItem menuSelectPath, menuShowOtherPaths, menuSavePath, menuSaveStations, menuShowChart, menuNewPath;
        GameMenuItem menuExtendPath, menuEditMetadata, menuReversePath;
        GameMenuItem menuHighlightLastPathSection2, menuEnableEditing2, menuShowCurrentEditorAction2, menuPgupExtendsPath2;
        GameMenuItem menuAutoFixAllNodes, menuAutoFixAllPaths;
        GameMenuItem menuShowTerrain, menuShowDMTerrain, menuShowPatchLines;
        GameMenuItem reductionNone, reductionAuto, reduction2, reduction4, reduction8, reduction16;
#pragma warning restore 1591
        #endregion

        private static string L(string text) => TrackViewer.catalog.GetString(text);

        /// <summary>
        /// constructor
        /// </summary>
        /// <param name="trackViewer">The trackviewer object we need for callbacks</param>
        public MenuControl(TrackViewer trackViewer)
        {
            this.trackViewer = trackViewer;
            BuildMenu();
            InitUserSettings();
        }

        private GameMenuItem Check(GameMenuItem parent, string header, Action<GameMenuItem> click = null)
        {
            return parent.Add(GameMenuItem.Checkable(L(header), click ?? (_ => UpdateMenuSettings())));
        }

        private GameMenuItem Item(GameMenuItem parent, string header, Action<GameMenuItem> click = null)
        {
            return parent.Add(new GameMenuItem(L(header), click));
        }

        private void BuildMenu()
        {
            // File
            menuFile = new GameMenuItem(L("File"));
            Item(menuFile, "(Re)load route (ctrl-R)", _ => MenuReloadRoute_Click());
            menuFile.Add(GameMenuItem.Separator());
            Item(menuFile, "Select install folder", _ => MenuInstallFolder_Click());
            menuSelectRoute = Item(menuFile, "Select route");
            menuSelectActivity = Item(menuFile, "Select activity");
            menuFile.Add(GameMenuItem.Separator());
            var search = Item(menuFile, "Search by index");
            Item(search, "Search trackNode", _ => OpenSearch(SearchableItem.TrackNode));
            Item(search, "Search trackNode for roads", _ => OpenSearch(SearchableItem.TrackNodeRoad));
            Item(search, "Search trackItem", _ => OpenSearch(SearchableItem.TrackItem));
            Item(search, "Search trackItem for roads", _ => OpenSearch(SearchableItem.TrackItemRoad));
            menuFile.Add(GameMenuItem.Separator());
            Item(menuFile, "Quit (Q)", _ => trackViewer.Quit());

            // View
            menuView = new GameMenuItem(L("View"));
            menuStationList = Item(menuView, "Center around station");
            menuPlatformList = Item(menuView, "Center around platform");
            menuSidingList = Item(menuView, "Center around siding");
            menuView.Add(GameMenuItem.Separator());
            var paths = Item(menuView, "Paths");
            menuShowTrainpath = Check(paths, "Show path (F11)");
            menuShowPATfile = Check(paths, "Show raw path from .pat file (shift-F11)");
            var coloring = Item(menuView, "Track Coloring");
            menuColorTracks = Check(coloring, "Color tracks");
            menuHighlightTracks = Check(coloring, "Highlight tracks (F10)");
            var additional = Item(menuView, "Additional views");
            menuShowInset = Check(additional, "Show inset");
            menuShowWorldTiles = Check(additional, "Show world tiles");
            menuShowGridLines = Check(additional, "Show grid lines");
            menuShowScaleRuler = Check(additional, "Show scale ruler");
            menuShowLonLat = Check(additional, "Show longitude/latitude");
            var labels = Item(menuView, "Labels");
            menuShowLabels = Check(labels, "Show labels");
            menuShowLabels.ToolTip = L("For adding a label, press L");
            Item(labels, "Load labels", _ => trackViewer.LoadLabels());
            Item(labels, "Save labels", _ => trackViewer.SaveLabels());
            menuView.Add(GameMenuItem.Separator());
            Item(menuView, "Save current zoom", _ => { if (trackViewer.CurrentRoute != null) trackViewer.DrawArea.Save(trackViewer.CurrentRoute.Path); });
            Item(menuView, "Restore saved zoom", _ => trackViewer.DrawArea.Restore());
            menuView.Add(GameMenuItem.Separator());
            Item(menuView, "Zoom in (=)", _ => trackViewer.DrawArea.ZoomCentered(-4));
            Item(menuView, "Zoom out (-)", _ => trackViewer.DrawArea.ZoomCentered(4));
            Item(menuView, "Zoom to tile (Z)", _ => trackViewer.DrawArea.ZoomToTileCentered());
            Item(menuView, "Zoom reset (R)", _ => trackViewer.DrawArea.ZoomReset(trackViewer.DrawTrackDB));
            menuView.Add(GameMenuItem.Separator());
            menuDoAntiAliasing = Check(menuView, "Anti aliasing (needs restart)", _ => MenuDoAntiAliasing_Click());

            // Track items
            menuTrackItems = new GameMenuItem(L("Track items"));
            Item(menuTrackItems, "Show all items", _ => MenuSetAllItems(true));
            Item(menuTrackItems, "Show no items", _ => MenuSetAllItems(false));
            menuHighlightItems = Check(menuTrackItems, "Highlight items (shift-F10)");
            var junctions = Item(menuTrackItems, "Junctions/ends");
            menuShowJunctionNodes = Check(junctions, "Show junctions");
            menuShowEndNodes = Check(junctions, "Show end nodes");
            menuShowCrossovers = Check(junctions, "Show crossovers");
            var sidings = Item(menuTrackItems, "Sidings");
            menuShowSidingMarkers = Check(sidings, "Show siding markers (F9)");
            menuShowSidingNames = Check(sidings, "Show siding names (shift-F9)");
            var platforms = Item(menuTrackItems, "Platforms");
            menuShowPlatformMarkers = Check(platforms, "Show platform markers (F8)");
            menuShowPlatformNames = Check(platforms, "Show platform names (shift-F8)");
            menuShowStationNames = Check(platforms, "Show station names (shift-F8)");
            var interactives = Item(menuTrackItems, "Interactives");
            menuShowSignals = Check(interactives, "Show signals (F7)");
            menuShowAllSignals = Check(interactives, "Show also other signals", _ => MenuShowAllSignals_Click());
            menuShowCrossings = Check(interactives, "Show level crossings");
            menuShowHazards = Check(interactives, "Show hazards");
            menuShowPickups = Check(interactives, "Show fuel/pickups");
            menuShowSoundRegions = Check(interactives, "Show sound regions");
            menuShowEvents = Check(interactives, "Show events");
            menuShowEventNames = Check(interactives, "Show event names");
            var posts = Item(menuTrackItems, "Posts");
            menuShowSpeedLimits = Check(posts, "Show speed limits (F5)");
            menuShowMileposts = Check(posts, "Show mileposts (shift-F5)");
            var roads = Item(menuTrackItems, "Roads");
            menuDrawRoads = Check(roads, "Draw roads");
            menuShowCarSpawners = Check(roads, "Show car spawners");
            menuShowRoadCrossings = Check(roads, "Show road crossings");

            // Statusbar
            menuStatusbar = new GameMenuItem(L("Statusbar"));
            menuStatusShowVectorSection = Check(menuStatusbar, "Show vector section info");
            menuStatusShowPATfile = Check(menuStatusbar, "Show PAT file info");
            menuStatusShowTrainpath = Check(menuStatusbar, "Show Path info");
            menuStatusShowTerrain = Check(menuStatusbar, "Show terrain info");
            menuStatusShowSignal = Check(menuStatusbar, "Show signal info");
            menuStatusShowNames = Check(menuStatusbar, "Show platform/station names");
            menuStatusShowEventNames = Check(menuStatusbar, "Show event names");

            // Preferences
            menuPreferences = new GameMenuItem(L("Preferences"));
            menuZoomIsCenteredOnMouse = Check(menuPreferences, "Zooming is centered on mouse (M)");
            menuUseMilesNotMeters = Check(menuPreferences, "Use miles i.s.o. meters");
            menuLanguage = Item(menuPreferences, "Select language");
            menuPreferences.Add(GameMenuItem.Separator());
            menuHighlightLastPathSection = Check(menuPreferences, "Highlight last-drawn section", _ => { menuHighlightLastPathSection2.IsChecked = menuHighlightLastPathSection.IsChecked; UpdateMenuSettings(); });
            menuEnableEditing = Check(menuPreferences, "Enable editing", _ => MenuEnableEditing_Click());
            menuShowCurrentEditorAction = Check(menuPreferences, "Show current mouse action", _ => { menuShowCurrentEditorAction2.IsChecked = menuShowCurrentEditorAction.IsChecked; UpdateMenuSettings(); });
            menuPgupExtendsPath = Check(menuPreferences, "Shift-PgUp also extends path", _ => { menuPgupExtendsPath2.IsChecked = menuPgupExtendsPath.IsChecked; UpdateMenuSettings(); });
            menuPreferences.Add(GameMenuItem.Separator());

            // Path editor
            menuPathEditor = new GameMenuItem(L("Path-Editor"));
            menuPathEditor.Add(new GameMenuItem(L("==Supporting MSTS paths only==")) { IsEnabled = false });
            menuSelectPath = Item(menuPathEditor, "Load path");
            menuShowOtherPaths = Item(menuPathEditor, "Show other paths", _ => MenuShowOtherPaths_Click());
            menuPathEditor.Add(GameMenuItem.Separator());
            menuSavePath = Item(menuPathEditor, "Save path", _ => trackViewer.PathEditor?.SavePath());
            menuSaveStations = Item(menuPathEditor, "Save visited stations", _ => trackViewer.PathEditor?.SaveStationNames());
            menuShowChart = Item(menuPathEditor, "Show chart", _ => trackViewer.ShowPathChart());
            menuPathEditor.Add(GameMenuItem.Separator());
            menuNewPath = Item(menuPathEditor, "New path", _ => MenuNewPath_Click());
            menuPathEditor.Add(GameMenuItem.Separator());
            menuExtendPath = Item(menuPathEditor, "Extend with other path");
            menuEditMetadata = Item(menuPathEditor, "Edit path metadata", _ => trackViewer.EditMetaData());
            menuReversePath = Item(menuPathEditor, "Reverse path", _ => { trackViewer.ReversePath(); UpdateMenuSettings(); });
            menuPathEditor.Add(GameMenuItem.Separator());
            menuHighlightLastPathSection2 = Check(menuPathEditor, "Highlight last-drawn section", _ => { menuHighlightLastPathSection.IsChecked = menuHighlightLastPathSection2.IsChecked; UpdateMenuSettings(); });
            menuEnableEditing2 = Check(menuPathEditor, "Enable editing", _ => { menuEnableEditing.IsChecked = menuEnableEditing2.IsChecked; MenuEnableEditing_Click(); });
            menuShowCurrentEditorAction2 = Check(menuPathEditor, "Show current mouse action", _ => { menuShowCurrentEditorAction.IsChecked = menuShowCurrentEditorAction2.IsChecked; UpdateMenuSettings(); });
            menuPgupExtendsPath2 = Check(menuPathEditor, "Shift-PgUp also extends path", _ => { menuPgupExtendsPath.IsChecked = menuPgupExtendsPath2.IsChecked; UpdateMenuSettings(); });
            menuPathEditor.Add(GameMenuItem.Separator());
            menuAutoFixAllNodes = Item(menuPathEditor, "Auto-fix all broken nodes", _ => trackViewer.PathEditor?.AutoFixAllBrokenNodes());
            menuAutoFixAllPaths = Item(menuPathEditor, "Auto-fix all broken paths", _ => trackViewer.AutoRestorePaths());

            // Terrain
            menuTerrain = new GameMenuItem(L("Terrain"));
            menuShowTerrain = Check(menuTerrain, "Show terrain textures (F6)", _ => MenuShowTerrain_Click());
            menuShowDMTerrain = Check(menuTerrain, "Show DM terrain textures (ctrl-F6)", _ => MenuShowTerrain_Click());
            menuShowPatchLines = Check(menuTerrain, "Show patch lines (shift-F6)", _ => MenuShowPatchLines_Click());
            menuTerrain.Add(GameMenuItem.Separator());
            var memory = Item(menuTerrain, "Memory reduction options");
            reductionNone = Reduction(memory, "No reduction", 1);
            reductionAuto = Reduction(memory, "Automatic reduction", 0);
            reduction2 = Reduction(memory, "Reduce by linear factor 2", 2);
            reduction4 = Reduction(memory, "Reduce by linear factor 4", 4);
            reduction8 = Reduction(memory, "Reduce by linear factor 8", 8);
            reduction16 = Reduction(memory, "Reduce by linear factor 16", 16);

            // Help
            menuHelp = new GameMenuItem(L("Help"));
            Item(menuHelp, "Shortcuts", _ => MenuShortcuts_Click());
            Item(menuHelp, "Documentation", _ => MenuDocumentation_Click());
            Item(menuHelp, "About", _ => MenuAbout_Click());

            Menu.BarItems.AddRange(new[] { menuFile, menuView, menuTrackItems, menuStatusbar, menuPreferences, menuPathEditor, menuTerrain, menuHelp });
        }

        private GameMenuItem Reduction(GameMenuItem parent, string header, int factor)
        {
            var item = parent.Add(new GameMenuItem(L(header), TerrainReductionOption_CheckChanged) { RadioGroup = "reductionOptions", Tag = factor });
            return item;
        }

        /// <summary>
        /// set the size of the window (also after rescaling)
        /// </summary>
        public void SetScreenSize(int width, int height)
        {
            Menu.SetScreenSize(width, height);
        }

        /// <summary>Is the menu (one of its popups) open</summary>
        public bool IsOpen => Menu.IsOpen;

        /// <summary>
        /// Init various settings from the stored user settings.
        /// </summary>
        public void InitUserSettings()
        {
            menuShowInset.IsChecked = Properties.Settings.Default.showInset;
            menuShowWorldTiles.IsChecked = Properties.Settings.Default.showWorldTiles;
            menuShowGridLines.IsChecked = Properties.Settings.Default.showGridLines;
            menuColorTracks.IsChecked = Properties.Settings.Default.colorTracks;
            menuHighlightTracks.IsChecked = Properties.Settings.Default.showTrackHighlights;
            menuHighlightItems.IsChecked = Properties.Settings.Default.showItemHighlights;

            menuShowJunctionNodes.IsChecked = Properties.Settings.Default.showJunctionNodes;
            menuShowEndNodes.IsChecked = Properties.Settings.Default.showEndNodes;
            menuShowCrossovers.IsChecked = Properties.Settings.Default.showCrossovers;
            menuShowSidingMarkers.IsChecked = Properties.Settings.Default.showSidingMarkers;
            menuShowSidingNames.IsChecked = Properties.Settings.Default.showSidingNames;
            menuShowPlatformMarkers.IsChecked = Properties.Settings.Default.showPlatformMarkers;
            menuShowPlatformNames.IsChecked = Properties.Settings.Default.showPlatformNames;
            menuShowStationNames.IsChecked = Properties.Settings.Default.showStationNames;
            menuShowCrossings.IsChecked = Properties.Settings.Default.showCrossings;
            menuShowSpeedLimits.IsChecked = Properties.Settings.Default.showSpeedLimits;
            menuShowMileposts.IsChecked = Properties.Settings.Default.showMileposts;
            menuShowHazards.IsChecked = Properties.Settings.Default.showHazards;
            menuShowSignals.IsChecked = Properties.Settings.Default.showSignals;
            menuShowAllSignals.IsChecked = Properties.Settings.Default.showAllSignals;
            menuShowPickups.IsChecked = Properties.Settings.Default.showPickups;
            menuShowSoundRegions.IsChecked = Properties.Settings.Default.showSoundRegions;
            menuShowEvents.IsChecked = Properties.Settings.Default.showEvents;
            menuShowEventNames.IsChecked = Properties.Settings.Default.showEventNames;

            menuShowPATfile.IsChecked = Properties.Settings.Default.showPATfile;
            menuShowTrainpath.IsChecked = Properties.Settings.Default.showTrainpath;
            menuHighlightLastPathSection.IsChecked = Properties.Settings.Default.highlightLastPathSection;
            menuHighlightLastPathSection2.IsChecked = Properties.Settings.Default.highlightLastPathSection;
            menuShowCurrentEditorAction.IsChecked = Properties.Settings.Default.showEditorAction;
            menuShowCurrentEditorAction2.IsChecked = Properties.Settings.Default.showEditorAction;
            menuPgupExtendsPath.IsChecked = Properties.Settings.Default.pgupExtendsPath;
            menuPgupExtendsPath2.IsChecked = Properties.Settings.Default.pgupExtendsPath;

            menuStatusShowVectorSection.IsChecked = Properties.Settings.Default.statusShowVectorSections;
            menuStatusShowPATfile.IsChecked = Properties.Settings.Default.statusShowPATfile;
            menuStatusShowTrainpath.IsChecked = Properties.Settings.Default.statusShowTrainpath;
            menuStatusShowTerrain.IsChecked = Properties.Settings.Default.statusShowTerrain;
            menuStatusShowSignal.IsChecked = Properties.Settings.Default.statusShowSignal;
            menuStatusShowNames.IsChecked = Properties.Settings.Default.statusShowNames;
            menuStatusShowEventNames.IsChecked = Properties.Settings.Default.statusShowEventNames;

            menuDrawRoads.IsChecked = Properties.Settings.Default.drawRoads;
            menuShowCarSpawners.IsChecked = Properties.Settings.Default.showCarSpawners;
            menuShowRoadCrossings.IsChecked = Properties.Settings.Default.showRoadCrossings;

            menuShowScaleRuler.IsChecked = Properties.Settings.Default.showScaleRuler;
            menuShowLonLat.IsChecked = Properties.Settings.Default.showLonLat;
            menuUseMilesNotMeters.IsChecked = Properties.Settings.Default.useMilesNotMeters;

            menuZoomIsCenteredOnMouse.IsChecked = Properties.Settings.Default.zoomIsCenteredOnMouse;

            menuShowLabels.IsChecked = Properties.Settings.Default.showLabels;

            // Terrain should be off by default. We do not want to burden people with having this load always
            menuShowTerrain.IsChecked = false;
            menuShowDMTerrain.IsChecked = false;
            menuShowPatchLines.IsChecked = false;

            foreach (GameMenuItem reduction in new[] { reductionNone, reductionAuto, reduction2, reduction4, reduction8, reduction16 })
            {
                reduction.IsChecked = ((int)reduction.Tag == Properties.Settings.Default.terrainReductionFactor);
            }

            UpdateMenuSettings();  // to be sure some other settings are done correctly

            menuDoAntiAliasing.IsChecked = Properties.Settings.Default.doAntiAliasing;
        }

        /// <summary>
        /// Update the (user) settings based on menu state
        /// </summary>
        void UpdateMenuSettings()
        {
            menuShowPATfile.IsEnabled = !menuEnableEditing.IsChecked;
            if (!menuShowPATfile.IsEnabled)
            {
                menuShowPATfile.IsChecked = false;
            }
            if (!menuShowSignals.IsChecked)
            {   // if signals are not shown, then also all signals cannot be shown.
                menuShowAllSignals.IsChecked = false;
            }
            if (menuShowStationNames.IsChecked)
            {
                menuShowPlatformNames.IsChecked = false;
            }

            if (menuShowTerrain.IsChecked || menuShowDMTerrain.IsChecked)
            {
                menuShowWorldTiles.IsChecked = false;
                menuShowWorldTiles.IsEnabled = false;
                menuShowPatchLines.IsEnabled = true;
            }
            else
            {
                menuShowWorldTiles.IsEnabled = true;
                menuShowPatchLines.IsEnabled = false;
            }

            Properties.Settings.Default.showInset = menuShowInset.IsChecked;
            Properties.Settings.Default.showWorldTiles = menuShowWorldTiles.IsChecked;
            Properties.Settings.Default.showGridLines = menuShowGridLines.IsChecked;
            Properties.Settings.Default.colorTracks = menuColorTracks.IsChecked;
            Properties.Settings.Default.showTrackHighlights = menuHighlightTracks.IsChecked;
            Properties.Settings.Default.showItemHighlights = menuHighlightItems.IsChecked;

            Properties.Settings.Default.showJunctionNodes = menuShowJunctionNodes.IsChecked;
            Properties.Settings.Default.showEndNodes = menuShowEndNodes.IsChecked;
            Properties.Settings.Default.showCrossovers = menuShowCrossovers.IsChecked;
            Properties.Settings.Default.showSidingMarkers = menuShowSidingMarkers.IsChecked;
            Properties.Settings.Default.showSidingNames = menuShowSidingNames.IsChecked;
            Properties.Settings.Default.showPlatformMarkers = menuShowPlatformMarkers.IsChecked;
            Properties.Settings.Default.showPlatformNames = menuShowPlatformNames.IsChecked;
            Properties.Settings.Default.showStationNames = menuShowStationNames.IsChecked;
            Properties.Settings.Default.showCrossings = menuShowCrossings.IsChecked;
            Properties.Settings.Default.showSpeedLimits = menuShowSpeedLimits.IsChecked;
            Properties.Settings.Default.showMileposts = menuShowMileposts.IsChecked;
            Properties.Settings.Default.showSignals = menuShowSignals.IsChecked;
            Properties.Settings.Default.showAllSignals = menuShowAllSignals.IsChecked;
            Properties.Settings.Default.showHazards = menuShowHazards.IsChecked;
            Properties.Settings.Default.showSoundRegions = menuShowSoundRegions.IsChecked;
            Properties.Settings.Default.showEvents = menuShowEvents.IsChecked;
            Properties.Settings.Default.showEventNames = menuShowEventNames.IsChecked;
            Properties.Settings.Default.showPickups = menuShowPickups.IsChecked;

            Properties.Settings.Default.showPATfile = menuShowPATfile.IsChecked;
            Properties.Settings.Default.showTrainpath = menuShowTrainpath.IsChecked;
            Properties.Settings.Default.highlightLastPathSection = menuHighlightLastPathSection.IsChecked;
            Properties.Settings.Default.showEditorAction = menuShowCurrentEditorAction.IsChecked;
            Properties.Settings.Default.pgupExtendsPath = menuPgupExtendsPath.IsChecked;

            Properties.Settings.Default.statusShowVectorSections = menuStatusShowVectorSection.IsChecked;
            Properties.Settings.Default.statusShowPATfile = menuStatusShowPATfile.IsChecked && menuShowPATfile.IsChecked;
            Properties.Settings.Default.statusShowTrainpath = menuStatusShowTrainpath.IsChecked && menuShowTrainpath.IsChecked;
            Properties.Settings.Default.statusShowTerrain = menuStatusShowTerrain.IsChecked && (menuShowTerrain.IsChecked || menuShowDMTerrain.IsChecked);
            Properties.Settings.Default.statusShowSignal = menuStatusShowSignal.IsChecked && menuShowSignals.IsChecked;
            Properties.Settings.Default.statusShowNames = menuStatusShowNames.IsChecked;
            Properties.Settings.Default.statusShowEventNames = menuStatusShowEventNames.IsChecked;

            Properties.Settings.Default.drawRoads = menuDrawRoads.IsChecked;
            Properties.Settings.Default.showCarSpawners = menuShowCarSpawners.IsChecked;
            Properties.Settings.Default.showRoadCrossings = menuShowRoadCrossings.IsChecked;

            Properties.Settings.Default.showScaleRuler = menuShowScaleRuler.IsChecked;
            Properties.Settings.Default.showLonLat = menuShowLonLat.IsChecked;
            Properties.Settings.Default.useMilesNotMeters = menuUseMilesNotMeters.IsChecked;

            Properties.Settings.Default.showLabels = menuShowLabels.IsChecked;
            Properties.Settings.Default.zoomIsCenteredOnMouse = menuZoomIsCenteredOnMouse.IsChecked;

            TrackViewer.SaveSettings();

            DrawColors.SetColoursFromOptions(menuColorTracks.IsChecked, menuShowWorldTiles.IsChecked, menuShowTerrain.IsChecked || menuShowDMTerrain.IsChecked);

            menuStatusShowPATfile.IsEnabled = menuShowPATfile.IsChecked;
            menuStatusShowTrainpath.IsEnabled = menuShowTrainpath.IsChecked;
            menuStatusShowTerrain.IsEnabled = menuShowTerrain.IsChecked || menuShowDMTerrain.IsChecked;
            menuStatusShowSignal.IsEnabled = menuShowSignals.IsChecked;

            menuSelectPath.IsEnabled = (trackViewer.CurrentRoute != null);
            menuNewPath.IsEnabled = (trackViewer.CurrentRoute != null);
            menuShowOtherPaths.IsEnabled = (trackViewer.CurrentRoute != null);
            menuSavePath.IsEnabled = (trackViewer.PathEditor != null);
            menuSaveStations.IsEnabled = (trackViewer.PathEditor != null);
            menuShowChart.IsEnabled = (trackViewer.PathEditor != null);
            menuEditMetadata.IsEnabled = menuEnableEditing.IsChecked;
            menuReversePath.IsEnabled = menuEnableEditing.IsChecked;
            menuExtendPath.IsEnabled = (trackViewer.PathEditor != null) && menuEnableEditing.IsChecked;
            menuAutoFixAllNodes.IsEnabled = menuEnableEditing.IsChecked && (trackViewer.PathEditor != null);
            menuAutoFixAllPaths.IsEnabled = (trackViewer.CurrentRoute != null);

            menuEnableEditing.IsEnabled = (trackViewer.PathEditor != null);
            menuEnableEditing2.IsEnabled = menuEnableEditing.IsEnabled;
            menuHighlightLastPathSection.IsEnabled = (trackViewer.PathEditor != null);
            menuHighlightLastPathSection2.IsEnabled = menuHighlightLastPathSection.IsEnabled;
            menuShowCurrentEditorAction.IsEnabled = menuEnableEditing.IsChecked;
            menuShowCurrentEditorAction2.IsEnabled = menuShowCurrentEditorAction.IsEnabled;
            menuPgupExtendsPath.IsEnabled = menuEnableEditing.IsChecked;
            menuPgupExtendsPath2.IsEnabled = menuEnableEditing.IsChecked;
        }

        private void MenuSetAllItems(bool isChecked)
        {
            menuShowJunctionNodes.IsChecked = isChecked;
            menuShowEndNodes.IsChecked = isChecked;
            menuShowCrossovers.IsChecked = isChecked;
            menuShowSidingMarkers.IsChecked = isChecked;
            menuShowSidingNames.IsChecked = isChecked;
            menuShowPlatformMarkers.IsChecked = isChecked;
            menuShowPlatformNames.IsChecked = false; // stationNames get preference
            menuShowStationNames.IsChecked = isChecked;
            menuShowCrossings.IsChecked = isChecked;
            menuShowSpeedLimits.IsChecked = isChecked;
            menuShowMileposts.IsChecked = isChecked;
            menuShowHazards.IsChecked = isChecked;
            menuShowSignals.IsChecked = isChecked;
            menuShowAllSignals.IsChecked = isChecked;
            menuShowEvents.IsChecked = isChecked;
            menuShowEventNames.IsChecked = isChecked;
            menuShowPickups.IsChecked = isChecked;
            menuShowSoundRegions.IsChecked = isChecked;

            menuDrawRoads.IsChecked = isChecked;
            menuShowCarSpawners.IsChecked = isChecked;
            menuShowRoadCrossings.IsChecked = isChecked;

            UpdateMenuSettings();  // to be sure some other settings are done correctly
        }

        private void MenuInstallFolder_Click()
        {
            trackViewer.SelectInstallFolder(() =>
            {
                CloseOtherPathsWindow();
                PopulateRoutes();
                PopulateActivitiesEmpty();
                UpdateMenuSettings();
            });
        }

        /// <summary>
        /// Update the menu to show the available routes
        /// </summary>
        public void PopulateRoutes()
        {
            menuSelectRoute.Items.Clear();
            if (trackViewer.Routes == null) return;
            foreach (ORTS.Menu.Route route in trackViewer.Routes)
            {
                ORTS.Menu.Route thisRoute = route;
                menuSelectRoute.Add(new GameMenuItem(route.Name, _ => MenuSelectRoute_Click(thisRoute)));
            }
        }

        /// <summary>
        /// The user has selected a route, load it.
        /// </summary>
        private void MenuSelectRoute_Click(ORTS.Menu.Route route)
        {
            CloseOtherPathsWindow();
            trackViewer.SetRoute(route, () =>
            {
                UpdateMenuSettings();
                Properties.Settings.Default.CurrentActivityName = "";
            });
        }

        /// <summary>
        /// Empty the activities menu after folder change and before route choice
        /// </summary>
        public void PopulateActivitiesEmpty()
        {
            menuSelectActivity.Items.Clear();
            Properties.Settings.Default.CurrentActivityName = "";
        }

        /// <summary>
        /// Update the menu to show the available activitiy names wich contain location events
        /// </summary>
        public void PopulateActivitiesMenu()
        {
            menuSelectActivity.Items.Clear();
            if (trackViewer.RouteData?.ActivityNames == null) return;
            foreach (string activityName in trackViewer.RouteData.ActivityNames)
            {
                string name = activityName;
                menuSelectActivity.Add(new GameMenuItem(activityName, _ => Properties.Settings.Default.CurrentActivityName = name));
            }
        }

        /// <summary>
        /// Load the last used route
        /// </summary>
        private void MenuReloadRoute_Click()
        {
            CloseOtherPathsWindow();
            trackViewer.ReloadRoute(() =>
            {
                UpdateMenuSettings();
                Properties.Settings.Default.CurrentActivityName = "";
            });
        }

        /// <summary>
        /// Update the menu to show the available paths.
        /// </summary>
        public void PopulatePaths()
        {
            menuSelectPath.Items.Clear();
            menuExtendPath.Items.Clear();
            if (trackViewer.Paths == null) return;
            foreach (ORTS.Menu.Path path in trackViewer.Paths)
            {
                ORTS.Menu.Path thisPath = path;
                string name = MakePathMenyEntryName(path);
                menuSelectPath.Add(new GameMenuItem(name, _ => MenuSelectPath_Click(thisPath)));
                menuExtendPath.Add(new GameMenuItem(name, _ => MenuExtendPath_Click(thisPath)));
            }
        }

        /// <summary>
        /// Update the menu to make sure all the stations are listed
        /// </summary>
        public void PopulateStations()
        {
            PopulateLocations(menuStationList, trackViewer.DrawTrackDB?.StationLocations);
        }

        /// <summary>
        /// Update the menu to make sure all the platforms are listed
        /// </summary>
        public void PopulatePlatforms()
        {
            PopulateLocations(menuPlatformList, trackViewer.DrawTrackDB?.PlatformLocations);
        }

        /// <summary>
        /// Update the menu to make sure all the sidings are listed
        /// </summary>
        public void PopulateSidings()
        {
            PopulateLocations(menuSidingList, trackViewer.DrawTrackDB?.SidingLocations);
        }

        private void PopulateLocations(GameMenuItem parent, Dictionary<string, ORTS.Common.WorldLocation> locations)
        {
            parent.Items.Clear();
            if (locations == null) return;
            foreach (string name in locations.Keys.OrderBy(a => a.ToString()))
            {
                string thisName = name;
                parent.Add(new GameMenuItem(name, _ =>
                {
                    if (locations.ContainsKey(thisName))
                    {
                        trackViewer.DrawArea.ShiftToLocation(locations[thisName]);
                    }
                }));
            }
        }

        /// <summary>
        /// The user has selected a path. Load it
        /// </summary>
        private void MenuSelectPath_Click(ORTS.Menu.Path path)
        {
            trackViewer.SetPath(path, () =>
            {
                if (trackViewer.PathEditor != null)
                {
                    trackViewer.PathEditor.EditingIsActive = menuEnableEditing.IsChecked;
                }
                if (!menuShowPATfile.IsChecked)
                {   // make sure path is visible either raw or (preferably) processed.
                    menuShowTrainpath.IsChecked = true;
                }
                UpdateMenuSettings();
            });
        }

        /// <summary>
        /// The user has selected a path. Use it to extend the path
        /// </summary>
        private void MenuExtendPath_Click(ORTS.Menu.Path path)
        {
            trackViewer.PathEditor?.ExtendWithPath(path);
            UpdateMenuSettings();
        }

        /// <summary>
        /// Convert a path (based on a .pat file) to a header for the menu
        /// </summary>
        /// <param name="path">The path containing name and filepath</param>
        /// <returns>string that can be used to defined menu header</returns>
        public static string MakePathMenyEntryName(ORTS.Menu.Path path)
        {
            string fileName = System.IO.Path.GetFileName(path.FilePath.Replace('\\', System.IO.Path.DirectorySeparatorChar));
            return path.Name + " ( " + fileName + " )";
        }

        private void MenuShortcuts_Click()
        {
            StringBuilder shortcuts = new StringBuilder();
            shortcuts.Append(TrackViewer.catalog.GetString("ctrl-R\tReload the route\n"));
            shortcuts.Append(TrackViewer.catalog.GetString("Q\tQuit\n"));
            shortcuts.Append("\n");
            shortcuts.Append(TrackViewer.catalog.GetString("=\tZoom-in (and keep zooming)\n"));
            shortcuts.Append(TrackViewer.catalog.GetString("shift-=\tZoom-in\n"));
            shortcuts.Append(TrackViewer.catalog.GetString("-\tZoom-out (and keep zooming)\n"));
            shortcuts.Append(TrackViewer.catalog.GetString("shift--\tZoom-out\n"));
            shortcuts.Append(TrackViewer.catalog.GetString("Z\tZoom to tile\n"));
            shortcuts.Append(TrackViewer.catalog.GetString("M\tToggle zoom center\n"));
            shortcuts.Append(TrackViewer.catalog.GetString("R\tZoom reset\n"));
            shortcuts.Append(TrackViewer.catalog.GetString("L\tAdd label\n"));
            shortcuts.Append("\n");
            shortcuts.Append(TrackViewer.catalog.GetString("shift-C\t\tShift center to current mouse location\n"));
            shortcuts.Append(TrackViewer.catalog.GetString("left arrow\tShift left\n"));
            shortcuts.Append(TrackViewer.catalog.GetString("down arrow\tShift down\n"));
            shortcuts.Append(TrackViewer.catalog.GetString("right arrow\tShift right\n"));
            shortcuts.Append(TrackViewer.catalog.GetString("up arrow\t\tShift up\n"));
            shortcuts.Append("\n");
            shortcuts.Append(TrackViewer.catalog.GetString("F5\tShow speed-limits\n"));
            shortcuts.Append(TrackViewer.catalog.GetString("shift-F5\tShow mileposts\n"));
            shortcuts.Append(TrackViewer.catalog.GetString("F6\tShow terrain\n"));
            shortcuts.Append(TrackViewer.catalog.GetString("ctrl-F6\tShow DM terrain\n"));
            shortcuts.Append(TrackViewer.catalog.GetString("shift-F6\tShow terrain patch lines\n"));
            shortcuts.Append(TrackViewer.catalog.GetString("F7\tshow signals\n"));
            shortcuts.Append(TrackViewer.catalog.GetString("F8\tShow platforms\n"));
            shortcuts.Append(TrackViewer.catalog.GetString("shift-F8\tShow platform-names\n"));
            shortcuts.Append(TrackViewer.catalog.GetString("F9\tShow sidings\n"));
            shortcuts.Append(TrackViewer.catalog.GetString("shift-F9\tShow siding-names\n"));
            shortcuts.Append(TrackViewer.catalog.GetString("F10\tHighlight tracks\n"));
            shortcuts.Append(TrackViewer.catalog.GetString("shift-F10\tHighlight items\n"));
            shortcuts.Append(TrackViewer.catalog.GetString("F11\tShow path\n"));
            shortcuts.Append(TrackViewer.catalog.GetString("shift-F11\tShow raw path\n"));
            shortcuts.Append("\n");
            shortcuts.Append(TrackViewer.catalog.GetString("C\t\tShift to center of current path node\n"));
            shortcuts.Append(TrackViewer.catalog.GetString("PgUp\t\tShow more of the path\n"));
            shortcuts.Append(TrackViewer.catalog.GetString("PgDn\t\tShow less of the path\n"));
            shortcuts.Append(TrackViewer.catalog.GetString("shift-PgUp\tShow the full path\n"));
            shortcuts.Append(TrackViewer.catalog.GetString("shift-PgDn\tShow only start point of path\n"));
            shortcuts.Append(TrackViewer.catalog.GetString("E\t\tPlace end-point\n"));
            shortcuts.Append(TrackViewer.catalog.GetString("W\t\tPlace a wait-point\n"));
            shortcuts.Append(TrackViewer.catalog.GetString("ctrl-Z\t\tUndo in path editor\n"));
            shortcuts.Append(TrackViewer.catalog.GetString("ctrl-Y\t\tRedo in path editor\n"));
            shortcuts.Append("\n");
            shortcuts.Append(TrackViewer.catalog.GetString("alt-?\t\tVarious keys to open submenus\n"));
            shortcuts.Append("\n");
            shortcuts.Append(TrackViewer.catalog.GetString("mouse-right click\tOn event shows event message\n"));
            Dialogs.ShowMessage(shortcuts.ToString(), TrackViewer.catalog.GetString("Keyboard shortcuts"));
        }

        private void MenuAbout_Click()
        {
            StringBuilder about = new StringBuilder();
            about.Append(TrackViewer.catalog.GetString("This is ORTS TrackViewer, version "));
            about.Append(TrackViewer.TrackViewerVersion);
            about.Append("\n");
            about.Append(TrackViewer.catalog.GetString("It is a 'Contribution' to ORTS, and as such\n"));
            about.Append(TrackViewer.catalog.GetString("not an integral part of ORTS."));
            Dialogs.ShowMessage(about.ToString(), "TrackViewer");
        }

        private void MenuDocumentation_Click()
        {
            string website = "https://www.openrails.org/learn/documents/";
            StringBuilder documentation = new StringBuilder();
            documentation.Append(TrackViewer.catalog.GetString("Documentation is available"));
            documentation.Append(": ");
            documentation.Append(website);
            documentation.Append(" (");
            documentation.Append(TrackViewer.catalog.GetString("Right column → Open Rails Trackviewer"));
            documentation.Append(")\n");
            documentation.Append(TrackViewer.catalog.GetString("Do you want to go to the website?"));
            Dialogs.Ask(documentation.ToString(), TrackViewer.catalog.GetString("Documentation"), MessageButtons.YesNoCancel, (result) =>
            {
                if (result == MessageResult.Yes)
                {
                    try
                    {
                        Process.Start(new ProcessStartInfo { FileName = website, UseShellExecute = true });
                    }
                    catch (Exception error)
                    {
                        Dialogs.ShowMessage(error.Message);
                    }
                }
            });
        }

        private void MenuDoAntiAliasing_Click()
        {
            Properties.Settings.Default.doAntiAliasing = menuDoAntiAliasing.IsChecked;
            TrackViewer.SaveSettings();
            trackViewer.SetAliasing();
        }

        /// <summary>
        /// Toggle whether zooming is around mouse or centered
        /// </summary>
        public void MenuToggleZoomingAroundMouse()
        {
            menuZoomIsCenteredOnMouse.IsChecked = !menuZoomIsCenteredOnMouse.IsChecked;
            UpdateMenuSettings();
        }

        /// <summary>
        /// Toggle whether the sidings are shown
        /// </summary>
        public void MenuToggleShowSidings()
        {
            menuShowSidingMarkers.IsChecked = !menuShowSidingMarkers.IsChecked;
            UpdateMenuSettings();
        }

        /// <summary>
        /// Toggle whether the siding names are shown
        /// </summary>
        public void MenuToggleShowSidingNames()
        {
            menuShowSidingNames.IsChecked = !menuShowSidingNames.IsChecked;
            UpdateMenuSettings();
        }

        /// <summary>
        /// Toggle whether the platforms are shown
        /// </summary>
        public void MenuToggleShowPlatforms()
        {
            menuShowPlatformMarkers.IsChecked = !menuShowPlatformMarkers.IsChecked;
            UpdateMenuSettings();
        }

        /// <summary>
        /// Circulate from station names, to platform names to neither
        /// </summary>
        public void MenuCirculatePlatformStationNames()
        {
            if (menuShowStationNames.IsChecked)
            {
                menuShowStationNames.IsChecked = false;
                menuShowPlatformNames.IsChecked = true;
            }
            else if (menuShowPlatformNames.IsChecked)
            {
                menuShowStationNames.IsChecked = false;
                menuShowPlatformNames.IsChecked = false;
            }
            else
            {
                menuShowStationNames.IsChecked = true;
                menuShowPlatformNames.IsChecked = false;
            }
            UpdateMenuSettings();
        }

        /// <summary>
        /// Toggle whether the train path is shown
        /// </summary>
        public void MenuToggleShowTrainpath()
        {
            menuShowTrainpath.IsChecked = !menuShowTrainpath.IsChecked;
            UpdateMenuSettings();
        }

        /// <summary>
        /// Toggle whether the PATfile (.pat file) is shown
        /// </summary>
        public void MenuToggleShowPatFile()
        {
            menuShowPATfile.IsChecked = !menuShowPATfile.IsChecked;
            UpdateMenuSettings();
        }

        /// <summary>
        /// Toggle whether highlighting of tracks is done
        /// </summary>
        public void MenuToggleHighlightTracks()
        {
            menuHighlightTracks.IsChecked = !menuHighlightTracks.IsChecked;
            UpdateMenuSettings();
        }

        /// <summary>
        /// Toggle whether highlighting of track-itemss is done
        /// </summary>
        public void MenuToggleHighlightItems()
        {
            menuHighlightItems.IsChecked = !menuHighlightItems.IsChecked;
            UpdateMenuSettings();
        }

        /// <summary>
        /// Toggle whether the signals are shown
        /// </summary>
        public void MenuToggleShowSignals()
        {
            menuShowSignals.IsChecked = !menuShowSignals.IsChecked;
            if (!menuShowSignals.IsChecked)
            {
                menuShowAllSignals.IsChecked = false;
            }
            UpdateMenuSettings();
        }

        /// <summary>
        /// Toggle whether the speedlimits are shown
        /// </summary>
        public void MenuToggleShowSpeedLimits()
        {
            menuShowSpeedLimits.IsChecked = !menuShowSpeedLimits.IsChecked;
            UpdateMenuSettings();
        }

        /// <summary>
        /// Toggle whether the mile posts are shown
        /// </summary>
        public void MenuToggleShowMilePosts()
        {
            menuShowMileposts.IsChecked = !menuShowMileposts.IsChecked;
            UpdateMenuSettings();
        }

        #region Terrain
        /// <summary>
        /// Toggle whether the terrain is shown or not
        /// </summary>
        public void MenuToggleShowTerrain()
        {
            menuShowTerrain.IsChecked = !menuShowTerrain.IsChecked;
            MenuShowTerrain_Click();
        }

        /// <summary>
        /// Toggle whether the Distance Mountain terrain is shown or not
        /// </summary>
        public void MenuToggleShowDMTerrain()
        {
            menuShowDMTerrain.IsChecked = !menuShowDMTerrain.IsChecked;
            MenuShowTerrain_Click();
        }

        /// <summary>
        /// Set whether the terrain is shown or not
        /// </summary>
        /// <param name="show">Set to true if you want to show the terrain</param>
        public void MenuSetShowTerrain(bool show)
        {
            menuShowTerrain.IsChecked = show;
            if (!show)
            {
                menuShowPatchLines.IsChecked = false;
            }
            MenuShowTerrain_Click();
        }

        /// <summary>
        /// Set whether the Distance Mountain terrain is shown or not
        /// </summary>
        /// <param name="show">Set to true if you want to show the terrain</param>
        public void MenuSetShowDMTerrain(bool show)
        {
            menuShowDMTerrain.IsChecked = show;
            MenuShowTerrain_Click();
        }

        private void MenuShowTerrain_Click()
        {
            UpdateMenuSettings();
            bool succeeded = trackViewer.SetTerrainVisibility(menuShowTerrain.IsChecked, menuShowDMTerrain.IsChecked);
            if (!succeeded)
            {
                if (menuShowTerrain.IsChecked == true)
                {
                    MenuSetShowTerrain(false);
                }
                if (menuShowDMTerrain.IsChecked == true)
                {
                    MenuSetShowDMTerrain(false);
                }
            }
        }

        /// <summary>
        /// Toggle whether the patchlines of the terrain are shown or not
        /// </summary>
        public void MenuToggleShowPatchLines()
        {
            menuShowPatchLines.IsChecked = !menuShowPatchLines.IsChecked;
            MenuShowPatchLines_Click();
        }

        private void MenuShowPatchLines_Click()
        {
            UpdateMenuSettings();
            trackViewer.SetPatchLineVisibility(menuShowPatchLines.IsChecked);
        }

        private void TerrainReductionOption_CheckChanged(GameMenuItem item)
        {
            Properties.Settings.Default.terrainReductionFactor = (int)item.Tag;
            UpdateMenuSettings();
            trackViewer.SetTerrainReduction();
        }
        #endregion

        private void OpenSearch(SearchableItem searchItem)
        {
            if (trackViewer.DrawTrackDB == null) return;
            Dialogs.Show(() =>
            {
                var searchControl = new SearchControl(trackViewer, searchItem);
                TrackViewer.Localize(searchControl);
                return searchControl;
            });
        }

        /// <summary>
        /// Add a new preference in the form of a string. The list of allowed options should also be given.
        /// The callback should be called whenever the option is changed (or possibly when the default is initialized).
        /// </summary>
        /// <param name="name">Name of the preference used for indexing within the program</param>
        /// <param name="description">Description of the preference used to present it to the user</param>
        /// <param name="options">The options a preference can take</param>
        /// <param name="defaultOption">The default option.</param>
        /// <param name="callback">The callback that will be called upon a change in the preference</param>
        public void AddStringPreference(string name, string description, string[] options, string defaultOption, StringPreferenceDelegate callback)
        {
            GameMenuItem preferenceItem = new GameMenuItem(description);
            foreach (string option in options)
            {
                string thisOption = option;
                preferenceItem.Add(new GameMenuItem(option, _ =>
                {
                    callback(thisOption);
                    //Store the preference
                    settingsDictionary[name] = thisOption;
                    settingsDictionary.Save();
                }));
            }
            menuPreferences.Add(preferenceItem);

            //See if there is a preference
            if (settingsDictionary.ContainsKey(name))
            {
                callback(settingsDictionary[name]);
            }
        }

        /// <summary>
        /// Enable editing and make sure this is visible in the menu as well.
        /// </summary>
        public void SetEnableEditing()
        {
            menuEnableEditing.IsChecked = true;
            MenuEnableEditing_Click();
        }

        private void MenuEnableEditing_Click()
        {
            menuEnableEditing2.IsChecked = menuEnableEditing.IsChecked;
            if (trackViewer.PathEditor != null)
            {
                trackViewer.PathEditor.EditingIsActive = menuEnableEditing.IsChecked;
            }
            UpdateMenuSettings();
        }

        private void MenuNewPath_Click()
        {
            trackViewer.NewPath(() =>
            {
                menuEnableEditing.IsChecked = true;
                menuShowTrainpath.IsChecked = true;
                UpdateMenuSettings();
            });
        }

        private void MenuShowAllSignals_Click()
        {
            // if someone selects all signals, also normal signals should be turned on.
            if (menuShowAllSignals.IsChecked)
            {
                menuShowSignals.IsChecked = true;
            }
            UpdateMenuSettings();
        }

        /// <summary>
        /// Populate the list of languages
        /// </summary>
        public void PopulateLanguages()
        {
            menuLanguage.Items.Clear();
            foreach (Language language in trackViewer.LanguageManager.Languages)
            {
                Language thisLanguage = language;
                menuLanguage.Add(new GameMenuItem(language.Name, _ => trackViewer.SelectLanguage(thisLanguage.Code))
                {
                    RadioGroup = "language",
                    IsChecked = language.Code == LanguageManager.CurrentLanguageCode,
                });
            }
        }

        private void MenuShowOtherPaths_Click()
        {
            if (trackViewer.DrawMultiplePaths == null) return;
            CloseOtherPathsWindow();
            DrawMultiplePaths multiPaths = trackViewer.DrawMultiplePaths;
            string[] pathNames = multiPaths.PathNames();
            Dialogs.Show(() =>
            {
                otherPathsWindow = new OtherPathsWindow(multiPaths, pathNames);
                TrackViewer.Localize(otherPathsWindow);
                return otherPathsWindow;
            }, (window) => { if (otherPathsWindow == window) otherPathsWindow = null; });
        }

        private void CloseOtherPathsWindow()
        {
            UiThread.Post(() =>
            {
                otherPathsWindow?.Close();
                otherPathsWindow = null;
            });
        }
    }

    #region IPreferenceChanger
    /// <summary>
    /// Delegate to enable callbacks for preference choices
    /// </summary>
    /// <param name="chosenOption">The option of the preferences that was chosen</param>
    public delegate void StringPreferenceDelegate(string chosenOption);

    /// <summary>
    /// This interface is intended to support various ways in preferences can be changed.
    /// By having a common interface implementation and definition are separated and hence more flexible
    /// </summary>
    public interface IPreferenceChanger
    {
        /// <summary>
        /// Add a new preference in the form of a string. The list of allowed options should also be given.
        /// The callback should be called whenever the option is changed (or possibly when the default is initialized).
        /// </summary>
        /// <param name="name">Name of the preference used for indexing within the program</param>
        /// <param name="description">Description of the preference used to present it to the user</param>
        /// <param name="options">The options a preference can take</param>
        /// <param name="defaultOption">The default option.</param>
        /// <param name="callback">The callback that will be called upon a change in the preference</param>
        void AddStringPreference(string name, string description, string[] options, string defaultOption, StringPreferenceDelegate callback);
    }
    #endregion

    #region SaveableSettingsDictionary
    /// <summary>
    /// Dictionary that supports saving to stored user settings.
    /// </summary>
    class SaveableSettingsDictionary : Dictionary<string,string>
    {
        /// <summary>
        /// Constructor. Also loads the values from stored settings.
        /// </summary>
        public SaveableSettingsDictionary()
        {
            if (Properties.Settings.Default.preferences == null)
            {
                Properties.Settings.Default.preferences = new StringCollection();
            }
            Dictionary<string, string> hiddenDictionary = ToDictionary(Properties.Settings.Default.preferences);
            foreach (KeyValuePair<string, string> kvp in hiddenDictionary)
            {
                this[kvp.Key] = kvp.Value;
            }
        }

        /// <summary>
        /// Save to stored settings
        /// </summary>
        public void Save()
        {
            Properties.Settings.Default.preferences = ToStringCollection(this);
            TrackViewer.SaveSettings();
        }

        /// <summary>
        /// Translate a StringCollection containing key, value pairs into a dictionary.
        /// </summary>
        private static Dictionary<string, string> ToDictionary(StringCollection stringCollection)
        {
            Dictionary<string, string> result = new Dictionary<string, string>();
            for (int i = 0; i < stringCollection.Count - 1; i += 2) // the -1 to prevent problems when the stringCollection has odd number of elements.
            {
                result[stringCollection[i]] = stringCollection[i + 1];
            }
            return result;
        }

        /// <summary>
        /// Translate from a dictionary to a StringCollection (organized in key, value pairs)
        /// </summary>
        private static StringCollection ToStringCollection(Dictionary<string, string> dictionary)
        {
            StringCollection result = new StringCollection();
            foreach (KeyValuePair<string,string> kvp in dictionary)
            {
                result.Add(kvp.Key);
                result.Add(kvp.Value);
            }
            return result;
        }
    }
    #endregion
}
