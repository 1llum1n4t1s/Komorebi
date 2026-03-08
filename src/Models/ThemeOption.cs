using System.Collections.Generic;

namespace Komorebi.Models
{
    public class ThemeOption
    {
        public const string DefaultKey = "Default";
        public const string DarkKey = "Dark";
        public const string LightKey = "Light";
        public const string ActiproLightKey = "ActiproLight";
        public const string ActiproDarkKey = "ActiproDark";

        public string Name { get; set; }
        public string Key { get; set; }

        public static readonly List<ThemeOption> Supported = new List<ThemeOption>()
        {
            new ThemeOption("Default", DefaultKey),
            new ThemeOption("Dark", DarkKey),
            new ThemeOption("Light", LightKey),
            new ThemeOption("Actipro Avalonia UI (Light)", ActiproLightKey),
            new ThemeOption("Actipro Avalonia UI (Dark)", ActiproDarkKey),
        };

        public ThemeOption(string name, string key)
        {
            Name = name;
            Key = key;
        }
    }
}
