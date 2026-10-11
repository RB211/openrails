// COPYRIGHT 2013 by the Open Rails project.
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

// This file is the responsibility of the 3D & Environment Team.

// The sound debug window. The window is an Avalonia window living on the Avalonia UI thread (see UiThread.cs);
// this class is created on the render thread and is the object the rest of the game talks to.
// Like the WinForms version, it reads the sound sources directly (read-only).

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Threading;
using Orts.Formats.Msts;
using Orts.Simulation.RollingStocks;
using ORTS.Common;
using AvWindow = Avalonia.Controls.Window;

namespace Orts.Viewer3D.Debugging
{
    public class SoundDebugForm : IDisposable
    {
        readonly Viewer Viewer;

        private DispatcherTimer UITimer;
        private double lastUpdateTime = 0;
        private int UpdateCounter = -1;

        private SoundSource SelectedSoundSource;

        AvWindow Window;
        bool AllowClose;
        bool Disposed;
        bool UpdateErrorReported;

        TreeView activeSoundTree;
        TreeView inactiveSoundTree;
        SoundTreeNode activeSoundList;
        SoundTreeNode inactiveSoundList;

        SelectableTextBlock speed, wheelRPM, tractiveEffort, tractivePower, variable1, variable2, variable2Booster, variable3,
            engineRPM, enginePower, engineTorque, backPressure, brakeCyl, curveForce, angleOfAttack, carFriction, carTunnelDistance,
            distance, distanceSquared, smsVolume, smsFrequency, waves, alSources;
        CheckBox sound3D, concreteSleepers, carInTunnel;

        public SoundDebugForm(Viewer viewer)
        {
            Viewer = viewer;
            UiThread.Post(CreateWindow);
        }

        /// <summary>
        /// Kept for compatibility with the WinForms version; visibility is controlled by <see cref="Viewer.SoundDebugFormEnabled"/>.
        /// </summary>
        public void Hide()
        {
        }

        /// <summary>
        /// Closes the window. Called on the render thread when the game ends; never blocks.
        /// </summary>
        public void Dispose()
        {
            if (Disposed) return;
            Disposed = true;
            UiThread.Post(() =>
            {
                UITimer?.Stop();
                if (Window == null) return;
                AllowClose = true;
                Window.Close();
            });
        }

        static string T(string text) => Viewer.Catalog.GetString(text);

