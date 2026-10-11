using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Platform.Storage;
using Orts.TimetableEditor.Model;
using Orts.TimetableEditor.ViewModel;
using Orts.TimetableEditor.Views;

namespace Orts.TimetableEditor
{
    /// <summary>
    /// Interaktionslogik für MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        public ObservableCollection<TabItem> tabs;
        private MainViewModel mainViewModel;
        private string RoutePath;
        private string tdbfilename;
        private string consistpath;
        public MainWindow()
        {
            InitializeComponent();
            Icon = App.LoadIcon();
            mainViewModel = new MainViewModel();
            MainView.DataContext = mainViewModel;
        }

        /// <summary>The route's Activities/OpenRails folder, matched case-insensitively.</summary>
        private string OpenRailsFolder => PathCase.Combine(RoutePath, "Activities", "OpenRails");

        private void Close_Click(object sender, RoutedEventArgs e)
        {
            Close();
        }

        private void GetConsistsPath()
        {
            // RoutePath is <content>/Routes/<route>; consists are in <content>/Trains/Consists.
            string contentPath = Path.GetDirectoryName(Path.GetDirectoryName(RoutePath));
            consistpath = PathCase.Combine(contentPath, "Trains", "Consists");
        }

        private async Task<string> PickFile(string title, string filterName, params string[] patterns)
        {
            // MSTS file names have arbitrary casing; offer upper-case variants of the patterns as well.
            var allPatterns = patterns.Concat(patterns.Select(p => p.ToUpperInvariant())).Distinct().ToList();
            var files = await StorageProvider.OpenFilePickerAsync(new FilePickerOpenOptions
            {
                Title = title,
                AllowMultiple = false,
                FileTypeFilter = new[]
                {
                    new FilePickerFileType(filterName) { Patterns = allPatterns },
                    FilePickerFileTypes.All,
                },
            });
            return files.Count > 0 ? files[0].TryGetLocalPath() : null;
        }

        /// <summary>
        /// If <paramref name="path"/> is a route's Activities/OpenRails folder, set up the route paths and return true.
        /// </summary>
        private bool SetRouteFromOpenRailsFolder(string path)
        {
            var openRails = new DirectoryInfo(path);
            if (!openRails.Name.Equals("openrails", StringComparison.OrdinalIgnoreCase) || openRails.Parent == null
                || !openRails.Parent.Name.Equals("activities", StringComparison.OrdinalIgnoreCase) || openRails.Parent.Parent == null)
                return false;

            RoutePath = openRails.Parent.Parent.FullName;
            foreach (string file in Directory.GetFiles(RoutePath))
            {
                if (file.ToLower().EndsWith(".tdb"))
                {
                    tdbfilename = file;
                }
            }
            GetConsistsPath();
            return true;
        }

        private void EnableTimetableMenus()
        {
            Menu_Save.IsEnabled = true;
            Menu_SaveList.IsEnabled = true;
            Menu_CloseTT.IsEnabled = true;
            MenuItem_Pools.IsEnabled = true;
            Menu_ZipTimetable.IsEnabled = true;
        }

        private async void Menu_NewTimetable(object sender, RoutedEventArgs e)
        {
            if (RoutePath == null || RoutePath == "")
            {
                string fileName = await PickFile("Select the route's track database", "*.tdb", "*.tdb");
                if (fileName != null)
                {
                    RoutePath = Path.GetDirectoryName(fileName);
                    tdbfilename = fileName;
                    GetConsistsPath();
                    mainViewModel.ConsistsAndPaths.ConsistsPath = consistpath;
                    mainViewModel.ConsistsAndPaths.PathsPath = PathCase.Combine(RoutePath, "Paths");
                    await mainViewModel.NewTimetable(tdbfilename, RoutePath);
                    Menu_Save.IsEnabled = true;
                    MenuItem_Pools.IsEnabled = true;
                    Menu_ZipTimetable.IsEnabled = true;
                }
            }
            else
            {
                await mainViewModel.NewTimetable(tdbfilename, RoutePath);
            }
        }

        private async void Menu_LoadTimetableList(object sender, RoutedEventArgs e)
        {
            if (RoutePath == null || RoutePath == "")
            {
                string fileName = await PickFile("Open Timetable List", "Open Rails Timetable List", "*.timetablelist-or");
                if (fileName != null)
                {
                    string path = Path.GetDirectoryName(fileName);
                    if (SetRouteFromOpenRailsFolder(path))
                    {
                        mainViewModel.ConsistsAndPaths.ConsistsPath = consistpath;
                        mainViewModel.ConsistsAndPaths.PathsPath = PathCase.Combine(RoutePath, "Paths");
                        string timetablesListFile = File.ReadAllText(fileName);
                        string[] timetables = timetablesListFile.Split('\n');
                        foreach (string timetable in timetables)
                        {
                            if (!timetable.StartsWith("#") && timetable.Trim() != "")
                            {
                                mainViewModel.LoadTimetable(tdbfilename, RoutePath, PathCase.Combine(OpenRailsFolder, timetable.Trim()));
                            }
                        }
                        EnableTimetableMenus();
                    }
                    else
                    {
                        await Dialogs.Show("The timetable list must be in a route's Activities/OpenRails folder.", "Open Timetable List");
                    }
                }
            }
        }

