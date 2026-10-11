// COPYRIGHT 2014 by the Open Rails project.
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

using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Input;
using Avalonia.Input.Platform;
using Avalonia.Interactivity;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Threading;
using ORTS.Settings;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;

namespace ORTS.ContentManager
{
    public class ContentManagerWindow : Window
    {
        readonly UserSettings Settings;
        readonly ContentManager ContentManager;

        static readonly Regex ContentBold = new Regex("^[\\w ]+:$");
        static readonly Regex ContentLink = new Regex("\u0001(.*?)\u0002(.*?)\u0001");

        const string LoadingText = "Loading...";

        readonly TextBox searchBox;
        readonly ListBox searchResults;
        readonly TreeView treeViewContent;
        readonly StackPanel contentPanel;
        readonly ScrollViewer contentScroller;
        string contentPlainText = "";

        Content PendingSelection;
        readonly List<string> PendingSelections = new List<string>();

        CancellationTokenSource SearchCancellation;
        const string InitialSearchPendingText = "Searching...";
        const int InitialSearchPendingLength = 9;
        int SearchPendingLength = InitialSearchPendingLength;
        DateTimeOffset SearchPendingTime;

        class SearchResult
        {
            public string Name;
            public string[] Path;
            public SearchResult(Content content, string path)
            {
                var placeEnd = Math.Max(path.LastIndexOf(" / "), path.LastIndexOf(" -> "));
                var place = path.Substring(0, placeEnd);
                Name = string.Format("{0} ({1}) in {2}", content.Name, content.Type, place);
                Path = path.Split(new[] { " / ", " -> " }, StringSplitOptions.None);
            }

            public override string ToString()
            {
                return Name;
            }
        }

        public ContentManagerWindow()
        {
            Title = "Open Rails Content Manager";
            Width = 844;
            Height = 538;
            WindowStartupLocation = WindowStartupLocation.CenterScreen;
            Icon = App.LoadIcon();

            searchBox = new TextBox { PlaceholderText = "Search for items...", Margin = new Thickness(0, 0, 0, 4) };
            searchBox.TextChanged += searchBox_TextChanged;
            DockPanel.SetDock(searchBox, Dock.Top);

            searchResults = new ListBox { Height = 200, IsVisible = false, Margin = new Thickness(0, 0, 0, 4) };
            searchResults.DoubleTapped += searchResults_DoubleClick;
            DockPanel.SetDock(searchResults, Dock.Top);

            treeViewContent = new TreeView();
            treeViewContent.AddHandler(TreeViewItem.ExpandedEvent, treeViewContent_Expanded);
            treeViewContent.SelectionChanged += treeViewContent_AfterSelect;

            var leftPanel = new DockPanel { MinWidth = 100 };
            leftPanel.Children.Add(searchBox);
            leftPanel.Children.Add(searchResults);
            leftPanel.Children.Add(new Border
            {
                BorderThickness = new Thickness(1),
                BorderBrush = Brushes.Gray,
                Child = treeViewContent,
            });

            contentPanel = new StackPanel { Margin = new Thickness(4) };
            contentScroller = new ScrollViewer
            {
                HorizontalScrollBarVisibility = ScrollBarVisibility.Auto,
                VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
                Content = contentPanel,
            };
            var copyItem = new MenuItem { Header = "Copy all text" };
            copyItem.Click += async (s, e) =>
            {
                var clipboard = GetTopLevel(this)?.Clipboard;
                if (clipboard != null)
                    await clipboard.SetTextAsync(contentPlainText);
            };
            var rightPanel = new Border
            {
                BorderThickness = new Thickness(1),
                BorderBrush = Brushes.Gray,
                MinWidth = 100,
                Child = contentScroller,
                ContextMenu = new ContextMenu { Items = { copyItem } },
                Background = Brushes.Transparent,
            };

            var grid = new Grid
            {
                Margin = new Thickness(12),
                ColumnDefinitions = new ColumnDefinitions("300,4,*"),
            };
            Grid.SetColumn(leftPanel, 0);
            var splitter = new GridSplitter { ResizeDirection = GridResizeDirection.Columns, Background = Brushes.Transparent };
            Grid.SetColumn(splitter, 1);
            Grid.SetColumn(rightPanel, 2);
            grid.Children.Add(leftPanel);
            grid.Children.Add(splitter);
            grid.Children.Add(rightPanel);
            Content = grid;

            string settingsError = null;
            try
            {
                Settings = new UserSettings(new string[0]);
            }
            catch (Exception error)
            {
                // The settings store (registry / INI) may not be available on this platform.
                settingsError = error.GetBaseException().ToString();
                Trace.TraceError("Unable to load Open Rails settings: {0}", error);
            }
            ContentManager = new ContentManager(Settings?.Folders);

            // add unit settings to ContentInfo static class; only considering explict setting or system setting
            if (Settings?.Units == "Metric") { ContentInfo.IsMetric = true; }
            else if (Settings?.Units == "UK") { ContentInfo.IsUK = true; }
            else // setting is Automatic or Route
            {
                ContentInfo.IsMetric = System.Globalization.RegionInfo.CurrentRegion.IsMetric;
                // special cases
                if (System.Globalization.RegionInfo.CurrentRegion.Name == "UK") { ContentInfo.IsUK = true; }
                else if (System.Globalization.RegionInfo.CurrentRegion.Name == "CA") { ContentInfo.IsMetric = false; } // Canada is metric, but RRs use imperial US
            }

            // Start off the tree with the Content Manager itself at the root and expand to show packages.
            var root = CreateContentNode(ContentManager);
            treeViewContent.Items.Add(root);
            root.IsExpanded = true;

            if (settingsError != null)
                ShowContentText("Error:\tUnable to load Open Rails settings, so no content folders are available.\n\n" + settingsError);
        }

