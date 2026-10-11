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
using Avalonia.Interactivity;
using Avalonia.Layout;
using Avalonia.Media;
using LibAE;
using LibAE.Formats;
using Orts.Formats.OR;

namespace AEWizard
{
    [Flags]
    public enum WizardButton
    {
        Back           = 0x00000001,
        Next           = 0x00000002,
        Finish         = 0x00000004,
        DisabledFinish = 0x00000008,
    }

    /// <summary>
    /// Multi-page wizard dialog. Closes with <c>true</c> when finished.
    /// </summary>
    public class WizardForm : Window
	{
        ActivityInfo activityInfo;
        RouteInfo routeInfo;

        public const string NextPage = "";
        public const string NoPageChange = null;

        private List<SinglePage> m_pages = new List<SinglePage>();
        private int m_selectedIndex = -1;

        protected Button m_backButton;
        protected Button m_nextButton;
        protected Button m_cancelButton;
        protected Button m_finishButton;
        readonly ContentControl m_pageHost = new ContentControl();

        ActivityDescr wiz1 = null;
        TrainInfo wiz2 = null;
        SelectRoute wiz3 = null;

        public WizardForm(ActivityInfo activity)
		{
			InitializeComponent();
            activityInfo = activity;
            wiz1 = new ActivityDescr();
            wiz1.activityInfo = activity;
            wiz1.completePage();
            wiz2 = new TrainInfo();
            wiz2.activityInfo = activity;
            wiz2.completePage();
            AddPage(wiz1);
            AddPage(wiz2);
		}

        public WizardForm(RouteInfo info)
        {
            InitializeComponent();
            routeInfo = info;
            wiz3 = new SelectRoute();
            wiz3.routeInfo = info;
            wiz3.completePage();
            AddPage(wiz3);
        }

		private void InitializeComponent()
		{
            Title = "Wizard";
            CanResize = false;
            ShowInTaskbar = false;
            SizeToContent = SizeToContent.WidthAndHeight;
            WindowStartupLocation = WindowStartupLocation.CenterOwner;

            m_backButton = new Button { Content = "< _Back", Width = 75 };
            m_backButton.Click += OnClickBack;
            m_nextButton = new Button { Content = "_Next >", Width = 75, IsDefault = true };
            m_nextButton.Click += OnClickNext;
            m_cancelButton = new Button { Content = "Cancel", Width = 75, IsCancel = true };
            m_cancelButton.Click += OnClickCancel;
            m_finishButton = new Button { Content = "_Finish", Width = 75, IsVisible = false };
            m_finishButton.Click += OnClickFinish;
            foreach (var button in new[] { m_backButton, m_nextButton, m_cancelButton, m_finishButton })
                button.HorizontalContentAlignment = HorizontalAlignment.Center;

            var buttons = new StackPanel
            {
                Orientation = Orientation.Horizontal,
                HorizontalAlignment = HorizontalAlignment.Right,
                Spacing = 6,
                Margin = new Thickness(10),
                Children = { m_backButton, m_nextButton, m_finishButton, m_cancelButton },
            };
            var separator = new Border { Height = 1, Background = Brushes.Gray };
            DockPanel.SetDock(buttons, Dock.Bottom);
            DockPanel.SetDock(separator, Dock.Bottom);
            Content = new DockPanel { Children = { buttons, separator, m_pageHost } };

            Opened += (s, e) =>
            {
                if (m_pages.Count > 0)
                    ActivatePage(0);
            };
        }

        void AddPage(SinglePage page)
        {
            page.Wizard = this;
            m_pages.Add(page);
            if (m_selectedIndex == -1)
                m_selectedIndex = 0;
        }

        private void ActivatePage( int newIndex )
        {
            if( newIndex < 0 || newIndex >= m_pages.Count )
                throw new ArgumentOutOfRangeException();

            SinglePage currentPage = null;
            if( m_selectedIndex != -1 && m_pageHost.Content != null )
            {
                currentPage = m_pages[m_selectedIndex];
                if( !currentPage.OnKillActive() )
                    return;
            }

            SinglePage newPage = m_pages[ newIndex ];
            if( !newPage.OnSetActive() )
                return;

            m_selectedIndex = newIndex;
            m_pageHost.Content = newPage;
            newPage.Focus();
        }

        private void OnClickBack( object sender, RoutedEventArgs e )
        {
            if( m_selectedIndex != -1 )
            {
                string pageName = m_pages[ m_selectedIndex ].OnWizardBack();
                switch( pageName )
                {
                    case NoPageChange:
                        break;

                    case NextPage:
                        if( m_selectedIndex - 1 >= 0 )
                            ActivatePage( m_selectedIndex - 1 );
                        break;

                    default:
                        foreach( SinglePage page in m_pages )
                        {
                            if( page.Name == pageName )
                                ActivatePage( m_pages.IndexOf( page ) );
                        }
                        break;
                }
            }
        }

        private void OnClickCancel( object sender, RoutedEventArgs e )
        {
            Close(false);
        }

        private void OnClickFinish( object sender, RoutedEventArgs e )
        {
            if( m_selectedIndex != -1 )
            {
                SinglePage page = m_pages[ m_selectedIndex ];
                if( page.OnWizardFinish() )
                {
                    if( page.OnKillActive() )
                        Close(true);
                }
            }
        }

        private void OnClickNext( object sender, RoutedEventArgs e )
        {
            if( m_selectedIndex != -1 )
            {
                string pageName = m_pages[ m_selectedIndex ].OnWizardNext();
                switch( pageName )
                {
                    case NoPageChange:
                        break;

                    case NextPage:
                        if( m_selectedIndex + 1 < m_pages.Count )
                            ActivatePage( m_selectedIndex + 1 );
                        break;

                    default:
                        foreach( SinglePage page in m_pages )
                        {
                            if( page.Name == pageName )
                                ActivatePage( m_pages.IndexOf( page ) );
                        }
                        break;
                }
            }
        }

        public bool Validate()
        {
            return true;
        }

        public void SetFinishText( string text )
        {
            m_finishButton.Content = text;
        }

        public void SetWizardButtons( WizardButton flags )
        {
            m_backButton.IsEnabled =
                (flags & WizardButton.Back) == WizardButton.Back;
            m_nextButton.IsEnabled =
                (flags & WizardButton.Next) == WizardButton.Next;
            m_nextButton.IsVisible =
                (flags & WizardButton.Finish) == 0 &&
                (flags & WizardButton.DisabledFinish) == 0;
            m_finishButton.IsEnabled =
                (flags & WizardButton.DisabledFinish) == 0;
            m_finishButton.IsVisible =
                (flags & WizardButton.Finish) == WizardButton.Finish ||
                (flags & WizardButton.DisabledFinish) == WizardButton.DisabledFinish;

            m_finishButton.IsDefault = m_finishButton.IsVisible;
            m_nextButton.IsDefault = !m_finishButton.IsVisible;
        }
    }
}
