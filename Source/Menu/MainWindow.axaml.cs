// COPYRIGHT 2009 - 2024 by the Open Rails project.
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
using System.Globalization;
using System.IO;
using System.Linq;
using System.Reflection.PortableExecutable;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Input.Platform;
using Avalonia.Interactivity;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Avalonia.Threading;
using Menu.Notifications;
using Orts.Formats.OR;
using ORTS.Common;
using ORTS.Menu;
using ORTS.Settings;
using ORTS.Updater;
using Activity = ORTS.Menu.Activity;
using Path = ORTS.Menu.Path;
using KeyModifiers = Avalonia.Input.KeyModifiers;

namespace Menu
{
    /// <summary>
    /// The result of one of the Menu's dialogs, like the WinForms DialogResult.
    /// </summary>
    public enum DialogResult { None, OK, Cancel, Retry }

    public partial class MainWindow : Window
    {
        public enum UserAction
        {
            SingleplayerNewGame,
            SingleplayerResumeSave,
            SingleplayerReplaySave,
            SingleplayerReplaySaveFromSave,
            MultiplayerServer,
            MultiplayerClient,
            SinglePlayerTimetableGame,
            SinglePlayerResumeTimetableGame,
            MultiplayerServerResumeSave,
            MultiplayerClientResumeSave
        }

        bool MenuInitialized;
        UserSettings Settings;
        TelemetryManager TelemetryManager;
        List<Folder> Folders = new List<Folder>();
        public List<Route> Routes = new List<Route>();
        List<Activity> Activities = new List<Activity>();
        public List<Consist> Consists = new List<Consist>();
        List<Path> Paths = new List<Path>();
        List<TimetableInfo> TimetableSets = new List<TimetableInfo>();
        List<WeatherFileInfo> TimetableWeatherFileSet = new List<WeatherFileInfo>();
        Task<List<Folder>> FolderLoader;
        Task<List<Route>> RouteLoader;
        Task<List<Activity>> ActivityLoader;
        Task<List<Consist>> ConsistLoader;
        Task<List<Path>> PathLoader;
        Task<List<TimetableInfo>> TimetableSetLoader;
        Task<List<WeatherFileInfo>> TimetableWeatherFileLoader;
        readonly UpdateManager UpdateManager;
        NotificationManager NotificationManager;

        readonly Bitmap ExpanderOpenImage = App.LoadBitmap("Expander-open.png");
        readonly Bitmap ExpanderClosedImage = App.LoadBitmap("Expander-closed.png");
        readonly Bitmap ExpanderClosedDisabledImage = App.LoadBitmap("Expander-closed-disabled.png");

        // Keyboard modifiers at the time of the last click/key press, for "Alt+Start copies the command line".
        KeyModifiers LastKeyModifiers;

        internal string RunActivityProgram => ExternalProcess.ExecutablePath("RunActivity");

        // Base items
        public Folder SelectedFolder => comboBoxFolder.SelectedItem as Folder;
        public Route SelectedRoute => comboBoxRoute.SelectedItem as Route;

        // Activity mode items
        public Activity SelectedActivity => comboBoxActivity.SelectedItem as Activity;
        public Consist SelectedConsist => comboBoxConsist.SelectedItem as Consist;
        public Path SelectedPath => comboBoxHeadTo.SelectedItem as Path;
        public string SelectedStartTime => comboBoxStartTime.IsEditable ? comboBoxStartTime.Text : comboBoxStartTime.SelectedItem?.ToString();

        // Timetable mode items
        public TimetableInfo SelectedTimetableSet => comboBoxTimetableSet.SelectedItem as TimetableInfo;
        public TimetableFileLite SelectedTimetable => comboBoxTimetable.SelectedItem as TimetableFileLite;
        public TimetableFileLite.TrainInformation SelectedTimetableTrain => comboBoxTimetableTrain.SelectedItem as TimetableFileLite.TrainInformation;
        public int SelectedTimetableDay => (comboBoxTimetableDay.SelectedItem as KeyedComboBoxItem)?.Key ?? 0;
        public WeatherFileInfo SelectedWeatherFile => comboBoxTimetableWeatherFile.SelectedItem as WeatherFileInfo;
        public Consist SelectedTimetableConsist;
        public Path SelectedTimetablePath;

        // Shared items
        public int SelectedStartSeason => (IsModeActivity ? comboBoxStartSeason.SelectedItem as KeyedComboBoxItem : comboBoxTimetableSeason.SelectedItem as KeyedComboBoxItem)?.Key ?? 1;
        public int SelectedStartWeather => (IsModeActivity ? comboBoxStartWeather.SelectedItem as KeyedComboBoxItem : comboBoxTimetableWeather.SelectedItem as KeyedComboBoxItem)?.Key ?? 0;

        public string SelectedSaveFile { get; set; }
        public UserAction SelectedAction { get; set; }

        bool IsModeActivity => radioButtonModeActivity.IsChecked == true;
        bool IsModeTimetable => radioButtonModeTimetable.IsChecked == true;

        public string BaseDocumentationUrl;

        #region Main Form
        public MainWindow()
        {
            InitializeComponent();
            Icon = App.LoadIcon();

            // Set title to show revision or build info.
            Title = String.Format(VersionInfo.Version.Length > 0 ? "{0} {1}" : "{0} build {2}", App.ProductName, VersionInfo.Version, VersionInfo.Build);
#if DEBUG
            Title = Title + " (debug)";
#endif
            ShowDetails();
            UpdateEnabled();
            UpdateManager = new UpdateManager(ApplicationInfo.ProcessDirectory, App.ProductName, VersionInfo.VersionOrBuild);

            BaseDocumentationUrl = "https://open-rails.readthedocs.io/en/latest";
            if (VersionInfo.Version.Length > 0)
            {
                if (VersionInfo.Version.StartsWith("T") || VersionInfo.Version.StartsWith("U"))
                {
                    BaseDocumentationUrl = "https://open-rails.readthedocs.io/en/unstable";
                }
            }

            comboBoxFolder.SelectionChanged += comboBoxFolder_SelectedIndexChanged;
            comboBoxRoute.SelectionChanged += comboBoxRoute_SelectedIndexChanged;
            radioButtonModeActivity.IsCheckedChanged += radioButtonMode_CheckedChanged;
            radioButtonModeTimetable.IsCheckedChanged += radioButtonMode_CheckedChanged;
            comboBoxActivity.SelectionChanged += comboBoxActivity_SelectedIndexChanged;
            comboBoxLocomotive.SelectionChanged += comboBoxLocomotive_SelectedIndexChanged;
            comboBoxConsist.SelectionChanged += comboBoxConsist_SelectedIndexChanged;
            comboBoxStartAt.SelectionChanged += comboBoxStartAt_SelectedIndexChanged;
            comboBoxHeadTo.SelectionChanged += comboBoxHeadTo_SelectedIndexChanged;
            comboBoxStartTime.PropertyChanged += (sender, e) =>
            {
                if (e.Property == ComboBox.TextProperty || e.Property == ComboBox.SelectedItemProperty)
                    comboBoxStartTime_TextChanged(sender, e);
            };
            comboBoxStartSeason.SelectionChanged += comboBoxStartSeason_SelectedIndexChanged;
            comboBoxStartWeather.SelectionChanged += comboBoxStartWeather_SelectedIndexChanged;
            comboBoxTimetableSet.SelectionChanged += comboBoxTimetableSet_SelectedIndexChanged;
            comboBoxTimetable.SelectionChanged += comboBoxTimetable_selectedIndexChanged;
            comboBoxTimetable.PropertyChanged += (sender, e) =>
            {
                if (e.Property == IsEnabledProperty)
                    comboBoxTimetable_EnabledChanged(sender, e);
            };
            comboBoxTimetableTrain.SelectionChanged += comboBoxTimetableTrain_SelectedIndexChanged;
            comboBoxTimetableDay.SelectionChanged += comboBoxTimetableDay_SelectedIndexChanged;
            comboBoxTimetableSeason.SelectionChanged += comboBoxTimetableSeason_SelectedIndexChanged;
            comboBoxTimetableWeather.SelectionChanged += comboBoxTimetableWeather_SelectedIndexChanged;
            comboBoxTimetableWeatherFile.SelectionChanged += comboBoxTimetableWeatherFile_SelectedIndexChanged;
            textBoxMPUser.TextChanged += textBoxMPUser_TextChanged;
            textBoxMPHost.TextChanged += textBoxMPUser_TextChanged;

            buttonDownloadContent.Click += buttonDownloadContent_Click;
            buttonOptions.Click += buttonOptions_Click;
            buttonStart.Click += buttonStart_Click;
            buttonResume.Click += buttonResume_Click;
            buttonStartMP.Click += buttonStartMP_Click;
            buttonResumeMP.Click += buttonResumeMP_Click;
            panelNotificationIcon.PointerPressed += (sender, e) => ToggleNotificationPages();
            panelDetails.SizeChanged += (sender, e) =>
            {
                if (e.WidthChanged)
                    UpdateExpanders();
            };

            // Remember the modifier keys used to press a button; Alt+Start copies the command line instead of starting.
            AddHandler(PointerPressedEvent, (sender, e) => LastKeyModifiers = e.KeyModifiers, RoutingStrategies.Tunnel, true);
            AddHandler(PointerReleasedEvent, (sender, e) => LastKeyModifiers = e.KeyModifiers, RoutingStrategies.Tunnel, true);
            AddHandler(KeyDownEvent, (sender, e) => LastKeyModifiers = e.KeyModifiers, RoutingStrategies.Tunnel, true);
            AddHandler(KeyUpEvent, (sender, e) => LastKeyModifiers = e.KeyModifiers, RoutingStrategies.Tunnel, true);

            // Only the first opening; after the simulator exits StartGame() re-runs this itself.
            EventHandler opened = null;
            opened = (sender, e) =>
            {
                Opened -= opened;
                MainForm_Shown();
            };
            Opened += opened;
            Closing += MainForm_FormClosing;
        }

