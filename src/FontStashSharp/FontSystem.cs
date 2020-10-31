using FontStashSharp.Interfaces;
using System;
using System.Collections.Generic;

#if MONOGAME || FNA
using Microsoft.Xna.Framework;
#elif STRIDE
using Stride.Core.Mathematics;
#else
using System.Drawing;
#endif

namespace FontStashSharp
{
	public class FontSystem : IDisposable
	{
		public bool EnableRichText { get; set; } = true;
		public bool HasBoldVariant
        {
            get
            {
                return _boldSource != null;
            }
        }
        public bool HasItalicVariant
        {
            get
            {
                return _italicSource != null;
            }
        }
        public bool HasBoldItalicVariant
        {
            get
            {
                return _boldItalicSource != null;
            }
        }

		readonly List<IFontSource> _fontSources = new List<IFontSource>();
		readonly Int32Map<DynamicSpriteFont> _fonts = new Int32Map<DynamicSpriteFont>();

		readonly IFontLoader _fontLoader;
		readonly ITexture2DCreator _textureCreator;

		IFontSource _boldSource;
		IFontSource _italicSource;
		IFontSource _boldItalicSource;
		FontAtlas _currentAtlas;
		Point _size;

		public readonly int BlurAmount;
		public readonly int StrokeAmount;

		public bool UseKernings = true;
		public int? DefaultCharacter = ' ';

		public int CharacterSpacing = 0;
		public int LineSpacing = 0;

		public FontAtlas CurrentAtlas
		{
			get
			{
				if (_currentAtlas == null)
				{
					_currentAtlas = new FontAtlas(_size.X, _size.Y, 256);
					Atlases.Add(_currentAtlas);
				}

				return _currentAtlas;
			}
		}

		public List<FontAtlas> Atlases { get; } = new List<FontAtlas>();

		public event EventHandler CurrentAtlasFull;

		public FontSystem(IFontLoader fontLoader, ITexture2DCreator textureCreator, int width, int height, int blurAmount = 0, int strokeAmount = 0)
		{
			if (fontLoader == null)
			{
				throw new ArgumentNullException(nameof(fontLoader));
			}

			if (textureCreator == null)
			{
				throw new ArgumentNullException(nameof(textureCreator));
			}

			_fontLoader = fontLoader;
			_textureCreator = textureCreator;

			if (width <= 0)
			{
				throw new ArgumentOutOfRangeException(nameof(width));
			}

			if (height <= 0)
			{
				throw new ArgumentOutOfRangeException(nameof(height));
			}

			if (blurAmount < 0 || blurAmount > 20)
			{
				throw new ArgumentOutOfRangeException(nameof(blurAmount));
			}

			if (strokeAmount < 0 || strokeAmount > 20)
			{
				throw new ArgumentOutOfRangeException(nameof(strokeAmount));
			}

			if (strokeAmount != 0 && blurAmount != 0)
			{
				throw new ArgumentException("Cannot have both blur and stroke.");
			}

			BlurAmount = blurAmount;
			StrokeAmount = strokeAmount;

			_size = new Point(width, height);
		}

		public void Dispose()
		{
			if (_fontSources != null)
			{
				foreach (var font in _fontSources)
					font.Dispose();
				_fontSources.Clear();
			}

			Atlases?.Clear();
			_currentAtlas = null;
			_fonts.Clear();
		}

		public void AddFont(byte[] data, bool bold, bool italic)
        {
            var fontSource = _fontLoader.Load(data);
            _fontSources.Add(fontSource);

            if (bold && italic)
            {
                _boldItalicSource = fontSource;
            }
            else if (bold)
            {
                _boldSource = fontSource;
            }
            else if (italic)
            {
                _italicSource = fontSource;
            }
		}

		public DynamicSpriteFont GetFont(int fontSize)
		{
			DynamicSpriteFont result;
			if (_fonts.TryGetValue(fontSize, out result))
			{
				return result;
			}

			result = new DynamicSpriteFont(this, fontSize);
			_fonts[fontSize] = result;
			return result;
		}

		public void Reset(int width, int height)
		{
			Atlases.Clear();
			_fonts.Clear();

			if (width == _size.X && height == _size.Y)
				return;

			_size = new Point(width, height);
		}

		public void Reset()
		{
			Reset(_size.X, _size.Y);
		}

		internal int? GetCodepointIndex(int codepoint, RichTextState rtState, out IFontSource font)
		{
			font = null;

            var g = default(int?);

            if (EnableRichText)
            {
                if (rtState.Bold && rtState.Italic)
                {
                    // Target bold-italic.
                    if (_boldItalicSource != null)
                    {
                        g = _boldItalicSource.GetGlyphId(codepoint);
                        if (g != null)
                        {
                            font = _boldItalicSource;
                            return g;
                        }
                    }
                    // Note that if bold-italic was requested, but there is no source
                    // or the source does not have the requested character, control
                    // will fall down into italic, and later possibly bold. So requesting a bold-italic
                    // character will result in just italic if bold-italic could not be found.
                }
                if (rtState.Italic)
                {
                    // Target italic.
                    if (_italicSource != null)
                    {
                        g = _italicSource.GetGlyphId(codepoint);
                        if (g != null)
                        {
                            font = _italicSource;
                            return g;
                        }
                    }
                }
                if (rtState.Bold)
                {
                    // Target bold.
                    if (_boldSource != null)
                    {
                        g = _boldSource.GetGlyphId(codepoint);
                        if (g != null)
                        {
                            font = _boldSource;
                            return g;
                        }
                    }
                }
			}

            foreach (var f in _fontSources)
			{
				g = f.GetGlyphId(codepoint);
				if (g != null)
				{
					font = f;
					break;
				}
			}

			return g;
		}

		internal void RenderGlyphOnAtlas(FontGlyph glyph)
		{
			var currentAtlas = CurrentAtlas;
			int gx = 0, gy = 0;
			var gw = glyph.Bounds.Width;
			var gh = glyph.Bounds.Height;
			if (!currentAtlas.AddRect(gw, gh, ref gx, ref gy))
			{
				CurrentAtlasFull?.Invoke(this, EventArgs.Empty);

				// This code will force creation of new atlas
				_currentAtlas = null;
				currentAtlas = CurrentAtlas;

				// Try to add again
				if (!currentAtlas.AddRect(gw, gh, ref gx, ref gy))
				{
					throw new Exception(string.Format("Could not add rect to the newly created atlas. gw={0}, gh={1}", gw, gh));
				}
			}

			glyph.Bounds.X = gx;
			glyph.Bounds.Y = gy;

			currentAtlas.RenderGlyph(_textureCreator, glyph, BlurAmount, StrokeAmount);

			glyph.Atlas = currentAtlas;
		}
	}
}
