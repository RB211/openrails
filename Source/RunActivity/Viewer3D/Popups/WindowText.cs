// COPYRIGHT 2011, 2012, 2013 by the Open Rails project.
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

#define WINDOWTEXT_SPRITEBATCH

using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using ORTS.Common;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Runtime.InteropServices;
using SkiaSharp;

namespace Orts.Viewer3D.Popups
{
    /// <summary>
    /// Font style flags; values match Orts.Viewer3D.Popups.FontStyle.
    /// </summary>
    [Flags]
    public enum FontStyle
    {
        Regular = 0,
        Bold = 1,
        Italic = 2,
        Underline = 4,
        Strikeout = 8,
    }

    public sealed class WindowTextManager
    {
        // THREAD SAFETY:
        //   All accesses must be done in local variables. No modifications to the objects are allowed except by
        //   assignment of a new instance (possibly cloned and then modified).
        Dictionary<string, WindowTextFont> Fonts = new Dictionary<string, WindowTextFont>();

        /// <summary>
        /// Provides a <see cref="WindowTextFont"/> for the specified <paramref name="fontFamily"/>,
        /// <paramref name="sizeInPt"/> and <paramref name="style"/>, scaled from points to pixels by
        /// the system-wide DPI scale.
        /// </summary>
        /// <remarks>
        /// All <see cref="WindowTextFont"/> instances are cached by the <see cref="WindowTextManager"/>.
        /// </remarks>
        /// <param name="fontFamily">Font family name (e.g. "Arial")</param>
        /// <param name="sizeInPt">Size of the font, in DPI-aware points (e.g. 9)</param>
        /// <param name="style">Style of the font (e.g. <c>FontStyle.Normal</c>)</param>
        /// <returns>A <see cref="WindowTextFont"/> that can be used to draw text of the given font family,
        /// size and style.</returns>
        public WindowTextFont GetScaled(string fontFamily, float sizeInPt, FontStyle style)
        {
            return GetExact(fontFamily, sizeInPt * DpiScale, style);
        }

        /// <summary>
        /// Provides a <see cref="WindowTextFont"/> for the specified <paramref name="fontFamily"/>,
        /// <paramref name="sizeInPt"/> and <paramref name="style"/>, as if the system was using
        /// 96 DPI (even when it isn't).
        /// </summary>
        /// <remarks>
        /// All <see cref="WindowTextFont"/> instances are cached by the <see cref="WindowTextManager"/>.
        /// </remarks>
        /// <param name="fontFamily">Font family name (e.g. "Arial")</param>
        /// <param name="sizeInPt">Size of the font, in 96 DPI points (e.g. 9)</param>
        /// <param name="style">Style of the font (e.g. <c>FontStyle.Normal</c>)</param>
        /// <returns>A <see cref="WindowTextFont"/> that can be used to draw text of the given font family,
        /// size and style.</returns>
        public WindowTextFont GetExact(string fontFamily, float sizeInPt, FontStyle style)
        {
            return GetExact(fontFamily, sizeInPt, style, 0);
        }

        /// <summary>
        /// Provides a <see cref="WindowTextFont"/> for the specified <paramref name="fontFamily"/>,
        /// <paramref name="sizeInPt"/> and <paramref name="style"/> with the specified <paramref name="outlineSize"/>,
        /// scaled from points to pixels by the system-wide DPI scale.
        /// </summary>
        /// <remarks>
        /// All <see cref="WindowTextFont"/> instances are cached by the <see cref="WindowTextManager"/>.
        /// </remarks>
        /// <param name="fontFamily">Font family name (e.g. "Arial")</param>
        /// <param name="sizeInPt">Size of the font, in DPI-aware points (e.g. 9)</param>
        /// <param name="style">Style of the font (e.g. <c>FontStyle.Normal</c>)</param>
        /// <param name="outlineSize">Size of the outline, in pixels (e.g. 2)</param>
        /// <returns>A <see cref="WindowTextFont"/> that can be used to draw text of the given font family,
        /// size and style with the given outline size.</returns>
        public WindowTextFont GetScaled(string fontFamily, float sizeInPt, FontStyle style, int outlineSize)
        {
            return GetExact(fontFamily, sizeInPt * DpiScale, style, outlineSize);
        }

