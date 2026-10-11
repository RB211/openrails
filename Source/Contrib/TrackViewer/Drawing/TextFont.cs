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
// Cross-platform replacement of Orts.Viewer3D.Popups.WindowTextFont (which uses GDI via System.Drawing and
// P/Invoke into gdi32). Glyphs are rasterised with SkiaSharp into texture atlas pages on demand.

using System;
using System.Collections.Generic;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using SkiaSharp;

namespace ORTS.TrackViewer.Drawing
{
    /// <summary>Horizontal alignment of text</summary>
    public enum LabelAlignment
    {
        /// <summary>Align left</summary>
        Left,
        /// <summary>Center</summary>
        Center,
        /// <summary>Align right</summary>
        Right,
    }

    /// <summary>
    /// A font that can draw text with a SpriteBatch. Must only be used from the game thread.
    /// </summary>
    public sealed class TextFont : IDisposable
    {
        private const int PageSize = 512;
        private static readonly string[] preferredFamilies = { "Segoe UI", "Noto Sans", "DejaVu Sans", "Liberation Sans", "Cantarell", "Arial", "sans-serif" };

        private readonly SKFont font;
        private readonly float ascent;
        private readonly int padding = 1;

        private struct Glyph
        {
            public int Page;
            public Rectangle Source;
            public float Advance;
            public int OffsetX;
            public int OffsetY;
        }

        private sealed class Page
        {
            public byte[] Pixels = new byte[PageSize * PageSize * 4];
            public Texture2D Texture;
            public bool Dirty = true;
            public int CursorX;
            public int CursorY;
            public int RowHeight;
        }

        private readonly Dictionary<char, Glyph> glyphs = new Dictionary<char, Glyph>();
        private readonly List<Page> pages = new List<Page>();

        /// <summary>Line height of the font in pixels</summary>
        public int Height { get; private set; }

        /// <summary>Size of the font in pixels</summary>
        public float SizeInPixels { get; private set; }

        /// <summary>
        /// Create a font.
        /// </summary>
        /// <param name="sizeInPt">Size in points (at 96 dpi), similar to System.Drawing fonts</param>
        public TextFont(float sizeInPt)
        {
            SizeInPixels = (float)Math.Round(sizeInPt * 96 / 72);
            font = new SKFont(FindTypeface(), SizeInPixels)
            {
                Edging = SKFontEdging.Antialias,
                Subpixel = false,
                Hinting = SKFontHinting.Normal,
            };
            SKFontMetrics metrics = font.Metrics;
            ascent = -metrics.Ascent;
            Height = (int)Math.Ceiling(-metrics.Ascent + metrics.Descent + metrics.Leading);
            if (Height <= 0) Height = (int)SizeInPixels + 2;
        }

        private static SKTypeface FindTypeface()
        {
            foreach (string family in preferredFamilies)
            {
                SKTypeface typeface = SKFontManager.Default.MatchFamily(family);
                if (typeface != null) return typeface;
            }
            return SKTypeface.Default;
        }

        /// <summary>
        /// Measure the width of a (single line) string in pixels
        /// </summary>
        public int MeasureString(string text)
        {
            if (String.IsNullOrEmpty(text)) return 0;
            float x = 0;
            float maxX = 0;
            foreach (char c in text)
            {
                if (c == '\n') { maxX = Math.Max(maxX, x); x = 0; continue; }
                x += GetGlyph(c).Advance;
            }
            return (int)Math.Ceiling(Math.Max(maxX, x));
        }

        /// <summary>
        /// Draw a string.
        /// </summary>
        /// <param name="spriteBatch">The spritebatch (between Begin and End)</param>
        /// <param name="position">Rectangle relative to which the string is aligned. Only X, Y and Width are used</param>
        /// <param name="offset">Offset added to the position (top-left of the text)</param>
        /// <param name="text">Text to draw. May contain newlines</param>
        /// <param name="align">Alignment within the width of the position rectangle</param>
        /// <param name="color">Text color</param>
        public void Draw(SpriteBatch spriteBatch, Rectangle position, Point offset, string text, LabelAlignment align, Color color)
        {
            if (String.IsNullOrEmpty(text)) return;
            int startX = position.X + offset.X;
            int y = position.Y + offset.Y;

            string[] lines = text.Split('\n');
            foreach (string line in lines)
            {
                float x = startX;
                if (align != LabelAlignment.Left)
                {
                    int width = MeasureString(line);
                    x += (align == LabelAlignment.Center) ? (int)((position.Width - width) / 2) : position.Width - width;
                }
                foreach (char c in line)
                {
                    Glyph glyph = GetGlyph(c);
                    if (glyph.Source.Width > 0)
                    {
                        Texture2D texture = GetTexture(spriteBatch.GraphicsDevice, glyph.Page);
                        var destination = new Vector2((float)Math.Round(x) + glyph.OffsetX, y + glyph.OffsetY);
                        spriteBatch.Draw(texture, destination, glyph.Source, color);
                    }
                    x += glyph.Advance;
                }
                y += Height;
            }
        }

