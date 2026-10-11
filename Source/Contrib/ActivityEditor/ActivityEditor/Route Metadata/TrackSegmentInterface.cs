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
using Avalonia.Controls;
using Avalonia.Interactivity;
using LibAE;
using Orts.Formats.OR;

namespace ActivityEditor.Route_Metadata
{
    public partial class TrackSegmentInterface : Window
    {
        public TrackSegment segment;
        public List<string> destination;
        TextBox segmentLab;

        public TrackSegmentInterface(TrackSegment trackSegment, List<string> givenDestination)
        {
            List<string> destination = givenDestination;
            segment = trackSegment;
            InitializeComponent();
        }

        void InitializeComponent()
        {
            Title = "TrackSegmentInterface";
            segmentLab = new TextBox { Width = 236 };
            var ok = new Button { Content = "OK", Width = 91 };
            ok.Click += TrackItf_OK_Click;
            Content = Forms.Layout(
                Forms.Row("Segment Label : ", segmentLab, 110),
                Forms.Buttons(ok));
            Forms.SetupDialog(this);
        }

        private void TrackItf_OK_Click(object sender, RoutedEventArgs e)
        {
            if (segmentLab.Text == "" || segmentLab.Text == null || segmentLab.Text == "Please, give a name")
            {
                this.segmentLab.Text = "Please, give a name";
            }
            else
            {
                segment.segmentLabel = segmentLab.Text;
                Close();
            }
        }
    }
}
