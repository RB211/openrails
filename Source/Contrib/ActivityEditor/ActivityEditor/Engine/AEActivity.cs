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
using System.IO;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using ActivityEditor;
using Avalonia.Platform.Storage;
using AEWizard;
using LibAE;
using LibAE.Formats;
using System.Xml.Serialization;
using Orts.Formats.OR;

namespace ActivityEditor.Engine
{
    public class AEActivity
    {
        public AERouteConfig aeRouteConfig;
        public ActivityInfo activityInfo;
        private bool tagPanelVisibility = true;
        private bool stationPanelVisibility = true;
        public bool ActivityClosing = false;

        public bool saveEnabled = false;
        public bool saveAsEnabled = false;
        public bool loadEnabled = true;    //  Maybe to change as preference ComponentItem setted


        public AEActivity()
        {
        }

        public string getActivityName()
        {
            return activityInfo.ActivityName;
        }

        public void LoadPanels(GroupBox activityBox)
        {
            activityBox.IsVisible = true;
        }

        public void LoadActivity(ActivityInfo activity)
        {

            activityInfo = activity;
            Load();
            return;
        }

        public void Save()
        {
            aeRouteConfig.SaveRoute();
            saveEnabled = true;
        }

        public void Load()
        {
            saveEnabled = true;
            saveAsEnabled = true;
        }

        public void New()
        {
            saveAsEnabled = true;
        }

        public bool GetTagVisibility()
        {
            return tagPanelVisibility;
        }

        public void SetTagVisibility(bool info)
        {
            tagPanelVisibility = info;
        }

        public bool GetStationVisibility()
        {
            return stationPanelVisibility;
        }

        public void SetStationVisibility(bool info)
        {
            stationPanelVisibility = info;
        }

        public async Task ClosegActivity()
        {
            if (await Dialogs.AskOkCancel("Do you want to save?", "Save activity ?"))
            {
                if (activityInfo.FileName == null || activityInfo.FileName.Length > 0)
                    if (!await SaveAs(activityInfo))
                    {
                        activityInfo.saveActivity();
                    }
            }
        }

        public async Task<bool> SaveAs(ActivityInfo activity)
        {
            var storage = Program.actEditor.StorageProvider;
            IStorageFolder startFolder = null;
            if (!string.IsNullOrEmpty(activity.RoutePath) && System.IO.Directory.Exists(activity.RoutePath))
                startFolder = await storage.TryGetFolderFromPathAsync(activity.RoutePath);
            var file = await storage.SaveFilePickerAsync(new FilePickerSaveOptions
            {
                Title = "Save Activity",
                SuggestedStartLocation = startFolder,
                DefaultExtension = "act.json",
                FileTypeChoices = new[] { new FilePickerFileType("Activity file") { Patterns = new[] { "*.act.json" } } },
            });
            var fileName = file?.TryGetLocalPath();
            if (fileName != null)
            {
                activity.saveActivity(fileName);
                return true;
            }
            return false;
        }

        public void AddActItem(GlobalItem item)
        {
            activityInfo.AddActItem(item);
        }

        public List<GlobalItem> getActItem()
        {
            return activityInfo.ActItem;
        }
    }
}
