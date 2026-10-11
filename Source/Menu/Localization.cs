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

using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using Avalonia.Markup.Xaml;
using GNU.Gettext;

namespace Menu
{
    /// <summary>
    /// Access to the GNU Gettext catalog for the Menu, replacing GNU.Gettext.WinForms' <c>Localizer.Localize()</c>.
    /// </summary>
    public static class Localization
    {
        public static readonly GettextResourceManager Catalog = new GettextResourceManager("Menu");

        static Dictionary<string, string> TrimmedTranslations;
        static CultureInfo TrimmedTranslationsCulture;

        /// <summary>
        /// Translates a string. Many of the existing translations were created from WinForms designer texts which had
        /// trailing spaces (used for layout); those are still found when given the trimmed text.
        /// </summary>
        public static string GetString(string text)
        {
            if (String.IsNullOrEmpty(text))
                return text;

            var translated = Catalog.GetString(text);
            if (translated != text)
                return translated;

            // The WinForms designer used Windows line endings.
            if (text.Contains('\n') && !text.Contains("\r\n"))
            {
                var windowsText = text.Replace("\n", "\r\n");
                translated = Catalog.GetString(windowsText);
                if (translated != windowsText)
                    return translated.Replace("\r\n", "\n");
            }

            var map = GetTrimmedTranslations();
            return map.TryGetValue(text.Trim(), out var trimmed) ? trimmed : text;
        }

        public static string GetStringFmt(string format, params object[] args)
        {
            return String.Format(GetString(format), args);
        }

        static Dictionary<string, string> GetTrimmedTranslations()
        {
            var culture = CultureInfo.CurrentUICulture;
            if (TrimmedTranslations != null && Equals(TrimmedTranslationsCulture, culture))
                return TrimmedTranslations;

            var map = new Dictionary<string, string>();
            try
            {
                // Walk from the specific culture to the neutral one, the first translation found wins.
                for (var c = culture; !c.Equals(CultureInfo.InvariantCulture); c = c.Parent)
                {
                    var set = Catalog.GetResourceSet(c, true, false);
                    if (set == null)
                        continue;
                    foreach (DictionaryEntry entry in set)
                    {
                        if (entry.Key is string key && entry.Value is string value && value.Length > 0)
                        {
                            var trimmedKey = key.Trim();
                            if (!map.ContainsKey(trimmedKey))
                                map[trimmedKey] = value.Trim();
                        }
                    }
                }
            }
            catch
            {
                // No translations available; the text stays in English.
            }
            TrimmedTranslations = map;
            TrimmedTranslationsCulture = culture;
            return map;
        }
    }

    /// <summary>
    /// XAML markup extension which translates a string with the Menu catalog: <c>Text="{l:Tr 'Route:'}"</c>.
    /// </summary>
    public class TrExtension : MarkupExtension
    {
        public TrExtension()
        {
        }

        public TrExtension(string text)
        {
            Text = text;
        }

        public string Text { get; set; }

        public override object ProvideValue(IServiceProvider serviceProvider)
        {
            return Localization.GetString(Text);
        }
    }
}
