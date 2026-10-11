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

using ActivityEditor.Engine;
using AEWizard;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Avalonia.Platform.Storage;
using Avalonia.Threading;
using LibAE.Formats;
using Orts.Formats.OR;
using ORTS.Common;
/// This module ...
///
/// Author: Stéfan Paitoni
/// Updates :
///

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;

namespace ActivityEditor
{
    public enum ToolClicks // Changed from ToolClicked as led to compiler warnings
    {
        NO_TOOL = 0,
        TAG = 1,
        STATION = 2,
        MOVE = 3,
        ROTATE = 4,
        START = 5,
        STOP = 6,
        WAIT = 7,
        ACTION = 8,
        CHECK = 9,
        AREA = 10,
        AREA_ADD = 11,
        DRAG = 12,
        ZOOM = 13,
        METASEGMENT = 14
    };

    /// <summary>
    /// Main window. The WinForms version was an MDI container with one child window per route view; here
    /// each <see cref="Viewer2D"/> is a tab.
    /// </summary>
    public partial class ActEditor : Window
    {
        private bool saveEnabled = false;
        private bool saveAsEnabled = false;
        private bool loadEnabled = false;
        public List<Viewer2D> viewer2ds;
        public Viewer2D selectedViewer;

        private bool focusOnViewer = false;

        public Drawing.Image ToolToUse = null;
        public Cursor CursorToUse = Cursor.Default;

        // Controls (formerly in ActEditor.Designer.cs)
        MenuItem activityToolStripMenuItem;
        MenuItem trafficToolStripMenuItem;
        MenuItem saveToolStripMenuItem;
        MenuItem saveAsToolStripMenuItem;
        MenuItem loadToolStripMenuItem;
        MenuItem loadMetada;
        MenuItem closeViewerMenuItem;
        TextBlock StatusEditor;
        public GroupBox activityOverview;
        public GroupBox routeData;
        TextBox ActivityAECB;
        StackPanel routeCFG;
        StackPanel toolStrip3;
        StackPanel activityCFG;
        TabControl viewerTabs;

        public ActEditor()
        {
            InitializeComponent();
            SelectTools(TypeEditor.NONE);
            viewer2ds = new List<Viewer2D>();
            selectedViewer = null;
            CheckCurrentStatus();
            updateNewMenuItems();
        }

