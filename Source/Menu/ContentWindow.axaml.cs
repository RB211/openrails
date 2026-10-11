// COPYRIGHT 2009, 2010, 2011, 2012, 2013, 2014, 2015 by the Open Rails project.
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
using System.ComponentModel;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Net.Http;
using System.Runtime.CompilerServices;
using System.Threading;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Avalonia.Platform.Storage;
using Avalonia.Threading;
using LibGit2Sharp;
using ORTS.Common;
using ORTS.Settings;
using KeyModifiers = Avalonia.Input.KeyModifiers;
using CancellationTokenSource = System.Threading.CancellationTokenSource;

namespace Menu
{
    public partial class ContentWindow : Window
    {
        /// <summary>
        /// A row of the "Auto Installed" grid.
        /// </summary>
        public class AutoInstallRow : INotifyPropertyChanged
        {
            public string Route { get; set; }
            string installed;
            public string Installed { get => installed; set { installed = value; Changed(); } }
            public string Url { get; set; }

            public event PropertyChangedEventHandler PropertyChanged;
            void Changed([CallerMemberName] string name = null) => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
        }

        /// <summary>
        /// A row of the "Manually Installed" grid.
        /// </summary>
        public class ManualInstallRow : INotifyPropertyChanged
        {
            string route;
            public string Route { get => route; set { route = value; Changed(); } }
            string path;
            public string Path { get => path; set { path = value; Changed(); } }
            public bool IsAutoInstalled { get; set; }

            public event PropertyChangedEventHandler PropertyChanged;
            void Changed([CallerMemberName] string name = null) => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
        }

        private readonly MainWindow MainForm;
        private readonly UserSettings Settings;
        private readonly string BaseDocumentationUrl;

        private readonly IDictionary<string, ContentRouteSettings.Route> AutoInstallRoutes;

        private string AutoInstallRouteName;

        private CancellationTokenSource ImageCancellation;
        private readonly string InfoTempFilename;

        private bool AutoInstallClosingBlocked;

        public bool AutoInstallDoingTheSumOfTheFileBytes = false;

        private readonly string ManualInstallBrouwseDir;

        private bool In_dataGridViewManualInstall_SelectionChanged = false;
        private bool In_buttonManualInstallAdd_Click = false;

        private bool ManualInstallChangesMade = false;

        private readonly bool NoInternet = false;

        // Closing has been confirmed by the user, or isn't to be questioned.
        private bool CloseConfirmed;

        // Errors from the download/install threads, shown afterwards by the UI thread.
        private string WorkerError;

        readonly ObservableCollection<AutoInstallRow> AutoInstallRows = new ObservableCollection<AutoInstallRow>();
        readonly ObservableCollection<ManualInstallRow> ManualInstallRows = new ObservableCollection<ManualInstallRow>();

        AutoInstallRow CurrentAutoInstallRow => dataGridViewAutoInstall.SelectedItem as AutoInstallRow;
        ManualInstallRow CurrentManualInstallRow => dataGridViewManualInstall.SelectedItem as ManualInstallRow;

        public ContentWindow(MainWindow mainForm, UserSettings settings, string baseDocumentationUrl)
        {
            InitializeComponent();
            Icon = App.LoadIcon();

            MainForm = mainForm;
            Settings = settings;
            BaseDocumentationUrl = baseDocumentationUrl;

            //
            // "Auto Installed" tab
            //
            string errorMsg = "";
            Settings.Content.ContentRouteSettings.LoadContent(ref errorMsg);
            if (!string.IsNullOrEmpty(errorMsg))
            {
                NoInternet = true;
                var message = Localization.GetStringFmt("Failed to access the content repository (check Internet connection); automatic download not available. Error: {0}", errorMsg);
                Opened += async (sender, e) => await MessageBox.Show(this, message, Localization.GetString("Attention"));
            }
            AutoInstallRoutes = Settings.Content.ContentRouteSettings.Routes;
            foreach (var pair in AutoInstallRoutes.OrderBy(r => r.Key))
            {
                string routeName = pair.Key;
                ContentRouteSettings.Route route = pair.Value;
                AutoInstallRows.Add(new AutoInstallRow
                {
                    Route = routeName,
                    Installed = route.Installed ? route.DateInstalled.ToString(CultureInfo.CurrentCulture.DateTimeFormat) : "",
                    Url = route.Url,
                });
                if (!route.Installed)
                {
                    changeManualInstallRoute(routeName);
                }
            }
            dataGridViewAutoInstall.ItemsSource = AutoInstallRows;

            if (string.IsNullOrEmpty(settings.ContentInstallPath))
            {
                settings.ContentInstallPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments), "Open Rails", "Content");
            }
            textBoxAutoInstallPath.Text = settings.ContentInstallPath;

            InfoTempFilename = Path.ChangeExtension(Path.GetTempFileName(), "html");

            // tab "Auto Installed" does not contain a Cancel button
            // it's too difficult and not logical to rollback an "Auto Installed" route
            buttonCancel.IsVisible = false;

            //
            // "Manually Installed" tab
            //
            foreach (var folder in Settings.Folders.Folders.OrderBy(f => f.Key))
            {
                ManualInstallRows.Add(new ManualInstallRow
                {
                    Route = folder.Key,
                    Path = folder.Value,
                    IsAutoInstalled = AutoInstallRoutes.ContainsKey(folder.Key),
                });
            }
            dataGridViewManualInstall.ItemsSource = ManualInstallRows;
            dataGridViewManualInstall.LoadingRow += (sender, e) =>
            {
                // Rows which belong to the "Auto Installed" routes are shown grey.
                if (e.Row.DataContext is ManualInstallRow row && row.IsAutoInstalled)
                    e.Row.Foreground = Brushes.Gray;
                else
                    e.Row.ClearValue(ForegroundProperty);
            };

            ManualInstallBrouwseDir = determineBrowseDir();
            buttonCancel.IsEnabled = false;

            dataGridViewAutoInstall.SelectionChanged += dataGridViewAutoInstall_SelectionChanged;
            dataGridViewManualInstall.SelectionChanged += dataGridViewManualInstall_SelectionChanged;
            dataGridViewAutoInstall.AddHandler(KeyDownEvent, dataGridViewAutoInstall_KeyDown, RoutingStrategies.Tunnel);
            dataGridViewManualInstall.AddHandler(KeyDownEvent, dataGridViewManualInstall_KeyDown, RoutingStrategies.Tunnel);
            textBoxAutoInstallPath.TextChanged += textBoxAutoInstallPath_TextChanged;
            buttonAutoInstallBrowse.Click += buttonAutoInstallBrowse_Click;
            buttonAutoInstallInfo.Click += buttonAutoInstallInfo_Click;
            buttonAutoInstallInstall.Click += buttonAutoInstallInstall_Click;
            buttonAutoInstallUpdate.Click += buttonAutoInstallUpdate_Click;
            buttonAutoInstallDelete.Click += buttonAutoInstallDelete_Click;
            textBoxManualInstallRoute.TextChanged += textBoxManualInstallRoute_TextChanged;
            textBoxManualInstallRoute.LostFocus += textBoxManualInstallRoute_Leave;
            textBoxManualInstallPath.TextChanged += textBoxManualInstallPath_TextChanged;
            buttonManualInstallBrowse.Click += buttonManualInstallBrowse_Click;
            buttonManualInstallAdd.Click += buttonManualInstallAdd_Click;
            buttonManualInstallDelete.Click += buttonManualInstallDelete_Click;
            tabControlContent.SelectionChanged += tabControlContent_Selecting;
            pbContent.PointerPressed += pbContent_Click;
            buttonOK.Click += buttonOK_Click;
            buttonCancel.Click += buttonCancel_Click;
            Closing += DownloadContentForm_FormClosing;

            setTextBoxesManualInstall();

            if (NoInternet)
            {
                // open the Manually Installed tab since the Auto Installed tab needs internet
                tabControlContent.SelectedItem = tabPageManuallyInstall;
            }

