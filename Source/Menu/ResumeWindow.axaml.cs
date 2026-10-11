// COPYRIGHT 2012, 2013, 2014 by the Open Rails project.
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

/*
This form adds the ability to save the state of the simulator (a Save) multiple times.
Saves are made to the folder Program.UserDataFolder (e.g.
    C:\Users\Chris\AppData\Roaming\Open Rails\  or  ~/.config/Open Rails/
and take the form  <activity file name> <date> <time>.save. E.g.
    yard_two 2012-03-20 22.07.36.save

As Saves for all routes are saved in the same folder and activity file names might be common to several routes,
the date and time elements ensure that the Save file names are unique.

If the player is not running an activity but exploring a route, the filename takes the form
<route folder name> <date> <time>.save. E.g.
    USA2 2012-03-20 22.07.36.save

The RunActivity program takes switches; one of these is -resume
The -resume switch can now take a Save file name as a parameter. E.g.
    RunActivity -resume "yard_two 2012-03-20 22.07.36"
or
    RunActivity -resume "yard_two 2012-03-20 22.07.36.save"

If no parameter is provided, then RunActivity uses the most recent Save.

New versions of Open Rails may be incompatible with Saves made by older versions. A mechanism is provided
here to reject Saves which are definitely incompatible and warn of Saves that may be incompatible. A Save that
is marked as "may be incompatible" may not be resumed successfully by the RunActivity which will
stop and issue an error message.

Some problems remain (see comments in the code):
1. A screen-capture image is saved along with the Save. The intention is that this image should be a thumbnail
   but I can't find how to code this successfully. In the meantime, the screen-capture image that is saved is full-size
   but displayed as a thumbnail.
*/

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Media.Imaging;
using Avalonia.Platform.Storage;
using MSTS;
using ORTS.Common;
using ORTS.Menu;
using ORTS.Settings;
using Path = System.IO.Path;
using KeyModifiers = Avalonia.Input.KeyModifiers;

namespace Menu
{
    public partial class ResumeWindow : Window
    {
        readonly UserSettings Settings;
        readonly Route Route;
        readonly Activity Activity;
        readonly MainWindow MainForm;
        readonly TimetableInfo Timetable;

        List<Save> Saves = new List<Save>();
        Task<List<Save>> SaveLoader;

        public class Save
        {
            public string File { get; private set; }
            public string PathName { get; private set; }
            public string RouteName { get; private set; }
            public TimeSpan GameTime { get; private set; }
            public DateTime RealTime { get; private set; }
            public string CurrentTile { get; private set; }
            public string Distance { get; private set; }
            public bool? Valid { get; private set; } // 3 possibilities: invalid, unknown validity, valid
            public string VersionOrBuild { get; private set; }
            public bool IsMultiplayer { get; private set; }

            public Save(string fileName, string youngestVersionFailedToRestore)
            {
                File = fileName;
                System.Threading.Thread.Sleep(10);
                using (BinaryReader inf = new BinaryReader(new FileStream(File, FileMode.Open, FileAccess.Read)))
                {
                    try
                    {
                        var version = inf.ReadString().Replace("\0", ""); // e.g. "0.9.0.1648" or "X1321" or "" (if compiled locally)
                        var build = inf.ReadString().Replace("\0", ""); // e.g. 0.0.5223.24629 (2014-04-20 13:40:58Z)
                        var versionOrBuild = version.Length > 0 ? version : build;
                        var valid = VersionInfo.GetValidity(version, build, youngestVersionFailedToRestore);
                        // Read in multiplayer flag/ route/activity/path/player data.
                        // Done so even if not elegant to be compatible with existing save files
                        var routeNameOrMultipl = inf.ReadString();
                        var routeName = "";
                        var isMultiplayer = false;
                        if (routeNameOrMultipl == "$Multipl$")
                        {
                            isMultiplayer = true;
                            routeName = inf.ReadString(); // Route name
                        }
                        else
                        {
                            routeName = routeNameOrMultipl; // Route name
                        }

                        var pathName = inf.ReadString(); // Path name
                        var gameTime = new DateTime().AddSeconds(inf.ReadInt32()).TimeOfDay; // Game time
                        var realTime = DateTime.FromBinary(inf.ReadInt64()); // Real time
                        var currentTileX = inf.ReadSingle(); // Player TileX
                        var currentTileZ = inf.ReadSingle(); // Player TileZ
                        var currentTile = String.Format("{0:F1}, {1:F1}", currentTileX, currentTileZ);
                        var initialTileX = inf.ReadSingle(); // Initial TileX
                        var initialTileZ = inf.ReadSingle(); // Initial TileZ
                        if (currentTileX < short.MinValue || currentTileX > short.MaxValue || currentTileZ < short.MinValue || currentTileZ > short.MaxValue) throw new InvalidDataException();
                        if (initialTileX < short.MinValue || initialTileX > short.MaxValue || initialTileZ < short.MinValue || initialTileZ > short.MaxValue) throw new InvalidDataException();

                        // DistanceFromInitial using Pythagoras theorem.
                        var distance = String.Format("{0:F1}", Math.Sqrt(Math.Pow(currentTileX - initialTileX, 2) + Math.Pow(currentTileZ - initialTileZ, 2)) * 2048);

                        PathName = pathName;
                        RouteName = routeName.Trim();
                        IsMultiplayer = isMultiplayer;
                        GameTime = gameTime;
                        RealTime = realTime;
                        CurrentTile = currentTile;
                        Distance = distance;
                        Valid = valid;
                        VersionOrBuild = versionOrBuild;
                    }
                    catch { }
                }
            }
        }