        void InitializeComponent()
        {
            Title = "Activity Editor";
            Width = 1100;
            Height = 750;
            Icon = App.LoadIcon();
            WindowStartupLocation = WindowStartupLocation.CenterScreen;

            // Menu
            var newToolStripMenuItem = new MenuItem { Header = "New" };
            activityToolStripMenuItem = new MenuItem { Header = "Activity" };
            ToolTip.SetTip(activityToolStripMenuItem, "Click to start new Activity Description (not ready)");
            activityToolStripMenuItem.Click += activityToolStripMenuItem_Click;
            trafficToolStripMenuItem = new MenuItem { Header = "Traffic" };
            ToolTip.SetTip(trafficToolStripMenuItem, "Click to create new Traffic description (not ready)");
            trafficToolStripMenuItem.Click += trafficToolStripMenuItem_Click;
            newToolStripMenuItem.Items.Add(activityToolStripMenuItem);
            newToolStripMenuItem.Items.Add(trafficToolStripMenuItem);
            newToolStripMenuItem.SubmenuOpened += newToolStripMenuItem_Click;

            saveToolStripMenuItem = new MenuItem { Header = "Save", IsEnabled = false };
            var metadataToolStripMenuItem = new MenuItem { Header = "Metadata" };
            metadataToolStripMenuItem.Click += SaveRouteCfg;
            saveToolStripMenuItem.Items.Add(metadataToolStripMenuItem);
            saveToolStripMenuItem.Click += saveToolStripMenuItem_Click;
            saveAsToolStripMenuItem = new MenuItem { Header = "Save As", IsEnabled = false };
            saveAsToolStripMenuItem.Click += saveAsToolStripMenuItem_Click;
            loadToolStripMenuItem = new MenuItem { Header = "Load Activity" };
            ToolTip.SetTip(loadToolStripMenuItem, "Click to load an existing OR Activity (not ready)");
            loadToolStripMenuItem.Click += loadToolStripMenuItem_Click;
            loadMetada = new MenuItem { Header = "Load Metada" };
            ToolTip.SetTip(loadMetada, "Click to Edit Metadata for a specified route");
            loadMetada.Click += UpdateRouteCfg;
            closeViewerMenuItem = new MenuItem { Header = "Close View", IsEnabled = false };
            closeViewerMenuItem.Click += (s, e) => CloseViewer(selectedViewer);
            var quitToolStripMenuItem = new MenuItem { Header = "Quit" };
            quitToolStripMenuItem.Click += quitToolStripMenuItem_Click;

            var fileToolStripMenuItem = new MenuItem { Header = "File" };
            fileToolStripMenuItem.Items.Add(newToolStripMenuItem);
            fileToolStripMenuItem.Items.Add(saveToolStripMenuItem);
            fileToolStripMenuItem.Items.Add(saveAsToolStripMenuItem);
            fileToolStripMenuItem.Items.Add(loadToolStripMenuItem);
            fileToolStripMenuItem.Items.Add(loadMetada);
            fileToolStripMenuItem.Items.Add(closeViewerMenuItem);
            fileToolStripMenuItem.Items.Add(new Separator());
            fileToolStripMenuItem.Items.Add(quitToolStripMenuItem);

            var editToolStripMenuItem = new MenuItem { Header = "Edit" };
            var preferenceToolStripMenuItem = new MenuItem { Header = "Options" };
            preferenceToolStripMenuItem.Click += preferenceToolStripMenuItem_Click;
            var importToolStripMenuItem = new MenuItem { Header = "Import" };
            var toolsToolStripMenuItem = new MenuItem { Header = "Tools" };
            toolsToolStripMenuItem.Items.Add(preferenceToolStripMenuItem);
            toolsToolStripMenuItem.Items.Add(importToolStripMenuItem);
            var aboutActivityEditorToolStripMenuItem = new MenuItem { Header = "About Activity Editor" };
            aboutActivityEditorToolStripMenuItem.Click += aboutActivityEditorToolStripMenuItem_Click;
            var helpToolStripMenuItem = new MenuItem { Header = "Help" };
            helpToolStripMenuItem.Items.Add(aboutActivityEditorToolStripMenuItem);

            var menu = new Menu();
            menu.Items.Add(fileToolStripMenuItem);
            menu.Items.Add(editToolStripMenuItem);
            menu.Items.Add(toolsToolStripMenuItem);
            menu.Items.Add(helpToolStripMenuItem);

            // Tool bars
            routeCFG = ToolStrip(
                ToolButton(AppResources._32, "Add Station Area", AddArea_Click),
                ToolButton(AppResources._31, "Station Configuration", null),
                ToolButton(AppResources.tag, "Add Tag", AddTag_Click),
                ToolButton(AppResources.SignalBox, "Add Station", AddStation_Click),
                ToolButton(AppResources.metasegment, "Edit MetaSegment", editMetaSegment));
            toolStrip3 = ToolStrip(
                ToolButton(AppResources.object_move, "Move", MoveSelected_Click),
                ToolButton(AppResources.object_rotate, "Rotate", RotateSelected_Click));
            activityCFG = ToolStrip(
                ToolButton(AppResources._98, "Activity Start", AddActivityStart_Click),
                ToolButton(AppResources._118, "Activity Stop", AddActivityStop_Click),
                ToolButton(AppResources._12, "Waiting Point", AddActivityWait_Click),
                ToolButton(AppResources._78, "Action Point", AddActivityAction_Click),
                ToolButton(AppResources._77, "Action Check", AddActivityEval_Click),
                ToolButton(AppResources.tag, "Add Tag", AddTag_Click));
            var toolStripPanel1 = new WrapPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(3, 0, 0, 0) };
            toolStripPanel1.Children.Add(routeCFG);
            toolStripPanel1.Children.Add(toolStrip3);
            toolStripPanel1.Children.Add(activityCFG);

            // Status bar
            StatusEditor = new TextBlock { Text = "Status", Margin = new Thickness(6, 2) };
            var statusStrip1 = new Border
            {
                Child = StatusEditor,
                BorderThickness = new Thickness(0, 1, 0, 0),
                BorderBrush = Brushes.LightGray,
            };