        static IEnumerable<TreeViewItem> ChildNodes(TreeViewItem node)
        {
            return node.Items.OfType<TreeViewItem>();
        }

        static IEnumerable<TreeViewItem> RootNodes(TreeView tree)
        {
            return tree.Items.OfType<TreeViewItem>();
        }

        void treeViewContent_Expanded(object sender, RoutedEventArgs e)
        {
            if (!(e.Source is TreeViewItem node))
                return;

            var children = ChildNodes(node).ToList();
            // Are we about to expand a not-yet-loaded node? This is identified by a single child with no text or tag.
            if (node.Tag is Content content && children.Count == 1 && (children[0].Header as string) == "" && children[0].Tag == null)
            {
                // Make use of the single child to show a loading message.
                var loadingNode = children[0];
                loadingNode.Header = LoadingText;
                Task.Run(() =>
                {
                    // Get all the different possible types of content from this content item.
                    var getChildren = ((ContentType[])Enum.GetValues(typeof(ContentType))).SelectMany(ct => content.Get(ct).Select(c => (Content: c, Missing: (string)null)));
                    var linkChildren = ContentLink.Matches(ContentInfo.GetText(content)).Cast<Match>().Select(linkMatch => ResolveLink(content, linkMatch.Groups[1].Value, (ContentType)Enum.Parse(typeof(ContentType), linkMatch.Groups[2].Value)));
                    Debug.Assert(!getChildren.Any() || !linkChildren.Any(), "Content item should not return items from Get(ContentType) and Get(string, ContentType)");
                    return getChildren.Concat(linkChildren).ToArray();
                }).ContinueWith(task =>
                {
                    // If the loading node is missing, we should abort here to avoid issues caused by double-loading.
                    if (!node.Items.Contains(loadingNode) || (loadingNode.Header as string) != LoadingText)
                        return;

                    // Collapse node if we're going to end up with no child nodes.
                    if (task.Exception == null && task.Result.Length == 0)
                        node.IsExpanded = false;

                    // Remove the loading node.
                    node.Items.Remove(loadingNode);

                    // Add either the error we encountered or the resulting content items.
                    if (task.Exception != null)
                        node.Items.Add(new TreeViewItem { Header = task.Exception.GetBaseException().Message });
                    else
                        foreach (var child in task.Result)
                            node.Items.Add(child.Content != null ? CreateContentNode(child.Content) : new TreeViewItem { Header = child.Missing });

                    CheckForPendingActions(node);
                }, TaskScheduler.FromCurrentSynchronizationContext());
            }
            else
            {
                Dispatcher.UIThread.Post(() => CheckForPendingActions(node));
            }
        }

