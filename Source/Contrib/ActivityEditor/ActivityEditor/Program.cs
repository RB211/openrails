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


using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using ActivityEditor.Engine;
using ActivityEditor.Internat;
using ActivityEditor.Preference;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Platform;
using Avalonia.Themes.Fluent;
using ORTS.Common;
using ORTS.Settings;

namespace ActivityEditor
{
    static class Program
    {
        public static IntalMngr intlMngr;
        public static string[] Arguments;
        public static string RegistryKey;     // ie @"SOFTWARE\OpenRails\ActivityEditor"
        public static string UserDataFolder;  // ie @"F:\Users\Wayne\AppData\Roaming\Open Rails"
        public static ActEditor actEditor;
        public static AEPreference aePreference { get; set; }
        /// <summary>Set when the Open Rails settings could not be loaded (e.g. the settings store is not available).</summary>
        public static string SettingsError;

        /// <summary>
        /// The main entry point for the application.
        /// </summary>
        [STAThread]
        static int Main(string[] args)
        {
            UserDataFolder = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), ApplicationInfo.ProductName ?? "Open Rails");
            if (!Directory.Exists(UserDataFolder)) Directory.CreateDirectory(UserDataFolder);
            RegistryKey = "SOFTWARE\\OpenRails\\ActivityEditor";

            var options = args.Where(a => (a.StartsWith("-") || a.StartsWith("/")));
            UserSettings settings = null;
            try
            {
                settings = new UserSettings(options);   //   GetSettings(options);
            }
            catch (Exception error)
            {
                // The settings store (registry / INI file) may not be available on this platform.
                SettingsError = error.GetBaseException().Message;
                Trace.TraceError("Unable to load Open Rails settings: {0}", error);
            }
            intlMngr = new IntalMngr(settings);

            return Init(settings, args.Where(a => !(a.StartsWith("-") || a.StartsWith("/"))).ToArray());
        }

        static int Init(UserSettings settings, string[] args)
        {
            return Init(settings, args, "");
        }

        static int Init(UserSettings settings, string[] args, string mode)
        {
            Console.WriteLine(mode.Length > 0 ? "Mode       = {0} {1}" : "Mode       = {1}", mode, args.Length == 1 ? "Activity" : "Explore");
            if (args.Length == 1)
            {
                Console.WriteLine("Activity   = {0}", args[0]);
            }
            else if (args.Length == 3)
            {
                Console.WriteLine("Activity   = {0}", args[0]);
            }
            else if (args.Length == 4)
            {
                Console.WriteLine("Activity   = {0}", args[0]);
            }
            else if (args.Length > 4)
            {
                Console.WriteLine("Path       = {0}", args[0]);
                Console.WriteLine("Consist    = {0}", args[1]);
                Console.WriteLine("Time       = {0}", args[2]);
                Console.WriteLine("Season     = {0}", args[3]);
                Console.WriteLine("Weather    = {0}", args[4]);
            }
            aePreference = AEPreference.loadXml();
            aePreference.CompleteSettings(settings);
            Arguments = args;
            //  PseudoSim va gérer l'ensemble du système
            int result = BuildAvaloniaApp().StartWithClassicDesktopLifetime(args);
            Program.aePreference.orConfig?.SaveConfig();
            Program.aePreference.saveXml();
            return result;
        }

        public static AppBuilder BuildAvaloniaApp()
        {
            return AppBuilder.Configure<App>()
                .UsePlatformDetect()
                .LogToTrace();
        }
    }

    class App : Application
    {
        public override void Initialize()
        {
            Styles.Add(new FluentTheme());
            // The route view is drawn on a white background with dark colours.
            RequestedThemeVariant = Avalonia.Styling.ThemeVariant.Light;
        }

        public override void OnFrameworkInitializationCompleted()
        {
            if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
            {
                Program.actEditor = new ActEditor();
                Program.aePreference.ActEditor = Program.actEditor;
                desktop.MainWindow = Program.actEditor;
            }
            base.OnFrameworkInitializationCompleted();
        }

        public static WindowIcon LoadIcon()
        {
            using (var stream = AssetLoader.Open(new Uri("avares://Contrib.ActivityEditor/ORTS.png")))
                return new WindowIcon(stream);
        }
    }
}