        #region Window construction
        void CreateWindow()
        {
            if (Disposed) return;

            Window = new AvWindow
            {
                Title = T("Sound Debug"),
                Width = 900,
                Height = 700,
                ShowActivated = false,
                FontSize = 12,
            };

            activeSoundTree = new TreeView();
            inactiveSoundTree = new TreeView();
            activeSoundList = new SoundTreeNode(activeSoundTree);
            inactiveSoundList = new SoundTreeNode(inactiveSoundTree);
            // Like the WinForms version: no keyboard navigation in the (constantly changing) trees
            activeSoundTree.AddHandler(InputElement.KeyDownEvent, (s, e) => e.Handled = true, RoutingStrategies.Tunnel);
            inactiveSoundTree.AddHandler(InputElement.KeyDownEvent, (s, e) => e.Handled = true, RoutingStrategies.Tunnel);
            // Only one node can be selected over both trees
            activeSoundTree.SelectionChanged += (s, e) => { if (activeSoundTree.SelectedItem != null) inactiveSoundTree.SelectedItem = null; };
            inactiveSoundTree.SelectionChanged += (s, e) => { if (inactiveSoundTree.SelectedItem != null) activeSoundTree.SelectedItem = null; };

            var variables = new Grid { ColumnDefinitions = new ColumnDefinitions("150,*") };
            int row = 0;
            SelectableTextBlock AddVariable(string label)
            {
                variables.RowDefinitions.Add(new RowDefinition(GridLength.Auto));
                var name = new TextBlock { Text = T(label), Margin = new Thickness(0, 2) };
                var value = new SelectableTextBlock { Text = "-", Margin = new Thickness(0, 2) };
                Grid.SetRow(name, row);
                Grid.SetRow(value, row);
                Grid.SetColumn(value, 1);
                variables.Children.Add(name);
                variables.Children.Add(value);
                row++;
                return value;
            }
            speed = AddVariable("Speed (m/s)");
            wheelRPM = AddVariable("Wheel RPM");
            tractiveEffort = AddVariable("Tractive Effort (N)");
            tractivePower = AddVariable("Tractive Power (W)");
            variable1 = AddVariable("Variable 1");
            variable2 = AddVariable("Variable 2");
            variable2Booster = AddVariable("Booster Variable 2");
            variable3 = AddVariable("Variable 3");
            engineRPM = AddVariable("Engine RPM");
            enginePower = AddVariable("Engine Power (W)");
            engineTorque = AddVariable("Engine Torque (N-m)");
            backPressure = AddVariable("Back Pressure (psi)");
            brakeCyl = AddVariable("Brake Cylinder (psi)");
            curveForce = AddVariable("Curve Force (N)");
            angleOfAttack = AddVariable("Angle of Attack (mRad)");
            carFriction = AddVariable("Car Friction Coefficient");
            carTunnelDistance = AddVariable("Car Tunnel Distance (m)");
            distance = AddVariable("Distance (m)");
            distanceSquared = AddVariable("Distance Squared (m²)");
            smsVolume = AddVariable("Volume");
            smsFrequency = AddVariable("Frequency (Hz)");

            CheckBox Indicator(string text) => new CheckBox { Content = T(text), IsHitTestVisible = false, Focusable = false, MinHeight = 0, Margin = new Thickness(0, 0, 12, 0) };
            sound3D = Indicator("3D");
            concreteSleepers = Indicator("Concrete Sleepers");
            carInTunnel = Indicator("Car In Tunnel");
            var indicators = new WrapPanel { Children = { sound3D, concreteSleepers, carInTunnel }, Margin = new Thickness(0, 4, 0, 0) };
            DockPanel.SetDock(indicators, Dock.Bottom);

            var cache = new Grid { ColumnDefinitions = new ColumnDefinitions("150,*"), RowDefinitions = new RowDefinitions("Auto,Auto") };
            var wavesLabel = new TextBlock { Text = T("Cached Wave Files"), Margin = new Thickness(0, 2) };
            waves = new SelectableTextBlock { Text = "-", Margin = new Thickness(0, 2) };
            var alSourcesLabel = new TextBlock { Text = T("AL Sound Sources"), Margin = new Thickness(0, 2) };
            alSources = new SelectableTextBlock { Text = "-", Margin = new Thickness(0, 2) };
            Grid.SetColumn(waves, 1);
            Grid.SetRow(alSourcesLabel, 1);
            Grid.SetRow(alSources, 1);
            Grid.SetColumn(alSources, 1);
            cache.Children.Add(wavesLabel);
            cache.Children.Add(waves);
            cache.Children.Add(alSourcesLabel);
            cache.Children.Add(alSources);

            var left = new Grid { RowDefinitions = new RowDefinitions("*,4,Auto") };
            var selectedSound = GroupBox(T("Selected Sound Source Variables"), new DockPanel { Children = { indicators, new ScrollViewer { Content = variables } } });
            var soundCache = GroupBox(T("Sound Cache"), cache);
            Grid.SetRow(soundCache, 2);
            left.Children.Add(selectedSound);
            left.Children.Add(new GridSplitter { ResizeDirection = GridResizeDirection.Rows, Background = Brushes.Transparent });
            Grid.SetRow(left.Children[1], 1);
            left.Children.Add(soundCache);

            var right = new Grid { RowDefinitions = new RowDefinitions("*,4,*") };
            var activeSounds = GroupBox(T("Active Sound Sources"), activeSoundTree);
            var inactiveSounds = GroupBox(T("Inactive Sound Sources"), inactiveSoundTree);
            var rightSplitter = new GridSplitter { ResizeDirection = GridResizeDirection.Rows, Background = Brushes.Transparent };
            Grid.SetRow(rightSplitter, 1);
            Grid.SetRow(inactiveSounds, 2);
            right.Children.Add(activeSounds);
            right.Children.Add(rightSplitter);
            right.Children.Add(inactiveSounds);

            var root = new Grid { ColumnDefinitions = new ColumnDefinitions("380,4,*"), Margin = new Thickness(3) };
            var splitter = new GridSplitter { ResizeDirection = GridResizeDirection.Columns, Background = Brushes.Transparent };
            Grid.SetColumn(splitter, 1);
            Grid.SetColumn(right, 2);
            root.Children.Add(left);
            root.Children.Add(splitter);
            root.Children.Add(right);
            Window.Content = root;

            Window.Closing += (s, e) =>
            {
                if (AllowClose) return;
                e.Cancel = true;
                Viewer.SoundDebugFormEnabled = false;
                Window.Hide();
            };
            UiThread.Track(Window);

            // initialise the timer used to handle user input
            UITimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(100) };
            UITimer.Tick += UITimer_Tick;
            UITimer.Start();
        }

