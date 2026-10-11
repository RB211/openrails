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
using System.Diagnostics;
using System.Threading;
using ORTS.TrackViewer.UserInterface;


namespace ORTS.TrackViewer
{
    static class Program
    {
        /// <summary>
        /// The main entry point for the application.
        /// The main thread runs the MonoGame loop; the Avalonia UI (dialogs) runs on its own thread, see UiThread.
        /// </summary>
        static void Main(string[] args)
        {
            // Avalonia must be initialised before anything touches its dispatcher.
            UiThread.Start();

            using (TrackViewer trackViewer = new TrackViewer(args))
            {
                // code below is modified version from what is found in GameStateRunActivity.cs
                if (Debugger.IsAttached) // Separate code path during debugging, so IDE stops at the problem and not at the message.
                {
                    trackViewer.Run();
                }
                else
                {
                    try
                    {
                        trackViewer.Run();
                    }
                    catch (Exception error)
                    {
                        Console.Error.WriteLine(error);
                        string errorSummary = error.GetType().FullName + ": " + error.Message;
                        string message = String.Format(
                                    System.Globalization.CultureInfo.CurrentCulture,
                                    "A fatal error has occured and {0} cannot continue.\n\n" +
                                    "    {1}\n\n" +
                                    "This error may be due to bad data or a bug. ",
                                    "TrackViewer", errorSummary);
                        ShowFatalError(message);
                    }
                }
            }
            UiThread.Stop();
        }

        /// <summary>
        /// Show a message box and wait (on the main thread) until it is closed.
        /// </summary>
        static void ShowFatalError(string message)
        {
            if (!UiThread.IsAvailable) return;
            // The answer callback is posted to the (no longer running) game loop, so wait for the window to close instead.
            Dialogs.Ask(message, "TrackViewer", MessageButtons.OK, (result) => { });
            var timeout = DateTime.Now.AddMinutes(10);
            while (Dialogs.ModalIsOpen && DateTime.Now < timeout)
            {
                Thread.Sleep(100);
            }
        }
    }
}
