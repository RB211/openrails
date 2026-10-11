// COPYRIGHT 2009, 2010, 2011, 2012, 2013 by the Open Rails project.
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
using System.Diagnostics;
using Avalonia;

namespace Menu
{
    static class Program
    {
        [STAThread]
        static int Main(string[] args)
        {
            if (Debugger.IsAttached)
                return BuildAvaloniaApp().StartWithClassicDesktopLifetime(args);

            try
            {
                return BuildAvaloniaApp().StartWithClassicDesktopLifetime(args);
            }
            catch (Exception error)
            {
                // The UI is gone by now, so all we can do is report to the console.
                Console.Error.WriteLine(error);
                return 1;
            }
        }

        // Also used by the Avalonia designer.
        public static AppBuilder BuildAvaloniaApp()
        {
            return AppBuilder.Configure<App>()
                .UsePlatformDetect()
                .LogToTrace();
        }

        /// <summary>
        /// Builds the RunActivity command line for the action chosen in the main window.
        /// Each element is one argument; they are passed to the process via <see cref="ProcessStartInfo.ArgumentList"/>
        /// so no quoting is needed, whatever the platform.
        /// </summary>
        public static List<string> GetRunActivityArguments(MainWindow mainWindow)
        {
            var parameters = new List<string>();
            switch (mainWindow.SelectedAction)
            {
                case MainWindow.UserAction.SingleplayerNewGame:
                    parameters.Add("-start");
                    break;
                case MainWindow.UserAction.SingleplayerResumeSave:
                    parameters.Add("-resume");
                    break;
                case MainWindow.UserAction.SingleplayerReplaySave:
                    parameters.Add("-replay");
                    break;
                case MainWindow.UserAction.SingleplayerReplaySaveFromSave:
                    parameters.Add("-replay_from_save");
                    break;
                case MainWindow.UserAction.MultiplayerClient:
                    parameters.Add("-multiplayerclient");
                    break;
                case MainWindow.UserAction.MultiplayerServer:
                    parameters.Add("-multiplayerserver");
                    break;
                case MainWindow.UserAction.SinglePlayerTimetableGame:
                    parameters.Add("-start");
                    break;
                case MainWindow.UserAction.SinglePlayerResumeTimetableGame:
                    parameters.Add("-resume");
                    break;
                case MainWindow.UserAction.MultiplayerClientResumeSave:
                    parameters.Add("-multiplayerclient");
                    break;
                case MainWindow.UserAction.MultiplayerServerResumeSave:
                    parameters.Add("-multiplayerserver");
                    break;
            }
            switch (mainWindow.SelectedAction)
            {
                case MainWindow.UserAction.SingleplayerNewGame:
                case MainWindow.UserAction.MultiplayerClient:
                case MainWindow.UserAction.MultiplayerServer:
                    if (mainWindow.SelectedActivity is ORTS.Menu.DefaultExploreActivity exploreActivity)
                    {
                        parameters.AddRange(new[] {
                            "-explorer",
                            exploreActivity.Path.FilePath,
                            exploreActivity.Consist.FilePath,
                            exploreActivity.StartTime,
                            ((int)exploreActivity.Season).ToString(),
                            ((int)exploreActivity.Weather).ToString(),
                        });
                    }
                    else if (mainWindow.SelectedActivity is ORTS.Menu.ExploreThroughActivity exploreThroughActivity)
                    {
                        parameters.AddRange(new[] {
                            "-exploreactivity",
                            exploreThroughActivity.Path.FilePath,
                            exploreThroughActivity.Consist.FilePath,
                            exploreThroughActivity.StartTime,
                            ((int)exploreThroughActivity.Season).ToString(),
                            ((int)exploreThroughActivity.Weather).ToString(),
                        });
                    }
                    else
                    {
                        parameters.Add("-activity");
                        parameters.Add(mainWindow.SelectedActivity.FilePath);
                    }
                    break;
                case MainWindow.UserAction.SingleplayerResumeSave:
                case MainWindow.UserAction.SingleplayerReplaySave:
                case MainWindow.UserAction.SingleplayerReplaySaveFromSave:
                case MainWindow.UserAction.MultiplayerClientResumeSave:
                case MainWindow.UserAction.MultiplayerServerResumeSave:
                    parameters.Add(mainWindow.SelectedSaveFile);
                    break;
                case MainWindow.UserAction.SinglePlayerTimetableGame:
                    parameters.AddRange(new[] {
                        "-timetable",
                        mainWindow.SelectedTimetableSet.fileName,
                        $"{mainWindow.SelectedTimetable}:{mainWindow.SelectedTimetableTrain}",
                        mainWindow.SelectedTimetableSet.Day.ToString(),
                        mainWindow.SelectedTimetableSet.Season.ToString(),
                        mainWindow.SelectedTimetableSet.Weather.ToString(),
                    });
                    if (!String.IsNullOrEmpty(mainWindow.SelectedTimetableSet.WeatherFile))
                        parameters.Add(mainWindow.SelectedTimetableSet.WeatherFile);
                    break;
                case MainWindow.UserAction.SinglePlayerResumeTimetableGame:
                    parameters.Add(mainWindow.SelectedSaveFile);
                    break;
            }
            return parameters;
        }
    }
}
