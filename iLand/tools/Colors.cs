using iLand.core;
using System;
using System.Collections.Generic;
using System.Drawing;

namespace iLand.tools
{
    internal class Colors
    {
        private List<Color> mBrewerDiv = new List<Color>() { ColorTranslator.FromHtml("#543005"), ColorTranslator.FromHtml("#8c510a"), ColorTranslator.FromHtml("#bf812d"), ColorTranslator.FromHtml("#dfc27d"),
                                                             ColorTranslator.FromHtml("#f6e8c3"), ColorTranslator.FromHtml("#f5f5f5"), ColorTranslator.FromHtml("#fdbf6f"), ColorTranslator.FromHtml("##c7eae5"),
                                                             ColorTranslator.FromHtml("#80cdc1"), ColorTranslator.FromHtml("#35978f"), ColorTranslator.FromHtml("#01665e"), ColorTranslator.FromHtml("#003c30") };
        private List<Color> mBrewerQual = new List<Color>() { ColorTranslator.FromHtml("#a6cee3"), ColorTranslator.FromHtml("#1f78b4"), ColorTranslator.FromHtml("#b2df8a"), ColorTranslator.FromHtml("#33a02c"),
                                                              ColorTranslator.FromHtml("#fb9a99"), ColorTranslator.FromHtml("#e31a1c"), ColorTranslator.FromHtml("#fdbf6f"), ColorTranslator.FromHtml("#ff7f00"),
                                                              ColorTranslator.FromHtml("#cab2d6"), ColorTranslator.FromHtml("#6a3d9a"), ColorTranslator.FromHtml("#ffff99"), ColorTranslator.FromHtml("#b15928") };
        private List<Color> mTerrainCol = new List<Color>() { ColorTranslator.FromHtml("#00A600"), ColorTranslator.FromHtml("#24B300"), ColorTranslator.FromHtml("#4CBF00"), ColorTranslator.FromHtml("#7ACC00"),
                                                              ColorTranslator.FromHtml("#ADD900"), ColorTranslator.FromHtml("#E6E600"), ColorTranslator.FromHtml("#E8C727"), ColorTranslator.FromHtml("#EAB64E"),
                                                              ColorTranslator.FromHtml("#ECB176"), ColorTranslator.FromHtml("#EEB99F"), ColorTranslator.FromHtml("#F0CFC8"), ColorTranslator.FromHtml("#F2F2F2") };
        
        private List<string> mColors;
        private List<string> mLabels;
        private List<string> mFactorLabels;
        private double mMinValue;
        private double mMaxValue;
        private GridViewType mCurrentType;
        private bool mAutoScale;
        private bool mHasFactors;
        private bool mNeedsPaletteUpdate;
        private string mCaption;
        private string mDescription;
        private double mMeterPerPixel;

        // properties
        public List<string> colors() { return mColors; }
        public List<string> labels() { return mLabels; }
        public List<string> factorLabels() { return mFactorLabels; }
        public int colorCount() { return mColors.Count; }
        public double minValue() { return mMinValue; }
        public double maxValue() { return mMaxValue; }

        public void setMinValue(double val)
        {
            if (val == mMinValue)
            {
                return;
            }
            mNeedsPaletteUpdate = true;
            setPalette(mCurrentType, (float)val, (float)mMaxValue);
            mMinValue = val;
        }
        public void setMaxValue(double val)
        {
            if (val == mMaxValue)
            {
                return;
            }
            mNeedsPaletteUpdate = true; 
            setPalette(mCurrentType, (float)mMinValue, (float)val); 
            mMaxValue = val;
        }

        public bool hasFactors() { return mHasFactors; }
        public bool autoScale() { return mAutoScale; }
        public void setAutoScale(bool value) 
        {
            if (value == mAutoScale)
            {
                return;
            }

            mAutoScale = value; 
            mNeedsPaletteUpdate = true; 
            setPalette(mCurrentType, (float)mMinValue, (float)mMaxValue); 
        }

        public string caption() { return mCaption; }
        public string description() { return mDescription; }

        public void setFactorColors(List<string> colors) { mColors = colors; }