        void MainForm_Shown()
        {
            // Options on our own command line are passed to the settings, like RunActivity does.
            // "/option" is only recognised on Windows as it's an absolute path elsewhere.
            var options = Environment.GetCommandLineArgs().Skip(1)
                .Where(a => a.StartsWith("-") || (OperatingSystem.IsWindows() && a.StartsWith("/")))
                .Select(a => a.Substring(1));
            Settings = new UserSettings(options);
            TelemetryManager = new TelemetryManager(Settings.Telemetry);

            Cursor = Cursor.Default;

            LoadOptions();
            LoadLanguage();

            if (!MenuInitialized)
            {
                var Seasons = new[] {
                    new KeyedComboBoxItem(0, Localization.GetString("Spring")),
                    new KeyedComboBoxItem(1, Localization.GetString("Summer")),
                    new KeyedComboBoxItem(2, Localization.GetString("Autumn")),
                    new KeyedComboBoxItem(3, Localization.GetString("Winter")),
                };
                var Weathers = new[] {
                    new KeyedComboBoxItem(0, Localization.GetString("Clear")),
                    new KeyedComboBoxItem(1, Localization.GetString("Snow")),
                    new KeyedComboBoxItem(2, Localization.GetString("Rain")),
                };
                var Difficulties = new[] {
                    Localization.GetString("Easy"),
                    Localization.GetString("Medium"),
                    Localization.GetString("Hard"),
                    "",
                };
                var Days = new[] {
                    new KeyedComboBoxItem(0, Localization.GetString("Monday")),
                    new KeyedComboBoxItem(1, Localization.GetString("Tuesday")),
                    new KeyedComboBoxItem(2, Localization.GetString("Wednesday")),
                    new KeyedComboBoxItem(3, Localization.GetString("Thursday")),
                    new KeyedComboBoxItem(4, Localization.GetString("Friday")),
                    new KeyedComboBoxItem(5, Localization.GetString("Saturday")),
                    new KeyedComboBoxItem(6, Localization.GetString("Sunday")),
                };

                foreach (var item in Seasons) comboBoxStartSeason.Items.Add(item);
                foreach (var item in Weathers) comboBoxStartWeather.Items.Add(item);
                foreach (var item in Difficulties) comboBoxDifficulty.Items.Add(item);

                // The same items can't be in two ComboBoxes at once in Avalonia, but the keys/values are all that matter.
                foreach (var item in Seasons) comboBoxTimetableSeason.Items.Add(new KeyedComboBoxItem(item.Key, item.Value));
                foreach (var item in Weathers) comboBoxTimetableWeather.Items.Add(new KeyedComboBoxItem(item.Key, item.Value));
                foreach (var item in Days) comboBoxTimetableDay.Items.Add(item);

                buttonTools.Flyout = CreateToolsMenu();
                buttonDocuments.Flyout = CreateDocumentsMenu();

                NotificationManager = new NotificationManager(this, UpdateManager, Settings, panelDetails);
            }
            else
            {
                NotificationManager.Settings = Settings;
            }

            ShowEnvironment();
            ShowTimetableEnvironment();

            CheckForUpdate();
            CheckForTelemetry();

            if (!MenuInitialized)
            {
                LoadFolderList();
                MenuInitialized = true;
            }
        }

        MenuFlyout CreateToolsMenu()
        {
            var menu = new MenuFlyout();
            var testing = new MenuItem { Header = Localization.GetString("Testing") };
            testing.Click += testingToolStripMenuItem_Click;
            menu.Items.Add(testing);

            var coreExecutables = new[] {
                "OpenRails",
                "Menu",
                "RunActivity",
                "Updater",
            };
            var tools = new List<MenuItem>();
            foreach (var executable in FindExecutables(ApplicationInfo.ProcessDirectory))
            {
                // Don't show any of the core parts of the application.
                if (coreExecutables.Contains(System.IO.Path.GetFileNameWithoutExtension(executable), StringComparer.OrdinalIgnoreCase))
                    continue;

                // On Windows the version information is in the .exe; elsewhere the apphost has none, so use the managed .dll.
                var assemblyFile = OperatingSystem.IsWindows() ? executable : executable + ".dll";
                FileVersionInfo toolInfo;
                try
                {
                    toolInfo = FileVersionInfo.GetVersionInfo(assemblyFile);
                }
                catch
                {
                    continue;
                }

                // Skip any executable that isn't part of this product (e.g. Visual Studio hosting files).
                if (toolInfo.ProductName != App.ProductName || String.IsNullOrEmpty(toolInfo.FileDescription))
                    continue;

                // Remove the product name from the tool's name and localise.
                var toolName = Localization.GetString(toolInfo.FileDescription.Replace(App.ProductName, "").Trim());

                // Create menu item to execute tool.
                var toolPath = executable;
                var toolIsConsole = GetImageSubsystem(assemblyFile) == Subsystem.WindowsCui;
                var item = new MenuItem { Header = toolName };
                item.Click += async (sender2, e2) =>
                {
                    try
                    {
                        if (toolIsConsole)
                        {
                            if (!ExternalProcess.StartInTerminal(toolPath, Array.Empty<string>()))
                                await MessageBox.Show(this, Localization.GetStringFmt("No terminal emulator was found to run {0}. Please run it from a terminal.", toolPath), App.ProductName);
                        }
                        else
                        {
                            ExternalProcess.Start(toolPath, Array.Empty<string>());
                        }
                    }
                    catch (Exception error)
                    {
                        await MessageBox.Show(this, error.Message, App.ProductName);
                    }
                };
                tools.Add(item);
            }
            // Add all the tools in alphabetical order.
            foreach (var tool in tools.OrderBy(t => (string)t.Header))
                menu.Items.Add(tool);
            return menu;
        }

        /// <summary>
        /// Our executables: *.exe on Windows, or apphosts (an executable file with a matching .dll) elsewhere.
        /// </summary>
        static IEnumerable<string> FindExecutables(string directory)
        {
            if (OperatingSystem.IsWindows())
                return Directory.GetFiles(directory, "*.exe");

            return Directory.GetFiles(directory)
                .Where(f => System.IO.Path.GetExtension(f).Length == 0 && File.Exists(f + ".dll"))
                .Where(f => !OperatingSystem.IsWindows() && (File.GetUnixFileMode(f) & UnixFileMode.UserExecute) != 0);
        }

        MenuFlyout CreateDocumentsMenu()
        {
            // Just like the tools, buttonDocuments is a button that is treated like a menu.
            var menu = new MenuFlyout();
            var docs = new List<MenuItem>();
            var path = System.IO.Path.Combine(Directory.GetCurrentDirectory(), "Documentation");
            if (!Directory.Exists(path))
                path = System.IO.Path.Combine(ApplicationInfo.ProcessDirectory, "Documentation");

            if (Directory.Exists(path))
            {
                // Load English documents
                LoadDocuments(docs, path);

                // Find any non-English documents by looking in Documentation/<language code>/, e.g. Documentation/es/
                foreach (var codePath in Directory.GetDirectories(path))
                {
                    // Extract the last folder in the path - the language code, e.g. "es"
                    var code = System.IO.Path.GetFileName(codePath);

                    // include any non-English documents that match the chosen language
                    if (code == Settings.Language)
                        LoadDocuments(docs, codePath, code);
                }
            }
            foreach (var doc in docs.OrderBy(d => (string)d.Header))
                menu.Items.Add(doc);

            var item = new MenuItem { Header = Localization.GetString("Online documents (opens browser)") };
            item.Click += (sender2, e2) => ExternalProcess.Open("https://www.openrails.org/learn/documents/");
            if (menu.Items.Count > 0)
                menu.Items.Add(new Separator());
            menu.Items.Add(item);
            return menu;
        }