        public string SelectedSaveFile { get; set; }
        public MainWindow.UserAction SelectedAction { get; set; }
        private bool Multiplayer { get; set; }

        /// <summary>
        /// The modifier keys used with the button which closed the window (for "Alt copies the command line").
        /// </summary>
        public KeyModifiers LastKeyModifiers { get; private set; }

        Save CurrentSave => gridSaves.SelectedItem as Save;

        public ResumeWindow(UserSettings settings, Route route, MainWindow.UserAction mainFormAction, Activity activity, TimetableInfo timetable,
            MainWindow parentForm)
        {
            MainForm = parentForm;
            SelectedAction = mainFormAction;
            Multiplayer = SelectedAction == MainWindow.UserAction.MultiplayerClient || SelectedAction == MainWindow.UserAction.MultiplayerServer;
            InitializeComponent();
            Icon = App.LoadIcon();

            Settings = settings;
            Route = route;
            Activity = activity;
            Timetable = timetable;

            checkBoxReplayPauseBeforeEnd.IsChecked = Settings.ReplayPauseBeforeEnd;
            numericReplayPauseBeforeEnd.Value = Settings.ReplayPauseBeforeEndS;

            gridSaves.SelectionChanged += gridSaves_SelectionChanged;
            gridSaves.DoubleTapped += gridSaves_DoubleClick;
            pictureBoxScreenshot.PointerPressed += pictureBoxScreenshot_Click;
            buttonResume.Click += buttonResume_Click;
            buttonDelete.Click += buttonDelete_Click;
            buttonUndelete.Click += buttonUndelete_Click;
            buttonDeleteInvalid.Click += buttonDeleteInvalid_Click;
            buttonReplayFromStart.Click += buttonReplayFromStart_Click;
            buttonReplayFromPreviousSave.Click += buttonReplayFromPreviousSave_Click;
            buttonImportExportSaves.Click += buttonImportExportSaves_Click;
            Closing += ResumeForm_FormClosing;

            AddHandler(PointerReleasedEvent, (sender, e) => LastKeyModifiers = e.KeyModifiers, RoutingStrategies.Tunnel, true);
            AddHandler(KeyDownEvent, (sender, e) => LastKeyModifiers = e.KeyModifiers, RoutingStrategies.Tunnel, true);

            gridSaves_SelectionChanged(null, null);

            var pathNameColumn = gridSaves.Columns[2];
            if (SelectedAction == MainWindow.UserAction.SinglePlayerTimetableGame)
            {
                Title = String.Format("{0} - {1} - {2}", Title, route.Name, Path.GetFileNameWithoutExtension(Timetable.fileName));
                pathNameColumn.IsVisible = true;
            }
            else
            {
                Title = String.Format("{0} - {1} - {2}", Title, route.Name, activity.FilePath != null ? activity.Name :
                    activity.Name == "+ " + Localization.GetString("Explore in Activity Mode") + " +" ? Localization.GetString("Explore in Activity Mode") : Localization.GetString("Explore Route"));
                pathNameColumn.IsVisible = activity.FilePath == null;
            }

            if (Multiplayer) Title += " - Multiplayer ";

            LoadSaves();
        }