        public event Action colorsChanged;
        public event Action scaleChanged;

        public void setCaption(string caption, string description = "")
        {
            if (mCaption == caption && mDescription == description)
            {
                return;
            }
            mCaption = caption;
            mDescription = description; 
            mNeedsPaletteUpdate = true;
        }

        // scale
        public double meterPerPixel() { return mMeterPerPixel; }
        public void setScale(double meter_per_pixel)
        {
            if (mMeterPerPixel == meter_per_pixel)
            {
                return;
            }
            mMeterPerPixel = meter_per_pixel;
            scaleChanged();
        }

        public void setPalette(GridViewType type, float min_val, float max_val)
        {
            if (mNeedsPaletteUpdate == false && type == mCurrentType && (mAutoScale == false || (minValue() == min_val && maxValue() == max_val)))
            {
                return;
            }

            mHasFactors = false;
            int n = 50;
            if (type >= GridViewType.GridViewBrewerDiv)
            {
                // categorical values...
                mHasFactors = true;
                n = mFactorLabels.Count;
                if (mFactorLabels.Count == 0)
                {
                    n = (int)max_val;
                    for (int i = 0; i < n; ++i)
                    {
                        mFactorLabels.Add("Label " + i);
                    }
                }
            }
            if (type != GridViewType.GridViewCustom)
            {
                mColors.Clear();
                for (int i = 0; i < n; ++i)
                {
                    if (mHasFactors)
                    {
                        mColors.Add(colorFromValue(i, type, 0.0F, 1.0F).ToString());
                    }
                    else
                    {
                        mColors.Add(colorFromValue(1.0F - i / (float)n, type, 0.0F, 1.0F).ToString());
                    }
                }
            }
            mLabels = new List<string>() { min_val.ToString(),
                                           ((3.0 * min_val + max_val) / 4.0).ToString(),
                                           ((min_val + max_val) / 2.0).ToString(),
                                           ((min_val + 3.0 * max_val) / 4.0).ToString(),
                                           max_val.ToString() };
            if (mAutoScale)
            {
                mMinValue = min_val;
                mMaxValue = max_val;
            }
            mCurrentType = type;
            mNeedsPaletteUpdate = false;
            colorsChanged();
        }

        public void setFactorLabels(List<string> labels)
        {
            mFactorLabels = labels;
            mNeedsPaletteUpdate = true;
        }

        public Colors(object parent)
        {
            mNeedsPaletteUpdate = true;
            mAutoScale = true;
            mHasFactors = false;
            mMeterPerPixel = 1.0;
            //default start palette
            //setPalette(GridViewType.GridViewRainbow, 0, 1);
            // factors test
            setCaption(String.Empty);
            setPalette(GridViewType.GridViewTerrain, 0, 4);
        }

        public Color colorFromPalette(int value, GridViewType view_type)
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

        public Color shadeColor(Color col, PointF coordinates, DEM dem)
        {
            if (dem != null)
            {
                float val = dem.viewGrid().constValueAt(coordinates); // scales from 0..1
                col.ToHsv(out double h, out double s, out double v);
                // we adjust the 'v', the lightness: if val=0.5 -> nothing changes
                v = Global.limit(v - (1.0 - val) * 0.4, 0.1, 1.0);
                return ColorExtensions.FromHsv(h, s, v);
            }

            return col;
        }

        // colors
        public Color colorFromValue(float value, float min_value, float max_value, bool reverse, bool black_white)
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

        public Color colorFromValue(float value, GridViewType view_type, float min_value, float max_value)
        {
            if (view_type == GridViewType.GridViewGray || view_type == GridViewType.GridViewGrayReverse)
            {
                return colorFromValue(value, min_value, max_value, view_type == GridViewType.GridViewGrayReverse, true);
            }

            if (view_type == GridViewType.GridViewRainbow || view_type == GridViewType.GridViewRainbowReverse)
            {
                return colorFromValue(value, min_value, max_value, view_type == GridViewType.GridViewRainbowReverse, false);
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

            return colorFromPalette((int)value, view_type);
        }
    }
}
