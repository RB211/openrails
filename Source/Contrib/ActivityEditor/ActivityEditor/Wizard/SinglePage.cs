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
using Avalonia.Controls;

namespace AEWizard
{
    /// <summary>
    /// Base class for a page of the <see cref="WizardForm"/>.
    /// </summary>
    public class SinglePage : UserControl
    {
        public SinglePage()
        {
            Width = 497;
            Height = 350;
        }

        /// <summary>The wizard hosting this page (set when the page is added to it).</summary>
        protected internal WizardForm Wizard { get; internal set; }

        /// <summary>WinForms Control.Validate() equivalent: there is no validation on the pages.</summary>
        protected bool Validate()
        {
            return true;
        }

        protected internal virtual bool OnKillActive()
        {
            return Validate();
        }

        protected internal virtual bool OnSetActive()
        {
            return true;
        }

        protected internal virtual string OnWizardBack()
        {
            return WizardForm.NextPage;
        }

        protected internal virtual bool OnWizardFinish()
        {
            return true;
        }

        protected internal virtual string OnWizardNext()
        {
            return WizardForm.NextPage;
        }
    }
}