        void ResumeForm_FormClosing(object sender, WindowClosingEventArgs e)
        {
            if (SaveLoader != null)
                SaveLoader.Cancel();
            // Release the screenshot.
            (pictureBoxScreenshot.Source as Bitmap)?.Dispose();
            pictureBoxScreenshot.Source = null;
        }

        void LoadSaves()
        {
            if (SaveLoader != null)
                SaveLoader.Cancel();

            var warning = "";
            SaveLoader = new Task<List<Save>>(this, () =>
            {
                var saves = new List<Save>();
                var directory = UserSettings.UserDataFolder;
                var prefix = String.Empty;

                if (SelectedAction == MainWindow.UserAction.SinglePlayerTimetableGame)
                {
                    prefix = Path.GetFileName(Route.Path) + " " + Path.GetFileNameWithoutExtension(Timetable.fileName);
                }
                else if (Activity.FilePath != null)
                {
                    prefix = Path.GetFileNameWithoutExtension(Activity.FilePath);
                }
                else if (Activity.Name == "- " + Localization.GetString("Explore Route") + " -")
                {
                    prefix = Path.GetFileName(Route.Path);
                }
                // Explore in activity mode
                else
                {
                    prefix = "ea$" + Path.GetFileName(Route.Path) + "$";
                }


                if (Directory.Exists(directory))
                {
                    foreach (var saveFile in Directory.GetFiles(directory, prefix + "*.save"))
                    {
                        try
                        {
                            // SavePacks are all in the same folder and activities may have the same name
                            // (e.g. Short Passenger Run shrtpass.act) but belong to a different route,
                            // so pick only the activities for the current route.
                            var save = new Save(saveFile, Settings.YoungestVersionFailedToRestore);
                            if (save.RouteName == Route.Name)
                            {
                                if (!save.IsMultiplayer ^ Multiplayer)
                                    saves.Add(save);
                            }
                            else    // In case you receive a SavePack where the activity is recognised but the route has been renamed.
                                    // Checks the route is not in your list of routes.
                                    // If so, add it with a warning.
                            {
                                if (!MainForm.Routes.Any(el => el.Name == save.RouteName))
                                {
                                    if (!save.IsMultiplayer ^ Multiplayer)
                                        saves.Add(save);
                                    // Save a warning to show later.
                                    warning += Localization.GetStringFmt("Warning: Save {0} found from a route with an unexpected name:\n{1}.\n\n", save.File, save.RouteName);
                                }
                            }
                        }
                        catch { }
                    }
                }
                return saves.OrderBy(s => s.RealTime).Reverse().ToList();
            }, async (saves) =>
            {
                Saves = saves;
                gridSaves.ItemsSource = Saves;
                labelInvalidSaves.Text = Localization.GetString(
                    "To prevent crashes and unexpected behaviour, Open Rails invalidates games saved from older versions if they fail to restore.\n") +
                    Localization.GetStringFmt("{0} of {1} saves for this route are no longer valid.", Saves.Count(s => (s.Valid == false)), Saves.Count);
                if (Saves.Count > 0)
                    gridSaves.SelectedIndex = 0;
                gridSaves_SelectionChanged(null, null);
                // Show warning after the list has been updated as this is more useful.
                if (warning != "")
                    await MessageBox.Show(this, warning, App.Caption);
            });
        }

        async System.Threading.Tasks.Task<bool> AcceptUseOfNonvalidSave(Save save)
        {
            var reply = await MessageBox.Show(this, Localization.GetStringFmt(
                "Restoring from a save made by version {1} of {0} may be incompatible with current version {2}. Please do not report any problems that may result.\n\nContinue?",
                App.ProductName, save.VersionOrBuild, VersionInfo.VersionOrBuild),
                App.Caption, MessageBoxButtons.YesNo);
            return reply == MessageBoxResult.Yes;
        }

