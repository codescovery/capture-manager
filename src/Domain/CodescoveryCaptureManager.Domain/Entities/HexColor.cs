using System.Windows.Media;

namespace CodescoveryCaptureManager.Domain.Entities
{
    public  class HexColor
    {
        public string Hex { get; }

        private HexColor(string hex)
        {
            Hex = hex;
        }

        public static implicit operator HexColor(string hex)
        {
            return new HexColor(hex);
        }
        public static implicit operator string(HexColor hexColor)
        {
            return hexColor.Hex;
        }

        public static implicit operator Color(HexColor hexColor)
        {

                var colorObject = ColorConverter.ConvertFromString(hexColor.Hex);
                if (colorObject is Color color)
                    return color;
                return Color.FromRgb(0, 0, 0);
        }
    }
}