        private async void Menu_LoadTimetable(object sender, RoutedEventArgs e)
        {
            if (RoutePath == null || RoutePath == "")
            {
                string fileName = await PickFile("Open Timetable", "Open Rails Timetable", "*.timetable-or", "*.timetable_or");
                if (fileName != null)
                {
                    await OpenTimetableFile(fileName);
                }
            }
            else
            {
                OpenSaveViewModel osvm = new OpenSaveViewModel();
                osvm.SetModus("Open");
                List<string> filter = new List<string>();
                filter.Add("*.timetable-or");
                filter.Add("*.timetable_or");
                osvm.ListFiles(OpenRailsFolder, filter);
                OpenSaveView osv = new OpenSaveView();
                osv.Title = "Open file";
                osv.DataContext = osvm;
                if (await Dialogs.ShowDialog(osv) && osvm.SelectedFileName != null)
                {
                    mainViewModel.LoadTimetable(tdbfilename, RoutePath, Path.Combine(OpenRailsFolder, osvm.SelectedFileName));
                    EnableTimetableMenus();
                }
            }
        }

        /// <summary>
        /// Open a timetable file from a route's Activities/OpenRails folder (also used for a file given on the command line).
        /// </summary>
        public async Task OpenTimetableFile(string fileName)
        {
            fileName = Path.GetFullPath(fileName);
            string path = Path.GetDirectoryName(fileName);
            if (SetRouteFromOpenRailsFolder(path))
            {
                try
                {
                    mainViewModel.LoadTimetable(tdbfilename, RoutePath, fileName);
                }
                catch (Exception error)
                {
                    await Dialogs.Show(error.Message, "Open Timetable");
                    return;
                }
                mainViewModel.ConsistsAndPaths.ConsistsPath = consistpath;
                mainViewModel.ConsistsAndPaths.PathsPath = PathCase.Combine(RoutePath, "Paths");
                mainViewModel.LoadPoolTrains(path);
                EnableTimetableMenus();
            }
            else
            {
                await Dialogs.Show("The timetable must be in a route's Activities/OpenRails folder.", "Open Timetable");
            }
        }

        private async void Menu_SaveTimetable(object sender, RoutedEventArgs e)
        {
            if (mainViewModel.SelectedTabIndex < 0 || mainViewModel.SelectedTabIndex >= mainViewModel.Tabs.Count)
                return;
            OpenSaveViewModel osvm = new OpenSaveViewModel();
            osvm.SetModus("Save");
            List<string> filter = new List<string>();
            filter.Add("*.timetable-or");
            filter.Add("*.timetable_or");
            osvm.ListFiles(OpenRailsFolder, filter);
            if (mainViewModel.Tabs[mainViewModel.SelectedTabIndex].Header.ToString().Contains(".timetable"))
            {
                string head = mainViewModel.Tabs[mainViewModel.SelectedTabIndex].Header.ToString();
                osvm.SelectedFileName = head;
            }
            OpenSaveView osv = new OpenSaveView();
            osv.Title = "Save file";
            osv.DataContext = osvm;
            bool ok = await Dialogs.ShowDialog(osv);
            if (string.IsNullOrEmpty(osvm.Filename))
                return;
            string ext = Path.GetExtension(osvm.Filename);
            if (ext != ".timetable-or" && ext != ".timetable_or")
            {
                osvm.Filename = Path.ChangeExtension(osvm.Filename, ".timetable-or");
            }
            if (ok)
            {
                bool can = true;
                string target = Path.Combine(OpenRailsFolder, osvm.Filename);
                if (File.Exists(target))
                {
                    if (!await Dialogs.AskYesNo("File already exists", "Overwrite File?"))
                    {
                        can = false;
                    }
                }
                if (can)
                {
                    Directory.CreateDirectory(OpenRailsFolder);
                    mainViewModel.SaveTimetable(target);
                }
            }
        }