        void LoadDocuments(List<MenuItem> docs, string folderPath, string code = null)
        {
            foreach (var filePath in Directory.GetFiles(folderPath))
            {
                var ext = System.IO.Path.GetExtension(filePath);
                var name = System.IO.Path.GetFileNameWithoutExtension(filePath);
                // These are the formats that can be selected.
                if (new[] { ".pdf", ".doc", ".docx", ".pptx", ".txt" }.Contains(ext.ToLowerInvariant()))
                {
                    var codeLabel = string.IsNullOrEmpty(code) ? "" : $" [{code}]";
                    var docPath = filePath;
                    var item = new MenuItem { Header = $"{name}{ext}{codeLabel}" };
                    item.Click += (sender2, e2) => ExternalProcess.Open(docPath);
                    docs.Add(item);
                }
            }
        }

        void MainForm_FormClosing(object sender, WindowClosingEventArgs e)
        {
            SaveOptions();
            if (RouteLoader != null)
                RouteLoader.Cancel();
            if (ActivityLoader != null)
                ActivityLoader.Cancel();
            if (ConsistLoader != null)
                ConsistLoader.Cancel();
            if (PathLoader != null)
                PathLoader.Cancel();
            if (TimetableSetLoader != null)
                TimetableSetLoader.Cancel();
            if (TimetableWeatherFileLoader != null)
                TimetableWeatherFileLoader.Cancel();

            // Remove any deleted saves
            if (Directory.Exists(UserSettings.DeletedSaveFolder))
                Directory.Delete(UserSettings.DeletedSaveFolder, true);   // true removes all contents as well as folder

            // Tidy up after versions which used SAVE.BIN
            var file = System.IO.Path.Combine(UserSettings.UserDataFolder, "SAVE.BIN");
            if (File.Exists(file))
                File.Delete(file);
        }

        void CheckForUpdate()
        {
            // Uses a custom Task class which pre-dates the System.Threading.Task but provides much the same features.
            new Task<UpdateManager>(this, () =>
            {
                UpdateManager.Check();
                return null;
            }, _ =>
            {
                LoadNotifications();
            });
        }

        void LoadNotifications()
        {
            NotificationManager.CheckNotifications();
            ShowNotificationPages();
            UpdateNotificationPageAlert();
        }

        public virtual void OnCheckUpdatesAgain(EventArgs e)
        {
            CheckForUpdate();
        }

        void CheckForTelemetry()
        {
            // DO NOT await this call as we want it to run in the background
            _ = TelemetryManager.SubmitIfDue(TelemetryType.System, () => new
            {
                SystemInfo.Application,
                SystemInfo.Runtime,
                SystemInfo.OperatingSystem,
                SystemInfo.InstalledMemoryMB,
                SystemInfo.CPUs,
                SystemInfo.GPUs,
                SystemInfo.Direct3DFeatureLevels
            });
        }

        public virtual void OnRefreshUpdates(EventArgs e)
        {
            // Self-updating is not available on every platform (there is no update channel), so there is nothing to refresh.
            if (UpdateManager.ChannelName != null)
            {
                var currentChannel = UpdateManager.ChannelName;
                // Force a Refresh of the notifications, which will check for updates and show the notification pages if there are any new notifications.
                UpdateManager.SetChannel(currentChannel); // The 'Refresh' button is only available for the 'Unstable' mode.
                UpdateManager.Check();
            }

            // Reload the notifications and show them as the 'Latest version available' may have changed.
            LoadNotifications();
        }

        void LoadLanguage()
        {
            if (Settings.Language.Length > 0)
            {
                try
                {
                    CultureInfo.DefaultThreadCurrentUICulture = new CultureInfo(Settings.Language);
                    CultureInfo.CurrentUICulture = CultureInfo.DefaultThreadCurrentUICulture;
                }
                catch { }
            }
            // The window's texts are translated when it is created (see TrExtension); a change of language restarts the Menu.
        }

        void RestartMenu()
        {
            Process.Start(new ProcessStartInfo(ApplicationInfo.ProcessFile) { UseShellExecute = false });
            Close();
        }
        #endregion

        #region Folders
        void comboBoxFolder_SelectedIndexChanged(object sender, EventArgs e)
        {
            LoadRouteList();
            LoadLocomotiveList();
            ShowDetails();
        }
        #endregion

        #region Routes
        void comboBoxRoute_SelectedIndexChanged(object sender, EventArgs e)
        {
            LoadActivityList();
            LoadStartAtList();
            LoadTimetableSetList();
            ShowDetails();
        }
        #endregion

        #region Mode
        void radioButtonMode_CheckedChanged(object sender, EventArgs e)
        {
            panelModeActivity.IsVisible = IsModeActivity;
            panelModeTimetable.IsVisible = IsModeTimetable;
            UpdateEnabled();
            ShowDetails();
        }
        #endregion

        #region Activities
        void comboBoxActivity_SelectedIndexChanged(object sender, EventArgs e)
        {
            ShowLocomotiveList();
            ShowConsistList();
            ShowStartAtList();
            ShowEnvironment();
            ShowDetails();
        }
        #endregion

        #region Locomotives
        void comboBoxLocomotive_SelectedIndexChanged(object sender, EventArgs e)
        {
            ShowConsistList();
            ShowDetails();
        }
        #endregion

        #region Consists
        void comboBoxConsist_SelectedIndexChanged(object sender, EventArgs e)
        {
            UpdateExploreActivity();
            ShowDetails();
        }
        #endregion

        #region Starting from
        void comboBoxStartAt_SelectedIndexChanged(object sender, EventArgs e)
        {
            ShowHeadToList();
        }
        #endregion

        #region Heading to
        void comboBoxHeadTo_SelectedIndexChanged(object sender, EventArgs e)
        {
            UpdateExploreActivity();
            ShowDetails();
        }
        #endregion

        #region Environment
        void comboBoxStartTime_TextChanged(object sender, EventArgs e)
        {
            UpdateExploreActivity();
        }

        void comboBoxStartSeason_SelectedIndexChanged(object sender, EventArgs e)
        {
            UpdateExploreActivity();
        }

        void comboBoxStartWeather_SelectedIndexChanged(object sender, EventArgs e)
        {
            UpdateExploreActivity();
        }
        #endregion

        #region Timetable Sets
        void comboBoxTimetableSet_SelectedIndexChanged(object sender, EventArgs e)
        {
            UpdateTimetableSet();
            ShowTimetableList();
            ShowTimetableEnvironment();  // Analoguous to activity change; may need to reload from menu selection (settings)
            ShowDetails();
        }
        #endregion

        #region Timetables
        void comboBoxTimetable_selectedIndexChanged(object sender, EventArgs e)
        {
            ShowTimetableTrainList();
            ShowDetails();
        }
        #endregion

        #region Timetable Trains
        void comboBoxTimetableTrain_SelectedIndexChanged(object sender, EventArgs e)
        {
            var selectedTrain = comboBoxTimetableTrain.SelectedItem as TimetableFileLite.TrainInformation;
            if (selectedTrain != null)
            {
                SelectedTimetableConsist = Consist.GetConsist(SelectedFolder, selectedTrain.LeadingConsist, selectedTrain.ReverseConsist);
                SelectedTimetablePath = Path.GetPath(SelectedRoute, selectedTrain.Path, false);
            }
            else
            {
                SelectedTimetableConsist = null;
                SelectedTimetablePath = null;
            }
            ShowDetails();
        }
        #endregion

        #region Timetable environment
        void comboBoxTimetableDay_SelectedIndexChanged(object sender, EventArgs e)
        {
            UpdateTimetableSet();
        }

        void comboBoxTimetableSeason_SelectedIndexChanged(object sender, EventArgs e)
        {
            UpdateTimetableSet();
        }

        void comboBoxTimetableWeather_SelectedIndexChanged(object sender, EventArgs e)
        {
            UpdateTimetableSet();
        }

        void comboBoxTimetableWeatherFile_SelectedIndexChanged(object sender, EventArgs e)
        {
            UpdateTimetableWeatherSet();
        }
        #endregion

        #region Multiplayer
        void textBoxMPUser_TextChanged(object sender, EventArgs e)
        {
            UpdateEnabled();
        }

        async System.Threading.Tasks.Task<bool> CheckUserName(string text)
        {
            string tmp = text ?? "";
            if (tmp.Length < 4 || tmp.Length > 10 || tmp.Contains("\"") || tmp.Contains("\'") || tmp.Contains(" ") || tmp.Contains("-") || Char.IsDigit(tmp, 0))
            {
                await MessageBox.Show(this, Localization.GetString("User name must be 4-10 characters long, cannot contain space, ', \" or - and must not start with a digit."), App.ProductName);
                return false;
            }
            return true;
        }