        static (Content Content, string Missing) ResolveLink(Content content, string name, ContentType type)
        {
            var c = content.Get(name, type);
            if (c != null)
                return (c, null);
            return (null, String.Format("Missing: {0} ({1})", name, type));
        }

        void CheckForPendingActions(TreeViewItem startNode)
        {
            if (PendingSelection != null)
            {
                var pendingSelectionNode = ChildNodes(startNode).FirstOrDefault(node => node.Tag as Content == PendingSelection);
                if (pendingSelectionNode != null)
                {
                    treeViewContent.SelectedItem = pendingSelectionNode;
                    pendingSelectionNode.BringIntoView();
                    treeViewContent.Focus();
                }
                PendingSelection = null;
            }
            if (PendingSelections.Count > 0)
            {
                var pendingSelectionNode = ChildNodes(startNode).FirstOrDefault(node => (node.Tag as Content)?.Name == PendingSelections[0]);
                PendingSelections.RemoveAt(0);
                if (pendingSelectionNode != null)
                {
                    treeViewContent.SelectedItem = pendingSelectionNode;
                    pendingSelectionNode.BringIntoView();
                    pendingSelectionNode.IsExpanded = true;
                }
            }
        }

        static TreeViewItem CreateContentNode(Content c)
        {
            var node = new TreeViewItem { Header = String.Format("{0} ({1})", c.Name, c.Type), Tag = c };
            node.Items.Add(new TreeViewItem { Header = "" });
            return node;
        }

        void treeViewContent_AfterSelect(object sender, SelectionChangedEventArgs e)
        {
            contentPanel.Children.Clear();
            contentPlainText = "";

            if (!(treeViewContent.SelectedItem is TreeViewItem node) || !(node.Tag is Content content))
                return;

            Trace.TraceInformation("Updating content view with content {0}", content);
            ShowContentText(ContentInfo.GetText(content));
            contentScroller.Offset = new Vector(0, 0);
        }

        /// <summary>
        /// Render the text from <see cref="ContentInfo.GetText(Content)"/>: lines with tab-separated columns
        /// (aligned to tab stops), bold "Label:" columns and \u0001name\u0002type\u0001 links.
        /// </summary>
        void ShowContentText(string text)
        {
            var plain = new StringBuilder();
            var lineHeight = FontSize * 1.33;
            var tabStops = new[] { lineHeight * 5, lineHeight * 15, lineHeight * 25, lineHeight * 35 };
            foreach (var line in text.Replace("\r\n", "\n").Split('\n'))
            {
                var lineText = ContentLink.Replace(line, "$1");
                plain.AppendLine(lineText);
                if (line.Length == 0)
                {
                    contentPanel.Children.Add(new TextBlock { Text = " " });
                    continue;
                }

                var linePanel = new TabStopPanel(tabStops, lineHeight * 10);
                var columns = line.Split('\t');
                for (var i = 0; i < columns.Length; i++)
                {
                    var column = columns[i];
                    // Bold column labels which are followed by another column.
                    var isLabel = i < columns.Length - 1 && ContentBold.IsMatch(column);
                    var cell = new StackPanel { Orientation = Orientation.Horizontal };
                    var position = 0;
                    foreach (Match link in ContentLink.Matches(column))
                    {
                        if (link.Index > position)
                            cell.Children.Add(MakeText(column.Substring(position, link.Index - position), isLabel));
                        cell.Children.Add(MakeLink(link.Groups[1].Value, link.Groups[2].Value));
                        position = link.Index + link.Length;
                    }
                    if (position < column.Length || column.Length == 0)
                        cell.Children.Add(MakeText(column.Substring(position), isLabel));
                    linePanel.Children.Add(cell);
                }
                contentPanel.Children.Add(linePanel);
            }
            contentPlainText = plain.ToString();
        }