        async void ResumeSave()
        {
            var save = CurrentSave;
            if (save == null)
                return;

            if (save.Valid != false) // I.e. true or null. Check is for safety as buttons should be disabled if Save is invalid.
            {
                if (await Found(save))
                {
                    if (save.Valid == null)
                        if (!await AcceptUseOfNonvalidSave(save))
                            return;

                    SelectedSaveFile = save.File;
                    var selectedAction = SelectedAction;
                    switch (SelectedAction)
                    {
                        case MainWindow.UserAction.SinglePlayerTimetableGame:
                            selectedAction = MainWindow.UserAction.SinglePlayerResumeTimetableGame;
                            break;
                        case MainWindow.UserAction.SingleplayerNewGame:
                            selectedAction = MainWindow.UserAction.SingleplayerResumeSave;
                            break;
                        case MainWindow.UserAction.MultiplayerClient:
                            selectedAction = MainWindow.UserAction.MultiplayerClientResumeSave;
                            break;
                        case MainWindow.UserAction.MultiplayerServer:
                            selectedAction = MainWindow.UserAction.MultiplayerServerResumeSave;
                            break;
                    }
                    SelectedAction = selectedAction;
                    Close(DialogResult.OK);
                }
            }
        }

        void gridSaves_SelectionChanged(object sender, EventArgs e)
        {
            // Clean up old thumbnail.
            if (pictureBoxScreenshot.Source is Bitmap oldImage)
            {
                pictureBoxScreenshot.Source = null;
                oldImage.Dispose();
            }

            // Load new thumbnail.
            if (gridSaves.SelectedItems.Count > 0)
            {
                var save = CurrentSave;
                if (save != null)
                {
                    var thumbFileName = Path.ChangeExtension(save.File, "png");
                    if (File.Exists(thumbFileName))
                    {
                        try
                        {
                            // Load into memory so the file isn't locked (it may be deleted).
                            using (var stream = new MemoryStream(File.ReadAllBytes(thumbFileName)))
                                pictureBoxScreenshot.Source = new Bitmap(stream);
                        }
                        catch { }
                    }

                    buttonDelete.IsEnabled = true;
                    buttonResume.IsEnabled = (save.Valid != false); // I.e. either true or null
                    var replayFileName = Path.ChangeExtension(save.File, "replay");
                    buttonReplayFromPreviousSave.IsEnabled = (save.Valid != false) && File.Exists(replayFileName) && !Multiplayer;
                    buttonReplayFromStart.IsEnabled = File.Exists(replayFileName) && !Multiplayer; // can Replay From Start even if Save is invalid.
                }
                else
                {
                    buttonDelete.IsEnabled = buttonResume.IsEnabled = buttonReplayFromStart.IsEnabled = buttonReplayFromPreviousSave.IsEnabled = false;
                }
            }
            else
            {
                buttonDelete.IsEnabled = buttonResume.IsEnabled = buttonReplayFromStart.IsEnabled = buttonReplayFromPreviousSave.IsEnabled = false;
            }

            buttonDeleteInvalid.IsEnabled = true; // Always enabled because there may be Saves to be deleted for other activities not just this one.
            buttonUndelete.IsEnabled = Directory.Exists(UserSettings.DeletedSaveFolder) && Directory.GetFiles(UserSettings.DeletedSaveFolder).Length > 0;
        }

        void gridSaves_DoubleClick(object sender, EventArgs e)
        {
            ResumeSave();
        }

        void pictureBoxScreenshot_Click(object sender, EventArgs e)
        {
            ResumeSave();
        }

        void buttonResume_Click(object sender, EventArgs e)
        {
            ResumeSave();
        }

        void buttonDelete_Click(object sender, EventArgs e)
        {
            var selectedRows = gridSaves.SelectedItems.OfType<Save>().ToList();
            if (selectedRows.Count > 0)
            {
                gridSaves.SelectedItems.Clear();

                if (!Directory.Exists(UserSettings.DeletedSaveFolder))
                    Directory.CreateDirectory(UserSettings.DeletedSaveFolder);

                foreach (var save in selectedRows)
                {
                    foreach (var fileName in new[]
                        { Path.GetFileName(save.File)
                        , Path.ChangeExtension(Path.GetFileName(save.File), "png")
                        , Path.ChangeExtension(Path.GetFileName(save.File), "replay")
                        , Path.ChangeExtension(Path.GetFileName(save.File), "txt")
                        , Path.ChangeExtension(Path.GetFileName(save.File), "evaluation.txt")
                        }
                    )
                    {
                        try
                        {
                            File.Move(Path.Combine(UserSettings.UserDataFolder, fileName), Path.Combine(UserSettings.DeletedSaveFolder, fileName));
                        }
                        catch { }
                    }
                }

                LoadSaves();
            }
        }