        #endregion

        #region Misc. buttons and options

        async void testingToolStripMenuItem_Click(object sender, EventArgs e)
        {
            await new TestingWindow(this, Settings).ShowDialog(this);
        }

        void buttonOptions_Click(object sender, EventArgs e)
        {
            ShowOptionsDialog();
        }

        public async void ShowOptionsDialog()
        {
            SaveOptions();

            var form = new OptionsWindow(Settings, UpdateManager, TelemetryManager, BaseDocumentationUrl);
            switch (await form.ShowDialog<DialogResult>(this))
            {
                case DialogResult.OK:
                    LoadFolderList();
                    CheckForUpdate();
                    break;
                case DialogResult.Retry:
                    RestartMenu();
                    break;
            }
        }

        public async void ShowTelemetryDialog()
        {
            await new TelemetryWindow(TelemetryManager).ShowDialog(this);
        }

        async void buttonDownloadContent_Click(object sender, EventArgs e)
        {
            await ShowContentDialog();
        }

        async System.Threading.Tasks.Task ShowContentDialog()
        {
            switch (await new ContentWindow(this, Settings, BaseDocumentationUrl).ShowDialog<DialogResult>(this))
            {
                case DialogResult.Retry:
                    RestartMenu();
                    break;
            }
        }

        void buttonStart_Click(object sender, EventArgs e)
        {
            SaveOptions();

            if (IsModeActivity)
            {
                SelectedAction = UserAction.SingleplayerNewGame;
                if (SelectedActivity != null)
                    StartGame();
            }
            else
            {
                SelectedAction = UserAction.SinglePlayerTimetableGame;
                if (SelectedTimetableTrain != null)
                    StartGame();
            }
        }

        void buttonResume_Click(object sender, EventArgs e)
        {
            OpenResumeForm(false);
        }

        void buttonResumeMP_Click(object sender, EventArgs e)
        {
            OpenResumeForm(true);
        }

        async void OpenResumeForm(bool multiplayer)
        {
            if (IsModeTimetable)
            {
                SelectedAction = UserAction.SinglePlayerTimetableGame;
            }
            else if (!multiplayer)
            {
                SelectedAction = UserAction.SingleplayerNewGame;
            }
            else if (radioButtonMPClient.IsChecked == true)
            {
                SelectedAction = UserAction.MultiplayerClient;
            }
            else
                SelectedAction = UserAction.MultiplayerServer;

            // if timetable mode but no timetable selected - no action
            if (SelectedAction == UserAction.SinglePlayerTimetableGame && (SelectedTimetableSet == null || multiplayer))
            {
                return;
            }

            // The Resume button's modifiers are what count for "Alt copies the command line", not the dialog's.
            var form = new ResumeWindow(Settings, SelectedRoute, SelectedAction, SelectedActivity, SelectedTimetableSet, this);
            if (await form.ShowDialog<DialogResult>(this) == DialogResult.OK)
            {
                SaveOptions();
                SelectedSaveFile = form.SelectedSaveFile;
                SelectedAction = form.SelectedAction;
                LastKeyModifiers = form.LastKeyModifiers;
                StartGame();
            }
        }

        async void buttonStartMP_Click(object sender, EventArgs e)
        {
            var modifiers = LastKeyModifiers;
            if (await CheckUserName(textBoxMPUser.Text) == false) return;
            SaveOptions();
            SelectedAction = radioButtonMPClient.IsChecked == true ? UserAction.MultiplayerClient : UserAction.MultiplayerServer;
            LastKeyModifiers = modifiers;
            StartGame();
        }

        /// <summary>
        /// Starts RunActivity for the selected action and waits for it to finish, hiding the Menu meanwhile.
        /// This replaces the WinForms loop of closing the main form with DialogResult.OK.
        /// </summary>
        async void StartGame()
        {
            var parameters = Program.GetRunActivityArguments(this);
            var joinedParameters = ExternalProcess.FormatCommandLine(parameters);

            if ((LastKeyModifiers & KeyModifiers.Alt) == KeyModifiers.Alt)
            {
                if (Clipboard != null)
                    await Clipboard.SetTextAsync(joinedParameters);
                await MessageBox.Show(this,
                    $"{ExternalProcess.ExecutableName("RunActivity")} arguments have been copied to the clipboard:" +
                    $"\n\n{joinedParameters}\n\n" +
                    "This is a debugging aid. If you wanted to start the simulator instead, select Start without holding down the Alt key.",
                    App.ProductName);
                return;
            }

            System.Diagnostics.Process process;
            try
            {
                process = ExternalProcess.Start(RunActivityProgram, parameters);
            }
            catch (Exception error)
            {
                await MessageBox.Show(this, Localization.GetStringFmt("Unable to start {0}:\n\n{1}", RunActivityProgram, error.Message), App.Caption);
                return;
            }

            Hide();
            try
            {
                await process.WaitForExitAsync();
            }
            finally
            {
                Show();
                Activate();
                // Like showing the WinForms dialog again: reload the settings (the simulator may have changed them).
                MainForm_Shown();
            }
        }

        #endregion

        #region Options
        void LoadOptions()
        {
            checkBoxWarnings.IsChecked = Settings.Logging;
            radioButtonModeActivity.IsChecked = Settings.IsModeActivity;
            radioButtonModeTimetable.IsChecked = !Settings.IsModeActivity;

            textBoxMPUser.Text = Settings.Multiplayer_User;
            textBoxMPHost.Text = Settings.Multiplayer_Host + ":" + Settings.Multiplayer_Port;
        }

        void SaveOptions()
        {
            if (Settings == null)
                return;

            Settings.Logging = checkBoxWarnings.IsChecked == true;
            Settings.Multiplayer_User = textBoxMPUser.Text ?? "";
            Settings.IsModeActivity = IsModeActivity;

            var mpHost = (textBoxMPHost.Text ?? "").Split(':');
            Settings.Multiplayer_Host = mpHost[0];
            if (mpHost.Length > 1)
            {
                var port = Settings.Multiplayer_Port;
                if (int.TryParse(mpHost[1], out port))
                    Settings.Multiplayer_Port = port;
            }
            else
            {
                Settings.Multiplayer_Port = (int)Settings.GetDefaultValue("Multiplayer_Port");
            }
            var locomotive = comboBoxLocomotive.SelectedItem as Locomotive;
            Settings.Menu_Selection = new[] {
                // Base items
                SelectedFolder != null ? SelectedFolder.Path : "",
                SelectedRoute != null ? SelectedRoute.Path : "",
                // Activity mode items / Explore mode items
                IsModeActivity ?
                    SelectedActivity != null && SelectedActivity.FilePath != null ? SelectedActivity.FilePath : SelectedActivity != null? SelectedActivity.Name : "" :
                    SelectedTimetableSet != null ? SelectedTimetableSet.fileName : "",
                IsModeActivity ?
                    SelectedActivity is ExploreActivity && locomotive != null && locomotive.FilePath != null ? locomotive.FilePath : "" :
                    SelectedTimetable != null ? SelectedTimetable.Description : "",
                IsModeActivity ?
                    SelectedActivity is ExploreActivity && SelectedConsist != null ? SelectedConsist.FilePath : "" :
                    SelectedTimetableTrain != null ? SelectedTimetableTrain.Column.ToString() : "",
                IsModeActivity ?
                    SelectedActivity is ExploreActivity && SelectedPath != null ? SelectedPath.FilePath : "" :
                    SelectedTimetableDay.ToString(),
                IsModeActivity ?
                    SelectedActivity is ExploreActivity ? SelectedStartTime ?? "" : "" :
                    "",
                // Shared items
                IsModeActivity ?
                    SelectedActivity is ExploreActivity ? SelectedStartSeason.ToString() : "" :
                    SelectedStartSeason.ToString(),
                IsModeActivity ?
                    SelectedActivity is ExploreActivity ? SelectedStartWeather.ToString() : "" :
                    SelectedStartWeather.ToString(),
            };
            Settings.Save();
        }
        #endregion