        /// <summary>
        /// Provides a <see cref="WindowTextFont"/> for the specified <paramref name="fontFamily"/>,
        /// <paramref name="sizeInPt"/> and <paramref name="style"/> with the specified <paramref name="outlineSize"/>,
        /// as if the system was using 96 DPI (even when it isn't).
        /// </summary>
        /// <remarks>
        /// All <see cref="WindowTextFont"/> instances are cached by the <see cref="WindowTextManager"/>.
        /// </remarks>
        /// <param name="fontFamily">Font family name (e.g. "Arial")</param>
        /// <param name="sizeInPt">Size of the font, in 96 DPI points (e.g. 9)</param>
        /// <param name="style">Style of the font (e.g. <c>FontStyle.Normal</c>)</param>
        /// <param name="outlineSize">Size of the outline, in pixels (e.g. 2)</param>
        /// <returns>A <see cref="WindowTextFont"/> that can be used to draw text of the given font family,
        /// size and style with the given outline size.</returns>
        public WindowTextFont GetExact(string fontFamily, float sizeInPt, FontStyle style, int outlineSize)
        {
            var fonts = Fonts;
            var key = String.Format("{0}:{1:F}:{2}:{3}", fontFamily, sizeInPt, style, outlineSize);
            if (!fonts.ContainsKey(key))
            {
                fonts = new Dictionary<string, WindowTextFont>(fonts);
                fonts.Add(key, new WindowTextFont(fontFamily, sizeInPt, style, outlineSize));
                Fonts = fonts;
            }
            return fonts[key];
        }

        /// <summary>
        /// System DPI relative to 96 DPI. OPENRAILS_DPI_SCALE overrides it (useful on Wayland, where the
        /// compositor's scale factor is not reported as a DPI).
        /// </summary>
        public static readonly float DpiScale = GetDpiScale();

        static float GetDpiScale()
        {
            if (float.TryParse(Environment.GetEnvironmentVariable("OPENRAILS_DPI_SCALE"), System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out var scale) && scale > 0)
                return scale;
            try
            {
                if (OperatingSystem.IsWindows())
                    return NativeMethods.GetDpiForSystem() / 96f;
                if (NativeMethods.SDL_GetDisplayDPI(0, out _, out _, out var verticalDpi) == 0 && verticalDpi > 0)
                    // X11 reports the physical DPI, which is noisy; round to the nearest quarter step like desktop scaling does.
                    return Math.Max(1, (float)Math.Round(verticalDpi / 96 * 4) / 4);
            }
            catch (Exception error) when (error is DllNotFoundException || error is EntryPointNotFoundException)
            {
            }
            return 1;
        }

        [CallOnThread("Loader")]
        public void Load(GraphicsDevice graphicsDevice)
        {
            var fonts = Fonts;
            foreach (var font in fonts.Values)
                font.Load(graphicsDevice);
        }
    }

    public sealed class WindowTextFont
    {
        readonly SKFont Font;
        readonly int FontHeight;
        readonly int OutlineSize;

        // THREAD SAFETY:
        //   All accesses must be done in local variables. No modifications to the objects are allowed except by
        //   assignment of a new instance (possibly cloned and then modified).
        CharacterGroup Characters;

        internal WindowTextFont(string fontFamily, float sizeInPt, FontStyle style, int outlineSize)
        {
            Font = new SKFont(ResolveTypeface(fontFamily, style), (int)Math.Round(sizeInPt * 96 / 72))
            {
                Edging = SKFontEdging.Antialias,
                Hinting = SKFontHinting.Normal,
                Subpixel = false,
            };
            FontHeight = (int)Math.Ceiling(Font.Spacing);
            OutlineSize = outlineSize;
            Characters = new CharacterGroup(Font, OutlineSize);
            if (Viewer3D.Viewer.Catalog != null)
            EnsureCharacterData(Viewer3D.Viewer.Catalog.GetString("ABCDEFGHIJKLMNOPQRSTUVWXYZabcdefghijklmnopqrstuvwxyz1234567890 \",.-+|!$%&/()=?;:'_[]"));
        }

