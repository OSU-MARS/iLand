﻿using iLand.Tool;
using System;
using System.Collections.Generic;

namespace iLand.Tree
{
    // http://iland-model.org/sapling+growth+and+competition
    public class SaplingGrowthParameters
    {
        public float BrowsingProbability { get; set; }
        public float HeightDiameterRatio { get; set; } // fixed height-diameter ratio used for saplings
        public Expression HeightGrowthPotential { get; private init; } // formula that expresses height growth potential
        public int MaxStressYears { get; set; } // trees die, if they are "stressed" for this number of consectuive years
        public float ReferenceRatio { get; set; } // f_ref (eq. 3) -> ratio reference site / optimum site
        public float ReinekeR { get; set; } // Reineke's R, i.e. maximum stem number for a dg of 25cm
        public List<float> RepresentedClasses { get; private init; } // lookup table for represented trees
        public float SproutGrowth { get; set; } // multiplier of growth for saplings regenerated by sprouts (0: no sprouts)
        public float StressThreshold { get; set; } // tree is considered as "stressed" if f_env_yr is below that threhold

        public SaplingGrowthParameters()
        {
            this.BrowsingProbability = 0.0F;
            this.HeightDiameterRatio = 80.0F;
            this.HeightGrowthPotential = new();
            this.MaxStressYears = 3;
            this.ReinekeR = 1450.0F;
            this.ReferenceRatio = 1.0F;
            this.RepresentedClasses = new();
            this.SproutGrowth = 0.0F;
            this.StressThreshold = 0.1F;
        }

        /// represented stem number by height of one cohort (using Reinekes Law): this uses a lookup table to improve performance
        public float RepresentedStemNumberFromHeight(float height)
        {
            return this.RepresentedClasses[Maths.Limit((int)MathF.Round(10.0F * height), 0, this.RepresentedClasses.Count)];
        }

        /// represented stem number by one cohort (using Reinekes Law):
        public float RepresentedStemNumberFromDiameter(float dbh)
        {
            return this.ReinekeR * MathF.Pow(dbh / 25.0F, -1.605F) / Constant.Grid.LightCellsPerHectare;
        }

        /// browsing probability
        public void SetupReinekeLookup()
        {
            // BUGBUG: constants?
            this.RepresentedClasses.Clear();
            for (int heightClass = 0; heightClass < Constant.Sapling.HeightClasses; ++heightClass)
            {
                float height = Constant.Sapling.HeightClassSize * heightClass + Constant.Sapling.MinimumHeight; // 0.05, 0.15, 0.25, ... 4.05
                float dbh = 100.0F * height / this.HeightDiameterRatio;
                this.RepresentedClasses.Add(this.RepresentedStemNumberFromDiameter(dbh));
            }
        }
    }
}
