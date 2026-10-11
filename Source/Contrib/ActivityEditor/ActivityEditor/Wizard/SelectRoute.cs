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
using Avalonia;
using Avalonia.Controls;
using LibAE;
using Orts.Formats.OR;

namespace AEWizard
{
    public class SelectRoute : SinglePage
    {
        private ComboBox routePathCB;
        public String RoutePath;
        public RouteInfo routeInfo { get; set; }

        public SelectRoute()
        {
            InitializeComponent();
        }

        public void completePage()
        {
            this.routePathCB.ItemsSource = routeInfo.routePaths;
            if (routeInfo.routePaths.Count > 0)
                this.routePathCB.SelectedIndex = 0;
        }

        string RoutePathText => routePathCB.SelectedItem as string ?? "";

        private void InitializeComponent()
        {
            routePathCB = new ComboBox { Width = 301 };
            Content = new StackPanel
            {
                Margin = new Thickness(3),
                Spacing = 6,
                Children =
                {
                    ActivityEditor.Forms.Title("Route Configuration: Select Route"),
                    ActivityEditor.Forms.Row("Route Path", routePathCB, 100),
                },
            };
        }

        protected internal override bool OnKillActive()
        {
            return Validate();
        }

        protected internal override bool OnSetActive()
        {
            Wizard.SetWizardButtons(WizardButton.Finish);
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
            routeInfo.route = RoutePathText;
            return true;
        }
    }
}