        void buttonUndelete_Click(object sender, EventArgs e)
        {
            if (Directory.Exists(UserSettings.DeletedSaveFolder))
            {
                foreach (var filePath in Directory.GetFiles(UserSettings.DeletedSaveFolder))
                {
                    try
                    {
                        File.Move(filePath, Path.Combine(UserSettings.UserDataFolder, Path.GetFileName(filePath)));
                    }
                    catch { }
                }

                Directory.Delete(UserSettings.DeletedSaveFolder);

                LoadSaves();
            }
        }

        async void buttonDeleteInvalid_Click(object sender, EventArgs e)
        {
            gridSaves.SelectedItems.Clear();

            var directory = UserSettings.UserDataFolder;
            if (Directory.Exists(directory))
            {
                var deletes = 0;
                foreach (var saveFile in Directory.GetFiles(directory, "*.save"))
                {
                    var save = new Save(saveFile, Settings.YoungestVersionFailedToRestore);
                    if (save.Valid == false)
                    {
                        foreach (var fileName in new[]
                            { save.File
                            , Path.ChangeExtension(save.File, "png")
                            , Path.ChangeExtension(save.File, "replay")
                            , Path.ChangeExtension(save.File, "txt")
                            , Path.ChangeExtension(save.File, "evaluation.txt")
                            }
                        )
                        {
                            try
                            {
                                File.Delete(fileName);
                            }
                            catch { }
                        }
                        deletes++;
                    }
                }
                await MessageBox.Show(this, Localization.GetStringFmt("{0} invalid saves have been deleted.", deletes), App.Caption);
            }
            LoadSaves();
        }

        private void buttonReplayFromStart_Click(object sender, EventArgs e)
        {
            SelectedAction = MainWindow.UserAction.SingleplayerReplaySave;
            InitiateReplay(true);
        }

        private void buttonReplayFromPreviousSave_Click(object sender, EventArgs e)
        {
            SelectedAction = MainWindow.UserAction.SingleplayerReplaySaveFromSave;
            InitiateReplay(false);
        }

        private async void InitiateReplay(bool fromStart)
        {
            var save = CurrentSave;
            if (save != null && await Found(save))
            {
                if (fromStart && (save.Valid == null))
                    if (!await AcceptUseOfNonvalidSave(save))
                        return;

                SelectedSaveFile = save.File;
                Settings.ReplayPauseBeforeEnd = checkBoxReplayPauseBeforeEnd.IsChecked == true;
                Settings.ReplayPauseBeforeEndS = (int)(numericReplayPauseBeforeEnd.Value ?? 0);
                Close(DialogResult.OK); // Anything but DialogResult.Cancel
            }
        }

        private async void buttonImportExportSaves_Click(object sender, EventArgs e)
        {
            var save = CurrentSave;
            await new ImportExportSaveWindow(save).ShowDialog(this);
            LoadSaves();
        }

