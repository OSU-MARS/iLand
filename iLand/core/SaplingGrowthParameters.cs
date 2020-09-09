﻿using iLand.tools;
using System;
using System.Collections.Generic;

namespace iLand.core
{
    internal class SaplingGrowthParameters
    {
        public Expression heightGrowthPotential; ///< formula that expresses height growth potential
        public int maxStressYears; ///< trees die, if they are "stressed" for this number of consectuive years
        public double stressThreshold; ///< tree is considered as "stressed" if f_env_yr is below that threhold
        public float hdSapling; ///< fixed height-diameter ratio used for saplings
        public double ReinekesR; ///< Reinekes R, i.e. maximum stem number for a dg of 25cm
        public double referenceRatio; ///< f_ref (eq. 3) -> ratio reference site / optimum site
        public double browsingProbability;
        public double sproutGrowth; ///< multiplier of growth for saplings regenerated by sprouts (0: no sprouts)
        public List<double> mRepresentedClasses; ///< lookup table for represented trees

        public SaplingGrowthParameters()
        {
            maxStressYears = 3;
            stressThreshold = 0.1;
            hdSapling = 80.0F;
            ReinekesR = 1450.0;
            referenceRatio = 1.0;
            browsingProbability = 0.0;
            sproutGrowth = 0.0;
        }

        /// represented stem number by height of one cohort (using Reinekes Law): this uses a lookup table to improve performance
        public double representedStemNumberH(double height)
        {
            return mRepresentedClasses[Global.limit((int)Math.Round(height * 10.0), 0, mRepresentedClasses.Count)];
        }

        /// represented stem number by one cohort (using Reinekes Law):
        public double representedStemNumber(double dbh)
        {
            return ReinekesR * Math.Pow(dbh / 25.0, -1.605) / (double)Constant.cPxPerHectare;
        }

        /// browsing probability
        public void setupReinekeLookup()
        {
            mRepresentedClasses.Clear();
            for (int i = 0; i < 41; i++)
            {
                double h = i / 10.0 + 0.05; // 0.05, 0.15, 0.25, ... 4.05
                double dbh = h / hdSapling * 100.0;
                mRepresentedClasses.Add(representedStemNumber(dbh));
            }
        }
    }
}
