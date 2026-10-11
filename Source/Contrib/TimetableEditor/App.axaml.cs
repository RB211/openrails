using System;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;
using Avalonia.Platform;

namespace Orts.TimetableEditor
{
    public partial class App : Application
    {
        public override void Initialize()
        {
            AvaloniaXamlLoader.Load(this);
        }

        public override void OnFrameworkInitializationCompleted()
        {
            if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
            {
                var window = new MainWindow();
                desktop.MainWindow = window;
                // Optional command line argument: a timetable file to open.
                if (desktop.Args != null && desktop.Args.Length > 0 && System.IO.File.Exists(desktop.Args[0]))
                {
                    var file = desktop.Args[0];
                    window.Opened += async (s, e) => await window.OpenTimetableFile(file);
                }
            }
            base.OnFrameworkInitializationCompleted();
        }

        public static WindowIcon LoadIcon()
        {
            using (var stream = AssetLoader.Open(new Uri("avares://Contrib.TimetableEditor/ORTS.png")))
                return new WindowIcon(stream);
        }
    }

    static class Program
    {
        [STAThread]
        static int Main(string[] args)
        {
            return BuildAvaloniaApp().StartWithClassicDesktopLifetime(args);
        }

        public static AppBuilder BuildAvaloniaApp()
        {
            return AppBuilder.Configure<App>()
                .UsePlatformDetect()
                .LogToTrace();
        }
    }
}