        /// <summary>
        /// Saves may come from other, foreign installations (i.e. not this PC).
        /// They can be replayed or resumed on this PC but they will contain activity / path / consist filenames
        /// and these may be inappropriate for this PC, typically having a different path.
        /// This method tries to use the paths in the Save if they exist on the current PC.
        /// If not, it prompts the user to locate a matching file from those on the current PC.
        ///
        /// The save file is then modified to contain filename(s) from the current PC instead.
        /// </summary>
        public async System.Threading.Tasks.Task<bool> Found(Save save)
        {
            if (SelectedAction == MainWindow.UserAction.SinglePlayerTimetableGame)
            {
                return true; // no additional actions required for timetable resume
            }
            else
            {
                try
                {
                    BinaryReader inf = new BinaryReader(new FileStream(save.File, FileMode.Open, FileAccess.Read));
                    var version = inf.ReadString();
                    var build = inf.ReadString();
                    var routeName = inf.ReadString();
                    var pathName = inf.ReadString();
                    var gameTime = inf.ReadInt32();
                    var realTime = inf.ReadInt64();
                    var currentTileX = inf.ReadSingle();
                    var currentTileZ = inf.ReadSingle();
                    var initialTileX = inf.ReadSingle();
                    var initialTileZ = inf.ReadSingle();
                    var tempInt = inf.ReadInt32();
                    var savedArgs = new string[tempInt];
                    for (var i = 0; i < savedArgs.Length; i++)
                        savedArgs[i] = inf.ReadString();

                    // Re-locate files if saved on another PC
                    var rewriteNeeded = false;
                    // savedArgs[0] contains Activity or Path filepath
                    var filePath = savedArgs[0];
                    if (!System.IO.File.Exists(filePath))
                    {
                        // Show the dialog and get result.
                        var newPath = await FindFile(filePath);
                        if (newPath == null)
                        {
                            inf.Close();
                            return false;
                        }
                        rewriteNeeded = true;
                        savedArgs[0] = newPath;
                    }
                    if (savedArgs.Length > 1)  // Explore, not Activity
                    {
                        // savedArgs[1] contains Consist filepath
                        filePath = savedArgs[1];
                        if (!System.IO.File.Exists(filePath))
                        {
                            // Show the dialog and get result.
                            var newPath = await FindFile(filePath);
                            if (newPath == null)
                            {
                                inf.Close();
                                return false;
                            }
                            rewriteNeeded = true;
                            savedArgs[1] = newPath;
                        }
                    }
                    if (rewriteNeeded)
                    {
                        using (BinaryWriter outf = new BinaryWriter(new FileStream(save.File + ".tmp", FileMode.Create, FileAccess.Write)))
                        {
                            // copy the start of the file
                            outf.Write(version);
                            outf.Write(build);
                            outf.Write(routeName);
                            outf.Write(pathName);
                            outf.Write(gameTime);
                            outf.Write(realTime);
                            outf.Write(currentTileX);
                            outf.Write(currentTileZ);
                            outf.Write(initialTileX);
                            outf.Write(initialTileZ);
                            outf.Write(savedArgs.Length);
                            // copy the pars which may have changed
                            for (var i = 0; i < savedArgs.Length; i++)
                                outf.Write(savedArgs[i]);
                            // copy the rest of the file
                            while (inf.BaseStream.Position < inf.BaseStream.Length)
                            {
                                outf.Write(inf.ReadByte());
                            }
                        }
                        inf.Close();
                        File.Replace(save.File + ".tmp", save.File, null);
                    }
                    else
                    {
                        inf.Close();
                    }
                }
                catch
                {
                }
            }
            return true;
        }

        /// <summary>
        /// Asks the user to locate a file referenced by a save made elsewhere.
        /// </summary>
        async System.Threading.Tasks.Task<string> FindFile(string filePath)
        {
            // Saves may have been made on Windows, so the path separators may be '\'.
            var fileName = filePath.Replace('\\', '/');
            fileName = fileName.Substring(fileName.LastIndexOf('/') + 1);
            var options = new FilePickerOpenOptions
            {
                Title = "Find location for file " + filePath,
                AllowMultiple = false,
                FileTypeFilter = new[]
                {
                    new FilePickerFileType(fileName) { Patterns = new[] { fileName } },
                    new FilePickerFileType(Localization.GetString("All files") + " (*.*)") { Patterns = new[] { "*" } },
                },
            };
            var mstsBase = GetMstsBase();
            if (mstsBase != null)
                options.SuggestedStartLocation = await StorageProvider.TryGetFolderFromPathAsync(mstsBase);
            var files = await StorageProvider.OpenFilePickerAsync(options);
            return files.FirstOrDefault()?.TryGetLocalPath();
        }

        /// <summary>
        /// The MSTS installation folder; it is found from the registry, so only on Windows.
        /// </summary>
        static string GetMstsBase()
        {
            if (!OperatingSystem.IsWindows())
                return null;
            try
            {
                return MSTSPath.Base();
            }
            catch
            {
                return null;
            }
        }
    }
}
