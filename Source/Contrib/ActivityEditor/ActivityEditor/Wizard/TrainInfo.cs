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
using System.Linq;
using Avalonia;
using Avalonia.Controls;
using LibAE;
using LibAE.Formats;

namespace AEWizard
{
    public partial class TrainInfo : SinglePage
    {
        private ComboBox trainConsistCB;
        private TextBox trainNameCB;
        public ActivityInfo activityInfo { get; set; }

        public TrainInfo()
        {
            InitializeComponent();
        }

        public void completePage()
        {
            var consists = activityInfo.trainConsists.Select(o => o.consistName).ToList();
            this.trainConsistCB.ItemsSource = consists;
            if (consists.Count > 0)
                this.trainConsistCB.SelectedIndex = 0;
        }

        private void InitializeComponent()
        {
            trainConsistCB = new ComboBox { Width = 301 };
            trainNameCB = new TextBox { Width = 301 };
            Content = new StackPanel
            {
                Margin = new Thickness(5, 2),
                Spacing = 6,
                Children =
                {
                    ActivityEditor.Forms.Title("Activity Editor: Specify Train Info"),
                    ActivityEditor.Forms.Row("Select Train Consist", trainConsistCB, 160),
                    ActivityEditor.Forms.Row("Train Name (Opt.)", trainNameCB, 160),
                },
            };
        }

        protected internal override bool OnKillActive()
        {
            return Wizard.Validate();
        }

        protected internal override bool OnSetActive()
        {
            Wizard.SetWizardButtons(WizardButton.Finish);
            return true;
        }

        protected internal override string OnWizardBack()
        {
            Wizard.SetWizardButtons(WizardButton.Next);
            return WizardForm.NextPage;
        }

        protected internal override bool OnWizardFinish()
        {
            return true;
        }

        protected internal override string OnWizardNext()
        {
            return WizardForm.NextPage;
        }
    }
}