            // Information panel on the right
            ActivityAECB = new TextBox { Width = 170 };
            ActivityAECB.TextChanged += ActivityAECB_TextChanged;
            ActivityAECB.KeyDown += ActEditorKeyDown;
            var button1 = new Button { Content = "Edit Description" };
            button1.Click += button1_Click;
            var button2 = new Button { Content = "v" };
            button2.Click += button2_Click;
            activityOverview = new GroupBox
            {
                Text = "Activity",
                IsVisible = false,
                Content = new StackPanel
                {
                    Spacing = 4,
                    Children =
                    {
                        ActivityAECB,
                        new StackPanel { Orientation = Orientation.Horizontal, Spacing = 4, Children = { button1, button2 } },
                    },
                },
            };
            DockPanel.SetDock(activityOverview, Dock.Top);
            routeData = new GroupBox { Text = "", IsVisible = false };
            routeData.GotFocus += routeData_Enter;
            var informationPanel = new DockPanel { Width = 210, LastChildFill = true };
            informationPanel.Children.Add(activityOverview);
            informationPanel.Children.Add(routeData);

            viewerTabs = new TabControl();
            viewerTabs.SelectionChanged += ViewerTabs_SelectionChanged;

            var root = new DockPanel();
            DockPanel.SetDock(menu, Dock.Top);
            DockPanel.SetDock(toolStripPanel1, Dock.Top);
            DockPanel.SetDock(statusStrip1, Dock.Bottom);
            DockPanel.SetDock(informationPanel, Dock.Right);
            root.Children.Add(menu);
            root.Children.Add(toolStripPanel1);
            root.Children.Add(statusStrip1);
            root.Children.Add(informationPanel);
            root.Children.Add(viewerTabs);
            Content = root;

            KeyDown += ActEditorKeyDown;
            Closing += ActEditor_Closing;
        }

