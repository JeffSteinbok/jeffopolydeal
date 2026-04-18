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
        public static string DisplayName(this PropertyColor color) => color switch
        {
            PropertyColor.LightBlue => "Light Blue",
            PropertyColor.DarkBlue => "Dark Blue",
            PropertyColor.Railroad => "Stadium",
            PropertyColor.Utility => "Grocery",
            _ => color.ToString(),
        };
    }
}