        /// <summary>
        /// Draw a string at the given location with left alignment.
        /// </summary>
        public void Draw(SpriteBatch spriteBatch, Point location, string text, Color color)
        {
            Draw(spriteBatch, new Rectangle(), location, text, LabelAlignment.Left, color);
        }

        /// <summary>
        /// Upload pending glyphs. Glyph rendering is done on demand, so this is only for compatibility.
        /// </summary>
        public void Load(GraphicsDevice graphicsDevice)
        {
            for (int i = 0; i < pages.Count; i++) GetTexture(graphicsDevice, i);
        }

        private Texture2D GetTexture(GraphicsDevice graphicsDevice, int pageIndex)
        {
            Page page = pages[pageIndex];
            if (page.Texture == null)
            {
                page.Texture = new Texture2D(graphicsDevice, PageSize, PageSize, false, SurfaceFormat.Color);
                page.Dirty = true;
            }
            if (page.Dirty)
            {
                page.Texture.SetData(page.Pixels);
                page.Dirty = false;
            }
            return page.Texture;
        }

        private Glyph GetGlyph(char c)
        {
            if (glyphs.TryGetValue(c, out Glyph glyph)) return glyph;
            glyph = RenderGlyph(c);
            glyphs[c] = glyph;
            return glyph;
        }

        private Glyph RenderGlyph(char c)
        {
            string s = c.ToString();
            float advance = font.MeasureText(s, out SKRect bounds);
            var glyph = new Glyph { Advance = advance, Page = 0, Source = Rectangle.Empty };
            if (char.IsWhiteSpace(c) || char.IsControl(c) || bounds.Width <= 0 || bounds.Height <= 0)
            {
                return glyph;
            }

            int left = (int)Math.Floor(bounds.Left) - padding;
            int top = (int)Math.Floor(bounds.Top) - padding;
            int width = (int)Math.Ceiling(bounds.Right) - left + padding;
            int height = (int)Math.Ceiling(bounds.Bottom) - top + padding;
            width = Math.Min(width, PageSize);
            height = Math.Min(height, PageSize);

            var info = new SKImageInfo(width, height, SKColorType.Alpha8, SKAlphaType.Premul);
            using (var bitmap = new SKBitmap(info))
            using (var canvas = new SKCanvas(bitmap))
            using (var paint = new SKPaint { Color = SKColors.White, IsAntialias = true })
            {
                canvas.Clear(SKColors.Transparent);
                canvas.DrawText(s, -left, -top, SKTextAlign.Left, font, paint);
                canvas.Flush();

                Page page = FindSpace(width, height, out int pageIndex);
                ReadOnlySpan<byte> alpha = bitmap.GetPixelSpan();
                int rowBytes = bitmap.RowBytes;
                for (int row = 0; row < height; row++)
                {
                    int target = ((page.CursorY + row) * PageSize + page.CursorX) * 4;
                    for (int col = 0; col < width; col++)
                    {
                        byte a = alpha[row * rowBytes + col];
                        // premultiplied white, as expected by SpriteBatch with BlendState.AlphaBlend
                        page.Pixels[target++] = a;
                        page.Pixels[target++] = a;
                        page.Pixels[target++] = a;
                        page.Pixels[target++] = a;
                    }
                }
                page.Dirty = true;

                glyph.Page = pageIndex;
                glyph.Source = new Rectangle(page.CursorX, page.CursorY, width, height);
                glyph.OffsetX = left;
                glyph.OffsetY = (int)Math.Round(ascent) + top;

                page.CursorX += width + 1;
                page.RowHeight = Math.Max(page.RowHeight, height);
            }
            return glyph;
        }

        private Page FindSpace(int width, int height, out int pageIndex)
        {
            if (pages.Count > 0)
            {
                Page current = pages[pages.Count - 1];
                if (current.CursorX + width > PageSize)
                {   // next row
                    current.CursorX = 0;
                    current.CursorY += current.RowHeight + 1;
                    current.RowHeight = 0;
                }
                if (current.CursorY + height <= PageSize)
                {
                    pageIndex = pages.Count - 1;
                    return current;
                }
            }
            var page = new Page();
            pages.Add(page);
            pageIndex = pages.Count - 1;
            return page;
        }

        /// <summary>Release textures</summary>
        public void Dispose()
        {
            foreach (Page page in pages) page.Texture?.Dispose();
            pages.Clear();
            glyphs.Clear();
            font.Dispose();
        }
    }
}
