// COPYRIGHT 2026 by the Open Rails project.
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
//
// A small menu system drawn with MonoGame inside the TrackViewer window. It replaces the WPF menu that used to be
// docked into the (WinForms) game window, as well as the WPF context menus of the path editor and labels.
// Everything here runs on the game thread, so menu actions can directly use the game state.

using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Input;
using ORTS.TrackViewer.Drawing;

namespace ORTS.TrackViewer.UserInterface
{
    /// <summary>
    /// A single item in a menu (or a menu bar).
    /// </summary>
    public class GameMenuItem
    {
        /// <summary>Text to show</summary>
        public string Header { get; set; }
        /// <summary>Name of a BasicShapes texture to use as icon (may be null)</summary>
        public string Icon { get; set; }
        /// <summary>Optional tooltip, shown at the bottom of the popup</summary>
        public string ToolTip { get; set; }
        /// <summary>Does clicking the item toggle IsChecked?</summary>
        public bool IsCheckable { get; set; }
        /// <summary>Is the item checked</summary>
        public bool IsChecked { get; set; }
        /// <summary>Can the item be clicked?</summary>
        public bool IsEnabled { get; set; } = true;
        /// <summary>Is the item shown at all?</summary>
        public bool IsVisible { get; set; } = true;
        /// <summary>Items with the same (non-null) group in the same menu behave like radio buttons</summary>
        public string RadioGroup { get; set; }
        /// <summary>Generic tag</summary>
        public object Tag { get; set; }
        /// <summary>Is this a separator line</summary>
        public bool IsSeparator { get; private set; }
        /// <summary>Sub items (if any)</summary>
        public List<GameMenuItem> Items { get; } = new List<GameMenuItem>();
        /// <summary>Called when the item is clicked (after toggling the check state)</summary>
        public event Action<GameMenuItem> Click;
        /// <summary>Called just before the sub menu of this item opens</summary>
        public event Action<GameMenuItem> SubmenuOpening;
        /// <summary>Called when the sub menu of this item has been closed</summary>
        public event Action<GameMenuItem> SubmenuClosed;

        /// <summary>Constructor</summary>
        public GameMenuItem() { }

        /// <summary>Constructor with header and optional click handler</summary>
        public GameMenuItem(string header, Action<GameMenuItem> click = null)
        {
            Header = header;
            if (click != null) Click += click;
        }

        /// <summary>Create a separator</summary>
        public static GameMenuItem Separator() => new GameMenuItem { IsSeparator = true, IsEnabled = false };

        /// <summary>Create a checkable item</summary>
        public static GameMenuItem Checkable(string header, Action<GameMenuItem> click = null, bool isChecked = false)
        {
            var item = new GameMenuItem(header, click) { IsCheckable = true, IsChecked = isChecked };
            return item;
        }

        /// <summary>Add a sub item and return it</summary>
        public GameMenuItem Add(GameMenuItem item)
        {
            Items.Add(item);
            return item;
        }

        /// <summary>Does the item have a visible sub menu</summary>
        public bool HasSubmenu => Items.Count > 0;

        internal void RaiseClick() => Click?.Invoke(this);
        internal void RaiseSubmenuOpening() => SubmenuOpening?.Invoke(this);
        internal void RaiseSubmenuClosed() => SubmenuClosed?.Invoke(this);

        /// <summary>Find a direct or indirect sub item by header</summary>
        public override string ToString() => Header ?? (IsSeparator ? "----" : "");
    }

    /// <summary>
    /// Menu bar with drop-down menus, and context menus, all drawn and handled in the game window.
    /// </summary>
    public class GameMenu
    {
        private class Popup
        {
            public GameMenuItem Owner;
            public Rectangle Bounds;
            public List<GameMenuItem> Items;
            public List<Rectangle> ItemBounds = new List<Rectangle>();
            public int FirstVisible;    // for scrolling
            public int LastVisible;     // inclusive
            public int Hover = -1;
            public bool CanScrollUp => FirstVisible > 0;
            public bool CanScrollDown => LastVisible < Items.Count - 1;
        }

        /// <summary>Items in the menu bar</summary>
        public List<GameMenuItem> BarItems { get; } = new List<GameMenuItem>();

        /// <summary>Show the bar or not (context-menu-only instances do not show a bar)</summary>
        public bool ShowBar { get; set; } = true;

