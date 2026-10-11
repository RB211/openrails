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
using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;
using LibAE.Formats;

namespace AEWizard
{
	public class ActivityDescr : SinglePage
    {
        private TextBox ActivityDescrCB;
        private TextBox activityNameCB;
        private ComboBox routePathCB;
        public String RoutePath;
        public ActivityInfo activityInfo { get; set; }

        public ActivityDescr()
		{
            InitializeComponent();
		}

        public void completePage()
        {
            this.routePathCB.ItemsSource = activityInfo.routePaths;
            if (activityInfo.routePaths.Count > 0)
                this.routePathCB.SelectedIndex = 0;
        }

        string RoutePathText => routePathCB.SelectedItem as string ?? "";

        private void InitializeComponent()
        {
            routePathCB = new ComboBox { HorizontalAlignment = Avalonia.Layout.HorizontalAlignment.Stretch };
            activityNameCB = new TextBox();
            ActivityDescrCB = new TextBox { AcceptsReturn = true, TextWrapping = TextWrapping.Wrap, Height = 150 };
            Content = new StackPanel
            {
                Margin = new Thickness(3),
                Spacing = 4,
                Children =
                {
                    ActivityEditor.Forms.Title("Activity Editor: Create new activity"),
                    new TextBlock { Text = "Route Path", FontSize = 13 },
                    routePathCB,
                    new TextBlock { Text = "Activity Name", FontSize = 13 },
                    activityNameCB,
                    new TextBlock { Text = "Activity Description", FontSize = 13 },
                    ActivityDescrCB,
                },
            };
        }

        protected internal override bool OnKillActive()
        {
            return Validate();
        }

        protected internal override bool OnSetActive()
        {
            return true;
        }

        protected internal override string OnWizardBack()
        {
            if (RoutePathText.Length <= 0)
                return WizardForm.NoPageChange;
            return WizardForm.NextPage;
        }

        protected internal override bool OnWizardFinish()
        {
            if (RoutePathText.Length <= 0)
                return false;
            return true;
        }

        protected internal override string OnWizardNext()
        {
            if (RoutePathText.Length <= 0)
                return WizardForm.NoPageChange;
            activityInfo.RoutePath = RoutePathText;
            activityInfo.ActivityName = activityNameCB.Text ?? "";
            activityInfo.ActivityDescr = ActivityDescrCB.Text ?? "";
            return WizardForm.NextPage;
        }
    }
}