        static Control GroupBox(string header, Control content)
        {
            var title = new TextBlock { Text = header, FontWeight = FontWeight.SemiBold, Margin = new Thickness(0, 0, 0, 4) };
            DockPanel.SetDock(title, Dock.Top);
            return new Border
            {
                BorderBrush = Brushes.Gray,
                BorderThickness = new Thickness(1),
                CornerRadius = new CornerRadius(3),
                Padding = new Thickness(6),
                Margin = new Thickness(3),
                Child = new DockPanel { Children = { title, content } },
            };
        }
        #endregion

        void UITimer_Tick(object sender, EventArgs e)
        {
            if (Disposed) return;

            var visible = Viewer.SoundDebugFormEnabled;
            if (visible && !Window.IsVisible)
                Window.Show();
            else if (!visible && Window.IsVisible)
                Window.Hide();

            if (Program.Viewer.RealTime - lastUpdateTime < 0.1f)
                return;
            else if (!visible)
            {
                // Reset the tree when closing the window
                if (activeSoundList.Count > 0)
                    activeSoundList.Clear();
                if (inactiveSoundList.Count > 0)
                    inactiveSoundList.Clear();
                UpdateCounter = -1;

                return;
            }
            lastUpdateTime = Program.Viewer.RealTime;

            try
            {
                UpdateContent();
            }
            catch (Exception error)
            {
                // The sound sources are changed concurrently by the game; skip this update.
                if (!UpdateErrorReported)
                {
                    UpdateErrorReported = true;
                    Trace.TraceInformation("Sound debug window: skipped an update: {0}", error.Message);
                }
            }
        }

