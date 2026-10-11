// COPYRIGHT 2009 - 2026 by the Open Rails project.
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
using System.Reflection;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;
using Avalonia.Media.Imaging;
using Avalonia.Platform;
using Avalonia.Threading;
using ORTS.Common;

namespace Menu
{
    public partial class App : Application
    {
        /// <summary>
        /// Equivalent of the WinForms <c>Application.ProductName</c>: the product name of the entry assembly ("Open Rails").
        /// </summary>
        public static string ProductName { get; } =
            Assembly.GetEntryAssembly()?.GetCustomAttribute<AssemblyProductAttribute>()?.Product ?? "Open Rails";

        /// <summary>
        /// The caption used for message boxes, like the WinForms version: product name and version.
        /// </summary>
        public static string Caption => ProductName + " " + VersionInfo.VersionOrBuild;

        public override void Initialize()
        {
            AvaloniaXamlLoader.Load(this);
        }

        public override void OnFrameworkInitializationCompleted()
        {
            Dispatcher.UIThread.UnhandledException += (sender, e) =>
            {
                // Mirror the WinForms behaviour of reporting the error rather than silently dying.
                Console.Error.WriteLine(e.Exception);
                e.Handled = true;
                _ = MessageBox.Show(null, e.Exception.ToString(), Caption);
            };

            if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
                desktop.MainWindow = new MainWindow();

            base.OnFrameworkInitializationCompleted();
        }

        /// <summary>
        /// Loads a bitmap from the embedded resources (Source/Menu/Resources).
        /// </summary>
        public static Bitmap LoadBitmap(string fileName)
        {
            using (var stream = AssetLoader.Open(new Uri("avares://Menu/Resources/" + fileName)))
                return new Bitmap(stream);
        }

        public static WindowIcon LoadIcon()
        {
            using (var stream = AssetLoader.Open(new Uri("avares://Menu/Resources/ORTS.ico")))
                return new WindowIcon(stream);
        }
    }
}