        /// <summary>Height of the bar in pixels</summary>
        public int BarHeight => ShowBar ? Font.Height + 2 * paddingY : 0;

        /// <summary>Is any menu popup open</summary>
        public bool IsOpen => popups.Count > 0;

        /// <summary>Was the last mouse/keyboard input used by the menu?</summary>
        public bool HasCapturedInput { get; private set; }

        /// <summary>Called when all popups are closed (after having been open)</summary>
        public event Action Closed;

        private readonly List<Popup> popups = new List<Popup>();
        private readonly List<Rectangle> barItemBounds = new List<Rectangle>();
        private GameMenuItem openBarItem;
        private int screenWidth = 1024;
        private int screenHeight = 768;
        private int hoverBarIndex = -1;
        private bool pressStartedInMenu;

        private const int paddingX = 8;
        private const int paddingY = 3;
        private const int iconColumn = 22;
        private const int arrowColumn = 18;
        private const int separatorHeight = 7;

        private static readonly Color barBackground = new Color(240, 240, 240);
        private static readonly Color popupBackground = new Color(250, 250, 250);
        private static readonly Color border = new Color(160, 160, 160);
        private static readonly Color hover = new Color(204, 228, 247);
        private static readonly Color pressedBar = new Color(204, 228, 247);
        private static readonly Color text = new Color(20, 20, 20);
        private static readonly Color disabledText = new Color(160, 160, 160);
        private static readonly Color separator = new Color(210, 210, 210);
        private static readonly Color tooltipBackground = new Color(255, 255, 225);

        private TextFont Font => FontManager.Instance.UIFont;
        private int ItemHeight => Font.Height + 2 * paddingY + 2;

        /// <summary>Set the size of the window (needed for placing popups)</summary>
        public void SetScreenSize(int width, int height)
        {
            screenWidth = width;
            screenHeight = height;
            CloseAll();
        }

        #region Opening and closing
        /// <summary>Open a context menu with the given items at the given location in the window</summary>
        public void OpenContextMenu(IEnumerable<GameMenuItem> items, int x, int y)
        {
            CloseAll();
            var root = new GameMenuItem("context");
            root.Items.AddRange(items);
            if (!root.Items.Any(i => i.IsVisible && !i.IsSeparator)) return;
            OpenPopup(root, new Rectangle(x, y, 0, 0), below: true);
        }

        /// <summary>Open the drop-down menu of one of the bar items (e.g. for Alt-key shortcuts)</summary>
        public void OpenBarMenu(GameMenuItem barItem)
        {
            int index = BarItems.IndexOf(barItem);
            if (index < 0 || !ShowBar) return;
            CloseAll();
            LayoutBar();
            openBarItem = barItem;
            OpenPopup(barItem, barItemBounds[index], below: true);
        }

        /// <summary>Close all popups</summary>
        public void CloseAll()
        {
            bool wasOpen = popups.Count > 0;
            CloseFrom(0);
            openBarItem = null;
            if (wasOpen) Closed?.Invoke();
        }

        private void CloseFrom(int level)
        {
            while (popups.Count > level)
            {
                Popup popup = popups[popups.Count - 1];
                popups.RemoveAt(popups.Count - 1);
                popup.Owner.RaiseSubmenuClosed();
            }
        }

        private void OpenPopup(GameMenuItem owner, Rectangle anchor, bool below)
        {
            owner.RaiseSubmenuOpening();
            var popup = new Popup
            {
                Owner = owner,
                Items = owner.Items.Where(i => i.IsVisible).ToList(),
            };
            // remove leading/trailing/double separators
            popup.Items = CleanSeparators(popup.Items);
            if (popup.Items.Count == 0) return;

            int maxTextWidth = popup.Items.Max(i => i.IsSeparator ? 0 : Font.MeasureString(i.Header ?? ""));
            int width = iconColumn + maxTextWidth + arrowColumn + 2 * paddingX;
            int fullHeight = popup.Items.Sum(i => i.IsSeparator ? separatorHeight : ItemHeight) + 2;

            int x = below ? anchor.X : anchor.Right;
            int y = below ? anchor.Bottom : anchor.Y;
            if (x + width > screenWidth)
            {
                x = below ? screenWidth - width : anchor.X - width;
            }
            x = Math.Max(0, x);
            int maxHeight = Math.Max(ItemHeight * 3, screenHeight - 2);
            int height = Math.Min(fullHeight, maxHeight);
            if (y + height > screenHeight)
            {
                y = Math.Max(0, screenHeight - height);
            }
            popup.Bounds = new Rectangle(x, y, width, height);
            popup.FirstVisible = 0;
            LayoutPopup(popup);
            popups.Add(popup);
        }