        /// <summary>
        /// Windows font names used by Open Rails, with metric-compatible or similar alternatives found on other systems.
        /// </summary>
        static readonly Dictionary<string, string[]> FontSubstitutes = new Dictionary<string, string[]>(StringComparer.OrdinalIgnoreCase)
        {
            { "Arial", new[] { "Liberation Sans", "Arimo", "Helvetica", "DejaVu Sans", "Noto Sans" } },
            { "Consolas", new[] { "Liberation Mono", "Cousine", "DejaVu Sans Mono", "Noto Sans Mono", "Menlo" } },
            { "Courier New", new[] { "Liberation Mono", "Cousine", "DejaVu Sans Mono" } },
            { "Segoe UI", new[] { "Noto Sans", "Cantarell", "DejaVu Sans", "Liberation Sans" } },
        };

        static SKTypeface ResolveTypeface(string fontFamily, FontStyle style)
        {
            var skStyle = new SKFontStyle((style & FontStyle.Bold) != 0 ? SKFontStyleWeight.Bold : SKFontStyleWeight.Normal, SKFontStyleWidth.Normal, (style & FontStyle.Italic) != 0 ? SKFontStyleSlant.Italic : SKFontStyleSlant.Upright);
            var candidates = new[] { fontFamily }.Concat(FontSubstitutes.TryGetValue(fontFamily, out var substitutes) ? substitutes : Array.Empty<string>());
            foreach (var candidate in candidates)
            {
                var typeface = SKFontManager.Default.MatchFamily(candidate, skStyle);
                if (typeface != null)
                    return typeface;
            }
            return SKTypeface.FromFamilyName(null, skStyle) ?? SKTypeface.Default;
        }

        /// <summary>
        /// Gets the line height of the font.
        /// </summary>
        public int Height
        {
            get
            {
                return FontHeight;
            }
        }

        /// <summary>
        /// Measures the width of a given string.
        /// </summary>
        /// <param name="text">Text to measure</param>
        /// <returns>The length of the text in pixels.</returns>
        public int MeasureString(string text)
        {
            EnsureCharacterData(text);
            var characters = Characters;

            var chIndexes = new int[text.Length];
            for (var i = 0; i < text.Length; i++)
                chIndexes[i] = characters.IndexOfCharacter(text[i]);

            var x = 0f;
            for (var i = 0; i < text.Length; i++)
            {
                x += characters.AbcWidths[chIndexes[i]].X;
                x += characters.AbcWidths[chIndexes[i]].Y;
                x += characters.AbcWidths[chIndexes[i]].Z;
            }
            return (int)x;
        }

#if WINDOWTEXT_SPRITEBATCH
        [CallOnThread("Render")]
        public void Draw(SpriteBatch spriteBatch, Point offset, string text, Color color)
        {
            Draw(spriteBatch, offset, 0, 0, text, LabelAlignment.Left, color, Color.Black);
        }

        [CallOnThread("Render")]
        public void Draw(SpriteBatch spriteBatch, Point offset, string text, Color color, Color outline)
        {
            Draw(spriteBatch, offset, 0, 0, text, LabelAlignment.Left, color, outline);
        }

        [CallOnThread("Render")]
        public void Draw(SpriteBatch spriteBatch, Rectangle position, Point offset, string text, LabelAlignment align, Color color)
        {
            Draw(spriteBatch, position, offset, 0, text, align, color, Color.Black);
        }

        [CallOnThread("Render")]
        public void Draw(SpriteBatch spriteBatch, Rectangle position, Point offset, string text, LabelAlignment align, Color color, float rotation)
        {
            Draw(spriteBatch, position, offset, text, align, color, Color.Black, rotation);
        }

        [CallOnThread("Render")]
        public void Draw(SpriteBatch spriteBatch, Rectangle position, Point offset, float rotation, string text, LabelAlignment align, Color color, Color outline)
        {
            offset.X += position.Location.X;
            offset.Y += position.Location.Y;
            Draw(spriteBatch, offset, rotation, position.Width, text, align, color, outline);
        }

        [CallOnThread("Render")]
        public void Draw(SpriteBatch spriteBatch, Rectangle position, Point offset, string text, LabelAlignment align, Color color, Color outline, float rotation)
        {
            offset.X += position.Location.X;
            offset.Y += position.Location.Y;
            Draw(spriteBatch, offset, rotation, position.Width, text, align, color, outline);
        }

