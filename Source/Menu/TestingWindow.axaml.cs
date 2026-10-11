// COPYRIGHT 2012, 2013 by the Open Rails project.
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
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using Avalonia.Controls;
using Avalonia.Threading;
using ORTS.Menu;
using ORTS.Settings;
using Activity = ORTS.Menu.Activity;
using Path = System.IO.Path;

namespace Menu
{
    public partial class TestingWindow : Window
    {
        public class TestActivity : INotifyPropertyChanged
        {
            public string DefaultSort { get; set; }
            public string Route { get; set; }
            public string Activity { get; set; }
            public string ActivityFilePath { get; set; }
            public bool ToTest { get; set; }

            bool tested;
            public bool Tested { get => tested; set => Set(ref tested, value); }
            bool passed;
            public bool Passed { get => passed; set => Set(ref passed, value); }
            string errors;
            public string Errors { get => errors; set => Set(ref errors, value); }
            string load;
            public string Load { get => load; set => Set(ref load, value); }
            string fps;
            public string FPS { get => fps; set => Set(ref fps, value); }

            public TestActivity(Folder folder, Route route, Activity activity)
            {
                DefaultSort = folder.Name + "/" + route.Name + "/" + activity.Name;
                Route = route.Name;
                Activity = activity.Name;
                ActivityFilePath = activity.FilePath;
            }

            public event PropertyChangedEventHandler PropertyChanged;