        private static List<GameMenuItem> CleanSeparators(List<GameMenuItem> items)
        {
            var result = new List<GameMenuItem>();
            foreach (GameMenuItem item in items)
            {
                if (item.IsSeparator && (result.Count == 0 || result[result.Count - 1].IsSeparator)) continue;
                result.Add(item);
            }
            while (result.Count > 0 && result[result.Count - 1].IsSeparator) result.RemoveAt(result.Count - 1);
            return result;
        }

        private void LayoutPopup(Popup popup)
        {
            popup.ItemBounds.Clear();
            int scrollMarker = 0;
            int available = popup.Bounds.Height - 2;
            bool needsScroll = popup.Items.Sum(i => i.IsSeparator ? separatorHeight : ItemHeight) > available;
            if (needsScroll) scrollMarker = ItemHeight / 2;
            int y = popup.Bounds.Y + 1 + (needsScroll ? scrollMarker : 0);
            int bottom = popup.Bounds.Bottom - 1 - (needsScroll ? scrollMarker : 0);
            popup.LastVisible = popup.FirstVisible - 1;
            for (int i = 0; i < popup.Items.Count; i++)
            {
                if (i < popup.FirstVisible)
                {
                    popup.ItemBounds.Add(Rectangle.Empty);
                    continue;
                }
                int h = popup.Items[i].IsSeparator ? separatorHeight : ItemHeight;
                if (y + h > bottom)
                {
                    popup.ItemBounds.Add(Rectangle.Empty);
                    continue;
                }
                popup.ItemBounds.Add(new Rectangle(popup.Bounds.X + 1, y, popup.Bounds.Width - 2, h));
                popup.LastVisible = i;
                y += h;
            }
        }

        private void LayoutBar()
        {
            barItemBounds.Clear();
            int x = 2;
            foreach (GameMenuItem item in BarItems)
            {
                if (!item.IsVisible)
                {
                    barItemBounds.Add(Rectangle.Empty);
                    continue;
                }
                int w = Font.MeasureString(item.Header ?? "") + 2 * paddingX;
                barItemBounds.Add(new Rectangle(x, 0, w, BarHeight));
                x += w;
            }
        }
        #endregion

