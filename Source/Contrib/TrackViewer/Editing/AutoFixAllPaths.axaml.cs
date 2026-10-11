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

using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using Avalonia.Controls;
using Avalonia.Interactivity;

using ORTS.TrackViewer.Drawing;
using ORTS.TrackViewer.UserInterface;
using ORTS.Menu;

namespace ORTS.TrackViewer.Editing
{
    /// <summary>
    /// Interaction logic for AutoFixAllPaths.axaml
    /// The fixing itself is done on the game thread (FixallAndShowResults); the window showing the results
    /// lives on the Avalonia UI thread.
    /// </summary>
    public partial class AutoFixAllPaths : Window
    {
        readonly Dictionary<string, List<string>> pathsThatAre;
        readonly List<PathEditor> modifiedPaths;

        /// <summary>Parameterless constructor for the XAML loader only</summary>
        public AutoFixAllPaths() { InitializeComponent(); }

        private AutoFixAllPaths(Dictionary<string, List<string>> pathsThatAre, List<PathEditor> modifiedPaths)
        {
            this.pathsThatAre = pathsThatAre;
            this.modifiedPaths = modifiedPaths;
            InitializeComponent();
            FillLists();
        }

        /// <summary>
        /// Load all the paths, fix them, and show the user what to do with the fixed paths.
        /// To be called on the game thread.
        /// </summary>
        /// <param name="routeData">The route information that contains track data base and track section data</param>
        /// <param name="drawTrackDB">The drawn tracks to know about where the mouse is</param>
        /// <param name="Paths">The list of paths that are availabel and that need to be checked and possibly fixed</param>
        /// <param name="callback">Callback that will be called showing the current processing that is being done</param>
        public static void FixallAndShowResults(RouteData routeData, DrawTrackDB drawTrackDB, Collection<Path> Paths, Action<string> callback)
        {
            var pathsThatAre = new Dictionary<string, List<string>>
            {
                ["UnmodifiedFine"] = new List<string>(),
                ["UnmodifiedBroken"] = new List<string>(),
                ["ModifiedFine"] = new List<string>(),
                ["ModifiedBroken"] = new List<string>(),
            };
            var modifiedPaths = new List<PathEditor>();

            // Loop through all available paths and fix each of them
            foreach (ORTS.Menu.Path path in Paths)
            {
                callback(TrackViewer.catalog.GetString("Processing .pat file ") + path.FilePath);
                string pathName = ORTS.TrackViewer.UserInterface.MenuControl.MakePathMenyEntryName(path);
                PathEditor pathFixer = new PathEditor(routeData, drawTrackDB, path);
                pathFixer.AutoFixAllBrokenNodes();
                if (pathFixer.HasModifiedPath)
                {
                    if (pathFixer.HasBrokenPath)
                    {
                        pathsThatAre["ModifiedBroken"].Add(pathName);
                    }
                    else
                    {
                        pathsThatAre["ModifiedFine"].Add(pathName);
                    }
                    modifiedPaths.Add(pathFixer);
                }
                else
                {
                    if (pathFixer.HasBrokenPath)
                    {
                        pathsThatAre["UnmodifiedBroken"].Add(pathName);
                    }
                    else
                    {
                        pathsThatAre["UnmodifiedFine"].Add(pathName);
                    }
                }
            }

            Dialogs.ShowModal(() =>
            {
                var window = new AutoFixAllPaths(pathsThatAre, modifiedPaths);
                TrackViewer.Localize(window);
                return window;
            }, null);
        }

        private void FillLists()
        {
            void Fill(string status, TextBlock label, ListBox list, bool enableSave)
            {
                if (pathsThatAre[status].Count == 0) return;
                label.IsVisible = true;
                list.IsVisible = true;
                list.ItemsSource = pathsThatAre[status];
                if (enableSave)
                {
                    SaveDirect.IsEnabled = true;
                    SaveAndConfirm.IsEnabled = true;
                }
            }
            Fill("ModifiedBroken", ModifiedBrokenLabel, ModifiedBrokenList, true);
            Fill("ModifiedFine", ModifiedFineLabel, ModifiedFineList, true);
            Fill("UnmodifiedBroken", UnmodifiedBrokenLabel, UnmodifiedBrokenList, false);
            Fill("UnmodifiedFine", UnmodifiedFineLabel, UnmodifiedFineList, false);
        }

        private void Cancel_Click(object sender, RoutedEventArgs e)
        {
            this.Close();
        }

        private void SaveDirect_Click(object sender, RoutedEventArgs e)
        {
            List<PathEditor> paths = modifiedPaths;
            GameThread.Post(() =>
            {
                foreach (PathEditor modifiedPath in paths)
                {
                    SavePatFile.WritePatFileDirect(modifiedPath.CurrentTrainPath);
                }
            });
            this.Close();
        }

        private void SaveAndConfirm_Click(object sender, RoutedEventArgs e)
        {
            List<PathEditor> paths = modifiedPaths;
            GameThread.Post(() => SaveNext(paths, 0));
            this.Close();
        }

        /// <summary>Save the paths one by one, each one after the user has handled the dialogs of the previous</summary>
        private static void SaveNext(List<PathEditor> paths, int index)
        {
            if (index >= paths.Count) return;
            SavePatFile.WritePatFile(paths[index].CurrentTrainPath, () => SaveNext(paths, index + 1));
        }

        private void SaveOverview_Click(object sender, RoutedEventArgs e)
        {
            var overview = pathsThatAre;
            Dialogs.SaveFile(TrackViewer.catalog.GetString("Save overview"), "modifiedpaths.txt", ".txt", "Text documents (.txt)", null, (fullFilePath) =>
            {
                if (String.IsNullOrEmpty(fullFilePath)) return;

                using (System.IO.StreamWriter file = new System.IO.StreamWriter(fullFilePath, false, System.Text.Encoding.Unicode))
                {
                    string[] statusses = { "ModifiedBroken", "ModifiedFine", "UnmodifiedBroken", "UnmodifiedFine" };
                    foreach (string status in statusses)
                    {
                        file.WriteLine("");
                        file.WriteLine(status + ":");
                        foreach (string pathName in overview[status])
                        {
                            file.WriteLine(pathName);
                        }
                    }
                }
            });
        }
    }
}
