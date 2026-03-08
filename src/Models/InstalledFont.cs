using System.Collections.Generic;
using System.Linq;

using Avalonia.Media;

namespace Komorebi.Models
{
    public static class InstalledFont
    {
        /// <summary>
        /// All installed font family names (with empty string at index 0 for "default").
        /// </summary>
        public static List<string> All { get; }

        /// <summary>
        /// Monospace-only font family names (with empty string at index 0 for "default").
        /// </summary>
        public static List<string> Monospace { get; }

        static InstalledFont()
        {
            var all = new List<string> { string.Empty };
            var mono = new List<string> { string.Empty };

            foreach (var font in FontManager.Current.SystemFonts.OrderBy(f => f.Name))
            {
                all.Add(font.Name);

                if (FontManager.Current.TryGetGlyphTypeface(
                        new Typeface(font), out var glyph) && glyph.Metrics.IsFixedPitch)
                    mono.Add(font.Name);
            }

            All = all;
            Monospace = mono;
        }
    }
}
