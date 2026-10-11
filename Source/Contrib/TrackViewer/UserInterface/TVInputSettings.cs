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
// Based on RunActivity\ORTS\InputSettings.cs
// But we copy some of the code because we need different commands here
//
// Here all possible key commands are defined (enumerated) as well as linked to a specific key or key combination.

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;

using ORTS.Settings;

namespace ORTS.TrackViewer.UserInterface
{
    /// <summary>
    /// Enumeration of all possible key-based commands that can be given by user. Link to key combinations is given later
    /// </summary>
    public enum TVUserCommands
    {
        /// <summary>Reload the route</summary>
        ReloadRoute,
        /// <summary>command for zooming in</summary>
        ZoomIn,
        /// <summary>command for zooming out</summary>
        ZoomOut,
        /// <summary>command for zooming in slowly</summary>
        ZoomInSlow,
        /// <summary>command for zooming out slowly</summary>
        ZoomOutSlow,
        /// <summary>command for resetting zoom</summary>
        ZoomReset,
        /// <summary>command for zooming in to tile-size</summary>
        ZoomToTile,
        /// <summary>command for toggling whether zooming is around mouse or center</summary>
        ToggleZoomAroundMouse,
        /// <summary>command for shifting view window left</summary>
        ShiftLeft,
        /// <summary>command for shifting view window right</summary>
        ShiftRight,
        /// <summary>command for shifting view window up</summary>
        ShiftUp,
        /// <summary>command for shifting view window down</summary>
        ShiftDown,
        /// <summary>command for shifting to a specific location</summary>
        ShiftToPathLocation,
        /// <summary>command for shift to current mouse location</summary>
        ShiftToMouseLocation,
        /// <summary>command for extending a train path</summary>
        ExtendPath,
        /// <summary>command for showing the full train path</summary>
        ExtendPathFull,
        /// <summary>command for reducing the drawn part of a train path</summary>
        ReducePath,
        /// <summary>command for showing only start node of a train path</summary>
        ReducePathFull,
        /// <summary>command for placing the end point of a path</summary>
        PlaceEndPoint,
        /// <summary>command for placing a wait point for a path</summary>
        PlaceWaitPoint,
        /// <summary>command for debugging the key map</summary>
        DebugDumpKeymap,
        /// <summary>command for adding a label</summary>
        AddLabel,
        /// <summary>command for quitting the application</summary>
        Quit,
        /// <summary>command for performing debug steps</summary>
        Debug,
        /// <summary>command for toggling showing sidings</summary>
        ToggleShowSidings,
        /// <summary>command for toggling showing siding names</summary>
        ToggleShowSidingNames,
        /// <summary>command for toggling showing platforms</summary>
        ToggleShowPlatforms,
        /// <summary>command for toggling showing platform names</summary>
        ToggleShowPlatformNames,
        /// <summary>command for toggling showing signals</summary>
        ToggleShowSignals,
        /// <summary>command for toggling showing the raw .pat file train path</summary>
        ToggleShowPatFile,
        /// <summary>command for toggling showing the train path</summary>
        ToggleShowTrainpath,
        /// <summary>key </summary>
        ToggleShowSpeedLimits,
        /// <summary>command for toggling showing mile posts</summary>
        ToggleShowMilePosts,
        /// <summary>command for toggling highlighting tracks</summary>
        ToggleHighlightTracks,
        /// <summary>command for toggling higlighting track items</summary>
        ToggleHighlightItems,
        /// <summary>command for toggling showing terrain textures</summary>
        ToggleShowTerrain,
        /// <summary>command for toggling showing Distant Mountain terrain textures</summary>
        ToggleShowDMTerrain,
        /// <summary>command for toggling showing patch lines around terrain textures</summary>
        ToggleShowPatchLines,
        /// <summary>command for allowing slow zoom with mouse</summary>
        MouseZoomSlow,
        /// <summary>Key modifier for drag actions</summary>
        EditorTakesMouseClickDrag,
        /// <summary>Key modifier for click actions</summary>
        EditorTakesMouseClickAction,
        /// <summary>command for redo in editor</summary>
        EditorRedo,
        /// <summary>command for undo in editor</summary>
        EditorUndo,
        /// <summary>Menu shortcut</summary>
        MenuFile,
        /// <summary>Menu shortcut</summary>
        MenuTrackItems,
        /// <summary>Menu shortcut</summary>
        MenuView,
        /// <summary>Menu shortcut</summary>
        MenuStatusbar,
        /// <summary>Menu shortcut</summary>
        MenuPreferences,
        /// <summary>Menu shortcut</summary>
        MenuPathEditor,
        /// <summary>Menu shortcut</summary>
        MenuTerrain,
        /// <summary>Menu shortcut</summary>
        MenuHelp,
    }

    /* not used, because ORTS.Settings.KeyModifiers is used
    [Flags]
    public enum KeyModifierss
    {
        None = 0,
        Shift = 1,
        Control = 2,
        Alt = 4
    }
    */

