namespace JeffopolyDeal.Models
{
    public enum PropertyColor
    {
        Brown,
        LightBlue,
        Pink,
        Orange,
        Red,
        Yellow,
        Green,
        DarkBlue,
        Railroad,
        Utility
    }

    public static class PropertyColorExtensions
    {
        /// <summary>
        /// Returns the display name for a property color, optionally using a theme's category names.
        /// </summary>
        public static string DisplayName(this PropertyColor color, string? themeName = null)
        {
            if (themeName != null)
            {
                var theme = ThemeLoader.Load(themeName);
                if (color == PropertyColor.Railroad && theme.CategoryNames.TryGetValue("Railroad", out var rr))
                    return rr;
                if (color == PropertyColor.Utility && theme.CategoryNames.TryGetValue("Utility", out var ut))
                    return ut;
            }
            return color switch
            {
                PropertyColor.LightBlue => "Light Blue",
                PropertyColor.DarkBlue => "Dark Blue",
                PropertyColor.Railroad => "Stadium",
                PropertyColor.Utility => "Grocery",
                _ => color.ToString(),
            };
        }
    }
}
