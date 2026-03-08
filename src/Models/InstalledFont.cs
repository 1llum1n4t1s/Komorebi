using System;
using System.Collections.Generic;

using Avalonia.Media;

namespace Komorebi.Models
{
    public static class InstalledFont
    {
        /// <summary>
        /// All font family names (system + bundled).
        /// </summary>
        public static List<string> All { get; }

        /// <summary>
        /// Monospace-only font family names (system + bundled).
        /// </summary>
        public static List<string> Monospace { get; }

        /// <summary>
        /// Bundled font families included in src/Resources/Fonts/.
        /// </summary>
        private static readonly HashSet<string> s_bundledAll = new(StringComparer.Ordinal)
        {
            "IBM Plex Sans JP",
            "JetBrains Mono",
            "M PLUS 2",
            "Moralerspace Neon JPDOC",
            "Murecho",
            "Noto Sans JP",
            "PlemolJP",
            "UDEV Gothic JPDOC",
            "Zen Kaku Gothic New",
        };

        /// <summary>
        /// Bundled monospace font families.
        /// </summary>
        private static readonly HashSet<string> s_bundledMono = new(StringComparer.Ordinal)
        {
            "JetBrains Mono",
            "PlemolJP",
            "UDEV Gothic JPDOC",
        };

        static InstalledFont()
        {
            var allNames = new SortedSet<string>(StringComparer.OrdinalIgnoreCase);
            var monoNames = new SortedSet<string>(StringComparer.OrdinalIgnoreCase);

            foreach (var font in FontManager.Current.SystemFonts)
            {
                allNames.Add(font.Name);

                if (FontManager.Current.TryGetGlyphTypeface(
                        new Typeface(font), out var glyph) && glyph.Metrics.IsFixedPitch)
                    monoNames.Add(font.Name);
            }

            foreach (var name in s_bundledAll)
                allNames.Add(name);

            foreach (var name in s_bundledMono)
                monoNames.Add(name);

            All = new List<string>(allNames);
            Monospace = new List<string>(monoNames);
        }
    }
}
