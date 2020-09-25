using iLand.core;
using System;
using System.Collections.Generic;
using System.Drawing;

namespace iLand.tools
{
    internal class Colors
    {
        private static readonly List<Color> mBrewerDiv = new List<Color>() { ColorTranslator.FromHtml("#543005"), ColorTranslator.FromHtml("#8c510a"), ColorTranslator.FromHtml("#bf812d"), ColorTranslator.FromHtml("#dfc27d"),
                                                                             ColorTranslator.FromHtml("#f6e8c3"), ColorTranslator.FromHtml("#f5f5f5"), ColorTranslator.FromHtml("#fdbf6f"), ColorTranslator.FromHtml("##c7eae5"),
                                                                             ColorTranslator.FromHtml("#80cdc1"), ColorTranslator.FromHtml("#35978f"), ColorTranslator.FromHtml("#01665e"), ColorTranslator.FromHtml("#003c30") };
        private static readonly List<Color> mBrewerQual = new List<Color>() { ColorTranslator.FromHtml("#a6cee3"), ColorTranslator.FromHtml("#1f78b4"), ColorTranslator.FromHtml("#b2df8a"), ColorTranslator.FromHtml("#33a02c"),
                                                                              ColorTranslator.FromHtml("#fb9a99"), ColorTranslator.FromHtml("#e31a1c"), ColorTranslator.FromHtml("#fdbf6f"), ColorTranslator.FromHtml("#ff7f00"),
                                                                              ColorTranslator.FromHtml("#cab2d6"), ColorTranslator.FromHtml("#6a3d9a"), ColorTranslator.FromHtml("#ffff99"), ColorTranslator.FromHtml("#b15928") };
        private static readonly List<Color> mTerrainCol = new List<Color>() { ColorTranslator.FromHtml("#00A600"), ColorTranslator.FromHtml("#24B300"), ColorTranslator.FromHtml("#4CBF00"), ColorTranslator.FromHtml("#7ACC00"),
                                                                              ColorTranslator.FromHtml("#ADD900"), ColorTranslator.FromHtml("#E6E600"), ColorTranslator.FromHtml("#E8C727"), ColorTranslator.FromHtml("#EAB64E"),
                                                                              ColorTranslator.FromHtml("#ECB176"), ColorTranslator.FromHtml("#EEB99F"), ColorTranslator.FromHtml("#F0CFC8"), ColorTranslator.FromHtml("#F2F2F2") };
        
        private GridViewType mCurrentType;
        private bool mNeedsPaletteUpdate;

        public bool AutoScale { get; private set; }
        public string Caption { get; private set; }
        public string Description { get; private set; }
        public bool HasFactors { get; private set; }
        public List<string> Labels { get; private set; }
        public List<string> FactorColors { get; set; }
        public int FactorColorCount() { return this.FactorColors.Count; }
        public List<string> FactorLabels { get; private set; }
        public double MetersPerPixel { get; private set; }
        public double MinValue { get; private set; }
        public double MaxValue { get; private set; }

        public void SetMinValue(double val)
        {
            if (val == MinValue)
            {
                return;
            }
            mNeedsPaletteUpdate = true;
            SetPalette(mCurrentType, (float)val, (float)MaxValue);
            MinValue = val;
        }

        public void SetMaxValue(double val)
        {
            if (val == MaxValue)
            {
                return;
            }
            mNeedsPaletteUpdate = true; 
            SetPalette(mCurrentType, (float)MinValue, (float)val); 
            MaxValue = val;
        }

        public void SetAutoScale(bool value) 
        {
            if (value == AutoScale)
            {
                return;
            }

            AutoScale = value; 
            mNeedsPaletteUpdate = true; 
            SetPalette(mCurrentType, (float)MinValue, (float)MaxValue); 
        }

        public event Action OnColorsChanged;
        public event Action OnScaleChanged;

        public void SetCaptionAndDescription(string caption, string description = "")
        {
            if (Caption == caption && Description == description)
            {
                return;
            }
            Caption = caption;
            Description = description; 
            mNeedsPaletteUpdate = true;
        }

        // scale
        public void SetScale(double meter_per_pixel)
        {
            if (MetersPerPixel == meter_per_pixel)
            {
                return;
            }
            MetersPerPixel = meter_per_pixel;
            OnScaleChanged();
        }

        public void SetPalette(GridViewType type, float min_val, float max_val)
        {
            if (mNeedsPaletteUpdate == false && type == mCurrentType && (AutoScale == false || (MinValue == min_val && MaxValue == max_val)))
            {
                return;
            }

            HasFactors = false;
            int n = 50;
            if (type >= GridViewType.GridViewBrewerDiv)
            {
                // categorical values...
                HasFactors = true;
                n = FactorLabels.Count;
                if (FactorLabels.Count == 0)
                {
                    n = (int)max_val;
                    for (int i = 0; i < n; ++i)
                    {
                        FactorLabels.Add("Label " + i);
                    }
                }
            }
            if (type != GridViewType.GridViewCustom)
            {
                FactorColors.Clear();
                for (int i = 0; i < n; ++i)
                {
                    if (HasFactors)
                    {
                        FactorColors.Add(ColorFromValue(i, type, 0.0F, 1.0F).ToString());
                    }
                    else
                    {
                        FactorColors.Add(ColorFromValue(1.0F - i / (float)n, type, 0.0F, 1.0F).ToString());
                    }
                }
            }
            Labels = new List<string>() { min_val.ToString(),
                                           ((3.0 * min_val + max_val) / 4.0).ToString(),
                                           ((min_val + max_val) / 2.0).ToString(),
                                           ((min_val + 3.0 * max_val) / 4.0).ToString(),
                                           max_val.ToString() };
            if (AutoScale)
            {
                MinValue = min_val;
                MaxValue = max_val;
            }
            mCurrentType = type;
            mNeedsPaletteUpdate = false;
            OnColorsChanged();
        }

