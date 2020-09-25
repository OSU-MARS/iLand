﻿using iLand.tools;
using System;
using System.Collections.Generic;

namespace iLand.core
{
    internal class SaplingGrowthParameters
    {
        public double BrowsingProbability { get; set; }
        public float HdSapling { get; set; } ///< fixed height-diameter ratio used for saplings
        public Expression HeightGrowthPotential { get; private set; } ///< formula that expresses height growth potential
        public int MaxStressYears { get; set; } ///< trees die, if they are "stressed" for this number of consectuive years
        public double ReferenceRatio { get; set; } ///< f_ref (eq. 3) -> ratio reference site / optimum site
        public double ReinekesR { get; set; } ///< Reinekes R, i.e. maximum stem number for a dg of 25cm
        public List<double> RepresentedClasses { get; private set; } ///< lookup table for represented trees
        public double SproutGrowth { get; set; } ///< multiplier of growth for saplings regenerated by sprouts (0: no sprouts)
        public double StressThreshold { get; set; } ///< tree is considered as "stressed" if f_env_yr is below that threhold

        public SaplingGrowthParameters()
        {
            this.BrowsingProbability = 0.0;
            this.ReinekesR = 1450.0;
            this.ReferenceRatio = 1.0;
            this.HdSapling = 80.0F;
            this.MaxStressYears = 3;
            this.RepresentedClasses = new List<double>();
            this.SproutGrowth = 0.0;
            this.StressThreshold = 0.1;
        }

        /// represented stem number by height of one cohort (using Reinekes Law): this uses a lookup table to improve performance
        public double RepresentedStemNumberFromHeight(double height)
        {
            return RepresentedClasses[Global.Limit((int)Math.Round(height * 10.0), 0, RepresentedClasses.Count)];
        }

        /// represented stem number by one cohort (using Reinekes Law):
        public double RepresentedStemNumberFromDiameter(double dbh)
        {
            return ReinekesR * Math.Pow(dbh / 25.0, -1.605) / (double)Constant.LightCellsPerHectare;
        }

        /// browsing probability
        public void SetupReinekeLookup()
        {
            RepresentedClasses.Clear();
            for (int i = 0; i < 41; i++)
            {
                double h = i / 10.0 + 0.05; // 0.05, 0.15, 0.25, ... 4.05
                double dbh = h / HdSapling * 100.0;
                RepresentedClasses.Add(RepresentedStemNumberFromDiameter(dbh));
            }
        }
    }
}
