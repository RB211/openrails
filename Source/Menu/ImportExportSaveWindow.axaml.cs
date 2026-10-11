// COPYRIGHT 2012, 2013, 2014 by the Open Rails project.
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
using System.IO;
using System.IO.Compression;
using System.Linq;
using Avalonia.Controls;
using Avalonia.Platform.Storage;
using ORTS.Settings;

namespace Menu
{
    public partial class ImportExportSaveWindow : Window
    {
        readonly ResumeWindow.Save Save;
        const string SavePackFileExtension = "ORSavePack";  // Includes "OR" in the extension as this may be emailed, downloaded and mixed in with non-OR files.

        public ImportExportSaveWindow(ResumeWindow.Save save)
        {
            InitializeComponent();
            Icon = App.LoadIcon();

            Save = save;
            if (!Directory.Exists(UserSettings.SavePackFolder)) Directory.CreateDirectory(UserSettings.SavePackFolder);
            UpdateFileList(null);
            bExport.IsEnabled = !(Save == null);

            bImportSave.Click += bImportSave_Click_1;
            bExport.Click += bExport_Click;
            bViewSavePacksFolder.Click += bViewSavePacksFolder_Click;
        }

        #region Event handlers
        private async void bImportSave_Click_1(object sender, EventArgs e)
        {
            // Show the dialog and get result.
            var options = new FilePickerOpenOptions
            {
                AllowMultiple = false,
                SuggestedStartLocation = await StorageProvider.TryGetFolderFromPathAsync(UserSettings.SavePackFolder),
                FileTypeFilter = new[]
                {
                    new FilePickerFileType(App.ProductName + " " + Localization.GetString("Save Packs") + " (*." + SavePackFileExtension + ")") { Patterns = new[] { "*." + SavePackFileExtension } },
                    new FilePickerFileType(Localization.GetString("All files") + " (*.*)") { Patterns = new[] { "*" } },
                },
            };
            var files = await StorageProvider.OpenFilePickerAsync(options);
            var fileName = files.FirstOrDefault()?.TryGetLocalPath();
            if (fileName != null)
            {
                ExtractFilesFromZip(fileName, UserSettings.UserDataFolder);
                UpdateFileList(Localization.GetStringFmt("Save Pack '{0}' imported successfully.", Path.GetFileNameWithoutExtension(fileName)));
            }
        }

        private void bExport_Click(object sender, EventArgs e)
        {
            // Create a Zip-compatible file/compressed folder containing:
            // all files with the same stem (i.e. *.save, *.png, *.replay, *.txt)

            // Copy files to new package in folder save_packs
            var fullFilePath = Path.Combine(UserSettings.UserDataFolder, Save.File);
            var toFile = Path.GetFileNameWithoutExtension(Save.File) + "." + SavePackFileExtension;
            var fullZipFilePath = Path.Combine(UserSettings.SavePackFolder, toFile);
            foreach (var fileName in new[] {
                fullFilePath,
                Path.ChangeExtension(fullFilePath, "png"),
                Path.ChangeExtension(fullFilePath, "replay"),
                Path.ChangeExtension(fullFilePath, "txt"),
            })
            {
                if (File.Exists(fileName))
                    AddFileToZip(fullZipFilePath, fileName);
            }
            UpdateFileList(Localization.GetStringFmt("Save Pack '{0}' exported successfully.", Path.GetFileNameWithoutExtension(Save.File)));
        }

        private void bViewSavePacksFolder_Click(object sender, EventArgs e)
        {
            string fullZipFilePath = null;
            if (Save != null)
            {
                var toFile = Path.GetFileNameWithoutExtension(Save.File) + "." + SavePackFileExtension;
                fullZipFilePath = Path.Combine(UserSettings.SavePackFolder, toFile);
            }

            if (OperatingSystem.IsWindows())
            {
                // Opens the Save Packs folder, selecting the exported SavePack if there is one.
                var arguments = fullZipFilePath != null && File.Exists(fullZipFilePath)
                    ? "/select,\"" + fullZipFilePath + "\""
                    : "\"" + UserSettings.SavePackFolder + "\"";
                Process.Start(Path.Combine(Environment.GetEnvironmentVariable("windir"), "explorer.exe"), arguments);
            }
            else
            {
                // File managers differ in how (or whether) they select a file, so just open the folder.
                ExternalProcess.Open(UserSettings.SavePackFolder);
            }
        }
        #endregion

        void UpdateFileList(string message)
        {
            var files = Directory.GetFiles(UserSettings.SavePackFolder, "*." + SavePackFileExtension);
            var text = String.IsNullOrEmpty(message) ? "" : message + Environment.NewLine;
            text += Localization.Catalog.GetPluralStringFmt("Save Pack folder contains {0} save pack:", "Save Pack folder contains {0} save packs:", files.Length);
            foreach (var s in files)
                text += Environment.NewLine + "    " + Path.GetFileNameWithoutExtension(s);
            textBoxSavePacks.Text = text;
        }

        static void AddFileToZip(string zipFilename, string fileToAdd)
        {
            using (var zip = ZipFile.Open(zipFilename, ZipArchiveMode.Update))
            {
                zip.CreateEntryFromFile(fileToAdd, Path.GetFileName(fileToAdd), CompressionLevel.Optimal);
            }
        }

        static void ExtractFilesFromZip(string zipFilename, string path)
        {
            using (var zip = ZipFile.OpenRead(zipFilename))
            {
                foreach (var part in zip.Entries)
                {
                    // Older save packs have an extra root file we don't need
                    if (part.FullName == "[Content_Types].xml") continue;

                    // Older save packs have percent-encoded paths
                    var fileName = Path.GetFullPath(Path.Combine(path, part.FullName.Replace("%20", " ")));

                    // Never write outside the user data folder.
                    if (!fileName.StartsWith(Path.GetFullPath(path), StringComparison.Ordinal)) continue;

                    try
                    {
                        if (Path.GetFileName(fileName).Length > 0)
                        {
                            Directory.CreateDirectory(Path.GetDirectoryName(fileName));
                            part.ExtractToFile(fileName, true);
                        }
                    }
                    catch { } // Ignore attempts to copy to a destination file locked by another process as
                    // ResumeForm locks PNG of selected save.
                }
            }
        }
    }
}