        /// <summary>
        /// Updates the form content. Warning: Creates garbage
        /// </summary>
        void UpdateContent()
        {
            var soundSources = Viewer.SoundProcess.GetSoundSources();

            int sourceIndex = 0;

            foreach (List<SoundSourceBase> src in soundSources.Values)
            {
                foreach (SoundSourceBase ssb in src.ToArray())
                {
                    List<SoundSource> sources = new List<SoundSource>();

                    if (ssb is SoundSource ss)
                    {
                        sources.Add(ss);
                    }
                    else if (ssb is TrackSoundSource tss)
                    {
                        sources.AddRange(tss.InSources);
                        sources.AddRange(tss.OutSources);
                    }

                    foreach (SoundSource source in sources)
                    {
                        // Lag reduction: Only update 1/20th of the sound sources each update,
                        // except on the first update, and except for the selected sound source
                        sourceIndex++;
                        if (UpdateCounter > -1 && (sourceIndex + UpdateCounter) % 20 != 0 && source != SelectedSoundSource)
                            continue;

                        SoundTreeNode node, subNode;
                        string nodeString, subNodeString = "";
                        string nodeKey, subNodeKey = "";

                        // If sound source is attached to a particular car, then group together all sources under that single car
                        if (source.Car != null)
                        {
                            // This is the main node for the car itself
                            nodeString = String.Format("Car {0}: {1}", source.Car.UiD.ToString(), Path.GetFileName(source.Car.WagFilePath));
                            nodeKey = nodeString;

                            // This is the node for the sound source specifically, goes underneath the car
                            subNodeString = String.Format("{0}{1}: {2} ", source.IsExternal ? "[EX]" : "[IN]", source.IsTrackSound ? "[TRK]" : "[CAR]", source.SMSFileName);
                            subNodeKey = subNodeString + source.GetHashCode().ToString();
                        }
                        else // Non-car sounds (eg: environment sound), no need for grouping
                        {
                            nodeString = String.Format("World X: {0}, Z: {1}: {2} ", source.WorldLocation.TileX, source.WorldLocation.TileZ, source.SMSFileName);
                            nodeKey = nodeString + source.GetHashCode().ToString();
                        }

                        var soundStreams = source.SoundStreams.ToArray();
                        if (soundStreams.Length > 0)
                        {
                            var activeNode = activeSoundList.Find(nodeKey);
                            var inactiveNode = inactiveSoundList.Find(nodeKey);

                            if (source.Active)
                                node = activeNode ?? activeSoundList.Add(nodeKey, nodeString);
                            else
                                node = inactiveNode ?? inactiveSoundList.Add(nodeKey, nodeString);

                            // Car sounds: Add sub-nodes to the main car node, but only if the main node is expanded
                            if (!String.IsNullOrEmpty(subNodeString) && !String.IsNullOrEmpty(subNodeKey))
                            {
                                // Remove this source from the opposite sub-node list
                                if (source.Active)
                                {
                                    if (inactiveNode != null)
                                    {
                                        inactiveNode.Remove(subNodeKey);

                                        // Remove the main node if it has no inactive sources remaining
                                        if (inactiveNode.Count == 0)
                                            inactiveSoundList.Remove(nodeKey);
                                    }
                                }
                                else
                                {
                                    if (activeNode != null)
                                    {
                                        activeNode.Remove(subNodeKey);

                                        // Remove the main node if it has no active sources remaining
                                        if (activeNode.Count == 0)
                                            activeSoundList.Remove(nodeKey);
                                    }
                                }

                                // Skip processing sub-nodes unless main node is expanded
                                if (!node.IsExpanded && node.Count > 1)
                                    continue;

                                node.Tag = source.Car;

                                subNode = node.Find(subNodeKey);
                                if (subNode == null)
                                {
                                    subNode = node.Add(subNodeKey, subNodeString);
                                    subNode.Tag = source;
                                }

                                // Do all future processing on the sub node
                                node = subNode;
                            }
                            else
                            {
                                node.Tag = source;

                                // Remove this source from the opposite node list
                                if (source.Active)
                                {
                                    if (inactiveNode != null)
                                        inactiveSoundList.Remove(nodeKey);
                                }
                                else
                                {
                                    if (activeNode != null)
                                        activeSoundList.Remove(nodeKey);
                                }
                            }

                            int activeSS = 0;
                            int streamIndex = 0;
                            foreach (SoundStream soundStream in soundStreams)
                            {
                                string[] playingData = soundStream.ALSoundSource.GetPlayingData();
                                if (playingData[0] != "-1")
                                    activeSS++;
                                if (node.IsExpanded || node.Count < soundStreams.Length)
                                {
                                    // Only update sound stream nodes one time unless main node is expanded
                                    string streamString = String.Format("{0}: {1} (cue: {2}) {3}", playingData);
                                    string streamKey = streamIndex.ToString() + soundStream.GetHashCode().ToString();

                                    var streamNode = node.Find(streamKey);
                                    if (streamNode == null)
                                    {
                                        streamNode = node.Add(streamKey, streamString);
                                        streamNode.Tag = soundStream;
                                    }
                                    else
                                    {
                                        streamNode.Text = streamString;
                                    }
                                }
                                streamIndex++;
                            }
                            node.Text = string.Format("{0}({1}{2}{3})", node.Text.Split('(')[0], activeSS, @"@", soundStreams.Length);
                        }
                    }
                }
            }

            // Fill selected node's data
            MSTSWagon selectedCar = null;
            SoundStream selectedStream = null;
            var selectedTag = (activeSoundTree.SelectedItem as TreeViewItem)?.Tag ?? (inactiveSoundTree.SelectedItem as TreeViewItem)?.Tag;

            if (selectedTag is SoundSource selectedSource)
            {
                SelectedSoundSource = selectedSource;
                selectedCar = SelectedSoundSource.Car;
            }
            else if (selectedTag is MSTSWagon car)
            {
                SelectedSoundSource = null;
                selectedCar = car;
            }
            else if (selectedTag is SoundStream stream)
            {
                selectedStream = stream;
                SelectedSoundSource = selectedStream.SoundSource;
                selectedCar = SelectedSoundSource.Car;
            }
            else
            {
                SelectedSoundSource = null;
            }


            if (selectedStream != null && selectedStream.ALSoundSource.SoundSourceID != -1)
            {
                //Stream distance:
                float[] pos = new float[3];
                OpenAL.alGetSource3f(selectedStream.ALSoundSource.SoundSourceID, OpenAL.AL_POSITION, out pos[0], out pos[1], out pos[2]);
                float[] lpos = new float[3];
                OpenAL.alGetListener3f(OpenAL.AL_POSITION, out lpos[0], out lpos[1], out lpos[2]);
                for (var j = 0; j < 3; j++)
                    pos[j] -= lpos[j];

                float squareDistance = pos[0] * pos[0] + pos[1] * pos[1] + pos[2] * pos[2];

                distance.Text = Math.Sqrt(squareDistance).ToString("0.0");
                distanceSquared.Text = squareDistance.ToString("0");

                OpenAL.alGetSourcei(selectedStream.ALSoundSource.SoundSourceID, OpenAL.AL_SOURCE_RELATIVE, out int relative);
                sound3D.IsChecked = relative == OpenAL.AL_FALSE;

                OpenAL.alGetSourcef(selectedStream.ALSoundSource.SoundSourceID, OpenAL.AL_GAIN, out float gain);
                smsVolume.Text = gain.ToString("0.000");

                OpenAL.alGetSourcef(selectedStream.ALSoundSource.SoundSourceID, OpenAL.AL_PITCH, out float freq);
                smsFrequency.Text = (freq * selectedStream.ALSoundSource.SampleRate).ToString("0");
            }
            else // No stream selected, fall back to distance relative to the sound source
            {
                smsVolume.Text = "-";
                smsFrequency.Text = "-";
                sound3D.IsChecked = false;

                if (SelectedSoundSource != null && SelectedSoundSource.WorldLocation != WorldLocation.None)
                {
                    float squareDistance = SelectedSoundSource.DistanceSquared;

                    distance.Text = Math.Sqrt(squareDistance).ToString("0.0");
                    distanceSquared.Text = squareDistance.ToString("0");
                }
                else
                {
                    distance.Text = "-";
                    distanceSquared.Text = "-";
                }
            }

            if (selectedCar != null)
            {
                speed.Text = Math.Abs(selectedCar.SpeedMpS).ToString("0.0");
                wheelRPM.Text = pS.TopM((float)(selectedCar.AbsWheelSpeedMpS / (2 * Math.PI * selectedCar.WheelRadiusM))).ToString("0.0");

                tractiveEffort.Text = (selectedCar.LocomotiveAxles.DriveForceN * Math.Sign(selectedCar.WheelSpeedMpS)).ToString("0"); // ensure positive for traction, negative for dynamics
                tractivePower.Text = (selectedCar.LocomotiveAxles.DrivePowerW).ToString("0");

                // Copy, so that the scaling below does not change the car's sound variables
                float[] Variable1 = (float[])selectedCar.Variable1.Clone();
                float Variable2 = selectedCar.Variable2;
                float Variable2Booster = selectedCar.Variable2_Booster;
                float Variable3 = selectedCar.Variable3;

                if (selectedCar is MSTSSteamLocomotive)
                {
                    for (int v1 = 0; v1 < Variable1.Length; v1++)
                        Variable1[v1] /= 100f;
                    Variable2 /= 100f;
                    Variable2Booster /= 100f;
                    Variable3 /= 100f;
                }
                if (selectedCar is MSTSElectricLocomotive)
                {
                    for (int v1 = 0; v1 < Variable1.Length; v1++)
                        Variable1[v1] /= 100f;
                    Variable2 /= 100f;
                }

                string variable1Text = Variable1[0].ToString("0.000");
                for (int v1 = 1; v1 < Variable1.Length; v1++)
                    variable1Text = variable1Text + ", " + Variable1[v1].ToString("0.000");
                variable1.Text = variable1Text;
                variable2.Text = Variable2.ToString("0.000");
                variable2Booster.Text = Variable2Booster.ToString("0.000");
                variable3.Text = Variable3.ToString("0.000");

                string engineRPMText = selectedCar.EnginesRPM[0].ToString("0.0");
                for (int r = 1; r < selectedCar.EnginesRPM.Length; r++)
                    engineRPMText = engineRPMText + ", " + selectedCar.EnginesRPM[r].ToString("0.0");
                engineRPM.Text = engineRPMText;
                string enginePowerText = selectedCar.EnginesPower[0].ToString("0");
                for (int p = 1; p < selectedCar.EnginesPower.Length; p++)
                    enginePowerText = enginePowerText + ", " + selectedCar.EnginesPower[p].ToString("0");
                enginePower.Text = enginePowerText;
                string engineTorqueText = selectedCar.EnginesTorque[0].ToString("0");
                for (int t = 1; t < selectedCar.EnginesTorque.Length; t++)
                    engineTorqueText = engineTorqueText + ", " + selectedCar.EnginesTorque[t].ToString("0");
                engineTorque.Text = engineTorqueText;

                backPressure.Text = selectedCar.BackPressurePSIG.ToString("0.0");

                brakeCyl.Text = selectedCar.BrakeSystem.GetCylPressurePSI().ToString("0.0");
                carFriction.Text = selectedCar.Train.WagonCoefficientFriction.ToString("0.000");

                curveForce.Text = selectedCar.CurveForceNFiltered.ToString("0");
                angleOfAttack.Text = selectedCar.CurveSquealAoAmRadFiltered.ToString("0.0");

                carTunnelDistance.Text = selectedCar.CarTunnelDistanceM.ToString("0.0");

                concreteSleepers.IsChecked = SharedSMSFileManager.ConcreteSleepers == 1.0f;
                carInTunnel.IsChecked = selectedCar.TrackSoundInTunnelTriggered == 1.0f;
            }
            else
            {
                speed.Text = "0";
                wheelRPM.Text = "-";
                tractiveEffort.Text = "-";
                tractivePower.Text = "-";

                variable1.Text = "-";
                variable2.Text = "-";
                variable2Booster.Text = "-";
                variable3.Text = "-";

                backPressure.Text = "-";

                engineRPM.Text = "-";
                enginePower.Text = "-";
                engineTorque.Text = "-";

                brakeCyl.Text = "-";
                carFriction.Text = "-";

                curveForce.Text = "-";
                angleOfAttack.Text = "-";

                carTunnelDistance.Text = "-";

                concreteSleepers.IsChecked = false;
                carInTunnel.IsChecked = false;
            }

            waves.Text = SoundItem.AllPieces.Count.ToString();
            alSources.Text = ALSoundSource.ActiveCount.ToString();

            UpdateCounter = (UpdateCounter + 1) % 20;
        }