        [CallOnThread("Render")]
        public void Draw(SpriteBatch spriteBatch, Point position, float rotation, int width, string text, LabelAlignment align, Color color, Color outline)
        {
            if (text == null) return;
            EnsureCharacterData(text);
            var characters = Characters;

            var chIndexes = new int[text.Length];
            for (var i = 0; i < text.Length; i++)
                chIndexes[i] = characters.IndexOfCharacter(text[i]);

            var rotationScale = Matrix.CreateRotationZ(rotation) * Matrix.CreateTranslation(position.X - OutlineSize, position.Y - OutlineSize, 0);

            var current = Vector2.Zero;
            if (align != LabelAlignment.Left)
            {
                for (var i = 0; i < text.Length; i++)
                {
                    current.X += characters.AbcWidths[chIndexes[i]].X;
                    current.X += characters.AbcWidths[chIndexes[i]].Y;
                    current.X += characters.AbcWidths[chIndexes[i]].Z;
                }
                if (align == LabelAlignment.Center)
                    current.X = (int)((width - current.X) / 2);
                else
                    current.X = width - current.X;
            }

            var start = current;
            var texture = characters.Texture;
            if (texture == null)
                return;

            if (OutlineSize > 0)
            {
                var outlineOffset = characters.BoxesMaxBottom;
                current = start;
                for (var i = 0; i < text.Length; i++)
                {
                    var box = characters.Boxes[chIndexes[i]];
                    box.Y += outlineOffset;
                    if (text[i] > ' ')
                        spriteBatch.Draw(texture, Vector2.Transform(current, rotationScale), box, outline, rotation, Vector2.Zero, Vector2.One, SpriteEffects.None, 0);
                    current.X += characters.AbcWidths[chIndexes[i]].X;
                    current.X += characters.AbcWidths[chIndexes[i]].Y;
                    current.X += characters.AbcWidths[chIndexes[i]].Z;
                    if (text[i] == '\n')
                    {
                        current.X = start.X;
                        current.Y += Height;
                    }
                }
            }
            current = start;
            for (var i = 0; i < text.Length; i++)
            {
                if (text[i] > ' ')
                    spriteBatch.Draw(texture, Vector2.Transform(current, rotationScale), characters.Boxes[chIndexes[i]], color, rotation, Vector2.Zero, Vector2.One, SpriteEffects.None, 0);
                current.X += characters.AbcWidths[chIndexes[i]].X;
                current.X += characters.AbcWidths[chIndexes[i]].Y;
                current.X += characters.AbcWidths[chIndexes[i]].Z;
                if (text[i] == '\n')
                {
                    current.X = start.X;
                    current.Y += Height;
                }
            }
        }
#else
        [CallOnThread("Updater")]
        public DrawData PrepareFrame(GraphicsDevice graphicsDevice, float width, float height, string text, LabelAlignment align)
        {
            // We'll crash creating 0-byte buffers below and there's nothing to be done with an empty string anyway.
            if (String.IsNullOrEmpty(text))
                return null;

            EnsureCharacterData(text);

            var chIndexes = new int[text.Length];
            for (var i = 0; i < text.Length; i++)
                chIndexes[i] = Characters.IndexOfCharacter(text[i]);

            var x = 0f;
            var y = 0f;
            if (align != LabelAlignment.Left)
            {
                for (var i = 0; i < text.Length; i++)
                {
                    x += Characters.AbcWidths[chIndexes[i]].X;
                    x += Characters.AbcWidths[chIndexes[i]].Y;
                    x += Characters.AbcWidths[chIndexes[i]].Z;
                }
                if (align == LabelAlignment.Center)
                    x = (width - x) / 2;
                else
                    x = width - x;
            }

            var vertexData = new VertexPositionTexture[text.Length * 4];
            var indexData = new short[text.Length * 6];
            for (var i = 0; i < text.Length; i++)
            {
                vertexData[i * 4 + 0] = new VertexPositionTexture(new Vector3(x + 0, y + 0, 0), new Vector2((float)Characters.Boxes[chIndexes[i]].Left / Characters.Texture.Width, (float)Characters.Boxes[chIndexes[i]].Top / Characters.Texture.Height));
                vertexData[i * 4 + 1] = new VertexPositionTexture(new Vector3(x + Characters.Boxes[chIndexes[i]].Width, y + 0, 0), new Vector2((float)Characters.Boxes[chIndexes[i]].Right / Characters.Texture.Width, (float)Characters.Boxes[chIndexes[i]].Top / Characters.Texture.Height));
                vertexData[i * 4 + 2] = new VertexPositionTexture(new Vector3(x + 0, y + Characters.Boxes[chIndexes[i]].Height, 0), new Vector2((float)Characters.Boxes[chIndexes[i]].Left / Characters.Texture.Width, (float)Characters.Boxes[chIndexes[i]].Bottom / Characters.Texture.Height));
                vertexData[i * 4 + 3] = new VertexPositionTexture(new Vector3(x + Characters.Boxes[chIndexes[i]].Width, y + Characters.Boxes[chIndexes[i]].Height, 0), new Vector2((float)Characters.Boxes[chIndexes[i]].Right / Characters.Texture.Width, (float)Characters.Boxes[chIndexes[i]].Bottom / Characters.Texture.Height));
                x += Characters.AbcWidths[chIndexes[i]].X;
                x += Characters.AbcWidths[chIndexes[i]].Y;
                x += Characters.AbcWidths[chIndexes[i]].Z;
                indexData[i * 6 + 0] = (short)(i * 4 + 0);
                indexData[i * 6 + 1] = (short)(i * 4 + 1);
                indexData[i * 6 + 2] = (short)(i * 4 + 2);
                indexData[i * 6 + 3] = (short)(i * 4 + 1);
                indexData[i * 6 + 4] = (short)(i * 4 + 3);
                indexData[i * 6 + 5] = (short)(i * 4 + 2);
            }
            var vertexBuffer = new VertexBuffer(graphicsDevice, typeof(VertexPositionTexture), vertexData.Length, BufferUsage.WriteOnly);
            vertexBuffer.SetData(vertexData);
            var indexBuffer = new IndexBuffer(graphicsDevice, typeof(short), indexData.Length, BufferUsage.WriteOnly);
            indexBuffer.SetData(indexData);
            return new DrawData(Characters, vertexBuffer, vertexData.Length, indexBuffer, text.Length * 2);
        }

