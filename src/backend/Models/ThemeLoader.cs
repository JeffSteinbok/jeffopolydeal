using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;

namespace JeffopolyDeal.Models
{
    public class ThemeData
    {
        public string Name { get; set; } = "Jeffopoly";
        public Dictionary<string, string> CategoryNames { get; set; } = new();
        public Dictionary<string, List<string>> Properties { get; set; } = new();
    }

    public static class ThemeLoader
    {
        private static readonly Dictionary<string, ThemeData> _cache = new(StringComparer.OrdinalIgnoreCase);
        private static readonly object _lock = new();

        public static ThemeData Load(string themeName)
        {
            if (string.IsNullOrWhiteSpace(themeName))
                themeName = "jeffopoly";

            lock (_lock)
            {
                if (_cache.TryGetValue(themeName, out var cached))
                    return cached;
            }

            var path = Path.Combine(AppContext.BaseDirectory, "Themes", $"{themeName}.json");
            if (!File.Exists(path))
            {
                // Fall back to jeffopoly
                path = Path.Combine(AppContext.BaseDirectory, "Themes", "jeffopoly.json");
                themeName = "jeffopoly";
            }

            lock (_lock)
            {
                if (_cache.TryGetValue(themeName, out var cached))
                    return cached;

                var json = File.ReadAllText(path);
                var theme = JsonSerializer.Deserialize<ThemeData>(json, new JsonSerializerOptions
                {
                    PropertyNameCaseInsensitive = true
                }) ?? throw new InvalidOperationException($"Failed to deserialize theme: {themeName}");

                _cache[themeName] = theme;
                return theme;
            }
        }

        /// <summary>
        /// Builds PropertyDef arrays from a theme, keyed by PropertyColor.
        /// </summary>
        public static Dictionary<PropertyColor, PropertyDef[]> BuildPropertyDefs(ThemeData theme)
        {
            var result = new Dictionary<PropertyColor, PropertyDef[]>();
            foreach (var (colorName, names) in theme.Properties)
            {
                if (!Enum.TryParse<PropertyColor>(colorName, ignoreCase: true, out var color))
                    continue;

                var prefix = color.ToString().ToLowerInvariant();
                var defs = new PropertyDef[names.Count];
                for (int i = 0; i < names.Count; i++)
                {
                    defs[i] = new PropertyDef($"{prefix}{i + 1}", names[i]);
                }
                result[color] = defs;
            }
            return result;
        }

        /// <summary>
        /// Gets the display name for a category (Railroad/Utility) from the theme.
        /// </summary>
        public static string GetCategoryDisplayName(ThemeData theme, PropertyColor color)
        {
            var key = color.ToString();
            if (theme.CategoryNames.TryGetValue(key, out var name))
                return name;

            return color switch
            {
                PropertyColor.LightBlue => "Light Blue",
                PropertyColor.DarkBlue => "Dark Blue",
                _ => color.ToString(),
            };
        }
    }
}