        #region Input
        /// <summary>
        /// Handle mouse and keyboard input. Returns true when the input was used by the menu, in which case the
        /// caller should not act on it.
        /// </summary>
        public bool HandleInput()
        {
            HasCapturedInput = false;
            int mx = TVUserInput.MouseLocationX;
            int my = TVUserInput.MouseLocationY;
            var mouse = new Point(mx, my);
            if (ShowBar) LayoutBar();

            if (IsOpen && HandleKeyboard())
            {
                HasCapturedInput = true;
                return true;
            }

            int barIndex = ShowBar ? barItemBounds.FindIndex(r => r.Contains(mouse)) : -1;
            hoverBarIndex = barIndex;
            int popupIndex = popups.FindLastIndex(p => p.Bounds.Contains(mouse));

            // wheel scrolling in a popup
            if (popupIndex >= 0 && TVUserInput.IsMouseWheelChanged())
            {
                Popup popup = popups[popupIndex];
                int steps = TVUserInput.MouseWheelChange() > 0 ? -3 : 3;
                Scroll(popup, steps);
                HasCapturedInput = true;
                return true;
            }

            // hovering
            if (popupIndex >= 0)
            {
                Popup popup = popups[popupIndex];
                int itemIndex = popup.ItemBounds.FindIndex(r => r.Contains(mouse));
                if (itemIndex >= 0 && popup.Items[itemIndex].IsSeparator) itemIndex = -1;
                if (TVUserInput.IsMouseMoved() || TVUserInput.IsMouseLeftButtonPressed())
                {
                    popup.Hover = itemIndex;
                    if (itemIndex >= 0)
                    {
                        GameMenuItem item = popup.Items[itemIndex];
                        bool alreadyOpen = popups.Count > popupIndex + 1 && popups[popupIndex + 1].Owner == item;
                        if (!alreadyOpen)
                        {
                            CloseFrom(popupIndex + 1);
                            if (item.HasSubmenu && item.IsEnabled)
                            {
                                OpenPopup(item, popup.ItemBounds[itemIndex], below: false);
                            }
                        }
                    }
                }
            }
            else if (barIndex >= 0 && IsOpen && openBarItem != null && BarItems[barIndex] != openBarItem && TVUserInput.IsMouseMoved())
            {   // moving along the bar while a menu is open: switch menus
                OpenBarMenu(BarItems[barIndex]);
            }

            if (TVUserInput.IsMouseLeftButtonPressed() || TVUserInput.IsMouseRightButtonPressed())
            {
                pressStartedInMenu = popupIndex >= 0 || barIndex >= 0;
                if (barIndex >= 0 && TVUserInput.IsMouseLeftButtonPressed())
                {
                    if (openBarItem == BarItems[barIndex]) CloseAll();
                    else OpenBarMenu(BarItems[barIndex]);
                    HasCapturedInput = true;
                    return true;
                }
                if (popupIndex < 0 && IsOpen)
                {   // click outside: close, and do not let the click do anything else
                    CloseAll();
                    HasCapturedInput = true;
                    return true;
                }
            }

            if (TVUserInput.IsMouseLeftButtonReleased() && popupIndex >= 0)
            {
                Popup popup = popups[popupIndex];
                int itemIndex = popup.ItemBounds.FindIndex(r => r.Contains(mouse));
                if (itemIndex >= 0) Activate(popup, itemIndex);
                HasCapturedInput = true;
                return true;
            }

            if (popupIndex >= 0 || barIndex >= 0)
            {
                HasCapturedInput = true;
                return true;
            }
            if (pressStartedInMenu && (TVUserInput.IsMouseLeftButtonDown() || TVUserInput.IsMouseLeftButtonReleased()))
            {   // a press that started in the menu does not become a drag of the map
                if (TVUserInput.IsMouseLeftButtonReleased()) pressStartedInMenu = false;
                HasCapturedInput = true;
                return true;
            }
            pressStartedInMenu = false;
            return IsOpen;
        }

        private void Scroll(Popup popup, int steps)
        {
            int newFirst = popup.FirstVisible + steps;
            int visibleCount = popup.LastVisible - popup.FirstVisible + 1;
            newFirst = Math.Min(newFirst, popup.Items.Count - visibleCount);
            newFirst = Math.Max(0, newFirst);
            if (newFirst == popup.FirstVisible) return;
            popup.FirstVisible = newFirst;
            popup.Hover = -1;
            CloseFrom(popups.IndexOf(popup) + 1);
            LayoutPopup(popup);
        }

        private bool HandleKeyboard()
        {
            Popup popup = popups[popups.Count - 1];
            if (TVUserInput.IsKeyPressed(Keys.Escape))
            {
                if (popups.Count > 1) CloseFrom(popups.Count - 1);
                else CloseAll();
                return true;
            }
            if (TVUserInput.IsKeyPressed(Keys.Down) || TVUserInput.IsKeyPressed(Keys.Up))
            {
                int direction = TVUserInput.IsKeyPressed(Keys.Down) ? 1 : -1;
                int index = popup.Hover;
                for (int n = 0; n < popup.Items.Count; n++)
                {
                    index = (index + direction + popup.Items.Count) % popup.Items.Count;
                    if (!popup.Items[index].IsSeparator) break;
                }
                popup.Hover = index;
                if (index < popup.FirstVisible) Scroll(popup, index - popup.FirstVisible);
                else if (index > popup.LastVisible) Scroll(popup, index - popup.LastVisible);
                CloseFrom(popups.IndexOf(popup) + 1);
                return true;
            }
            if (TVUserInput.IsKeyPressed(Keys.Right) && popup.Hover >= 0 && popup.Items[popup.Hover].HasSubmenu)
            {
                OpenPopup(popup.Items[popup.Hover], popup.ItemBounds[popup.Hover], below: false);
                if (popups.Count > 0) popups[popups.Count - 1].Hover = 0;
                return true;
            }
            if (TVUserInput.IsKeyPressed(Keys.Left))
            {
                if (popups.Count > 1) CloseFrom(popups.Count - 1);
                else if (openBarItem != null)
                {
                    int index = BarItems.IndexOf(openBarItem);
                    OpenBarMenu(BarItems[(index - 1 + BarItems.Count) % BarItems.Count]);
                }
                return true;
            }
            if (TVUserInput.IsKeyPressed(Keys.Right) && openBarItem != null)
            {
                int index = BarItems.IndexOf(openBarItem);
                OpenBarMenu(BarItems[(index + 1) % BarItems.Count]);
                return true;
            }
            if (TVUserInput.IsKeyPressed(Keys.Enter) && popup.Hover >= 0)
            {
                Activate(popup, popup.Hover);
                return true;
            }
            // While a menu is open, swallow all keys so they do not act on the map
            return TVUserInput.AnyKeyDown();
        }

