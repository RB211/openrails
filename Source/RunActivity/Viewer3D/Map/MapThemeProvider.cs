// COPYRIGHT 2023 by the Open Rails project.
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

using System.Collections.Generic;
using System.Linq;
using Avalonia.Media;

namespace Orts.Viewer3D.Map
{
    public class MapThemeProvider
    {
        public void InitializeThemes()
        {
            var LightTheme = new ThemeStyle
            {
                IsDark = false,
                BackColor = Colors.Transparent,
                ForeColor = Colors.Black,
                PanelBackColor = Color.FromRgb(240, 240, 240),
                MapCanvasColor = Colors.White,
                TrackColor = Color.FromRgb(46, 64, 83),
            };

            var DarkTheme = new ThemeStyle
            {
                IsDark = true,
                BackColor = Color.FromRgb(44, 62, 80),
                ForeColor = Color.FromRgb(247, 249, 249),
                PanelBackColor = Color.FromRgb(28, 40, 51),
                MapCanvasColor = Color.FromRgb(44, 62, 80),
                TrackColor = Color.FromRgb(234, 236, 238),
            };

            // Reference for "solarized" themes: https://github.com/altercation/solarized?tab=readme-ov-file#the-values
            var LightSolarizedTheme = new ThemeStyle
            {
                IsDark = false,
                BackColor = Color.FromRgb(253, 246, 227),
                ForeColor = Color.FromRgb(101, 123, 131),
                PanelBackColor = Color.FromRgb(238, 232, 213),
                MapCanvasColor = Color.FromRgb(253, 246, 227),
                TrackColor = Color.FromRgb(88, 110, 117),
            };

            var DarkSolarizedTheme = new ThemeStyle
            {
                IsDark = true,
                BackColor = Color.FromRgb(0, 43, 54),
                ForeColor = Color.FromRgb(131, 148, 150),
                PanelBackColor = Color.FromRgb(28, 40, 51),
                MapCanvasColor = Color.FromRgb(0, 43, 54),
                TrackColor = Color.FromRgb(147, 161, 161),
            };

            Themes.Add("light", LightTheme);
            Themes.Add("light-solarized", LightSolarizedTheme);
            Themes.Add("dark-solarized", DarkSolarizedTheme);
            Themes.Add("dark", DarkTheme);
        }

        private readonly Dictionary<string, ThemeStyle> Themes = new Dictionary<string, ThemeStyle>();

        public ThemeStyle GetTheme(string themeName)
        {
            if (themeName != null && Themes.TryGetValue(themeName, out var theme))
            {
                return theme;
            }

            // Handle the case when the theme doesn't exist
            return null;
        }

        public string[] GetThemes()
        {
            return Themes.Keys.ToArray();
        }
    }

    public class ThemeStyle
    {
        /// <summary>Use the dark variant of the Avalonia theme for the controls</summary>
        public bool IsDark;
        public Color BackColor;
        public Color ForeColor;
        public Color PanelBackColor;
        public Color MapCanvasColor;
        public Color TrackColor;
    }
}
