using System.Collections.Generic;

namespace Komorebi.Models
{
    public class ThemeOption
    {
        public string Name { get; set; }
        public string Key { get; set; }

        public static readonly List<ThemeOption> Supported = new List<ThemeOption>()
        {
            new ThemeOption("Default", "Default"),
            new ThemeOption("Dark", "Dark"),
            new ThemeOption("Light", "Light"),
            new ThemeOption("Actipro Avalonia UI (Light)", "ActiproLight"),
            new ThemeOption("Actipro Avalonia UI (Dark)", "ActiproDark"),
        };

        public ThemeOption(string name, string key)
        {
            Name = name;
            Key = key;
        }
    }
}