        private void Activate(Popup popup, int itemIndex)
        {
            GameMenuItem item = popup.Items[itemIndex];
            if (item.IsSeparator || !item.IsEnabled) return;
            if (item.HasSubmenu)
            {
                bool alreadyOpen = popups.Count > popups.IndexOf(popup) + 1 && popups[popups.IndexOf(popup) + 1].Owner == item;
                if (!alreadyOpen)
                {
                    CloseFrom(popups.IndexOf(popup) + 1);
                    OpenPopup(item, popup.ItemBounds[itemIndex], below: false);
                }
                return;
            }
            if (item.RadioGroup != null)
            {
                foreach (GameMenuItem other in popup.Owner.Items.Where(i => i.RadioGroup == item.RadioGroup))
                {
                    other.IsChecked = false;
                }
                item.IsChecked = true;
            }
            else if (item.IsCheckable)
            {
                item.IsChecked = !item.IsChecked;
            }
            CloseAll();
            item.RaiseClick();
        }
        #endregion

        #region Drawing
        /// <summary>
        /// Draw the bar and any open popups. Must be called inside SpriteBatch Begin/End.
        /// </summary>
        public void Draw()
        {
            if (ShowBar)
            {
                LayoutBar();
                BasicShapes.FillRectangle(new Rectangle(0, 0, screenWidth, BarHeight), barBackground);
                BasicShapes.FillRectangle(new Rectangle(0, BarHeight - 1, screenWidth, 1), separator);
                for (int i = 0; i < BarItems.Count; i++)
                {
                    if (!BarItems[i].IsVisible) continue;
                    Rectangle bounds = barItemBounds[i];
                    if (BarItems[i] == openBarItem || (i == hoverBarIndex && !IsOpen))
                    {
                        BasicShapes.FillRectangle(bounds, pressedBar);
                    }
                    BasicShapes.DrawString(Font, new Point(bounds.X + paddingX, bounds.Y + paddingY), BarItems[i].IsEnabled ? text : disabledText, BarItems[i].Header);
                }
            }

            foreach (Popup popup in popups)
            {
                DrawPopup(popup);
            }

            // tooltip of the hovered item in the deepest popup
            if (popups.Count > 0)
            {
                Popup last = popups.FindLast(p => p.Hover >= 0);
                if (last != null && !String.IsNullOrEmpty(last.Items[last.Hover].ToolTip))
                {
                    Rectangle itemBounds = last.ItemBounds[last.Hover];
                    string tip = last.Items[last.Hover].ToolTip;
                    int w = Font.MeasureString(tip) + 2 * paddingX;
                    int x = Math.Min(itemBounds.X + 20, screenWidth - w);
                    var tipBounds = new Rectangle(Math.Max(0, x), itemBounds.Bottom + 2, w, Font.Height + 2 * paddingY);
                    BasicShapes.FillRectangle(tipBounds, tooltipBackground);
                    BasicShapes.DrawRectangleOutline(tipBounds, border);
                    BasicShapes.DrawString(Font, new Point(tipBounds.X + paddingX, tipBounds.Y + paddingY), text, tip);
                }
            }
        }