    /// <summary>
    /// static class to map keyboard combinations to enumeration
    /// </summary>
    public static class TVInputSettings
    {
        /// <summary>
        /// Array of commands that have been defined and for which a key-combination can and should be defined below
        /// </summary>
        public static UserCommandInput[] Commands = new UserCommandInput[Enum.GetNames(typeof(TVUserCommands)).Length];
        
        //static readonly string[] KeyboardLayout = new[] {
        //    "[01 ]   [3B ][3C ][3D ][3E ]   [3F ][40 ][41 ][42 ]   [43 ][44 ][57 ][58 ]   [37 ][46 ][11D]",
        //    "                                                                                            ",
        //    "[29 ][02 ][03 ][04 ][05 ][06 ][07 ][08 ][09 ][0A ][0B ][0C ][0D ][0E     ]   [52 ][47 ][49 ]",
        //    "[0F   ][10 ][11 ][12 ][13 ][14 ][15 ][16 ][17 ][18 ][19 ][1A ][1B ][2B   ]   [53 ][4F ][51 ]",
        //    "[3A     ][1E ][1F ][20 ][21 ][22 ][23 ][24 ][25 ][26 ][27 ][28 ][1C      ]                  ",
        //    "[2A       ][2C ][2D ][2E ][2F ][30 ][31 ][32 ][33 ][34 ][35 ][36         ]        [48 ]     ",
        //    "[1D   ][    ][38  ][39                          ][    ][    ][    ][1D   ]   [4B ][50 ][4D ]",
        //};