        /// <summary>
        /// A node of a sound tree, with its children accessible by key (like the WinForms TreeNodeCollection).
        /// </summary>
        class SoundTreeNode
        {
            readonly ItemsControl Container; // The TreeView for the root, else the TreeViewItem
            readonly Dictionary<string, SoundTreeNode> Children = new Dictionary<string, SoundTreeNode>();

            public SoundTreeNode(ItemsControl container)
            {
                Container = container;
            }

            TreeViewItem Item => Container as TreeViewItem;

            public object Tag
            {
                get => Item?.Tag;
                set { if (Item != null) Item.Tag = value; }
            }

            public string Text
            {
                get => Item?.Header as string ?? "";
                set { if (Item != null && (Item.Header as string) != value) Item.Header = value; }
            }

            public bool IsExpanded => Item?.IsExpanded ?? true;

            public int Count => Children.Count;

            public SoundTreeNode Find(string key) => Children.TryGetValue(key, out var node) ? node : null;

            public SoundTreeNode Add(string key, string text)
            {
                var node = new SoundTreeNode(new TreeViewItem { Header = text });
                Children[key] = node;
                Container.Items.Add(node.Container);
                return node;
            }

            public void Remove(string key)
            {
                if (Children.TryGetValue(key, out var node))
                {
                    Children.Remove(key);
                    Container.Items.Remove(node.Container);
                }
            }

            public void Clear()
            {
                Children.Clear();
                Container.Items.Clear();
            }
        }
    }
}