        #region Enabled state
        void UpdateEnabled()
        {
            comboBoxFolder.IsEnabled = comboBoxFolder.Items.Count > 0;
            comboBoxRoute.IsEnabled = comboBoxRoute.Items.Count > 0;
            comboBoxActivity.IsEnabled = comboBoxActivity.Items.Count > 0;
            comboBoxLocomotive.IsEnabled = comboBoxLocomotive.Items.Count > 0 && SelectedActivity is ExploreActivity;
            comboBoxConsist.IsEnabled = comboBoxConsist.Items.Count > 0 && SelectedActivity is ExploreActivity;
            comboBoxStartAt.IsEnabled = comboBoxStartAt.Items.Count > 0 && SelectedActivity is ExploreActivity;
            comboBoxHeadTo.IsEnabled = comboBoxHeadTo.Items.Count > 0 && SelectedActivity is ExploreActivity;
            comboBoxStartTime.IsEnabled = comboBoxStartSeason.IsEnabled = comboBoxStartWeather.IsEnabled = SelectedActivity is ExploreActivity;
            SetEditable(comboBoxStartTime, SelectedActivity is ExploreActivity);
            comboBoxTimetable.IsEnabled = comboBoxTimetableSet.Items.Count > 0;
            comboBoxTimetableTrain.IsEnabled = comboBoxTimetable.Items.Count > 0;
            comboBoxTimetableWeatherFile.IsEnabled = comboBoxTimetableWeatherFile.Items.Count > 0;
            //Avoid to Start with a non valid Activity/Locomotive/Consist.
            buttonResume.IsEnabled = buttonStart.IsEnabled = IsModeActivity && !GetText(comboBoxActivity).StartsWith("<") && !GetText(comboBoxLocomotive).StartsWith("<") ?
                SelectedActivity != null && (!(SelectedActivity is ExploreActivity) || (comboBoxConsist.Items.Count > 0 && comboBoxHeadTo.Items.Count > 0)) :
                SelectedTimetableTrain != null;
            buttonResumeMP.IsEnabled = buttonStartMP.IsEnabled = buttonStart.IsEnabled && !String.IsNullOrEmpty(textBoxMPUser.Text) && !String.IsNullOrEmpty(textBoxMPHost.Text);
        }

        static string GetText(ComboBox comboBox)
        {
            return (comboBox.IsEditable ? comboBox.Text : comboBox.SelectedItem?.ToString()) ?? "";
        }

        /// <summary>
        /// Switches between an editable combobox (WinForms DropDown) and a list-only one (WinForms DropDownList), keeping the text.
        /// </summary>
        static void SetEditable(ComboBox comboBox, bool editable)
        {
            if (comboBox.IsEditable == editable)
                return;
            var text = GetText(comboBox);
            comboBox.IsEditable = editable;
            if (editable)
                comboBox.Text = text;
        }
        #endregion

        #region Folder list
        public void LoadFolderList()
        {
            var initialized = MenuInitialized;
            Folders.Clear();
            ShowFolderList();

            FolderLoader = new Task<List<Folder>>(this, () => Folder.GetFolders(Settings).OrderBy(f => f.Name).ToList(), async (folders) =>
            {
                Folders = folders;
                ShowFolderList();
                if (Folders.Count > 0)
                    comboBoxFolder.Focus();

                if (!initialized && Folders.Count == 0)
                    await ShowContentDialog();
            });
        }

        public void LoadFolderListWithoutTask()
        {
            Folders = Folder.GetFolders(Settings).OrderBy(f => f.Name).ToList();
            ShowFolderList();
        }

        void ShowFolderList()
        {
            comboBoxFolder.Items.Clear();
            foreach (var folder in Folders)
                comboBoxFolder.Items.Add(folder);
            UpdateFromMenuSelection<Folder>(comboBoxFolder, UserSettings.Menu_SelectionIndex.Folder, f => f.Path);
            UpdateEnabled();
        }
        #endregion

        #region Route list
        public void LoadRouteList()
        {
            if (RouteLoader != null)
                RouteLoader.Cancel();

            Routes.Clear();
            Activities.Clear();
            Paths.Clear();
            ShowRouteList();
            ShowActivityList();
            ShowStartAtList();
            ShowHeadToList();

            var selectedFolder = SelectedFolder;
            if (selectedFolder == null)
                return;
            RouteLoader = new Task<List<Route>>(this, () => Route.GetRoutes(selectedFolder).OrderBy(r => r.ToString()).ToList(), (routes) =>
            {
                Routes = routes;
                ShowRouteList();
            });
        }

        void ShowRouteList()
        {
            comboBoxRoute.Items.Clear();
            foreach (var route in Routes)
                comboBoxRoute.Items.Add(route);
            UpdateFromMenuSelection<Route>(comboBoxRoute, UserSettings.Menu_SelectionIndex.Route, r => r.Path);
            if (Settings.Menu_Selection.Length > (int)UserSettings.Menu_SelectionIndex.Activity)
            {
                var path = Settings.Menu_Selection[(int)UserSettings.Menu_SelectionIndex.Activity]; // Activity or Timetable
                var extension = System.IO.Path.GetExtension(path).ToLower();
                if (extension == ".act")
                    radioButtonModeActivity.IsChecked = true;
                else if (extension == ".timetable_or" || extension == ".timetable-or")
                    radioButtonModeTimetable.IsChecked = true;
            }
            UpdateEnabled();
        }
        #endregion

        #region Activity list
        public void LoadActivityList()
        {
            if (ActivityLoader != null)
                ActivityLoader.Cancel();

            Activities.Clear();
            ShowActivityList();

            var selectedFolder = SelectedFolder;
            var selectedRoute = SelectedRoute;
            if (selectedFolder == null || selectedRoute == null)
                return;
            ActivityLoader = new Task<List<Activity>>(this, () => Activity.GetActivities(selectedFolder, selectedRoute).OrderBy(a => a.ToString()).ToList(), (activities) =>
            {
                Activities = activities;
                ShowActivityList();
            });
        }

        void ShowActivityList()
        {
            comboBoxActivity.Items.Clear();
            foreach (var activity in Activities)
                comboBoxActivity.Items.Add(activity);
            UpdateFromMenuSelection<Activity>(comboBoxActivity, UserSettings.Menu_SelectionIndex.Activity, a => a.FilePath);
            UpdateEnabled();
        }

        void UpdateExploreActivity()
        {
            if (SelectedActivity == null || !(SelectedActivity is ExploreActivity))
                return;

            var exploreActivity = SelectedActivity as ExploreActivity;
            exploreActivity.Consist = SelectedConsist;
            exploreActivity.Path = SelectedPath;
            exploreActivity.StartTime = SelectedStartTime;
            exploreActivity.Season = (Orts.Formats.Msts.SeasonType)SelectedStartSeason;
            exploreActivity.Weather = (Orts.Formats.Msts.WeatherType)SelectedStartWeather;
        }
        #endregion

        #region Consist lists
        public void LoadLocomotiveList()
        {
            if (ConsistLoader != null)
                ConsistLoader.Cancel();

            Consists.Clear();
            ShowLocomotiveList();
            ShowConsistList();

            var selectedFolder = SelectedFolder;
            if (selectedFolder == null)
                return;
            ConsistLoader = new Task<List<Consist>>(this, () => Consist.GetConsists(selectedFolder).OrderBy(a => a.ToString()).ToList(), (consists) =>
            {
                Consists = consists;
                if (SelectedActivity == null || SelectedActivity is ExploreActivity)
                    ShowLocomotiveList();
            });
        }

        void ShowLocomotiveList()
        {
            if (SelectedActivity == null || SelectedActivity is ExploreActivity)
            {
                comboBoxLocomotive.Items.Clear();
                comboBoxLocomotive.Items.Add(new Locomotive());
                foreach (var loco in Consists.Where(c => c.Locomotive != null).Select(c => c.Locomotive).Distinct().OrderBy(l => l.ToString()))
                    comboBoxLocomotive.Items.Add(loco);
                if (comboBoxLocomotive.Items.Count == 1)
                    comboBoxLocomotive.Items.Clear();
                UpdateFromMenuSelection<Locomotive>(comboBoxLocomotive, UserSettings.Menu_SelectionIndex.Locomotive, l => l.FilePath);
            }
            else
            {
                var consist = SelectedActivity.Consist;
                comboBoxLocomotive.Items.Clear();
                comboBoxLocomotive.Items.Add(consist.Locomotive);
                comboBoxLocomotive.SelectedIndex = 0;
                comboBoxConsist.Items.Clear();
                comboBoxConsist.Items.Add(consist);
                comboBoxConsist.SelectedIndex = 0;
            }
            UpdateEnabled();
        }

        void ShowConsistList()
        {
            if (SelectedActivity == null || SelectedActivity is ExploreActivity)
            {
                comboBoxConsist.Items.Clear();
                var locomotive = comboBoxLocomotive.SelectedItem;
                if (locomotive != null)
                {
                    foreach (var consist in Consists.Where(c => locomotive.Equals(c.Locomotive)).OrderBy(c => c.Name))
                        comboBoxConsist.Items.Add(consist);
                }
                UpdateFromMenuSelection<Consist>(comboBoxConsist, UserSettings.Menu_SelectionIndex.Consist, c => c.FilePath);
            }
            UpdateEnabled();
        }
        #endregion