        static TextBlock MakeText(string text, bool bold)
        {
            return new SelectableTextBlock { Text = text, FontWeight = bold ? FontWeight.Bold : FontWeight.Normal };
        }

        TextBlock MakeLink(string name, string type)
        {
            var link = new TextBlock
            {
                Text = name,
                Foreground = Brushes.RoyalBlue,
                TextDecorations = TextDecorations.Underline,
                Cursor = new Cursor(StandardCursorType.Hand),
            };
            link.PointerReleased += (s, e) => LinkClicked(name, type);
            return link;
        }

        void LinkClicked(string name, string type)
        {
            if (!(treeViewContent.SelectedItem is TreeViewItem selectedNode) || !(selectedNode.Tag is Content content))
                return;
            if (!Enum.TryParse(type, out ContentType contentType))
                return;

            PendingSelection = content.Get(name, contentType);
            if (selectedNode.IsExpanded && !ChildNodes(selectedNode).Any(node => (node.Header as string) == LoadingText))
            {
                var pendingSelectionNode = ChildNodes(selectedNode).FirstOrDefault(node => node.Tag as Content == PendingSelection);
                if (pendingSelectionNode != null)
                {
                    treeViewContent.SelectedItem = pendingSelectionNode;
                    pendingSelectionNode.BringIntoView();
                    treeViewContent.Focus();
                }
                PendingSelection = null;
            }
            else
            {
                selectedNode.IsExpanded = true;
            }
        }

        void searchBox_TextChanged(object sender, TextChangedEventArgs e)
        {
            if (SearchCancellation != null)
            {
                SearchCancellation.Cancel();
                SearchCancellation = null;
            }

            var searchText = searchBox.Text ?? "";
            searchResults.Items.Clear();
            searchResults.IsVisible = searchText.Length > 0;
            if (!searchResults.IsVisible)
                return;

            searchResults.Items.Add("");
            SearchPendingTime = DateTimeOffset.Now;

            var cancellation = new CancellationTokenSource();
            var token = cancellation.Token;
            SearchCancellation = cancellation;

            Task.Run(() =>
            {
                var search = searchText.ToLowerInvariant();
                var finds = new List<SearchResult>();
                var pending = new Dictionary<string, Content>();
                pending.Add(ContentManager.Name, ContentManager);

                while (pending.Count > 0 && !token.IsCancellationRequested)
                {
                    var path = pending.Keys.First();
                    var content = pending[path];
                    pending.Remove(path);

                    // do not add root element to search results, see bug 1944070
                    if (content.Type != ContentType.Root && content.Name.ToLowerInvariant().Contains(search))
                        finds.Add(new SearchResult(content, path));

                    foreach (var child in ((ContentType[])Enum.GetValues(typeof(ContentType))).SelectMany(ct => content.Get(ct)))
                        if (!pending.ContainsKey(path + " / " + child.Name))
                            pending.Add(path + " / " + child.Name, child);

                    foreach (var child in ContentLink.Matches(ContentInfo.GetText(content)).Cast<Match>().Select(linkMatch => content.Get(linkMatch.Groups[1].Value, (ContentType)Enum.Parse(typeof(ContentType), linkMatch.Groups[2].Value))).Where(linkContent => linkContent != null))
                        if (!pending.ContainsKey(path + " -> " + child.Name))
                            pending.Add(path + " -> " + child.Name, child);

                    var snapshot = finds.ToList();
                    var pendingCount = pending.Count;
                    Dispatcher.UIThread.Post(() =>
                    {
                        if (!token.IsCancellationRequested)
                            UpdateSearchResults(snapshot, pendingCount, false);
                    });
                }

                return finds;
            }, token).ContinueWith(task =>
            {
                if (token.IsCancellationRequested || task.IsCanceled)
                    return;

                if (task.Exception != null)
                {
                    MessageBox.Show(this, task.Exception.GetBaseException().Message, Title);
                    return;
                }

                UpdateSearchResults(task.Result, 0, true);
            }, TaskScheduler.FromCurrentSynchronizationContext());
        }

