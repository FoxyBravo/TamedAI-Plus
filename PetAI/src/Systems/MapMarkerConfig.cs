using System.Drawing;

namespace PetAI
{
    public class MapMarkerConfig
    {
        public float UpdateIntervalSeconds { get; set; } = 2;
        public string DefaultColor { get; set; } = "#ffffff";
        public string DefaultIcon { get; set; } = "pawprint";
        public string DownedColor { get; set; } = "#ff0000";
        public bool TrackTamingPets { get; set; } = true;
        public int FullScanMinutes { get; set; } = 5;

        public static int ColorStringToArgb(string nameOrHex)
        {
            var c = ColorTranslator.FromHtml(nameOrHex);
            c = Color.FromArgb(255, c.R, c.G, c.B);
            return c.ToArgb();
        }
    }
}