        /// <summary>
        /// Set the default mapping from keys or key-combinations to commands
        /// </summary>
        public static void SetDefaults()
        {
            // Note: the original used keyboard scan codes, translated via user32 MapVirtualKey (Windows-only).
            // XNA virtual keys are used instead; these follow the active keyboard layout rather than the physical key.
            Commands[(int)TVUserCommands.ReloadRoute] = new ORTS.Settings.UserCommandKeyInput(Keys.R, ORTS.Settings.KeyModifiers.Control);
            Commands[(int)TVUserCommands.ZoomIn]     = new ORTS.Settings.UserCommandKeyInput(Keys.OemPlus);
            Commands[(int)TVUserCommands.ZoomOut]    = new ORTS.Settings.UserCommandKeyInput(Keys.OemMinus);
            Commands[(int)TVUserCommands.ZoomInSlow] = new ORTS.Settings.UserCommandKeyInput(Keys.OemPlus, ORTS.Settings.KeyModifiers.Shift);
            Commands[(int)TVUserCommands.ZoomOutSlow]= new ORTS.Settings.UserCommandKeyInput(Keys.OemMinus, ORTS.Settings.KeyModifiers.Shift);
            Commands[(int)TVUserCommands.ZoomReset]  = new ORTS.Settings.UserCommandKeyInput(Keys.R);
            Commands[(int)TVUserCommands.ZoomToTile] = new ORTS.Settings.UserCommandKeyInput(Keys.Z);
            Commands[(int)TVUserCommands.ShiftLeft]  = new ORTS.Settings.UserCommandKeyInput(Keys.Left);
            Commands[(int)TVUserCommands.ShiftRight] = new ORTS.Settings.UserCommandKeyInput(Keys.Right);
            Commands[(int)TVUserCommands.ShiftUp]    = new ORTS.Settings.UserCommandKeyInput(Keys.Up);
            Commands[(int)TVUserCommands.ShiftDown]  = new ORTS.Settings.UserCommandKeyInput(Keys.Down);
            Commands[(int)TVUserCommands.ShiftToPathLocation] = new ORTS.Settings.UserCommandKeyInput(Keys.C);
            Commands[(int)TVUserCommands.ShiftToMouseLocation] = new ORTS.Settings.UserCommandKeyInput(Keys.C, ORTS.Settings.KeyModifiers.Shift);
            Commands[(int)TVUserCommands.ToggleZoomAroundMouse] = new ORTS.Settings.UserCommandKeyInput(Keys.M);
            
            Commands[(int)TVUserCommands.ToggleShowSpeedLimits] = new ORTS.Settings.UserCommandKeyInput(Keys.F5);
            Commands[(int)TVUserCommands.ToggleShowMilePosts] = new ORTS.Settings.UserCommandKeyInput(Keys.F5, ORTS.Settings.KeyModifiers.Shift);
            Commands[(int)TVUserCommands.ToggleShowTerrain] = new ORTS.Settings.UserCommandKeyInput(Keys.F6);
            Commands[(int)TVUserCommands.ToggleShowDMTerrain] = new ORTS.Settings.UserCommandKeyInput(Keys.F6, ORTS.Settings.KeyModifiers.Control);
            Commands[(int)TVUserCommands.ToggleShowPatchLines] = new ORTS.Settings.UserCommandKeyInput(Keys.F6, ORTS.Settings.KeyModifiers.Shift);
            Commands[(int)TVUserCommands.ToggleShowSignals] = new ORTS.Settings.UserCommandKeyInput(Keys.F7);
            Commands[(int)TVUserCommands.ToggleShowPlatforms] = new ORTS.Settings.UserCommandKeyInput(Keys.F8);
            Commands[(int)TVUserCommands.ToggleShowPlatformNames] = new ORTS.Settings.UserCommandKeyInput(Keys.F8, ORTS.Settings.KeyModifiers.Shift);
            Commands[(int)TVUserCommands.ToggleShowSidings] = new ORTS.Settings.UserCommandKeyInput(Keys.F9);
            Commands[(int)TVUserCommands.ToggleShowSidingNames] = new ORTS.Settings.UserCommandKeyInput(Keys.F9, ORTS.Settings.KeyModifiers.Shift);
            Commands[(int)TVUserCommands.ToggleHighlightTracks] = new ORTS.Settings.UserCommandKeyInput(Keys.F10);
            Commands[(int)TVUserCommands.ToggleHighlightItems] = new ORTS.Settings.UserCommandKeyInput(Keys.F10, ORTS.Settings.KeyModifiers.Shift);
            Commands[(int)TVUserCommands.ToggleShowTrainpath] = new ORTS.Settings.UserCommandKeyInput(Keys.F11);
            Commands[(int)TVUserCommands.ToggleShowPatFile] = new ORTS.Settings.UserCommandKeyInput(Keys.F11, ORTS.Settings.KeyModifiers.Shift);

            Commands[(int)TVUserCommands.ExtendPath] = new ORTS.Settings.UserCommandKeyInput(Keys.PageUp);
            Commands[(int)TVUserCommands.ExtendPathFull] = new ORTS.Settings.UserCommandKeyInput(Keys.PageUp, ORTS.Settings.KeyModifiers.Shift);
            Commands[(int)TVUserCommands.ReducePath] = new ORTS.Settings.UserCommandKeyInput(Keys.PageDown);
            Commands[(int)TVUserCommands.ReducePathFull] = new ORTS.Settings.UserCommandKeyInput(Keys.PageDown, ORTS.Settings.KeyModifiers.Shift);
            Commands[(int)TVUserCommands.PlaceEndPoint] = new ORTS.Settings.UserCommandKeyInput(Keys.E);
            Commands[(int)TVUserCommands.PlaceWaitPoint] = new ORTS.Settings.UserCommandKeyInput(Keys.W);

            Commands[(int)TVUserCommands.AddLabel]   = new ORTS.Settings.UserCommandKeyInput(Keys.L);
            Commands[(int)TVUserCommands.Quit]       = new ORTS.Settings.UserCommandKeyInput(Keys.Q);
            Commands[(int)TVUserCommands.Debug]      = new ORTS.Settings.UserCommandKeyInput(Keys.OemPeriod);
            Commands[(int)TVUserCommands.DebugDumpKeymap] = new ORTS.Settings.UserCommandKeyInput(Keys.F1, ORTS.Settings.KeyModifiers.Alt);

            Commands[(int)TVUserCommands.MouseZoomSlow] = new UserCommandModifierInput(Settings.KeyModifiers.Shift);

            Commands[(int)TVUserCommands.EditorTakesMouseClickAction] = new UserCommandModifierInput(Settings.KeyModifiers.Shift);
            Commands[(int)TVUserCommands.EditorTakesMouseClickDrag] = new UserCommandModifierInput(Settings.KeyModifiers.Control);
            Commands[(int)TVUserCommands.EditorUndo] = new ORTS.Settings.UserCommandKeyInput(Keys.Z, ORTS.Settings.KeyModifiers.Control);
            Commands[(int)TVUserCommands.EditorRedo] = new ORTS.Settings.UserCommandKeyInput(Keys.Y, ORTS.Settings.KeyModifiers.Control);

            Commands[(int)TVUserCommands.MenuFile] = new ORTS.Settings.UserCommandKeyInput(Keys.F, ORTS.Settings.KeyModifiers.Alt);
            Commands[(int)TVUserCommands.MenuView] = new ORTS.Settings.UserCommandKeyInput(Keys.V, ORTS.Settings.KeyModifiers.Alt);
            Commands[(int)TVUserCommands.MenuTrackItems] = new ORTS.Settings.UserCommandKeyInput(Keys.I, ORTS.Settings.KeyModifiers.Alt);
            Commands[(int)TVUserCommands.MenuStatusbar] = new ORTS.Settings.UserCommandKeyInput(Keys.S, ORTS.Settings.KeyModifiers.Alt);
            Commands[(int)TVUserCommands.MenuPreferences] = new ORTS.Settings.UserCommandKeyInput(Keys.P, ORTS.Settings.KeyModifiers.Alt);
            Commands[(int)TVUserCommands.MenuPathEditor] = new ORTS.Settings.UserCommandKeyInput(Keys.E, ORTS.Settings.KeyModifiers.Alt);
            Commands[(int)TVUserCommands.MenuTerrain] = new ORTS.Settings.UserCommandKeyInput(Keys.T, ORTS.Settings.KeyModifiers.Alt);
            Commands[(int)TVUserCommands.MenuHelp] = new ORTS.Settings.UserCommandKeyInput(Keys.H, ORTS.Settings.KeyModifiers.Alt);
        }

    }
}