        [CallOnThread("Render")]
        public void Draw(GraphicsDevice graphicsDevice, DrawData data)
        {
            if (data != null)
            {
                graphicsDevice.VertexDeclaration = new VertexDeclaration(graphicsDevice, VertexPositionTexture.VertexElements);
                graphicsDevice.Vertices[0].SetSource(data.VertexBuffer, 0, VertexPositionTexture.SizeInBytes);
                graphicsDevice.Indices = data.IndexBuffer;
                graphicsDevice.DrawIndexedPrimitives(PrimitiveType.TriangleList, 0, 0, data.VertexCount, 0, data.PrimitiveCount);
            }
        }

        public bool IsValid(DrawData LastDrawData)
        {
            return (LastDrawData != null) && (LastDrawData.Characters == Characters);
        }
#endif

        [CallOnThread("Loader")]
        public void Load(GraphicsDevice graphicsDevice)
        {
            Characters.Load(graphicsDevice);
        }

        void EnsureCharacterData(string text)
        {
            var characters = Characters;

            foreach (var ch in text)
            {
                if (!characters.ContainsCharacter(ch))
                {
                    Characters = new CharacterGroup(text.ToCharArray(), characters);
                    break;
                }
            }
        }

#if !WINDOWTEXT_SPRITEBATCH
        public sealed class DrawData
        {
            internal readonly CharacterGroup Characters;
            internal readonly VertexBuffer VertexBuffer;
            internal readonly int VertexCount;
            internal readonly IndexBuffer IndexBuffer;
            internal readonly int PrimitiveCount;
            internal DrawData(CharacterGroup characters, VertexBuffer vertexBuffer, int vertexCount, IndexBuffer indexBuffer, int primitiveCount)
            {
                Characters = characters;
                VertexBuffer = vertexBuffer;
                VertexCount = vertexCount;
                IndexBuffer = indexBuffer;
                PrimitiveCount = primitiveCount;
            }
        }
#endif