        void UpdateSearchResults(List<SearchResult> results, int pending, bool done)
        {
            if (done || (DateTimeOffset.Now - SearchPendingTime).TotalSeconds >= 0.5)
            {
                foreach (var result in results.Skip(searchResults.Items.Count - 1))
                    searchResults.Items.Add(result);
                SearchPendingLength++;
                if (SearchPendingLength > InitialSearchPendingText.Length) SearchPendingLength = InitialSearchPendingLength;
                searchResults.Items[0] = "[" + pending + "/" + results.Count + "] " + InitialSearchPendingText.Substring(0, SearchPendingLength);
                SearchPendingTime = DateTimeOffset.Now;
            }
            if (done)
            {
                searchResults.Items.RemoveAt(0);
            }
        }

        static void CollapseAll(IEnumerable<TreeViewItem> nodes)
        {
            foreach (var node in nodes)
            {
                CollapseAll(ChildNodes(node));
                node.IsExpanded = false;
            }
        }

        void searchResults_DoubleClick(object sender, TappedEventArgs e)
        {
            var result = searchResults.SelectedItem as SearchResult;
            if (result == null)
                return;

            PendingSelections.Clear();
            PendingSelections.AddRange(result.Path.Skip(1).ToArray());
            treeViewContent.Focus();
            CollapseAll(RootNodes(treeViewContent));
            var root = RootNodes(treeViewContent).First();
            treeViewContent.SelectedItem = root;
            root.IsExpanded = true;
        }
    }

    /// <summary>
    /// Lays out children horizontally, starting each child at the next tab stop after the end of the previous one
    /// (emulating tab characters in a rich text box).
    /// </summary>
    class TabStopPanel : Panel
    {
        readonly double[] TabStops;
        readonly double TabIncrement;

        public TabStopPanel(double[] tabStops, double tabIncrement)
        {
            TabStops = tabStops;
            TabIncrement = tabIncrement;
        }

        double NextTabStop(double x)
        {
            foreach (var stop in TabStops)
                if (stop > x)
                    return stop;
            var last = TabStops.Length > 0 ? TabStops[TabStops.Length - 1] : 0;
            return last + Math.Ceiling((x - last + 0.001) / TabIncrement) * TabIncrement;
        }

        IEnumerable<(Control Child, double X)> Layout()
        {
            var x = 0.0;
            var first = true;
            foreach (var child in Children)
            {
                if (!first)
                    x = NextTabStop(x);
                first = false;
                yield return (child, x);
                x += child.DesiredSize.Width;
            }
        }

        protected override Size MeasureOverride(Size availableSize)
        {
            var height = 0.0;
            foreach (var child in Children)
            {
                child.Measure(new Size(double.PositiveInfinity, double.PositiveInfinity));
                height = Math.Max(height, child.DesiredSize.Height);
            }
            var width = Layout().Select(l => l.X + l.Child.DesiredSize.Width).DefaultIfEmpty(0).Max();
            return new Size(width, height);
        }

        protected override Size ArrangeOverride(Size finalSize)
        {
            foreach (var (child, x) in Layout())
                child.Arrange(new Rect(x, 0, child.DesiredSize.Width, finalSize.Height));
            return finalSize;
        }
    }

    /// <summary>
    /// Minimal replacement for System.Windows.Forms.MessageBox.
    /// </summary>
    static class MessageBox
    {
        public static Task Show(Window owner, string message, string title)
        {
            var dialog = new Window
            {
                Title = title,
                SizeToContent = SizeToContent.WidthAndHeight,
                MaxWidth = 800,
                CanResize = false,
                WindowStartupLocation = WindowStartupLocation.CenterOwner,
            };
            var ok = new Button { Content = "OK", HorizontalAlignment = HorizontalAlignment.Right, IsDefault = true, MinWidth = 80 };
            ok.Click += (s, e) => dialog.Close();
            dialog.Content = new StackPanel
            {
                Margin = new Thickness(16),
                Spacing = 16,
                Children =
                {
                    new SelectableTextBlock { Text = message, TextWrapping = TextWrapping.Wrap },
                    ok,
                },
            };
            return dialog.ShowDialog(owner);
        }
    }
}
