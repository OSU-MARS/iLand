using System;
using System.Drawing;

namespace iLand.Tool
{
    internal static class ColorExtensions
    {
        public static Color Darker(this Color color, int factor)
        {
            color.ToHsv(out float hue, out float saturation, out float value);
            value /= factor;
            return ColorExtensions.FromHsv(hue, saturation, value);
        }

        public static Color FromHsv(float hue, float saturation, float value)
        {
            int hi = (int)Math.Floor(hue / 60) % 6;
            float f = hue / 60 - MathF.Floor(hue / 60);

            value *= 255;
            int v = (int)value;
            int p = (int)(value * (1 - saturation));
            int q = (int)(value * (1 - f * saturation));
            int t = (int)(value * (1 - (1 - f) * saturation));

            if (hi == 0)
            {
                return Color.FromArgb(255, v, t, p);
            }
            else if (hi == 1)
            {
                return Color.FromArgb(255, q, v, p);
            }
            else if (hi == 2)
            {
                return Color.FromArgb(255, p, v, t);
            }
            else if (hi == 3)
            {
                return Color.FromArgb(255, p, q, v);
            }
            else if (hi == 4)
            {
                return Color.FromArgb(255, t, p, v);
            }
            else
            {
                return Color.FromArgb(255, v, p, q);
            }
        }

        public static Color Lighter(this Color color, int factor)
        {
            color.ToHsv(out float hue, out float saturation, out float value);
            value *= factor;
            return ColorExtensions.FromHsv(hue, saturation, value);
        }

        public static void ToHsv(this Color color, out float hue, out float saturation, out float value)
        {
            int max = Math.Max(color.R, Math.Max(color.G, color.B));
            int min = Math.Min(color.R, Math.Min(color.G, color.B));

            hue = color.GetHue();
            saturation = (max == 0) ? 0.0F : 1 - min / max;
            value = max / 255;
        }
    }
}