        static StackPanel ToolStrip(params Button[] buttons)
        {
            var strip = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 2, 8, 2), Spacing = 2 };
            foreach (var button in buttons)
                strip.Children.Add(button);
            return strip;
        }

        static Button ToolButton(Bitmap image, string text, EventHandler<RoutedEventArgs> click)
        {
            var button = new Button
            {
                Padding = new Thickness(3),
                Content = image != null ? new Image { Source = image, Width = 24, Height = 24 } : (object)text,
            };
            ToolTip.SetTip(button, text);
            if (click != null)
                button.Click += click;
            return button;
        }

        private void StatusEditor_Click(object sender, EventArgs e)
        {
        }

        /// <summary>Show a message in the status bar; may be called from any thread.</summary>
        public void DisplayStatusMessage(string info)
        {
            if (Dispatcher.UIThread.CheckAccess())
                this.StatusEditor.Text = info;
            else
                Dispatcher.UIThread.Post(() => this.StatusEditor.Text = info);
        }

        private void CheckCurrentStatus()
        {
            if (Program.SettingsError != null)
            {
                DisplayStatusMessage("Unable to load Open Rails settings: " + Program.SettingsError);
            }
            if (!Program.aePreference.CheckPrefValidity())
            {
                DisplayStatusMessage("Please, Configure your Path!");
                return;
            }

            DisplayStatusMessage("Create New Activity");
        }

        private void newToolStripMenuItem_Click(object sender, RoutedEventArgs e)
        {
            updateNewMenuItems();
        }

        private async void quitToolStripMenuItem_Click(object sender, RoutedEventArgs e)
        {
            if (await Dialogs.AskOkCancel("Really Quit?", "Exit"))
            {
                quitConfirmed = true;
                Close();
            }
        }

        bool quitConfirmed;
        bool closingViewers;
        private async void ActEditor_Closing(object sender, WindowClosingEventArgs e)
        {
            // Close the viewers first, so they can save their route metadata / ask to save the activity.
            if (viewer2ds.Count == 0 || closingViewers)
                return;
            e.Cancel = true;
            closingViewers = true;
            foreach (var viewer in viewer2ds.ToList())
                await CloseViewerAsync(viewer);
            Close();
        }

        private async void loadToolStripMenuItem_Click(object sender, RoutedEventArgs e)
        {
            IStorageFolder startFolder = null;
            if (!string.IsNullOrEmpty(Program.aePreference.AEPath) && Directory.Exists(Program.aePreference.AEPath))
                startFolder = await StorageProvider.TryGetFolderFromPathAsync(Program.aePreference.AEPath);
            var files = await StorageProvider.OpenFilePickerAsync(new FilePickerOpenOptions
            {
                Title = "Load Activity",
                SuggestedStartLocation = startFolder,
                FileTypeFilter = new[]
                {
                    new FilePickerFileType("Activity file") { Patterns = new[] { "*.act.json" } },
                    FilePickerFileTypes.All,
                },
            });
            var fileName = files.Count > 0 ? files[0].TryGetLocalPath() : null;
            if (fileName != null)
            {
                ActivityInfo activityInfo = ActivityInfo.loadActivity(fileName);
                if (activityInfo == null)
                    return;
                await OpenViewer(() => new PseudoSim(Program.aePreference.settings), sim => sim.LoadRoute(activityInfo.RoutePath, TypeEditor.ACTIVITY),
                    sim => new Viewer2D(this, activityInfo, sim));
            }
        }

        /// <summary>
        /// Load the route data in the background (the WinForms version blocked the UI), then create the viewer.
        /// </summary>
        private async Task OpenViewer(Func<PseudoSim> createSim, Action<PseudoSim> load, Func<PseudoSim, Viewer2D> createViewer)
        {
            Cursor = new Cursor(StandardCursorType.AppStarting);
            try
            {
                var sim = createSim();
                sim.Start();
                await Task.Run(() => load(sim));
                Viewer2D newViewer = createViewer(sim);
                viewer2ds.Add(newViewer);
                var close = new Button { Content = "x", Padding = new Thickness(4, 0), Margin = new Thickness(6, 0, 0, 0), FontSize = 10 };
                close.Click += (s, e) => CloseViewer(newViewer);
                var tab = new TabItem
                {
                    Header = new StackPanel { Orientation = Orientation.Horizontal, Children = { new TextBlock { Text = newViewer.Text }, close } },
                    Content = newViewer,
                    Tag = newViewer,
                };
                viewerTabs.Items.Add(tab);
                focusOnViewer = true;
                viewerTabs.SelectedItem = tab;
                setFocus(newViewer);
                DisplayStatusMessage("Load Succeed");
            }
            catch (Exception error)
            {
                DisplayStatusMessage("Load failed: " + error.Message);
                await Dialogs.Show(error.ToString(), "Load failed");
            }
            finally
            {
                Cursor = Cursor.Default;
            }
        }

        private async void CloseViewer(Viewer2D viewer)
        {
            await CloseViewerAsync(viewer);
        }

        private async Task CloseViewerAsync(Viewer2D viewer)
        {
            if (viewer == null)
                return;
            await viewer.CloseViewer();
            var tab = viewerTabs.Items.OfType<TabItem>().FirstOrDefault(t => t.Tag == viewer);
            if (tab != null)
                viewerTabs.Items.Remove(tab);
            viewer2ds.Remove(viewer);
            if (selectedViewer == viewer)
                selectedViewer = null;
            closeViewerMenuItem.IsEnabled = viewer2ds.Count > 0;
        }

        private void ViewerTabs_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if ((viewerTabs.SelectedItem as TabItem)?.Tag is Viewer2D viewer)
            {
                focusOnViewer = true;
                setFocus(viewer);
            }
        }

        private void saveToolStripMenuItem_Click(object sender, RoutedEventArgs e)
        {
            if (e.Source != sender)
                return;
            this.selectedViewer?.Save();
            DisplayStatusMessage("Save Activity Info Status");
        }

        private async void activityToolStripMenuItem_Click(object sender, RoutedEventArgs e)
        {   //  Try to create a new activity Object with wizard
            ActivityInfo activityInfo = new ActivityInfo();
            activityInfo.config(Program.aePreference.RoutePaths);
            WizardForm wiz = new WizardForm(activityInfo);
            if (await Dialogs.ShowDialog(wiz) is true)
            {
                await OpenViewer(() => new PseudoSim(Program.aePreference.settings), sim => sim.LoadRoute(activityInfo.RoutePath, TypeEditor.ACTIVITY),
                    sim => new Viewer2D(this, activityInfo, sim));
            }
        }

        private void trafficToolStripMenuItem_Click(object sender, RoutedEventArgs e)
        {
            //  For Generic Traffic definition
        }

        private async void aboutActivityEditorToolStripMenuItem_Click(object sender, RoutedEventArgs e)
        {
            AboutActEdit aae = new AboutActEdit();
            await Dialogs.ShowDialog(aae);
        }

        private async void preferenceToolStripMenuItem_Click(object sender, RoutedEventArgs e)
        {
            Preference.Options optionWindow = new Preference.Options();
            await Dialogs.ShowDialog(optionWindow);
            updateNewMenuItems();
            CheckCurrentStatus();
        }

        private void updateNewMenuItems()
        {
            if (Program.aePreference.classFilled == true)
            {
                this.activityToolStripMenuItem.IsEnabled = true;
                this.trafficToolStripMenuItem.IsEnabled = true;
                this.loadMetada.IsEnabled = true;

            }
            else
            {
                this.activityToolStripMenuItem.IsEnabled = false;
                this.trafficToolStripMenuItem.IsEnabled = false;
                this.loadMetada.IsEnabled = false;
            }

        }

        private async void button1_Click(object sender, RoutedEventArgs e)
        {
            if (selectedViewer == null)
            {
                DisplayStatusMessage("No Activity Selected");
                return;
            }
            DisplayStatusMessage("Type In Activity Descr");
            SimpleTextEd simpleTextEd = new SimpleTextEd();
            simpleTextEd.aEditer.Text = selectedViewer.getDescr();
            await Dialogs.ShowDialog(simpleTextEd);
            if (!string.IsNullOrEmpty(simpleTextEd.aEditer.Text))
            {
                selectedViewer.setDescr(simpleTextEd.aEditer.Text);
                DisplayStatusMessage("Act Description Updated");
            }
        }

        private void ActivityAECB_TextChanged(object sender, EventArgs e)
        {
        }

        private void button2_Click(object sender, RoutedEventArgs e)
        {
            if (selectedViewer == null)
                return;
        }

        private void AddTag_Click(object sender, RoutedEventArgs e)
        {
            if (selectedViewer == null)
                return;
            DisplayStatusMessage("Please, Place Tag");
            selectedViewer.SetToolClicked(ToolClicks.TAG);
        }

        private void AddStation_Click(object sender, RoutedEventArgs e)
        {
            if (selectedViewer == null)
                return;
            DisplayStatusMessage("Please, Place Station");
            selectedViewer.SetToolClicked(ToolClicks.STATION);
        }

        #region Activity
        //  Not used actuially
        private void AddActivityStart_Click(object sender, RoutedEventArgs e)
        {
            DisplayStatusMessage("Please, Place Start Activity");
            ToolToUse = AppResources.LoadImage("icon/Activity_start.png");
        }

        private void AddActivityStop_Click(object sender, RoutedEventArgs e)
        {
            DisplayStatusMessage("Please, Place Stop Activity");
            ToolToUse = AppResources.LoadImage("icon/Activity_stop.png");
        }

        private void AddActivityWait_Click(object sender, RoutedEventArgs e)
        {
            DisplayStatusMessage("Please, Place Wait Point Activity");
            ToolToUse = AppResources.LoadImage("icon/Activity_wait.png");
        }

        private void AddActivityAction_Click(object sender, RoutedEventArgs e)
        {
            DisplayStatusMessage("Please, Place Action Point Activity");
            ToolToUse = AppResources.LoadImage("icon/Action.png");
        }

        private void AddActivityEval_Click(object sender, RoutedEventArgs e)
        {
            DisplayStatusMessage("Please, Place Evaluation Point Activity");
            ToolToUse = AppResources.LoadImage("icon/Activity_check.png");
        }
        #endregion

        private void MoveSelected_Click(object sender, RoutedEventArgs e)
        {
            if (selectedViewer == null)
                return;
            DisplayStatusMessage("Please, Place Move Tool");
            selectedViewer.SetToolClicked(ToolClicks.MOVE);
        }

        private void RotateSelected_Click(object sender, RoutedEventArgs e)
        {
            if (selectedViewer == null)
                return;
            DisplayStatusMessage("Place Rotate Tool");
            selectedViewer.SetToolClicked(ToolClicks.ROTATE);
        }

        public void UnsetToolClick()
        {
            DisplayStatusMessage("Wait Action Form");
            ToolToUse = null;
            this.Cursor = Cursor.Default;
            CursorToUse = Cursor.Default;
        }

        private void AddArea_Click(object sender, RoutedEventArgs e)
        {
            if (selectedViewer == null)
                return;
            DisplayStatusMessage("Add Station Area");
            selectedViewer.SetToolClicked(ToolClicks.AREA);
        }

        private void saveAsToolStripMenuItem_Click(object sender, RoutedEventArgs e)
        {
            this.selectedViewer?.Save();
        }

        public void setFocus(Viewer2D viewer)
        {
            if (viewer == null || !focusOnViewer)
                return;
            if (selectedViewer != viewer)
            {
                if (selectedViewer != null)
                {
                    selectedViewer.UnsetFocus();
                }
                viewer.SetFocus();
                selectedViewer = viewer;
                if (viewer.ViewerMode == TypeEditor.ACTIVITY)
                {
                    activityOverview.IsVisible = true;
                    ActivityAECB.Text = viewer.aeConfig.aeActivity.activityInfo.ActivityName;
                }
                this.loadToolStripMenuItem.IsEnabled = this.loadEnabled;
                this.saveAsToolStripMenuItem.IsEnabled = this.saveAsEnabled;
                this.saveToolStripMenuItem.IsEnabled = this.saveEnabled;
                closeViewerMenuItem.IsEnabled = true;
                viewer.FocusView();
            }
        }

        private void routeData_Enter(object sender, EventArgs e)
        {
            focusOnViewer = false;
        }

        public bool askFocus()
        {
            focusOnViewer = true;
            return focusOnViewer;
        }

        private async void UpdateRouteCfg(object sender, RoutedEventArgs e)
        {
            RouteInfo routeInfo = new RouteInfo();
            routeInfo.config(Program.aePreference.RoutePaths);
            WizardForm wiz = new WizardForm(routeInfo);
            if (await Dialogs.ShowDialog(wiz) is true)
            {
                await OpenViewer(() => new PseudoSim(Program.aePreference.settings), sim => sim.LoadRoute(routeInfo.route, TypeEditor.ROUTECONFIG),
                    sim => new Viewer2D(this, routeInfo.route, sim));
            }
        }

        static int cntAct = 0;
        static int cntRoute = 0;
        public void SelectTools(TypeEditor viewerMode)
        {
            if (viewerMode == TypeEditor.ACTIVITY)
            {
                cntAct++;
            }
            else if (viewerMode == TypeEditor.ROUTECONFIG)
            {
                cntRoute++;
            }
            UpdateToolsVisibility();
        }

        public void UnselectTools(TypeEditor viewerMode)
        {
            if (viewerMode == TypeEditor.ACTIVITY)
            {
                cntAct--;
            }
            else if (viewerMode == TypeEditor.ROUTECONFIG)
            {
                cntRoute--;
            }
            if (cntAct < 0)
                cntAct = 0;
            if (cntRoute < 0)
                cntRoute = 0;
            UpdateToolsVisibility();
        }

        void UpdateToolsVisibility()
        {
            this.activityCFG.IsVisible = cntAct > 0;
            this.routeCFG.IsVisible = cntRoute > 0;
            this.toolStrip3.IsVisible = cntRoute > 0 || cntAct > 0;
        }

        private void ActEditorKeyDown(object sender, KeyEventArgs e)
        {
            if (selectedViewer != null)
            {
                selectedViewer.Viewer2D_KeyDown(sender, e);
            }
        }

        private void editMetaSegment(object sender, RoutedEventArgs e)
        {
            if (selectedViewer == null)
                return;
            DisplayStatusMessage("Edit Metadata for Segment");
            selectedViewer.SetToolClicked(ToolClicks.METASEGMENT);
        }

        private void SaveRouteCfg(object sender, RoutedEventArgs e)
        {

        }
    }
}