            if (AutoInstallRows.Count == 0)
            {
                // disable all auto install buttons for a complete empty auto install grid
                // might be the case when no internet available
                DisableAutoInstallButtonsWithoutWait();
            }
            else
            {
                dataGridViewAutoInstall.SelectedIndex = 0;
            }

            // set focus to datagridview so that arrow keys can be used to scroll thru the list
            Opened += (sender, e) => dataGridViewAutoInstall.Focus();
        }

        void changeManualInstallRoute(string Route)
        {
            bool found = false;

            foreach (var folder in Settings.Folders.Folders)
            {
                if (folder.Key == Route)
                {
                    // Route found in folder settings
                    found = true;
                }
            }

            if (found)
            {
                // search for the next route (1), (2) etc. not found in folder settings
                int seqNr = 1;
                string route = Route + " (" + seqNr + ")";
                while (found)
                {
                    found = false;
                    foreach (var folder in Settings.Folders.Folders)
                    {
                        if (folder.Key == route)
                        {
                            found = true;
                            seqNr++;
                            route = Route + " (" + seqNr + ")";
                        }
                    }
                }
                Settings.Folders.Folders[route] = Settings.Folders.Folders[Route];
                Settings.Folders.Folders.Remove(Route);
            }
        }

        private void tabControlContent_Selecting(object sender, SelectionChangedEventArgs e)
        {
            if (e.Source != tabControlContent)
                return;
            if (tabControlContent.SelectedItem == tabPageAutoInstall)
            {
                dataGridViewAutoInstall.Focus();
                buttonCancel.IsVisible = false;
            }
            else if (tabControlContent.SelectedItem == tabPageManuallyInstall)
            {
                dataGridViewManualInstall.Focus();
                buttonCancel.IsVisible = true;
                buttonCancel.IsEnabled = ManualInstallChangesMade;
            }
        }

        private void pbContent_Click(object sender, EventArgs e)
        {
            string fileName = "";
            if (tabControlContent.SelectedItem == tabPageAutoInstall)
            {
                fileName = BaseDocumentationUrl + "/start.html#content";
            }
            if (tabControlContent.SelectedItem == tabPageManuallyInstall)
            {
                fileName = BaseDocumentationUrl + "/start.html#installation-profiles";
            }

            ExternalProcess.Open(fileName);
        }

        System.Threading.Tasks.Task<MessageBoxResult> ShowMessage(string message, MessageBoxButtons buttons = MessageBoxButtons.OK, MessageBoxResult defaultResult = MessageBoxResult.None)
        {
            return MessageBox.Show(this, message, Localization.GetString("Attention"), buttons, defaultResult);
        }

        #region Auto Installed
        //
        // "Auto Installed" tab
        //

        private void dataGridViewAutoInstall_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Tab && e.KeyModifiers != KeyModifiers.Shift)
            {
                // pressing tab in this datagridview now takes you into the next field
                // default unwanted behaviour is going thru the rows/columns of the datagridview
                textBoxAutoInstallPath.Focus();
                e.Handled = true;
            }
            if (e.Key == Key.Tab && e.KeyModifiers == KeyModifiers.Shift)
            {
                // same for pressing shift-tab
                tabControlContent.Focus();
                e.Handled = true;
            }
        }

        private async void dataGridViewAutoInstall_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (CurrentAutoInstallRow == null)
                return;

            DisableAutoInstallButtons();

            AutoInstallRouteName = CurrentAutoInstallRow.Route;

            // picture box handling

            if (pictureBoxAutoInstallRoute.Source is Bitmap oldImage)
            {
                pictureBoxAutoInstallRoute.Source = null;
                oldImage.Dispose();
            }
            ImageCancellation?.Cancel();
            ImageCancellation = null;

            var imageUrl = AutoInstallRoutes[AutoInstallRouteName].Image;

            // text box with description

            textBoxAutoInstallRoute.Text = AutoInstallRoutes[AutoInstallRouteName].Description;

            // buttons
            EnableAutoInstalButtons();

            // set focus back to datagridview
            dataGridViewAutoInstall.Focus();

            if (!string.IsNullOrEmpty(imageUrl))
            {
                // grabbing the picture from the web might take a few seconds, so do it in the background
                var cancellation = ImageCancellation = new CancellationTokenSource();
                try
                {
                    // wait one second as the user might scroll through the list
                    await System.Threading.Tasks.Task.Delay(1000, cancellation.Token);
                    using (var client = new HttpClient())
                    {
                        var bytes = await client.GetByteArrayAsync(imageUrl, cancellation.Token);
                        if (!cancellation.IsCancellationRequested)
                        {
                            using (var stream = new MemoryStream(bytes))
                                pictureBoxAutoInstallRoute.Source = new Bitmap(stream);
                        }
                    }
                }
                catch
                {
                    // route lives whithout a picture, not such a problem
                }
            }
        }

        private void textBoxAutoInstallPath_TextChanged(object sender, EventArgs e)
        {
            Settings.ContentInstallPath = textBoxAutoInstallPath.Text;
        }

        private async void buttonAutoInstallBrowse_Click(object sender, EventArgs e)
        {
            var folder = await BrowseForFolder(textBoxAutoInstallPath.Text, Localization.GetString("Main Path where route is to be installed"));
            if (folder != null)
                textBoxAutoInstallPath.Text = folder;
        }

        async System.Threading.Tasks.Task<string> BrowseForFolder(string initialPath, string title)
        {
            var options = new FolderPickerOpenOptions { Title = title, AllowMultiple = false };
            if (!string.IsNullOrEmpty(initialPath) && Directory.Exists(initialPath))
                options.SuggestedStartLocation = await StorageProvider.TryGetFolderFromPathAsync(initialPath);
            var folders = await StorageProvider.OpenFolderPickerAsync(options);
            return folders.FirstOrDefault()?.TryGetLocalPath();
        }

        private async void buttonAutoInstallInfo_Click(object sender, EventArgs e)
        {
            await writeAndStartInfoFile();
        }

        private async System.Threading.Tasks.Task writeAndStartInfoFile()
        {
            using (StreamWriter outputFile = new StreamWriter(InfoTempFilename))
            {
                ContentRouteSettings.Route route = AutoInstallRoutes[AutoInstallRouteName];

                outputFile.WriteLine(string.Format("<title>OR: {0}</title>", AutoInstallRouteName));
                outputFile.WriteLine(string.Format("<h3>{0}:</h3>", AutoInstallRouteName));

                string description = route.Description.Replace("\n", "<br />");
                outputFile.WriteLine(string.Format("<p>{0}</p>", description));

                outputFile.WriteLine(string.Format("<img height='350' width='600' src = '{0}'/>", route.Image));

                if (string.IsNullOrWhiteSpace(route.AuthorUrl))
                {
                    outputFile.WriteLine(string.Format("<p>" + Localization.GetString("created by") + ": {0}</p>",
                        route.AuthorName));
                }
                else
                {
                    outputFile.WriteLine(string.Format("<p>" + Localization.GetString("created by") + ": <a href='{0}'>{1}</a></p>",
                        route.AuthorUrl, route.AuthorName));
                }

                if (!string.IsNullOrWhiteSpace(route.Screenshot))
                {
                    outputFile.WriteLine(string.Format("<p>" + Localization.GetString("screenshots") + ": <a href='{0}'>{1}</a></p>",
                        route.Screenshot, route.Screenshot));
                }

                if (route.getDownloadType() == ContentRouteSettings.DownloadType.github)
                {
                    outputFile.WriteLine("<p>" + Localization.GetString("Downloadable: GitHub format") + "<br>");
                    outputFile.WriteLine(Localization.GetStringFmt("- " + Localization.GetString("From:") + "{0}<br>", route.Url));
                    if (route.InstallSize > 0)
                    {
                        outputFile.WriteLine("- " + Localization.GetStringFmt("Install size: {0} GB",
                            (route.InstallSize / (1024.0 * 1024 * 1024)).ToString("N")) + "<br></p>");
                    }
                }
                if (route.getDownloadType() == ContentRouteSettings.DownloadType.zip)
                {
                    outputFile.WriteLine(Localization.GetString("<p>Downloadable: zip format<br>"));
                    outputFile.WriteLine(Localization.GetStringFmt("- From: {0}<br>", route.Url));
                    if (route.InstallSize > 0)
                    {
                        outputFile.WriteLine("- " + Localization.GetStringFmt("Install size: {0} GB",
                            (route.InstallSize / (1024.0 * 1024 * 1024)).ToString("N")) + "<br>");
                    }
                    if (route.DownloadSize > 0)
                    {
                        outputFile.WriteLine("- " + Localization.GetStringFmt("Download size: {0} GB",
                            (route.DownloadSize / (1024.0 * 1024 * 1024)).ToString("N")) + "<br></p>");
                    }
                }

                if (route.Installed)
                {
                    outputFile.WriteLine("<p>" + Localization.GetString("Installed") + ":<br>");
                    outputFile.WriteLine(Localization.GetStringFmt("- " + Localization.GetString("At") + ": {0}<br>", route.DateInstalled.ToString(CultureInfo.CurrentCulture.DateTimeFormat)));
                    outputFile.WriteLine(Localization.GetStringFmt("- " + Localization.GetString("In") + ": \"{0}\"<br>", route.DirectoryInstalledIn));
                    outputFile.WriteLine(Localization.GetStringFmt("- " + Localization.GetString("Content name") + ": \"{0}\"<br>", route.ContentName));
                    outputFile.WriteLine(Localization.GetStringFmt("- " + Localization.GetString("Content Directory") + ": \"{0}\"<br></p>", route.ContentDirectory));
                }

                outputFile.WriteLine("<p>" + Localization.GetString("Start options") + ":<br>");
                if (string.IsNullOrWhiteSpace(route.Start.Route))
                {
                    // no start information
                    outputFile.WriteLine("- " + Localization.GetString("None") + "<br></p>");
                }
                else
                {
                    outputFile.WriteLine("- " + Localization.GetString("Installation profile") + ": " + AutoInstallRouteName + "<br>");
                    outputFile.WriteLine("- " + Localization.GetString("Route") + ": " + route.Start.Route + "<br>");
                    outputFile.WriteLine("- " + Localization.GetString("Activity") + ": " + route.Start.Activity + "<br>");
                    if (!string.IsNullOrEmpty(route.Start.Locomotive))
                        outputFile.WriteLine("- " + Localization.GetString("Locomotive") + ": " + route.Start.Locomotive + "<br>");
                    if (!string.IsNullOrEmpty(route.Start.Consist))
                        outputFile.WriteLine("- " + Localization.GetString("Consist") + ": " + route.Start.Consist + "<br>");
                    if (!string.IsNullOrEmpty(route.Start.StartingAt))
                        outputFile.WriteLine("- " + Localization.GetString("Starting at") + ": " + route.Start.StartingAt + "<br>");
                    if (!string.IsNullOrEmpty(route.Start.HeadingTo))
                        outputFile.WriteLine("- " + Localization.GetString("Heading to") + ": " + route.Start.HeadingTo + "<br>");
                    if (!string.IsNullOrEmpty(route.Start.Time))
                        outputFile.WriteLine("- " + Localization.GetString("Time") + ": " + route.Start.Time + "<br>");
                    if (!string.IsNullOrEmpty(route.Start.Season))
                        outputFile.WriteLine("- " + Localization.GetString("Season") + ": " + route.Start.Season + "<br>");
                    if (!string.IsNullOrEmpty(route.Start.Weather))
                        outputFile.WriteLine("- " + Localization.GetString("Weather") + ": " + route.Start.Weather + "<br></p>");
                }

                if (Directory.Exists(route.DirectoryInstalledIn))
                {
                    if (route.Installed && route.getDownloadType() == ContentRouteSettings.DownloadType.zip)
                    {
                        infoChangedAndAddedFileForZipDownloadType(route, outputFile);
                    }
                    if (route.Installed && route.getDownloadType() == ContentRouteSettings.DownloadType.github)
                    {
                        bool bothLocalAsRemoteUpdatesFound = await infoChangedAndAddedFileForGitHubDownloadType(route, outputFile);
                        if (bothLocalAsRemoteUpdatesFound)
                        {
                            outputFile.WriteLine("<p><b>" + Localization.GetString("ATTENTION: both local and remote updates found.") + "</b><br>");
                            outputFile.WriteLine("<b>" + Localization.GetString("Update might fail if the update tries to overwrite a changed local file.") + "</b><br>");
                            outputFile.WriteLine("<b>" + Localization.GetString("If that's the case you are on your own!") + "</b><br>");
                            outputFile.WriteLine("<b>" + Localization.GetString("Get a git expert at your desk or just Delete and Install the route again.") + "</b><br></p>");
                        }
                    }
                }
                else
                {
                    if (!string.IsNullOrEmpty(route.DirectoryInstalledIn))
                    {
                        outputFile.WriteLine("<p>" + Localization.GetStringFmt("Directory {0} does not exist", route.DirectoryInstalledIn) + "</b><br></p>");
                    }
                }
            }
            try
            {
                // show html file in default browser
                ExternalProcess.Open(InfoTempFilename);
            }
            catch (Exception e)
            {
                string message = Localization.GetStringFmt("Error opening html page {0} in browser. Error: {1}", InfoTempFilename, e.ToString());
                await ShowMessage(message);
            }
        }

        private void infoChangedAndAddedFileForZipDownloadType(ContentRouteSettings.Route route, StreamWriter outputFile)
        {
            List<string> changedAndAddedFiles = getChangedFiles(route.DirectoryInstalledIn, route.DateInstalled);
            outputFile.WriteLine("<p id='changed'>" + Localization.GetString("Changed local file(s) after the install (timestamp check)") + ":<br>");
            if (changedAndAddedFiles.Count == 0)
            {
                outputFile.WriteLine("- " + Localization.GetString("No changed files found") + "<br></p>");
            }
            else
            {
                foreach (string changedFile in changedAndAddedFiles)
                {
                    outputFile.WriteLine(changedFile + "<br>");
                }
                outputFile.WriteLine("</p>");
            }
            changedAndAddedFiles = getAddedFiles(route.DirectoryInstalledIn, route.DateInstalled);
            outputFile.WriteLine("<p>" + Localization.GetString("Added local file(s) after the install (timestamp check)") + ":<br>");
            if (changedAndAddedFiles.Count == 0)
            {
                outputFile.WriteLine("- " + Localization.GetString("No added files found") + "<br></p>");
            }
            else
            {
                foreach (string changedFile in changedAndAddedFiles)
                {
                    outputFile.WriteLine(changedFile + "<br>");
                }
                outputFile.WriteLine("</p>");
            }
        }

        private async System.Threading.Tasks.Task<bool> infoChangedAndAddedFileForGitHubDownloadType(ContentRouteSettings.Route route, StreamWriter outputFile)
        {
            bool changedFound = false;
            bool remoteUpdateFound = false;

            List<string> changedAndAddedFiles = getChangedGitFiles(route.DirectoryInstalledIn);
            outputFile.WriteLine("<p>" + Localization.GetString("Changed local file(s) after the install (git status)") + ":<br>");
            if (changedAndAddedFiles.Count == 0)
            {
                outputFile.WriteLine("- " + Localization.GetString("No changed files found") + "<br></p>");
            }
            else
            {
                changedFound = true;
                foreach (string changedFile in changedAndAddedFiles)
                {
                    outputFile.WriteLine(changedFile + "<br>");
                }
                outputFile.WriteLine("</p>");
            }
            changedAndAddedFiles = getAddedGitFiles(route.DirectoryInstalledIn);
            outputFile.WriteLine("<p>" + Localization.GetString("Added local file(s) after the install (git status)") + ":<br>");
            if (changedAndAddedFiles.Count == 0)
            {
                outputFile.WriteLine("- " + Localization.GetString("No added files found") + "<br></p>");
            }
            else
            {
                changedFound = true;
                foreach (string changedFile in changedAndAddedFiles)
                {
                    outputFile.WriteLine(changedFile + "<br>");
                }
                outputFile.WriteLine("</p>");
            }

            outputFile.WriteLine("<p>" + Localization.GetString("Remote GitHub Updates available:") + "<br>");

            List<string> commitStrings = await getCommits(route.DirectoryInstalledIn);
            if (commitStrings.Count > 0)
            {
                remoteUpdateFound = true;
                for (int index = 0; index < commitStrings.Count; index += 4)
                {
                    outputFile.WriteLine(string.Format("- commit {0}<br>", commitStrings[index]));
                    outputFile.WriteLine(Localization.GetStringFmt("On {0} by {1}",
                        commitStrings[index + 1], commitStrings[index + 2]) + ":<br>");
                    outputFile.WriteLine(commitStrings[index + 3] + "<br><br>");
                }
                outputFile.WriteLine("</p>");
            }
            else
            {
                outputFile.WriteLine("- " + Localization.GetString("No updates found") + "<br></p>");
            }

            return changedFound && remoteUpdateFound;
        }

        private async void buttonAutoInstallInstall_Click(object sender, EventArgs e)
        {
            ContentRouteSettings.Route route = AutoInstallRoutes[AutoInstallRouteName];
            var row = CurrentAutoInstallRow;

            string installPath = textBoxAutoInstallPath.Text ?? "";
            if (installPath.EndsWith(Path.DirectorySeparatorChar.ToString()) || installPath.EndsWith(Path.AltDirectorySeparatorChar.ToString()))
            {
                installPath = installPath.Remove(installPath.Length - 1, 1);
            }
            installPath = Path.GetFullPath(installPath);

            string installPathRoute = Path.Combine(installPath, AutoInstallRouteName);
            string message;

            DisableAutoInstallButtons();

            message = Localization.GetStringFmt("Route to be installed in \"{0}\", are you sure?", installPathRoute);
            if (await ShowMessage(message, MessageBoxButtons.YesNo) != MessageBoxResult.Yes)
            {
                // cancelled
                EnableAutoInstalButtons();
                return;
            }

            // various checks for the directory where the route is installed

            if (await isWrongPath(this, installPath))
            {
                // cancelled
                EnableAutoInstalButtons();
                return;
            }

            try
            {
                // The route's directory may not exist yet; check the space where it will be.
                var existingPath = installPathRoute;
                while (!Directory.Exists(existingPath) && Path.GetDirectoryName(existingPath) != null)
                    existingPath = Path.GetDirectoryName(existingPath);
                DriveInfo dInfo = new DriveInfo(existingPath);

                long size = route.InstallSize + route.DownloadSize;

                if (size > (dInfo.AvailableFreeSpace * 1.1))
                {
                    message = Localization.GetStringFmt("Not enough diskspace on drive {0} ({1}), available {2} kB, needed {3} kB, still continue?",
                        dInfo.Name,
                        dInfo.VolumeLabel,
                        (dInfo.AvailableFreeSpace / 1024).ToString("N0"),
                        (size / 1024).ToString("N0"));

                    if (await ShowMessage(message, MessageBoxButtons.YesNo) != MessageBoxResult.Yes)
                    {
                        // cancelled
                        EnableAutoInstalButtons();
                        return;
                    }
                }
            }
            catch (Exception error) when (error is DriveNotFoundException || error is IOException || error is ArgumentException || error is UnauthorizedAccessException)
            {
                message = Localization.GetStringFmt("Drive not available");
                await ShowMessage(message);
                // cancelled
                EnableAutoInstalButtons();
                return;
            }

            if (!Directory.Exists(installPathRoute))
            {
                try
                {
                    Directory.CreateDirectory(installPathRoute);
                }
                catch (Exception createDirectoryException)
                {
                    message = Localization.GetStringFmt("Directory \"{0}\" cannot be created: {1}",
                        installPathRoute, createDirectoryException.Message);
                    await ShowMessage(message);
                    EnableAutoInstalButtons();
                    return;
                }
            }
            else
            {
                if ((Directory.GetFiles(installPathRoute).Length != 0) ||
                    (Directory.GetDirectories(installPathRoute).Length != 0))
                {
                    message = Localization.GetStringFmt("Directory \"{0}\" exists and is not empty", installPathRoute);
                    await ShowMessage(message);
                    EnableAutoInstalButtons();
                    return;
                }
            }

            // the download

            row.Installed = Localization.GetString("Installing...");

            WorkerError = null;
            var routeName = AutoInstallRouteName;
            if (!await System.Threading.Tasks.Task.Run(() => downloadRoute(routeName, row, installPathRoute)))
            {
                if (WorkerError != null)
                    await ShowMessage(WorkerError);
                EnableAutoInstalButtons();
                return;
            }

            // insert row in Options, tab Content

            if (!await insertRowInOptions(installPathRoute))
            {
                EnableAutoInstalButtons();
                return;
            }

            route.Installed = true;

            DateTime dateTimeNow = DateTime.Now;
            row.Installed = dateTimeNow.ToString(CultureInfo.CurrentCulture.DateTimeFormat);
            route.DateInstalled = dateTimeNow;

            route.DirectoryInstalledIn = installPathRoute;

            Settings.Folders.Save();
            Settings.Content.Save();

            MainForm.LoadFolderListWithoutTask();
            SelectInComboBox(MainForm.comboBoxFolder, AutoInstallRouteName);

            await MessageBox.Show(this, Localization.GetString("Route installed."), Localization.GetString("Done"));

            EnableAutoInstalButtons();
        }

        private bool downloadRoute(string routeName, AutoInstallRow row, string installPathRoute)
        {
            ContentRouteSettings.Route route = AutoInstallRoutes[routeName];
            bool returnValue = false;

            Thread downloadThread = new Thread(() =>
            {
                if (route.getDownloadType() == ContentRouteSettings.DownloadType.github)
                {
                    returnValue = doTheClone(route.Url, installPathRoute);
                }
                if (route.getDownloadType() == ContentRouteSettings.DownloadType.zip)
                {
                    returnValue = doTheZipDownload(route.Url, Path.Combine(installPathRoute, routeName + ".zip"));
                }
            });
            // start download in thread to be able to show the progress in the main thread
            downloadThread.Start();

            while (downloadThread.IsAlive)
            {
                TotalBytes = 0;

                sumMB(installPathRoute);
                var kB = (TotalBytes / 1024).ToString("N0");
                Dispatcher.UIThread.Post(() => row.Installed = string.Format("Downloaded: {0} kB", kB));

                downloadThread.Join(1000);
            }

            if (returnValue)
            {
                if (route.Url.EndsWith(".zip"))
                {
                    Thread installThread = new Thread(() =>
                    {
                        returnValue = doTheUnzipInstall(Path.Combine(installPathRoute, routeName + ".zip"), installPathRoute);
                    });
                    installThread.Start();

                    long bytesZipfile = TotalBytes;

                    while (installThread.IsAlive)
                    {
                        TotalBytes = -bytesZipfile;
                        sumMB(installPathRoute);
                        var kB = (TotalBytes / 1024).ToString("N0");
                        Dispatcher.UIThread.Post(() => row.Installed = string.Format("Installed: {0} kB", kB));

                        installThread.Join(1000);
                    }
                }
            }

            Dispatcher.UIThread.Post(() => row.Installed = "");

            return returnValue;
        }

        private bool doTheClone(string url, string installPathRoute)
        {
            try
            {
                Repository.Clone(url, installPathRoute);
            }
            catch (LibGit2SharpException libGit2SharpException)
            {
                WorkerError = Localization.GetStringFmt("Error during github download: {0}", libGit2SharpException.Message);
                return false;
            }

            return true;
        }

        private bool doTheZipDownload(string url, string installPathRouteZipfileName)
        {
            try
            {
                using (var client = new HttpClient())
                using (var response = client.GetAsync(url, HttpCompletionOption.ResponseHeadersRead).GetAwaiter().GetResult())
                {
                    response.EnsureSuccessStatusCode();
                    using (var input = response.Content.ReadAsStream())
                    using (var output = File.Create(installPathRouteZipfileName))
                        input.CopyTo(output);
                }
            }
            catch (Exception error)
            {
                WorkerError = Localization.GetStringFmt("Error during download zipfile {0}: {1}", url, error.Message);
                return false;
            }

            return true;
        }

        private bool doTheUnzipInstall(string installPathRouteZipfileName, string installPathRoute)
        {
            try
            {
                ZipFile.ExtractToDirectory(installPathRouteZipfileName, installPathRoute);
                File.Delete(installPathRouteZipfileName);
            }
            catch (Exception error)
            {
                WorkerError = Localization.GetStringFmt("Error during unzip zipfile {0}: {1}", installPathRouteZipfileName, error.Message);
                return false;
            }

            return true;
        }

        long TotalBytes = 0;

        private bool sumMB(string path)
        {
            try
            {
                foreach (string fileName in Directory.GetFiles(path))
                {
                    try
                    {
                        TotalBytes += new FileInfo(fileName).Length;
                    }
                    catch (Exception)
                    {
                        // this summing is also used during the delete,
                        // so sometimes the file is already gone
                    }
                }

                foreach (string directoryName in Directory.GetDirectories(path))
                {
                    try
                    {
                        sumMB(directoryName);
                    }
                    catch (Exception)
                    {
                        return false;
                    }
                }
            }
            catch (Exception)
            {
                return false;
            }

            return true;
        }

        private async System.Threading.Tasks.Task<bool> insertRowInOptions(string installPathRoute)
        {
            // sometimes the route is located one directory level deeper, determine real installPathRoute
            string installPathRouteReal;

            if (Directory.Exists(Path.Combine(installPathRoute, "routes")) || Directory.Exists(Path.Combine(installPathRoute, "ROUTES")))
            {
                installPathRouteReal = installPathRoute;
            }
            else
            {
                string[] directories = Directory.GetDirectories(installPathRoute);
                int indexDirectories = 0;
                while ((indexDirectories < directories.Length) &&
                    !Directory.Exists(Path.Combine(directories[indexDirectories], "routes")) &&
                    !Directory.Exists(Path.Combine(directories[indexDirectories], "ROUTES")))
                {
                    indexDirectories++;
                }
                if (indexDirectories < directories.Length)
                {
                    installPathRouteReal = Path.Combine(installPathRoute, directories[indexDirectories]);
                }
                else
                {
                    string message = Localization.GetString("Incorrect route, directory \"routes\" not found");
                    await ShowMessage(message);
                    return false;
                }
            }

            string routeName = AutoInstallRouteName;
            int index = 0;
            var sortedFolders = Settings.Folders.Folders.OrderBy(pair => pair.Key).ToDictionary(pair => pair.Key, pair => pair.Value);
            foreach (KeyValuePair<string, string> folderSetting in sortedFolders)
            {
                if (folderSetting.Key == routeName)
                {
                    if (!folderSetting.Value.Equals(installPathRouteReal, StringComparison.OrdinalIgnoreCase))
                    {
                        index++;
                        routeName = string.Format("{0} ({1})", AutoInstallRouteName, index);
                    }
                }
            }
            Settings.Folders.Folders[routeName] = installPathRouteReal;
            AutoInstallRoutes[AutoInstallRouteName].ContentName = routeName;
            AutoInstallRoutes[AutoInstallRouteName].ContentDirectory = installPathRouteReal;

            // insert into Manually Installed data grid
            ManualInstallRows.Add(new ManualInstallRow
            {
                Route = routeName,
                Path = installPathRouteReal,
                IsAutoInstalled = true,
            });
            SortManualInstallRows();

            return true;
        }

        void SortManualInstallRows()
        {
            var sorted = ManualInstallRows.OrderBy(r => r.Route, StringComparer.CurrentCulture).ToList();
            for (var i = 0; i < sorted.Count; i++)
            {
                var oldIndex = ManualInstallRows.IndexOf(sorted[i]);
                if (oldIndex != i)
                    ManualInstallRows.Move(oldIndex, i);
            }
        }

        private async void buttonAutoInstallUpdate_Click(object sender, EventArgs e)
        {
            ContentRouteSettings.Route route = AutoInstallRoutes[AutoInstallRouteName];
            string message;

            DisableAutoInstallButtons();

            List<string> commitStrings = await getCommits(route.DirectoryInstalledIn);
            if (commitStrings.Count > 0)
            {
                message = Localization.GetString("Remote updates found. Do you want to continue?");
                if (await ShowMessage(message, MessageBoxButtons.YesNo, MessageBoxResult.No) != MessageBoxResult.Yes)
                {
                    // cancelled
                    EnableAutoInstalButtons();
                    return;
                }
                if (areThereChangedAddedFiles(route))
                {
                    message = Localization.GetString("Changed or added local files found, Update might fail. Do you want to continue?");
                    if (await ShowMessage(message, MessageBoxButtons.YesNo, MessageBoxResult.No) != MessageBoxResult.Yes)
                    {
                        // cancelled
                        EnableAutoInstalButtons();
                        return;
                    }
                }
                WorkerError = null;
                if (await System.Threading.Tasks.Task.Run(() => doThePull(route)))
                {
                    message = Localization.GetString("Updates installed");
                    await ShowMessage(message);
                }
                else if (WorkerError != null)
                {
                    await ShowMessage(WorkerError);
                }
            }
            else
            {
                message = Localization.GetString("No updates found");
                await ShowMessage(message);
            }

            SelectInComboBox(MainForm.comboBoxFolder, AutoInstallRouteName);

            EnableAutoInstalButtons();
        }

        private bool doThePull(ContentRouteSettings.Route route)
        {
            try
            {
                using (var repo = new Repository(route.DirectoryInstalledIn))
                {
                    LibGit2Sharp.PullOptions options = new LibGit2Sharp.PullOptions
                    {
                        FetchOptions = new FetchOptions()
                    };

                    // User information to create a merge commit
                    var signature = new LibGit2Sharp.Signature(
                        new Identity("Open Rails", "www.openrails.org"), DateTimeOffset.Now);

                    // Pull
                    Commands.Pull(repo, signature, options);
                }
            }
            catch (LibGit2SharpException libGit2SharpException)
            {
                WorkerError = Localization.GetStringFmt("Error during github pull: {0}", libGit2SharpException.Message);
                return false;
            }
            return true;
        }

        private async void buttonAutoInstallDelete_Click(object sender, EventArgs e)
        {
            ContentRouteSettings.Route route = AutoInstallRoutes[AutoInstallRouteName];
            var row = CurrentAutoInstallRow;
            string message;

            DisableAutoInstallButtons();

            message = Localization.GetStringFmt("Directory \"{0}\" is to be deleted, are you really sure?", route.DirectoryInstalledIn);
            if (await ShowMessage(message, MessageBoxButtons.YesNo, MessageBoxResult.No) != MessageBoxResult.Yes)
            {
                // cancelled
                EnableAutoInstalButtons();
                return;
            }

            if (Directory.Exists(route.DirectoryInstalledIn))
            {

                if (areThereChangedAddedFiles(route))
                {
                    message = Localization.GetStringFmt("Changed or added local files found in Directory \"{0}\". Do you want to continue?", route.DirectoryInstalledIn);
                    if (await ShowMessage(message, MessageBoxButtons.YesNo, MessageBoxResult.No) != MessageBoxResult.Yes)
                    {
                        // cancelled
                        EnableAutoInstalButtons();
                        return;
                    }
                }

                await System.Threading.Tasks.Task.Run(() => deleteRoute(row, route.DirectoryInstalledIn));
            }

            if (Settings.Folders.Folders.ContainsKey(route.ContentName))
            {
                if (Settings.Folders.Folders[route.ContentName] == route.ContentDirectory)
                {
                    Settings.Folders.Folders.Remove(route.ContentName);
                    var manualRow = ManualInstallRows.FirstOrDefault(r => r.Route == route.ContentName);
                    if (manualRow != null)
                    {
                        ManualInstallRows.Remove(manualRow);
                    }
                }
                Settings.Folders.Save();
            }

            route.Installed = false;
            route.DateInstalled = DateTime.MinValue;
            route.DirectoryInstalledIn = "";

            // remove from registry, but leave entry in Routes for this route name
            ContentRouteSettings.Route routeSaved = route;
            Settings.Content.Save();
            AutoInstallRoutes[AutoInstallRouteName] = routeSaved;

            row.Installed = "";

            MainForm.LoadFolderListWithoutTask();

            EnableAutoInstalButtons();
        }

        private void deleteRoute(AutoInstallRow row, string directoryInstalledIn)
        {
            Thread deleteThread = new Thread(() =>
            {
                ContentRouteSettings.directoryDelete(directoryInstalledIn, ref AutoInstallDoingTheSumOfTheFileBytes);
            });
            // start delete in thread to be able to show the progress in the main thread
            deleteThread.Start();

            while (deleteThread.IsAlive)
            {
                // this will stop the delete until the sum is done
                AutoInstallDoingTheSumOfTheFileBytes = true;

                TotalBytes = 0;
                if (sumMB(directoryInstalledIn))
                {
                    var kB = (TotalBytes / 1024).ToString("N0");
                    Dispatcher.UIThread.Post(() => row.Installed = string.Format("Left: {0} kB", kB));
                }

                AutoInstallDoingTheSumOfTheFileBytes = false;

                deleteThread.Join(1000);
            }
        }

        private void DisableAutoInstallButtons()
        {
            setCursorToWaitCursor();
            DisableAutoInstallButtonsWithoutWait();
        }

        private void DisableAutoInstallButtonsWithoutWait()
        {
            dataGridViewAutoInstall.IsEnabled = false;
            textBoxAutoInstallPath.IsEnabled = false;
            buttonAutoInstallBrowse.IsEnabled = false;
            buttonAutoInstallInfo.IsEnabled = false;
            buttonAutoInstallInstall.IsEnabled = false;
            buttonAutoInstallUpdate.IsEnabled = false;
            buttonAutoInstallDelete.IsEnabled = false;
            buttonOK.IsEnabled = false;
            if (AutoInstallRows.Count == 0)
            {
                // Nothing to wait for in an empty grid; the OK button must still work.
                buttonOK.IsEnabled = true;
                textBoxAutoInstallPath.IsEnabled = true;
                buttonAutoInstallBrowse.IsEnabled = true;
            }
        }

        private void setCursorToWaitCursor()
        {
            AutoInstallClosingBlocked = true;

            Cursor = new Cursor(StandardCursorType.Wait);
            MainForm.Cursor = new Cursor(StandardCursorType.Wait);
        }

        private void EnableAutoInstalButtons()
        {
            ContentRouteSettings.Route route = AutoInstallRoutes[AutoInstallRouteName];

            dataGridViewAutoInstall.IsEnabled = true;
            textBoxAutoInstallPath.IsEnabled = true;
            buttonAutoInstallBrowse.IsEnabled = true;
            buttonAutoInstallInfo.IsEnabled = !NoInternet;
            buttonAutoInstallInstall.IsEnabled = !(route.Installed || NoInternet);
            buttonAutoInstallUpdate.IsEnabled = route.Installed && (route.getDownloadType() == ContentRouteSettings.DownloadType.github) && !NoInternet;
            buttonAutoInstallDelete.IsEnabled = route.Installed;
            buttonOK.IsEnabled = true;

            setCursorToDefaultCursor();
        }

        private void setCursorToDefaultCursor()
        {
            Cursor = Cursor.Default;
            MainForm.Cursor = Cursor.Default;

            AutoInstallClosingBlocked = false;
        }

        /// <summary>
        /// Selects the main window's installation profile (or other item) by name, like the WinForms determineSelectedIndex().
        /// </summary>
        private void SelectInComboBox(ComboBox comboBox, string compareWith)
        {
            try
            {
                comboBox.SelectedIndex = determineSelectedIndex(comboBox, compareWith);
            }
            catch (StartNotFound)
            {
                // The installation profile isn't (yet) in the list; leave the selection alone.
            }
        }

        private int determineSelectedIndex(ComboBox comboBox, string compareWith)
        {
            bool found = false;
            int index = 0;

            string classOfItem;
            if (comboBox.Items.Count > 0)
            {
                classOfItem = comboBox.Items[0].GetType().Name;
            }
            else
            {
                throw new StartNotFound(compareWith);
            }

            if ((classOfItem == "DefaultExploreActivity") && (compareWith == "- Explore Route -"))
            {
                // "- Explore Route -" gets translated,
                // so it might not be found in the combobox
                found = true;
                index = 0;
            }
            if ((classOfItem == "DefaultExploreActivity") && (compareWith == "+ Explore in Activity Mode +"))
            {
                // "+ Explore in Activity Mode +" gets translated
                found = true;
                index = 1;
            }

            while (!found && (index < comboBox.Items.Count))
            {
                string comboboxName = "";
                switch (classOfItem)
                {
                    case "Folder":
                        comboboxName = ((ORTS.Menu.Folder)comboBox.Items[index]).Name;
                        break;
                    case "Route":
                        comboboxName = ((ORTS.Menu.Route)comboBox.Items[index]).Name;
                        break;
                    case "DefaultExploreActivity":
                        comboboxName = ((ORTS.Menu.Activity)comboBox.Items[index]).Name;
                        break;
                    case "Locomotive":
                        comboboxName = ((ORTS.Menu.Locomotive)comboBox.Items[index]).Name;
                        break;
                    case "Consist":
                        comboboxName = ((ORTS.Menu.Consist)comboBox.Items[index]).Name;
                        break;
                    case "String":
                        comboboxName = (string)comboBox.Items[index];
                        break;
                    case "Path":
                        comboboxName = ((ORTS.Menu.Path)comboBox.Items[index]).End;
                        break;
                    case "KeyedComboBoxItem":
                        comboboxName = ((MainWindow.KeyedComboBoxItem)comboBox.Items[index]).Value;
                        break;
                }

                if (comboboxName == compareWith)
                {
                    found = true;
                }
                else
                {
                    index++;
                }
            }
            if (!found)
            {
                if (classOfItem == "Route")
                {
                    // if a route is not found and amount of routes is 1
                    // then default to this one and only route
                    if (comboBox.Items.Count == 1)
                    {
                        index = 0;
                    }
                    else
                    {
                        throw new StartNotFound(compareWith);
                    }
                }
                else
                {
                    throw new StartNotFound(compareWith);
                }
            }

            return index;
        }

        private class StartNotFound : Exception
        {
            public StartNotFound(string message)
                : base(message)
            { }
        }

        private bool areThereChangedAddedFiles(ContentRouteSettings.Route route)
        {
            if (route.getDownloadType() == ContentRouteSettings.DownloadType.zip)
            {
                return
                    (getChangedFiles(route.DirectoryInstalledIn, route.DateInstalled).Count > 0) ||
                    (getAddedFiles(route.DirectoryInstalledIn, route.DateInstalled).Count > 0);
            }
            if (route.getDownloadType() == ContentRouteSettings.DownloadType.github)
            {
                return
                    (getChangedGitFiles(route.DirectoryInstalledIn).Count > 0) ||
                    (getAddedGitFiles(route.DirectoryInstalledIn).Count > 0);
            }

            return false;
        }

        private static List<string> getChangedFiles(string directoryName, DateTime dateInstalled)
        {
            return getChangedAndAddedFiles(directoryName, dateInstalled, true);
        }

        private static List<string> getAddedFiles(string directoryName, DateTime dateInstalled)
        {
            return getChangedAndAddedFiles(directoryName, dateInstalled, false);
        }

        private static List<string> getChangedAndAddedFiles(string directoryName, DateTime dateInstalled, bool checkForChanged)
        {
            List<string> changedFiles = new List<string>();

            if (Directory.Exists(directoryName))
            {
                getChangedAndAddedFilesDeeper(directoryName, dateInstalled, changedFiles, checkForChanged);
            }

            return changedFiles;
        }

        private static void getChangedAndAddedFilesDeeper(string directoryName, DateTime dateInstalled, List<string> changedFiles, bool checkForChanged)
        {
            foreach (string filename in Directory.GetFiles(directoryName))
            {
                FileInfo fi = new FileInfo(filename);
                if (checkForChanged)
                {
                    if ((fi.LastWriteTime > dateInstalled) && (fi.CreationTime < dateInstalled))
                    {
                        changedFiles.Add(fi.FullName);
                    }
                }
                else
                {
                    // check for new files, creation date after date installed
                    if (fi.CreationTime > dateInstalled)
                    {
                        changedFiles.Add(fi.FullName);
                    }
                }
            }
            foreach (string subDirectoryName in Directory.GetDirectories(directoryName))
            {
                getChangedAndAddedFilesDeeper(subDirectoryName, dateInstalled, changedFiles, checkForChanged);
            }
        }

        private static List<string> getChangedGitFiles(string directoryName)
        {
            return getChangedAndAddedGitFiles(directoryName, true);
        }

        private static List<string> getAddedGitFiles(string directoryName)
        {
            return getChangedAndAddedGitFiles(directoryName, false);
        }

        private static List<string> getChangedAndAddedGitFiles(string directoryName, bool checkForChanged)
        {
            List<string> changedFiles = new List<string>();

            using (var repo = new Repository(directoryName))
            {
                foreach (var item in repo.RetrieveStatus(new LibGit2Sharp.StatusOptions()))
                {
                    if (checkForChanged)
                    {
                        if (item.State == FileStatus.NewInWorkdir)
                        {
                            changedFiles.Add(item.FilePath);
                        }
                    }
                    else
                    {
                        if (item.State == FileStatus.ModifiedInWorkdir)
                        {
                            changedFiles.Add(item.FilePath);
                        }
                    }
                }
            }

            return changedFiles;
        }

        private async System.Threading.Tasks.Task<List<string>> getCommits(string installPathRoute)
        {
            List<string> commits = new List<string>();

            try
            {
                using (var repo = new Repository(installPathRoute))
                {
                    var options = new FetchOptions
                    {
                        Prune = true,
                        TagFetchMode = TagFetchMode.Auto
                    };
                    var remote = repo.Network.Remotes["origin"];
                    var msg = "Fetching remote";
                    var refSpecs = remote.FetchRefSpecs.Select(x => x.Specification);
                    Commands.Fetch(repo, remote.Name, refSpecs, options, msg);

                    var filter = new CommitFilter
                    {
                        ExcludeReachableFrom = repo.Branches["main"],
                        IncludeReachableFrom = repo.Branches["origin/Head"],
                    };
                    // at first I tried to create a List<Commit>, but that resulted
                    // in some strange access violation when reading the list
                    // --> hence this workaround
                    foreach (Commit commit in repo.Commits.QueryBy(filter))
                    {
                        commits.Add(commit.Id.ToString());
                        commits.Add(commit.Author.When.ToString(CultureInfo.CurrentCulture.DateTimeFormat));
                        commits.Add(commit.Author.Name);
                        commits.Add(commit.Message);
                    }
                }
            }
            catch (LibGit2SharpException libGit2SharpException)
            {
                string message = Localization.GetStringFmt("Error during GitHub pull, updates might not be installed, error: {0}", libGit2SharpException.Message);
                await ShowMessage(message);
            }

            return commits;
        }
        #endregion

        #region Manually Installed
        //
        // "Manually Installed" tab
        //

        private void dataGridViewManualInstall_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Tab && e.KeyModifiers != KeyModifiers.Shift)
            {
                // pressing tab in this datagridview now takes you into the next field
                // default unwanted behaviour is going thru the rows/columns of the datagridview
                buttonManualInstallAdd.Focus();
                e.Handled = true;
            }
            if (e.Key == Key.Tab && e.KeyModifiers == KeyModifiers.Shift)
            {
                // same for pressing shift-tab
                tabControlContent.Focus();
                e.Handled = true;
            }
        }

        private void dataGridViewManualInstall_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (!In_buttonManualInstallAdd_Click)
            {
                In_dataGridViewManualInstall_SelectionChanged = true;
                setTextBoxesManualInstall();
                In_dataGridViewManualInstall_SelectionChanged = false;
            }
        }

        private void textBoxManualInstallRoute_TextChanged(object sender, EventArgs e)
        {
            if (!In_dataGridViewManualInstall_SelectionChanged && CurrentManualInstallRow != null)
            {
                // only update the grid when user is filling/changing the route in the textbox
                CurrentManualInstallRow.Route = textBoxManualInstallRoute.Text;
                ManualInstallChangesMade = true;
                buttonCancel.IsEnabled = true;
            }
        }

        private void textBoxManualInstallRoute_Leave(object sender, EventArgs e)
        {
            if (CurrentManualInstallRow == null || !textBoxManualInstallRoute.IsEnabled)
                return;
            string route = (textBoxManualInstallRoute.Text ?? "").Trim();
            textBoxManualInstallRoute.Text = determineUniqueRoute(route);
            if (textBoxManualInstallRoute.Text != route)
            {
                CurrentManualInstallRow.Route = textBoxManualInstallRoute.Text;
            }
        }

        private async void textBoxManualInstallPath_TextChanged(object sender, EventArgs e)
        {
            if (!In_dataGridViewManualInstall_SelectionChanged && CurrentManualInstallRow != null)
            {
                var row = CurrentManualInstallRow;
                var path = textBoxManualInstallPath.Text ?? "";
                // only update the grid when user is filling/changing the path in the textbox
                if (!await isWrongPath(this, path))
                {
                    row.Path = path;
                    ManualInstallChangesMade = true;
                    buttonCancel.IsEnabled = true;
                }
            }
        }

        private async void buttonManualInstallBrowse_Click(object sender, EventArgs e)
        {
            var folder = await BrowseForFolder(textBoxManualInstallPath.Text, Localization.GetString("Path where the route is installed"));
            if (folder != null)
                textBoxManualInstallPath.Text = folder;
        }

        private async void buttonManualInstallAdd_Click(object sender, EventArgs e)
        {
            In_buttonManualInstallAdd_Click = true;

            var currentRow = CurrentManualInstallRow;
            var addedRow = new ManualInstallRow { Route = "", Path = "" };
            ManualInstallRows.Add(addedRow);
            dataGridViewManualInstall.SelectedItem = addedRow;
            dataGridViewManualInstall.ScrollIntoView(addedRow, null);

            var folder = await BrowseForFolder(ManualInstallBrouwseDir, Localization.GetString("Path where the route is installed"));
            if (folder != null)
            {
                // OK, fill the empty row
                string route = determineUniqueRoute(Path.GetFileName(folder.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar)));
                addedRow.Route = route;
                addedRow.Path = folder;
                ManualInstallChangesMade = true;
                buttonCancel.IsEnabled = ManualInstallChangesMade;
            }
            else
            {
                // Cancel, set focus back to where is was and remove the empty row
                ManualInstallRows.Remove(addedRow);
                if (currentRow != null)
                {
                    dataGridViewManualInstall.SelectedItem = currentRow;
                    dataGridViewManualInstall.ScrollIntoView(currentRow, null);
                }
            }

            In_buttonManualInstallAdd_Click = false;

            In_dataGridViewManualInstall_SelectionChanged = true;
            setTextBoxesManualInstall();
            In_dataGridViewManualInstall_SelectionChanged = false;
        }

        private void buttonManualInstallDelete_Click(object sender, EventArgs e)
        {
            if (CurrentManualInstallRow != null)
            {
                ManualInstallRows.Remove(CurrentManualInstallRow);
                ManualInstallChangesMade = true;
                buttonCancel.IsEnabled = ManualInstallChangesMade;
            }
        }

        private void setTextBoxesManualInstall()
        {
            var row = CurrentManualInstallRow;
            if (row != null)
            {
                string route = row.Route ?? "";
                string path = row.Path ?? "";

                if (!AutoInstallRoutes.ContainsKey(route))
                {
                    // route not automatically installed
                    textBoxManualInstallRoute.Text = route;
                    textBoxManualInstallPath.Text = path;
                    textBoxManualInstallRoute.IsEnabled = true;
                    textBoxManualInstallPath.IsEnabled = true;
                    buttonManualInstallBrowse.IsEnabled = true;
                    buttonManualInstallDelete.IsEnabled = true;
                }
                else
                {
                    // route automatically installed
                    textBoxManualInstallRoute.Text = "";
                    textBoxManualInstallPath.Text = "";
                    textBoxManualInstallRoute.IsEnabled = false;
                    textBoxManualInstallPath.IsEnabled = false;
                    buttonManualInstallBrowse.IsEnabled = false;
                    buttonManualInstallDelete.IsEnabled = false;
                }
            }
            else
            {
                // empty form, like after installing OR
                textBoxManualInstallRoute.Text = "";
                textBoxManualInstallPath.Text = "";
                textBoxManualInstallRoute.IsEnabled = false;
                textBoxManualInstallPath.IsEnabled = false;
                buttonManualInstallBrowse.IsEnabled = false;
                buttonManualInstallDelete.IsEnabled = false;
            }
        }

        string determineUniqueRoute(string Route)
        {
            string route = Route;
            long seqNr = 0;
            bool foundUniqueRoute = false;

            if (AutoInstallRoutes.ContainsKey(route))
            {
                // route already exists in the AutoInstall routes
                seqNr = 1;
                route = Route + " (" + seqNr + ")";
            }

            var selected = CurrentManualInstallRow;
            while (!foundUniqueRoute)
            {
                bool found = false;
                foreach (var row in ManualInstallRows)
                {
                    if ((row != selected) && (row.Route == route))
                    {
                        seqNr++;
                        route = Route + " (" + seqNr + ")";
                        found = true;
                    }
                }
                if (!found)
                {
                    foundUniqueRoute = true;
                }
            }

            return route;
        }

        string determineBrowseDir()
        {
            Dictionary<string, int> subDirs = new Dictionary<string, int>();
            foreach (var folder in Settings.Folders.Folders)
            {
                DirectoryInfo parentDir;
                try
                {
                    parentDir = Directory.GetParent(folder.Value);
                }
                catch
                {
                    continue;
                }
                if (parentDir == null) { continue; }  // ignore content in top-level dir
                string subDir = parentDir.ToString();
                if (subDirs.ContainsKey(subDir))
                {
                    subDirs[subDir] += 1;
                }
                else
                {
                    subDirs.Add(subDir, 1);
                }
            }

            int max = 0;
            foreach (var subDir in subDirs)
            {
                if (subDir.Value > max)
                {
                    max = subDir.Value;
                }
            }

            string browseDir = "";
            foreach (var subDir in subDirs)
            {
                if (subDir.Value == max)
                {
                    browseDir = subDir.Key;
                }
            }

            return browseDir;
        }

        #endregion

        private static async System.Threading.Tasks.Task<bool> isWrongPath(Window owner, string path)
        {
            var programFolder = ApplicationInfo.ProcessDirectory;
            // Windows file names are case-insensitive, Linux ones are not.
            var comparison = OperatingSystem.IsLinux() ? StringComparison.Ordinal : StringComparison.OrdinalIgnoreCase;
            if (path.IndexOf(programFolder, comparison) >= 0)
            {
                // check added because a succesful Update operation will empty the Open Rails folder and lose any content stored within it.
                await MessageBox.Show(owner, Localization.GetString
                        ($"Cannot use content from any folder which lies inside the Open Rails program folder {programFolder}\n\n"),
                    "Invalid content location");
                return true;
            }

            return false;
        }

        private async void buttonCancel_Click(object sender, EventArgs e)
        {
            string message = Localization.GetString("Cancel: changes made in the 'Manually Installed' tab will not be saved, are you sure?");
            if (await ShowMessage(message, MessageBoxButtons.YesNo) == MessageBoxResult.No)
            {
                // not sure, cancel the cancel
                return;
            }
            // sure to cancel the changes
            ManualInstallChangesMade = false;
            CloseConfirmed = true;
            Close(DialogResult.Cancel);
        }

        private void buttonOK_Click(object sender, EventArgs e)
        {
            // save "Manually Installed" tab changes into the registry/ini file via Settings.Folders.Folders

            // loop settings to check if a setting is deleted or changed
            foreach (var folder in Settings.Folders.Folders.ToList())
            {
                string route = folder.Key;
                string path = folder.Value;
                if (!AutoInstallRoutes.ContainsKey(folder.Key))
                {
                    // not part of the "Auto Installed" routes
                    string pathInSettings = findPathInDgvManualInstall(route);
                    if (string.IsNullOrEmpty(pathInSettings))
                    {
                        // deleted, remove from settings
                        Settings.Folders.Folders.Remove(route);
                    }
                    else
                    {
                        if (!path.Equals(pathInSettings))
                        {
                            // path changed
                            Settings.Folders.Folders[route] = pathInSettings;
                        }
                    }
                }
            }

            // loop datagridview to find new entries

            foreach (var row in ManualInstallRows)
            {
                string route = row.Route ?? "";
                string path = row.Path ?? "";

                if (!AutoInstallRoutes.ContainsKey(route) && route.Length > 0)
                {
                    // not part of the "Auto Installed" routes
                    if (!Settings.Folders.Folders.ContainsKey(route))
                    {
                        Settings.Folders.Folders.Add(route, path);

                        // set the main form to this added route
                        MainForm.LoadFolderListWithoutTask();
                        SelectInComboBox(MainForm.comboBoxFolder, route);
                    }
                }
            }

            Settings.Save();

            ManualInstallChangesMade = false;

            CloseConfirmed = true;
            Close(DialogResult.OK);
        }

        private string findPathInDgvManualInstall(string route)
        {
            foreach (var row in ManualInstallRows)
            {
                if ((row.Route ?? "").Equals(route))
                {
                    // return Path found
                    return row.Path;
                }
            }

            return "";
        }

        private async void DownloadContentForm_FormClosing(object sender, WindowClosingEventArgs formClosingEventArgs)
        {
            if (AutoInstallClosingBlocked)
            {
                // cancelled event, so continue
                formClosingEventArgs.Cancel = true;
                return;
            }

            if (ManualInstallChangesMade && !CloseConfirmed)
            {
                // Avalonia can't wait for a message box here, so cancel the close and close again when confirmed.
                formClosingEventArgs.Cancel = true;
                string message = Localization.GetString("Cancel: changes made in the 'Manually Installed' tab will not be saved, are you sure?");
                if (await ShowMessage(message, MessageBoxButtons.YesNo) == MessageBoxResult.Yes)
                {
                    CloseConfirmed = true;
                    Close(DialogResult.Cancel);
                }
                return;
            }

            ImageCancellation?.Cancel();
            try
            {
                if (pictureBoxAutoInstallRoute.Source is Bitmap image)
                {
                    pictureBoxAutoInstallRoute.Source = null;
                    image.Dispose();
                }
                File.Delete(InfoTempFilename);
            }
            catch
            {
                // just ignore, the files are in the user temp directory anyway
            }
        }
    }
}