        #region Path lists
        public void LoadStartAtList()
        {
            if (PathLoader != null)
                PathLoader.Cancel();

            Paths.Clear();
            ShowStartAtList();
            ShowHeadToList();

            var selectedRoute = SelectedRoute;
            if (selectedRoute == null)
                return;
            PathLoader = new Task<List<Path>>(this, () => Path.GetPaths(selectedRoute, false).OrderBy(a => a.ToString()).ToList(), (paths) =>
            {
                Paths = paths;
                if (SelectedActivity == null || SelectedActivity is ExploreActivity)
                    ShowStartAtList();
            });
        }

        void ShowStartAtList()
        {
            if (SelectedActivity == null || SelectedActivity is ExploreActivity)
            {
                comboBoxStartAt.Items.Clear();
                foreach (var place in Paths.Select(p => p.Start).Distinct().OrderBy(s => s.ToString()))
                    comboBoxStartAt.Items.Add(place);
                UpdateFromMenuSelection<Path>(comboBoxStartAt, UserSettings.Menu_SelectionIndex.Path, c => c.FilePath);
            }
            else
            {
                var path = SelectedActivity.Path;
                comboBoxStartAt.Items.Clear();
                comboBoxStartAt.Items.Add(path.Start);
                comboBoxStartAt.SelectedIndex = 0;
                comboBoxHeadTo.Items.Clear();
                comboBoxHeadTo.Items.Add(path);
                comboBoxHeadTo.SelectedIndex = 0;
            }
            UpdateEnabled();
        }

        void ShowHeadToList()
        {
            if (SelectedActivity == null || SelectedActivity is ExploreActivity)
            {
                comboBoxHeadTo.Items.Clear();
                foreach (var path in Paths.Where(p => p.Start == (string)comboBoxStartAt.SelectedItem))
                    comboBoxHeadTo.Items.Add(path);
                UpdateFromMenuSelection<Path>(comboBoxHeadTo, UserSettings.Menu_SelectionIndex.Path, c => c.FilePath);
            }
            UpdateEnabled();
        }
        #endregion

        #region Environment
        void ShowEnvironment()
        {
            if (SelectedActivity == null || SelectedActivity is ExploreActivity)
            {
                // The start time can be typed in for exploring.
                SetEditable(comboBoxStartTime, true);
                comboBoxStartTime.Items.Clear();
                foreach (var hour in Enumerable.Range(0, 24))
                    comboBoxStartTime.Items.Add(String.Format("{0}:00", hour));

                UpdateFromMenuSelection<string>(comboBoxStartTime, UserSettings.Menu_SelectionIndex.Time, "12:00");
                UpdateFromMenuSelection<KeyedComboBoxItem>(comboBoxStartSeason, UserSettings.Menu_SelectionIndex.Season, s => s.Key.ToString(), new KeyedComboBoxItem(1, ""));
                UpdateFromMenuSelection<KeyedComboBoxItem>(comboBoxStartWeather, UserSettings.Menu_SelectionIndex.Weather, w => w.Key.ToString(), new KeyedComboBoxItem(0, ""));
                comboBoxDifficulty.SelectedIndex = 3;
                comboBoxDuration.Items.Clear();
                comboBoxDuration.Items.Add("");
                comboBoxDuration.SelectedIndex = 0;
            }
            else
            {
                SetEditable(comboBoxStartTime, false);
                comboBoxStartTime.Items.Clear();
                comboBoxStartTime.Items.Add(SelectedActivity.StartTime.FormattedStartTime());
                comboBoxStartTime.SelectedIndex = 0;
                comboBoxStartSeason.SelectedIndex = (int)SelectedActivity.Season;
                comboBoxStartWeather.SelectedIndex = (int)SelectedActivity.Weather;
                comboBoxDifficulty.SelectedIndex = (int)SelectedActivity.Difficulty;
                comboBoxDuration.Items.Clear();
                comboBoxDuration.Items.Add(SelectedActivity.Duration.FormattedDurationTime());
                comboBoxDuration.SelectedIndex = 0;
            }
        }
        #endregion

        #region Timetable Set list
        public void LoadTimetableSetList()
        {
            if (TimetableSetLoader != null)
                TimetableSetLoader.Cancel();
            if (TimetableWeatherFileLoader != null)
                TimetableWeatherFileLoader.Cancel();

            TimetableSets.Clear();
            ShowTimetableSetList();

            var selectedFolder = SelectedFolder;
            var selectedRoute = SelectedRoute;
            if (selectedFolder == null || selectedRoute == null)
                return;
            TimetableSetLoader = new Task<List<TimetableInfo>>(this, () => TimetableInfo.GetTimetableInfo(selectedFolder, selectedRoute).OrderBy(a => a.ToString()).ToList(), (timetableSets) =>
            {
                TimetableSets = timetableSets;
                ShowTimetableSetList();
            });

            TimetableWeatherFileLoader = new Task<List<WeatherFileInfo>>(this, () => WeatherFileInfo.GetTimetableWeatherFiles(selectedFolder, selectedRoute).OrderBy(a => a.ToString()).ToList(), (timetableWeatherFileSet) =>
            {
                TimetableWeatherFileSet = timetableWeatherFileSet;
                ShowTimetableWeatherSet();
            });
        }

        void ShowTimetableSetList()
        {
            comboBoxTimetableSet.Items.Clear();
            foreach (var timetableSet in TimetableSets)
                comboBoxTimetableSet.Items.Add(timetableSet);
            UpdateFromMenuSelection<TimetableInfo>(comboBoxTimetableSet, UserSettings.Menu_SelectionIndex.TimetableSet, t => t.fileName);
            UpdateEnabled();
        }

        void UpdateTimetableSet()
        {
            if (SelectedTimetableSet != null)
            {
                SelectedTimetableSet.Day = SelectedTimetableDay;
                SelectedTimetableSet.Season = SelectedStartSeason;
                SelectedTimetableSet.Weather = SelectedStartWeather;
            }
        }

        void ShowTimetableWeatherSet()
        {
            comboBoxTimetableWeatherFile.Items.Clear();
            foreach (var weatherFile in TimetableWeatherFileSet)
            {
                comboBoxTimetableWeatherFile.Items.Add(weatherFile);
                UpdateEnabled();
            }
        }

        void UpdateTimetableWeatherSet()
        {
            if (SelectedTimetableSet != null && SelectedWeatherFile != null)
                SelectedTimetableSet.WeatherFile = SelectedWeatherFile.GetFullName();
        }

        #endregion

        #region Timetable list
        public void ShowTimetableList()
        {
            comboBoxTimetable.Items.Clear();
            if (SelectedTimetableSet != null)
            {
                foreach (var timetable in SelectedTimetableSet.ORTTList)
                    comboBoxTimetable.Items.Add(timetable);
                UpdateFromMenuSelection<TimetableFileLite>(comboBoxTimetable, UserSettings.Menu_SelectionIndex.Timetable, t => t.Description);
            }
            UpdateEnabled();
        }
        #endregion

        #region Timetable Train list
        public void ShowTimetableTrainList()
        {
            comboBoxTimetableTrain.Items.Clear();
            if (SelectedTimetable != null)
            {
                var trains = SelectedTimetableSet.ORTTList[comboBoxTimetable.SelectedIndex].Trains;
                trains.Sort();
                foreach (var train in trains)
                    comboBoxTimetableTrain.Items.Add(train);
                UpdateFromMenuSelection<TimetableFileLite.TrainInformation>(comboBoxTimetableTrain, UserSettings.Menu_SelectionIndex.Train, t => t.Column.ToString());
            }
            UpdateEnabled();
        }
        #endregion

        #region Timetable environment
        void ShowTimetableEnvironment()
        {
            UpdateFromMenuSelection<KeyedComboBoxItem>(comboBoxTimetableDay, UserSettings.Menu_SelectionIndex.Day, d => d.Key.ToString(), new KeyedComboBoxItem(0, ""));
            UpdateFromMenuSelection<KeyedComboBoxItem>(comboBoxTimetableSeason, UserSettings.Menu_SelectionIndex.Season, s => s.Key.ToString(), new KeyedComboBoxItem(1, ""));
            UpdateFromMenuSelection<KeyedComboBoxItem>(comboBoxTimetableWeather, UserSettings.Menu_SelectionIndex.Weather, w => w.Key.ToString(), new KeyedComboBoxItem(0, ""));
        }
        #endregion

        #region Details
        void ShowDetails()
        {
            if (NotificationManager != null)
                NotificationManager.ArePagesVisible = false;
            ClearPanel();
            AddDetails();
            UpdateExpanders();
        }