        private async void Menu_CloseTimetable(object sender, RoutedEventArgs e)
        {
            if (mainViewModel.SelectedTabIndex > -1)
            {
                int oldtabindex = mainViewModel.SelectedTabIndex;
                if (await Dialogs.AskYesNo("Do you really want to close the timetable?", "Close Timetable"))
                {
                    mainViewModel.Tables.RemoveAt(oldtabindex);
                    mainViewModel.Tabs.RemoveAt(oldtabindex);
                    if (mainViewModel.Tabs.Count > 0)
                    {
                        if (oldtabindex > 0)
                        {
                            mainViewModel.SelectedTabIndex = oldtabindex - 1;
                        }
                        else
                        {
                            mainViewModel.SelectedTabIndex = oldtabindex;
                        }
                    }
                    else
                    {
                        Menu_Save.IsEnabled = false;
                        Menu_SaveList.IsEnabled = false;
                        Menu_CloseTT.IsEnabled = false;
                        MenuItem_Pools.IsEnabled = false;
                        Menu_ZipTimetable.IsEnabled = false;
                    }
                }
            }
        }

        private async void Menu_SaveTimetableList(object sender, RoutedEventArgs e)
        {
            OpenSaveViewModel osvm = new OpenSaveViewModel();
            osvm.SetModus("Save");
            List<string> filter = new List<string>();
            filter.Add("*.timetablelist-or");
            osvm.ListFiles(OpenRailsFolder, filter);
            OpenSaveView osv = new OpenSaveView();
            osv.Title = "Save file";
            osv.DataContext = osvm;
            if (await Dialogs.ShowDialog(osv) && !string.IsNullOrEmpty(osvm.Filename))
            {
                bool can = true;
                string target = Path.Combine(OpenRailsFolder, Path.GetFileNameWithoutExtension(osvm.Filename) + ".timetablelist-or");
                if (File.Exists(target))
                {
                    if (!await Dialogs.AskYesNo("File already exists", "Overwrite File?"))
                    {
                        can = false;
                    }
                }
                if (can)
                {
                    mainViewModel.SaveTimetablelist(target);
                }
            }
        }

        private async Task LoadConsistsAndPaths(PoolViewModel poolvm)
        {
            if (poolvm.ConsistsAndPaths.Consists.Count == 0 || poolvm.ConsistsAndPaths.Paths.Count == 0)
            {
                // actually we don't need the consists here
                await Dialogs.RunWithWaitingWindow(() => poolvm.ConsistsAndPaths.LoadMissing());
            }
        }

        private async void New_Pool_Click(object sender, RoutedEventArgs e)
        {
            PoolViewModel poolvm = new PoolViewModel();
            poolvm.RoutePath = OpenRailsFolder;
            poolvm.ConsistsAndPaths = mainViewModel.ConsistsAndPaths;
            await LoadConsistsAndPaths(poolvm);
            poolvm.CreatePool();
            PoolView poolView = new PoolView();
            poolView.DataContext = poolvm;
            await Dialogs.ShowDialog(poolView);
        }

        private async void New_Turntable_Pool_Click(object sender, RoutedEventArgs e)
        {
            PoolViewModel poolvm = new PoolViewModel();
            poolvm.RoutePath = OpenRailsFolder;
            poolvm.ConsistsAndPaths = mainViewModel.ConsistsAndPaths;
            await LoadConsistsAndPaths(poolvm);
            poolvm.CreateTurntablePool();
            PoolView poolView = new PoolView();
            poolView.DataContext = poolvm;
            await Dialogs.ShowDialog(poolView);
        }

        private async void Open_Pool_CLick(object sender, RoutedEventArgs e)
        {
            PoolViewModel poolvm = new PoolViewModel();
            poolvm.ConsistsAndPaths = mainViewModel.ConsistsAndPaths;
            await LoadConsistsAndPaths(poolvm);
            OpenSaveViewModel openSave = new OpenSaveViewModel();
            openSave.SetModus("Open");
            List<string> filter = new List<string>();
            filter.Add("*.pool-or");
            filter.Add("*.turntable-or");
            openSave.ListFiles(OpenRailsFolder, filter);
            OpenSaveView osv = new OpenSaveView();
            osv.Title = "Open file";
            osv.DataContext = openSave;
            if (await Dialogs.ShowDialog(osv) && !string.IsNullOrEmpty(openSave.Filename))
            {
                if (Path.GetExtension(openSave.Filename).Equals(".turntable-or", StringComparison.OrdinalIgnoreCase))
                {
                    poolvm.TurntableMode = true;
                }
                poolvm.ReadPoolFile(Path.Combine(OpenRailsFolder, openSave.Filename));
                PoolView poolView = new PoolView();
                poolView.DataContext = poolvm;
                if (await Dialogs.ShowDialog(poolView))
                {
                    mainViewModel.PoolTrains.Clear();
                    mainViewModel.LoadPoolTrains(OpenRailsFolder);
                }
            }
        }

        private void Menu_Zip_Timetable(object sender, RoutedEventArgs e)
        {
            // This function is not complete yet. Therefore it is hidden in the menu
            mainViewModel.GetUsedPathsAndCons();
        }
    }
}