            void Set<T>(ref T field, T value, [CallerMemberName] string name = null)
            {
                field = value;
                // The test runner works in the background; the grid must be told on the UI thread.
                Dispatcher.UIThread.Post(() => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name)));
            }
        }

        Task<List<TestActivity>> TestActivityLoader;
        List<TestActivity> TestActivities = new List<TestActivity>();

        Task<int> TestActivitiesRunner;
        bool ClearedLogs;

        readonly MainWindow MainForm;
        readonly UserSettings Settings;
        readonly string SummaryFilePath = Path.Combine(UserSettings.UserDataFolder, "TestingSummary.csv");
        readonly string LogFilePath = Path.Combine(UserSettings.UserDataFolder, "TestingLog.txt");

        public TestingWindow(MainWindow mainForm, UserSettings settings)
        {
            InitializeComponent();
            Icon = App.LoadIcon();

            MainForm = mainForm;
            Settings = settings;

            buttonTestAll.Click += buttonTestAll_Click;
            buttonTest.Click += buttonTest_Click;
            buttonCancel.Click += buttonCancel_Click;
            buttonNoSort.Click += buttonNoSort_Click;
            buttonSummary.Click += buttonSummary_Click;
            buttonDetails.Click += buttonDetails_Click;
            gridTestActivities.SelectionChanged += (sender, e) => UpdateButtons();
            Closing += TestingForm_FormClosing;

            UpdateButtons();

            LoadActivities();
        }

        void TestingForm_FormClosing(object sender, WindowClosingEventArgs e)
        {
            if (TestActivityLoader != null)
                TestActivityLoader.Cancel();
            if (TestActivitiesRunner != null)
                TestActivitiesRunner.Cancel();
        }

        void UpdateButtons()
        {
            buttonTestAll.IsEnabled = TestActivitiesRunner == null && TestActivities.Count > 0;
            buttonTest.IsEnabled = TestActivitiesRunner == null && gridTestActivities.SelectedItems.Count > 0;
            buttonCancel.IsEnabled = TestActivitiesRunner != null && !TestActivitiesRunner.Cancelled;
            buttonSummary.IsEnabled = TestActivitiesRunner == null && File.Exists(SummaryFilePath);
            buttonDetails.IsEnabled = TestActivitiesRunner == null && File.Exists(LogFilePath);
        }

        void LoadActivities()
        {
            if (TestActivityLoader != null)
                TestActivityLoader.Cancel();

            TestActivityLoader = new Task<List<TestActivity>>(this, () =>
            {
                return (from f in Folder.GetFolders(Settings)
                        from r in Route.GetRoutes(f)
                        from a in Activity.GetActivities(f, r)
                        where !(a is ORTS.Menu.ExploreActivity)
                        orderby a.Name
                        select new TestActivity(f, r, a)).ToList();
            }, (testActivities) =>
            {
                TestActivities = testActivities;
                ShowInDefaultOrder();
                UpdateButtons();
            });
        }

        void ShowInDefaultOrder()
        {
            // Setting the items again also clears any sorting by column.
            gridTestActivities.ItemsSource = TestActivities.OrderBy(a => a.DefaultSort, StringComparer.CurrentCulture).ToList();
        }

        void buttonTestAll_Click(object sender, EventArgs e)
        {
            TestMarkedActivities(gridTestActivities.ItemsSource.Cast<TestActivity>());
        }

        void buttonTest_Click(object sender, EventArgs e)
        {
            var selected = gridTestActivities.SelectedItems.Cast<TestActivity>().ToList();
            TestMarkedActivities(gridTestActivities.ItemsSource.Cast<TestActivity>().Where(a => selected.Contains(a)));
        }

        void buttonCancel_Click(object sender, EventArgs e)
        {
            TestActivitiesRunner.Cancel();
            UpdateButtons();
        }

        void buttonNoSort_Click(object sender, EventArgs e)
        {
            ShowInDefaultOrder();
        }

        void buttonSummary_Click(object sender, EventArgs e)
        {
            ExternalProcess.Open(SummaryFilePath);
        }

        void buttonDetails_Click(object sender, EventArgs e)
        {
            ExternalProcess.Open(LogFilePath);
        }

        void TestMarkedActivities(IEnumerable<TestActivity> rows)
        {
            if (TestActivitiesRunner != null)
                TestActivitiesRunner.Cancel();

            // Force the enumeration to be evaluated so that when we run the code in the background it doesn't matter if the grid changes.
            var items = rows.ToList();
            var overrideSettings = checkBoxOverride.IsChecked == true;

            Task<int> runner = null;
            runner = TestActivitiesRunner = new Task<int>(this, () =>
            {
                // "-option" works on every platform; "/option" is a path on Linux.
                var parameters = new List<string> {
                    "-Test",
                    "-Logging",
                    "-LoggingFilename=" + Path.GetFileName(LogFilePath),
                    "-LoggingPath=" + Path.GetDirectoryName(LogFilePath),
                    "-Profiling",
                    "-ProfilingTime=10",
                    "-ShowErrorDialogs=False",
                };
                if (overrideSettings)
                    parameters.Add("-Skip-User-Settings");

                if (!ClearedLogs)
                {
                    using (var writer = File.CreateText(SummaryFilePath))
                        writer.WriteLine("Route,Activity,Passed,Errors,Warnings,Infos,Loading,FPS");
                    using (var writer = File.CreateText(LogFilePath))
                        writer.Flush();
                    ClearedLogs = true;
                }

                var summaryFilePosition = 0L;
                using (var reader = File.OpenText(SummaryFilePath))
                    summaryFilePosition = reader.BaseStream.Length;

                foreach (var item in items)
                {
                    var process = ExternalProcess.Start(MainForm.RunActivityProgram, parameters.Concat(new[] { item.ActivityFilePath }));
                    process.WaitForExit();

                    item.Tested = true;
                    item.Passed = process.ExitCode == 0;
                    using (var reader = File.OpenText(SummaryFilePath))
                    {
                        reader.BaseStream.Seek(summaryFilePosition, SeekOrigin.Begin);
                        var line = reader.ReadLine();
                        if (!String.IsNullOrEmpty(line) && reader.EndOfStream)
                        {
                            var csv = line.Split(',');
                            item.Errors = String.Format("{0}/{1}/{2}", int.Parse(csv[3]), int.Parse(csv[4]), int.Parse(csv[5]));
                            item.Load = String.Format("{0,6:F1}s", float.Parse(csv[6]));
                            item.FPS = String.Format("{0,6:F1}", float.Parse(csv[7]));
                        }
                        else
                        {
                            reader.ReadToEnd();
                            item.Passed = false;
                        }
                        summaryFilePosition = reader.BaseStream.Position;
                    }
                    if (runner.Cancelled)
                        break;

                    Dispatcher.UIThread.Post(() => gridTestActivities.ScrollIntoView(item, null));
                }
                return 0;
            }, () =>
            {
                TestActivitiesRunner = null;
                UpdateButtons();
            });
            UpdateButtons();
        }
    }
}