        sealed internal class CharacterGroup
        {
            const int BoxSpacing = 1;

            readonly SKFont Font;
            readonly int OutlineSize;
            readonly char[] Characters;
            public readonly Rectangle[] Boxes;
            public readonly int BoxesMaxRight;
            public readonly int BoxesMaxBottom;
            public readonly Vector3[] AbcWidths;
            public Texture2D Texture { get; private set; }

            public CharacterGroup(SKFont font, int outlineSize)
            {
                Font = font;
                OutlineSize = outlineSize;
                Characters = new char[0];
                Boxes = new Rectangle[0];
                BoxesMaxRight = 0;
                BoxesMaxBottom = 0;
                AbcWidths = new Vector3[0];
            }

            public CharacterGroup(char[] characters, CharacterGroup mergeGroup)
                : this(characters, mergeGroup.Font, mergeGroup.OutlineSize, mergeGroup.Characters, mergeGroup.Boxes, mergeGroup.AbcWidths)
            {
            }

            CharacterGroup(char[] characters, SKFont mergeFont, int mergeOutlineSize, char[] mergeCharacters, Rectangle[] mergeBoxes, Vector3[] mergeAbcWidths)
            {
                Font = mergeFont;
                OutlineSize = mergeOutlineSize;
                Characters = characters.Union(mergeCharacters).OrderBy(c => c).ToArray();
                Boxes = new Rectangle[Characters.Length];
                AbcWidths = new Vector3[Characters.Length];

                var mergeIndex = 0;
                var spacing = BoxSpacing + OutlineSize;
                var x = spacing;
                var y = spacing;
                var height = (int)Math.Ceiling(Font.Spacing) + 1;
                for (var i = 0; i < Characters.Length; i++)
                {
                    // Copy ABC widths from merge data or calculate ourselves.
                    if ((mergeIndex < mergeCharacters.Length) && (mergeCharacters[mergeIndex] == Characters[i]))
                    {
                        AbcWidths[i] = mergeAbcWidths[mergeIndex];
                        mergeIndex++;
                    }
                    else
                    {
                        AbcWidths[i] = GetAbcWidths(Characters[i]);
                    }
                    Boxes[i] = new Rectangle(x, y, (int)(Math.Max(0, AbcWidths[i].X) + AbcWidths[i].Y + Math.Max(0, AbcWidths[i].Z) + 2 * OutlineSize), height + 2 * OutlineSize);
                    x += Boxes[i].Width + BoxSpacing;
                    if (x >= 256)
                    {
                        x = BoxSpacing;
                        y += Boxes[i].Height + BoxSpacing;
                    }
                }

                // TODO: Copy boxes from the merge data.
                BoxesMaxRight = Boxes.Max(b => b.Right);
                BoxesMaxBottom = Boxes.Max(b => b.Bottom);
            }

            /// <summary>
            /// Equivalent of Win32 GetCharABCWidthsFloat: A is the left side bearing, B the ink width and C the
            /// right side bearing, so A + B + C is the advance. Missing glyphs use whatever fallback the font gives.
            /// </summary>
            Vector3 GetAbcWidths(char character)
            {
                var text = character.ToString();
                var glyphs = Font.GetGlyphs(text);
                var widths = new float[glyphs.Length];
                var bounds = new SKRect[glyphs.Length];
                Font.GetGlyphWidths(glyphs, widths, bounds);
                var advance = (float)Math.Round(widths.Sum());
                if (glyphs.Length == 0 || bounds[0].IsEmpty)
                    return new Vector3(0, advance, 0);
                var a = (float)Math.Floor(bounds[0].Left);
                var b = (float)Math.Ceiling(bounds[0].Right) - a;
                return new Vector3(a, b, advance - a - b);
            }

            public bool ContainsCharacter(char character)
            {
                return Array.BinarySearch(Characters, character) >= 0;
            }

            public int IndexOfCharacter(char character)
            {
                return Array.BinarySearch(Characters, character);
            }