        public void SetFactorLabels(List<string> labels)
        {
            FactorLabels = labels;
            mNeedsPaletteUpdate = true;
        }

        public Colors()
        {
            mNeedsPaletteUpdate = true;
            AutoScale = true;
            HasFactors = false;
            MetersPerPixel = 1.0;
            //default start palette
            //setPalette(GridViewType.GridViewRainbow, 0, 1);
            // factors test
            SetCaptionAndDescription(String.Empty);
            SetPalette(GridViewType.GridViewTerrain, 0, 4);
        }

        public Color ColorFromPalette(int value, GridViewType view_type)
        {
            if (value < 0)
            {
                return Color.White;
            }
            int n = Math.Max(value, 0) % 12;
            Color col;
            switch (view_type)
            {
                case GridViewType.GridViewBrewerDiv:
                    col = mBrewerDiv[n];
                    break;
                case GridViewType.GridViewBrewerQual: 
                    col = mBrewerQual[n]; 
                    break;
                case GridViewType.GridViewTerrain: 
                    col = mTerrainCol[n]; 
                    break;
                default: 
                    return default;
            }
            if (value < 12)
            {
                return col;
            }
            n = Math.Max(value, 0) % 60;
            if (n < 12)
            {
                return col;
            }
            if (n < 24)
            {
                return col.Darker(200);
            }
            if (n < 36)
            {
                return col.Lighter(150);
            }
            if (n < 48)
            {
                return col.Darker(300);
            }
            return col.Lighter(200);
        }

        public Color ShadeColor(Color col, PointF coordinates, DEM dem)
        {
            if (dem != null)
            {
                float val = dem.EnsureViewGrid()[coordinates]; // scales from 0..1
                col.ToHsv(out double h, out double s, out double v);
                // we adjust the 'v', the lightness: if val=0.5 -> nothing changes
                v = Global.Limit(v - (1.0 - val) * 0.4, 0.1, 1.0);
                return ColorExtensions.FromHsv(h, s, v);
            }

            return col;
        }

        // colors
        public Color ColorFromValue(float value, float min_value, float max_value, bool reverse, bool black_white)
        {
            float rval = value;
            rval = Math.Max(min_value, rval);
            rval = Math.Min(max_value, rval);
            if (reverse)
            {
                rval = max_value - rval;
            }
            float rel_value;
            Color col;
            if (min_value < max_value)
            {
                // default: high values -> red (h=0), low values to blue (h=high)
                rel_value = 1 - (rval - min_value) / (max_value - min_value);
                if (black_white)
                {
                    int c = (int)((1.0 - rel_value) * 255);
                    col = Color.FromArgb(c, c, c);
                }
                else
                {
                    col = ColorExtensions.FromHsv(0.66666666666 * rel_value, 0.95, 0.95);
                }
            }
            else
            {
                col = Color.White;
            }
            return col;
        }

        public Color ColorFromValue(float value, GridViewType view_type, float min_value, float max_value)
        {
            if (view_type == GridViewType.GridViewGray || view_type == GridViewType.GridViewGrayReverse)
            {
                return ColorFromValue(value, min_value, max_value, view_type == GridViewType.GridViewGrayReverse, true);
            }

            if (view_type == GridViewType.GridViewRainbow || view_type == GridViewType.GridViewRainbowReverse)
            {
                return ColorFromValue(value, min_value, max_value, view_type == GridViewType.GridViewRainbowReverse, false);
            }

            if (view_type == GridViewType.GridViewGreens || view_type == GridViewType.GridViewBlues || view_type == GridViewType.GridViewReds)
            {
                float rval = value;
                rval = Math.Max(min_value, rval);
                rval = Math.Min(max_value, rval);
                float rel_value = (max_value != min_value) ? (rval - min_value) / (max_value - min_value) : 0;
                int r, g, b;
                switch (view_type)
                {
                    case GridViewType.GridViewGreens:  // 11,111,19
                        r = (int)(220 - rel_value * (220 - 11));
                        g = (int)(220 - rel_value * (220 - 111)); 
                        b = (int)(220 - rel_value * (220 - 19)); 
                        break;
                    case GridViewType.GridViewBlues: //15,67,138
                        r = (int)(220 - rel_value * (220 - 15)); 
                        g = (int)(220 - rel_value * (220 - 67)); 
                        b = (int)(220 - rel_value * (220 - 138));
                        break;
                    case GridViewType.GridViewReds: //219,31,72
                        r = (int)(240 - rel_value * (220 - 219)); 
                        g = (int)(240 - rel_value * (220 - 31)); 
                        b = (int)(240 - rel_value * (220 - 72)); 
                        break;
                    default: 
                        r = g = b = 0;
                        break;
                }
                return Color.FromArgb(r, g, b);
            }

            if (view_type == GridViewType.GridViewHeat)
            {
                float rval = value;
                rval = Math.Max(min_value, rval);
                rval = Math.Min(max_value, rval);
                float rel_value = 1 - (rval - min_value) / (max_value - min_value);
                int g = 255, b = 0;
                if (rel_value < 0.5)
                {
                    g = (int)(rel_value * 2.0F * 255);
                }
                if (rel_value > 0.5)
                {
                    b = (int)((rel_value - 0.5) * 2.0F * 255);
                }
                return Color.FromArgb(255, g, b);
            }

            return ColorFromPalette((int)value, view_type);
        }
    }
}