        private void AddDetails()
        {
            if (SelectedRoute != null && SelectedRoute.Description != null)
                AddDetail(Localization.GetStringFmt("Route: {0}", SelectedRoute.Name), SelectedRoute.Description.Split('\n'));

            if (IsModeActivity)
            {
                if (SelectedConsist != null && SelectedConsist.Locomotive != null && SelectedConsist.Locomotive.Description != null)
                {
                    AddDetail(Localization.GetStringFmt("Locomotive: {0}", SelectedConsist.Locomotive.Name), SelectedConsist.Locomotive.Description.Split('\n'));
                }
                if (SelectedActivity != null && SelectedActivity.Description != null)
                {
                    AddDetail(Localization.GetStringFmt("Activity: {0}", SelectedActivity.Name), SelectedActivity.Description.Split('\n'));
                    AddDetail(Localization.GetString("Activity Briefing"), (SelectedActivity.Briefing ?? "").Split('\n'));
                }
                else if (SelectedPath != null)
                {
                    AddDetail(Localization.GetStringFmt("Path: {0}", SelectedPath.Name), new[] {
                        Localization.GetStringFmt("Starting at: {0}", SelectedPath.Start),
                        Localization.GetStringFmt("Heading to: {0}", SelectedPath.End)
                    });
                }
            }
            if (IsModeTimetable)
            {
                if (SelectedTimetableSet != null)
                    AddDetail(Localization.GetStringFmt("Timetable set: {0}", SelectedTimetableSet), new string[0]);
                // Description not shown as no description is available for a timetable set.

                if (SelectedTimetable != null)
                    AddDetail(Localization.GetStringFmt("Timetable: {0}", SelectedTimetable), SelectedTimetable.Briefing.Split('\n'));

                if (SelectedTimetableTrain != null)
                {
                    AddDetail(Localization.GetStringFmt("Train: {0}", SelectedTimetableTrain), HideStartParameters(SelectedTimetableTrain.ToInfo()));

                    if (SelectedTimetableConsist != null)
                    {
                        AddDetail(Localization.GetStringFmt("Consist: {0}", SelectedTimetableConsist.Name), new string[0]);
                        if (SelectedTimetableConsist.Locomotive != null && SelectedTimetableConsist.Locomotive.Description != null)
                            AddDetail(Localization.GetStringFmt("Locomotive: {0}", SelectedTimetableConsist.Locomotive.Name), SelectedTimetableConsist.Locomotive.Description.Split('\n'));
                    }
                    if (SelectedTimetablePath != null)
                        AddDetail(Localization.GetStringFmt("Path: {0}", SelectedTimetablePath.Name), SelectedTimetablePath.ToInfo());
                }
            }
        }

        /// <summary>
        /// Change
        ///     "Start time: 10:30$$create=00:04/ahead=0040ElghLE70F363U"
        /// to
        ///     "Start time: 10:30"
        /// for higher-level presentation
        /// </summary>
        /// <param name="info"></param>
        /// <returns></returns>
        private string[] HideStartParameters(string[] info)
        {
            var fullStartTime = info[0].TrimStart();
            var startTimeArray = fullStartTime.Split('$');
            var shortStartTime = startTimeArray[0];
            info[0] = shortStartTime;
            return info;
        }

        const int SummaryLines = 5;

        readonly List<Detail> Details = new List<Detail>();
        class Detail
        {
            public readonly TextBlock Title;
            public readonly Button Expander;
            public readonly Image ExpanderImage;
            public readonly TextBlock Description;
            public bool Expanded;
            public Detail(TextBlock title, Button expander, Image expanderImage, TextBlock description)
            {
                Title = title;
                Expander = expander;
                ExpanderImage = expanderImage;
                Description = description;
                Expanded = false;
            }
        }

        internal void ClearPanel()
        {
            Details.Clear();
            panelDetails.Children.Clear();
        }

        void AddDetail(string title, string[] lines)
        {
            var titleControl = new TextBlock { Text = title, FontWeight = FontWeight.Bold, TextWrapping = TextWrapping.Wrap, Margin = new Thickness(2, 6, 2, 2) };
            var expanderImage = new Image { Source = ExpanderClosedDisabledImage, Width = 9, Height = 9 };
            var expanderControl = new Button { Classes = { "flat" }, Content = expanderImage, VerticalAlignment = VerticalAlignment.Center, IsEnabled = false, Width = 18, Height = 18 };
            var header = new DockPanel { LastChildFill = true };
            DockPanel.SetDock(expanderControl, Dock.Right);
            header.Children.Add(expanderControl);
            header.Children.Add(titleControl);
            panelDetails.Children.Add(header);

            // Show only the first few lines (the summary) until the expander is clicked.
            var descriptionControl = new SelectableTextBlock
            {
                Text = String.Join("\n", lines),
                TextWrapping = TextWrapping.Wrap,
                TextTrimming = TextTrimming.CharacterEllipsis,
                MaxLines = SummaryLines,
                Margin = new Thickness(2),
            };
            panelDetails.Children.Add(descriptionControl);

            var detail = new Detail(titleControl, expanderControl, expanderImage, descriptionControl);
            expanderControl.Click += (sender, e) =>
            {
                detail.Expanded = !detail.Expanded;
                detail.ExpanderImage.Source = detail.Expanded ? ExpanderOpenImage : ExpanderClosedImage;
                detail.Description.MaxLines = detail.Expanded ? 0 : SummaryLines;
            };
            Details.Add(detail);
        }

        /// <summary>
        /// Enable the expander only if the full description is longer than the summary. Otherwise, disable the expander.
        /// </summary>
        void UpdateExpanders()
        {
            var width = panelDetails.Bounds.Width - 4;
            if (width <= 0)
                return;
            foreach (var detail in Details)
            {
                var full = new TextBlock { Text = detail.Description.Text, TextWrapping = TextWrapping.Wrap, FontSize = detail.Description.FontSize, FontFamily = detail.Description.FontFamily };
                var summary = new TextBlock { Text = detail.Description.Text, TextWrapping = TextWrapping.Wrap, FontSize = detail.Description.FontSize, FontFamily = detail.Description.FontFamily, MaxLines = SummaryLines };
                full.Measure(new Size(width, double.PositiveInfinity));
                summary.Measure(new Size(width, double.PositiveInfinity));
                var expandable = full.DesiredSize.Height > summary.DesiredSize.Height + 1;
                detail.Expander.IsEnabled = expandable;
                if (!expandable)
                    detail.Expanded = false;
                detail.ExpanderImage.Source = !expandable ? ExpanderClosedDisabledImage : detail.Expanded ? ExpanderOpenImage : ExpanderClosedImage;
            }
        }
        #endregion

        #region Utility functions
        void UpdateFromMenuSelection<T>(ComboBox comboBox, UserSettings.Menu_SelectionIndex index, T defaultValue)
        {
            UpdateFromMenuSelection<T>(comboBox, index, _ => _.ToString(), defaultValue);
        }

        void UpdateFromMenuSelection<T>(ComboBox comboBox, UserSettings.Menu_SelectionIndex index, Func<T, string> map)
        {
            UpdateFromMenuSelection<T>(comboBox, index, map, default(T));
        }

        /// <summary>
        /// Update the combobox with the selection stored in the menu selection settings (from the previous run).
        /// If the menu selection settings do not match the current selection use the default; except for
        /// "Explore in Activity Mode" also try the content route settings (for the route).
        /// </summary>
        void UpdateFromMenuSelection<T>(ComboBox comboBox, UserSettings.Menu_SelectionIndex index, Func<T, string> map, T defaultValue)
        {
            if (Settings == null)
                return;

            string value = GetValueFromMenuSelection(index);
            if (!string.IsNullOrEmpty(value))
            {
                // index 5 is both Path and Day
                if (comboBox == comboBoxStartAt || comboBox == comboBoxHeadTo)
                {
                    var path = Paths.Find(x => x.FilePath.Equals(value, StringComparison.OrdinalIgnoreCase));
                    if (path == null) value = "";
                    else if (comboBox == comboBoxStartAt) value = path.Start;
                    else value = path.End;
                    searchInComboBoxAndSet(comboBox, value);
                }
                else
                {
                    if (comboBox.IsEditable) comboBox.Text = value;
                    else SelectComboBoxItem<T>(comboBox, item => map(item) == value);
                }
            }
            else
            {
                // try the content route info
                var routes = Settings.Content.ContentRouteSettings.Routes;
                if ((SelectedFolder != null && routes.ContainsKey(SelectedFolder.Name) && routes[SelectedFolder.Name].Installed) &&
                    (!string.IsNullOrEmpty(routes[SelectedFolder.Name].Start.Route)))
                {
                    var route = routes[SelectedFolder.Name];
                    string valueComboboxToSetTo = "";
                    switch (index)
                    {
                        case UserSettings.Menu_SelectionIndex.Route:
                            valueComboboxToSetTo = route.Start.Route;
                            break;
                        case UserSettings.Menu_SelectionIndex.Activity:  // also matches TimetableSet (in timetable mode)
                            valueComboboxToSetTo = route.Start.Activity;
                            break;
                        case UserSettings.Menu_SelectionIndex.Locomotive:  // also matches Timetable (in timetable mode)
                            valueComboboxToSetTo = route.Start.Locomotive;
                            break;
                        case UserSettings.Menu_SelectionIndex.Consist:
                            valueComboboxToSetTo = route.Start.Consist;
                            break;
                        case UserSettings.Menu_SelectionIndex.Path:  // also matches Day (in timetable mode)
                            if (comboBox == comboBoxStartAt) valueComboboxToSetTo = route.Start.StartingAt;
                            else if (comboBox == comboBoxHeadTo) valueComboboxToSetTo = route.Start.HeadingTo;
                            else if (comboBox == comboBoxTimetableDay) valueComboboxToSetTo = route.Start.HeadingTo;  // also used for Day
                            else valueComboboxToSetTo = "";  // should never happen
                            break;
                        case UserSettings.Menu_SelectionIndex.Time:
                            valueComboboxToSetTo = route.Start.Time;
                            break;
                        case UserSettings.Menu_SelectionIndex.Season:
                            valueComboboxToSetTo = route.Start.Season;
                            break;
                        case UserSettings.Menu_SelectionIndex.Weather:
                            valueComboboxToSetTo = route.Start.Weather;
                            break;
                        default:
                            break;
                    }

                    if (!string.IsNullOrEmpty(valueComboboxToSetTo))
                    {
                        if (comboBox.IsEditable)
                            comboBox.Text = valueComboboxToSetTo;
                        else
                            searchInComboBoxAndSet(comboBox, valueComboboxToSetTo);
                    }
                    else
                    {
                        SetToDefault(comboBox, index, map, defaultValue);
                    }
                }
                else
                {
                    SetToDefault(comboBox, index, map, defaultValue);
                }
            }
        }