            /// <summary>
            /// Renders all characters as white-on-black into a 32bpp buffer, matching what the GDI version produced.
            /// </summary>
            byte[] GetBitmapData(Rectangle rectangle)
            {
                var info = new SKImageInfo(rectangle.Width, rectangle.Height, SKColorType.Rgba8888, SKAlphaType.Premul);
                var buffer = new byte[info.BytesSize];
                var handle = GCHandle.Alloc(buffer, GCHandleType.Pinned);
                try
                {
                    using (var surface = SKSurface.Create(info, handle.AddrOfPinnedObject(), info.RowBytes))
                    using (var paint = new SKPaint { Color = SKColors.White, IsAntialias = true })
                    {
                        var canvas = surface.Canvas;
                        canvas.Clear(SKColors.Black);
                        var baseline = -Font.Metrics.Ascent;
                        for (var i = 0; i < Characters.Length; i++)
                            canvas.DrawText(Characters[i].ToString(), Boxes[i].X + OutlineSize, Boxes[i].Y + OutlineSize + baseline, SKTextAlign.Left, Font, paint);
                        canvas.Flush();
                    }
                }
                finally
                {
                    handle.Free();
                }
                return buffer;
            }

            [CallOnThread("Loader")]
            public void Load(GraphicsDevice graphicsDevice)
            {
                if (Texture != null || Characters.Length == 0)
                    return;

                var rectangle = new Rectangle(0, 0, BoxesMaxRight, BoxesMaxBottom);
                var buffer = GetBitmapData(rectangle);

                for (var y = 0; y < rectangle.Height; y++)
                {
                    for (var x = 0; x < rectangle.Width; x++)
                    {
                        var offset = y * rectangle.Width * 4 + x * 4;
                        // alpha = (red + green + blue) / 3.
                        buffer[offset + 3] = (byte)((buffer[offset + 0] + buffer[offset + 1] + buffer[offset + 2]) / 3);
                        // red|green|blue = Color.White;
                        buffer[offset + 2] = 255;
                        buffer[offset + 1] = 255;
                        buffer[offset + 0] = 255;
                    }
                }

                if (OutlineSize > 0)
                {
                    var outlineBuffer = new byte[buffer.Length];
                    Array.Copy(buffer, outlineBuffer, buffer.Length);
                    for (var offsetX = -OutlineSize; offsetX <= OutlineSize; offsetX++)
                    {
                        for (var offsetY = -OutlineSize; offsetY <= OutlineSize; offsetY++)
                        {
                            if (Math.Sqrt(offsetX * offsetX + offsetY * offsetY) <= OutlineSize)
                            {
                                for (var x = OutlineSize; x < rectangle.Width - OutlineSize; x++)
                                {
                                    for (var y = OutlineSize; y < rectangle.Height - OutlineSize; y++)
                                    {
                                        var offset = y * rectangle.Width * 4 + x * 4;
                                        var outlineOffset = offset + offsetY * rectangle.Width * 4 + offsetX * 4;
                                        outlineBuffer[outlineOffset + 3] = (byte)Math.Min(255, outlineBuffer[outlineOffset + 3] + buffer[offset + 3]);
                                    }
                                }
                            }
                        }
                    }
                    var combinedBuffer = new byte[buffer.Length * 2];
                    Array.Copy(buffer, 0, combinedBuffer, 0, buffer.Length);
                    Array.Copy(outlineBuffer, 0, combinedBuffer, buffer.Length, buffer.Length);
                    buffer = combinedBuffer;
                    rectangle.Height *= 2;
                }

                var texture = new Texture2D(graphicsDevice, rectangle.Width, rectangle.Height, false, SurfaceFormat.Color); // Color = 32bppRgb
                texture.SetData(buffer);
                Texture = texture;
            }
        }
    }

    static class NativeMethods
    {
        [DllImport("user32.dll")]
        public static extern uint GetDpiForSystem();

        /// <summary>SDL2, exported by MonoGame's native runtime.</summary>
        [DllImport("mgruntime", CallingConvention = CallingConvention.Cdecl)]
        public static extern int SDL_GetDisplayDPI(int displayIndex, out float diagonalDpi, out float horizontalDpi, out float verticalDpi);
    }
}