        private void DrawPopup(Popup popup)
        {
            // shadow
            BasicShapes.FillRectangle(new Rectangle(popup.Bounds.X + 3, popup.Bounds.Y + 3, popup.Bounds.Width, popup.Bounds.Height), new Color(0, 0, 0, 60));
            BasicShapes.FillRectangle(popup.Bounds, popupBackground);
            BasicShapes.DrawRectangleOutline(popup.Bounds, border);

            for (int i = popup.FirstVisible; i <= popup.LastVisible; i++)
            {
                GameMenuItem item = popup.Items[i];
                Rectangle bounds = popup.ItemBounds[i];
                if (item.IsSeparator)
                {
                    BasicShapes.FillRectangle(new Rectangle(bounds.X + iconColumn, bounds.Y + bounds.Height / 2, bounds.Width - iconColumn - 4, 1), separator);
                    continue;
                }
                bool isOpenOwner = popups.Any(p => p.Owner == item);
                if ((i == popup.Hover || isOpenOwner) && item.IsEnabled)
                {
                    BasicShapes.FillRectangle(bounds, hover);
                }
                Color color = item.IsEnabled ? text : disabledText;
                int iconSize = Math.Min(16, bounds.Height - 4);
                var iconBounds = new Rectangle(bounds.X + (iconColumn - iconSize) / 2 + 2, bounds.Y + (bounds.Height - iconSize) / 2, iconSize, iconSize);
                if (item.IsChecked)
                {
                    DrawCheckMark(iconBounds, color, item.RadioGroup != null);
                }
                else if (item.Icon != null)
                {
                    BasicShapes.DrawTextureInRectangle(item.Icon, iconBounds);
                }
                else if (item.IsCheckable)
                {
                    BasicShapes.DrawRectangleOutline(new Rectangle(iconBounds.X + 2, iconBounds.Y + 2, iconBounds.Width - 4, iconBounds.Height - 4), separator);
                }
                BasicShapes.DrawString(Font, new Point(bounds.X + iconColumn + paddingX, bounds.Y + paddingY + 1), color, item.Header ?? "");
                if (item.HasSubmenu)
                {
                    DrawArrow(new Rectangle(bounds.Right - arrowColumn, bounds.Y, arrowColumn, bounds.Height), color);
                }
            }

            if (popup.CanScrollUp)
            {
                DrawScrollMarker(new Rectangle(popup.Bounds.X, popup.Bounds.Y + 1, popup.Bounds.Width, ItemHeight / 2), up: true);
            }
            if (popup.CanScrollDown)
            {
                DrawScrollMarker(new Rectangle(popup.Bounds.X, popup.Bounds.Bottom - 1 - ItemHeight / 2, popup.Bounds.Width, ItemHeight / 2), up: false);
            }
        }

        private static void DrawCheckMark(Rectangle bounds, Color color, bool isRadio)
        {
            if (isRadio)
            {
                int d = bounds.Width / 2;
                BasicShapes.FillRectangle(new Rectangle(bounds.Center.X - d / 2, bounds.Center.Y - d / 2, d, d), color);
                return;
            }
            var p1 = new Vector2(bounds.X + bounds.Width * 0.2f, bounds.Y + bounds.Height * 0.55f);
            var p2 = new Vector2(bounds.X + bounds.Width * 0.42f, bounds.Y + bounds.Height * 0.78f);
            var p3 = new Vector2(bounds.X + bounds.Width * 0.82f, bounds.Y + bounds.Height * 0.25f);
            BasicShapes.DrawLine(2f, color, p1, p2);
            BasicShapes.DrawLine(2f, color, p2, p3);
        }

        private static void DrawArrow(Rectangle bounds, Color color)
        {
            int cx = bounds.Center.X;
            int cy = bounds.Center.Y;
            for (int i = 0; i < 4; i++)
            {
                BasicShapes.FillRectangle(new Rectangle(cx - 2 + i, cy - 3 + i, 1, 7 - 2 * i), color);
            }
        }

        private static void DrawScrollMarker(Rectangle bounds, bool up)
        {
            BasicShapes.FillRectangle(bounds, separator);
            int cx = bounds.Center.X;
            int cy = bounds.Center.Y;
            for (int i = 0; i < 4; i++)
            {
                int w = up ? 2 * i + 1 : 7 - 2 * i;
                BasicShapes.FillRectangle(new Rectangle(cx - w / 2, cy - 2 + i, w, 1), text);
            }
        }
        #endregion
    }
}