        /// <summary>
        /// Get the combobox's value from the menu selection in the settings.
        /// Checks that folder, route and activity/timetable-set match.
        /// Returns the value from the settings, or an empty string.
        /// </summary>
        string GetValueFromMenuSelection(UserSettings.Menu_SelectionIndex index)
        {
            if (Settings.Menu_Selection.Length <= (int)index)
                return ""; // not in menu selection settings

            else if (index == UserSettings.Menu_SelectionIndex.Folder)
                return Settings.Menu_Selection[(int)index];

            else if (SelectedFolder == null)
                return ""; // no current folder to match to

            else if (SelectedFolder.Path != Settings.Menu_Selection[(int)UserSettings.Menu_SelectionIndex.Folder])
                return ""; // current folder and menu selection settings folder don't match

            else if (index == UserSettings.Menu_SelectionIndex.Route)
                return Settings.Menu_Selection[(int)index];

            else if (SelectedRoute == null)
                return ""; // no current route to match to

            else if (SelectedRoute.Path != Settings.Menu_Selection[(int)UserSettings.Menu_SelectionIndex.Route])
                return ""; // current route and menu selection settings route don't match

            else if (index == UserSettings.Menu_SelectionIndex.Activity || index == UserSettings.Menu_SelectionIndex.TimetableSet)
                return Settings.Menu_Selection[(int)index];

            else if (IsModeActivity && SelectedActivity == null)
                return ""; // no current activity to match to

            else if (IsModeTimetable && SelectedTimetableSet == null)
                return ""; // no current timetable set to match to

            else if (IsModeActivity && SelectedActivity.Name != Settings.Menu_Selection[(int)UserSettings.Menu_SelectionIndex.Activity])
                return ""; // current activity and menu selection settings activity don't match

            else if (IsModeTimetable && SelectedTimetableSet.fileName != Settings.Menu_Selection[(int)UserSettings.Menu_SelectionIndex.TimetableSet])
                return ""; // current timetable set is different from timetable set in menu selection setting

            else
                return Settings.Menu_Selection[(int)index];
        }

        /// <summary>
        /// Search the DropDown combobox (editable) for the specified string value.
        /// When found, set the combobox to the value, otherwise to the first defined value.
        /// Leave unselected when there are no defined values.
        /// </summary>
        void searchInComboBoxAndSet(ComboBox comboBox, string valueComboboxToSetTo)
        {
            for (var i = 0; i < comboBox.Items.Count; i++)
            {
                if (comboBox.Items[i].ToString() == valueComboboxToSetTo)
                {
                    comboBox.SelectedIndex = i;
                    return;
                }
            }
            if (comboBox.Items.Count > 0)
            {
                comboBox.SelectedIndex = 0;
            }
        }

        /// <summary>
        /// Set the combobox to the specified default (item).
        /// </summary>
        void SetToDefault<T>(ComboBox comboBox, UserSettings.Menu_SelectionIndex index, Func<T, string> map, T defaultValue)
        {
            if (comboBox.IsEditable)
            {
                comboBox.Text = defaultValue != null ? map(defaultValue) : "";
            }
            else
            {
                if (defaultValue != null)
                {
                    SelectComboBoxItem<T>(comboBox, item => map(item) == map(defaultValue));
                }
                else
                {
                    if (comboBox.Items.Count > 0)
                    {
                        comboBox.SelectedIndex = 0;
                    }
                }
            }
        }

        /// <summary>
        /// Select  the the specified item in the combobox (not editable).
        /// When not found, set it to the first item.
        /// Leave unselected when there are no defined items.
        /// </summary>
        void SelectComboBoxItem<T>(ComboBox comboBox, Func<T, bool> predicate)
        {
            if (comboBox.Items.Count == 0)
                return;

            var index = (int)UserSettings.Menu_SelectionIndex.Activity;
            for (var i = 0; i < comboBox.Items.Count; i++)
            {
                if (comboBox.Items[i] is T && predicate((T)comboBox.Items[i]) || (Settings.Menu_Selection.Length > i && comboBox.Items[i].ToString() == Settings.Menu_Selection[index]))
                {
                    comboBox.SelectedIndex = i;
                    return;
                }
            }
            comboBox.SelectedIndex = 0;
        }

        public class KeyedComboBoxItem
        {
            public readonly int Key;
            public readonly string Value;

            public override string ToString()
            {
                return Value;
            }

            public KeyedComboBoxItem(int key, string value)
            {
                Key = key;
                Value = value;
            }
        }
        #endregion

        #region Executable utils
        /// <summary>
        /// Reads the PE subsystem (GUI or console) of a Windows executable or a .NET assembly (which has a PE header on every platform).
        /// </summary>
        static Subsystem GetImageSubsystem(string fileName)
        {
            try
            {
                using (var stream = File.OpenRead(fileName))
                using (var reader = new PEReader(stream))
                    return reader.PEHeaders.PEHeader?.Subsystem ?? Subsystem.Unknown;
            }
            catch
            {
                return Subsystem.Unknown;
            }
        }
        #endregion

        void comboBoxTimetable_EnabledChanged(object sender, EventArgs e)
        {
            //Debrief Eval TTActivity.
            if (!comboBoxTimetable.IsEnabled)
            {
                //comboBoxTimetable.Enabled == false then we erase comboBoxTimetable and comboBoxTimetableTrain data.
                if (comboBoxTimetable.Items.Count > 0)
                {
                    comboBoxTimetable.Items.Clear();
                    comboBoxTimetableTrain.Items.Clear();
                    buttonStart.IsEnabled = false;
                }
            }
            //TO DO: Debrief Eval TTActivity
        }

        #region Notifications
        private void ToggleNotificationPages()
        {
            if (NotificationManager == null)
                return;
            if (NotificationManager.ArePagesVisible == false)
            {
                NotificationManager.ArePagesVisible = true; // Set before calling ShowNotifcations()
                ShowNotificationPages();
            }
            else
            {
                NotificationManager.ArePagesVisible = false;
                ShowDetails();
            }
        }

        public void ShowNotificationPages()
        {
            NotificationManager.ArePagesVisible = true;
            ClearPanel();
            NotificationManager.PopulatePage();
            UpdateNotificationPageAlert();
            NotificationManager.Page.FlowNDetails();
        }

        public void UpdateNotificationPageAlert()
        {
            if (NotificationManager.NewPages.Viewed < NotificationManager.NewPages.Count)
            {
                pbNotificationsSome.IsVisible = true;
                pbNotificationsNone.IsVisible = false;
                lblNotificationCount.IsVisible = true;
                lblNotificationCount.Text = $"{NotificationManager.NewPages.Count - NotificationManager.NewPages.Viewed}";
            }
            else
            {
                pbNotificationsSome.IsVisible = false;
                pbNotificationsNone.IsVisible = true;
                lblNotificationCount.IsVisible = false;
            }
        }

        public void DoNotificationButton(int index)
        {
            NotificationManager.Page.DoButton(UpdateManager, index);
        }

        public void Next_Click(object sender, EventArgs e)
        {
            NotificationManager.ChangePage(1);
        }

        public void Previous_Click(object sender, EventArgs e)
        {
            NotificationManager.ChangePage(-1);
        }

        #endregion Notifications
    }
}
