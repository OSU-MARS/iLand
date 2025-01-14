﻿// C++/core/{ species.h, species.cpp }
using iLand.Tool;
using System;
using System.Collections.Generic;
using System.Diagnostics;

namespace iLand.Tree
{
    // https://iland-model.org/sapling+growth+and+competition
    public class SaplingGrowthParameters // C++: SaplingGrowthParameters
    {
        public float AdultSproutProbability { get; set; } ///< annual chance of creating a sprouting sapling cell from an adult tree of resprouting species
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
            this.AdultSproutProbability = 0.0F;
            this.BrowsingProbability = 0.0F;
            this.HeightDiameterRatio = 80.0F;
            this.HeightGrowthPotential = new();
            this.MaxStressYears = 3;
            this.ReinekeR = 1450.0F;
            this.ReferenceRatio = 1.0F;
            this.RepresentedClasses = new((int)(Constant.RegenerationLayerHeight / Constant.Sapling.HeightClassSizeInM) + 1);
            this.SproutGrowth = 0.0F;
            this.StressThreshold = 0.1F;
        }

        /// represented stem number by height of one cohort (using Reinekes Law): this uses a lookup table to improve performance
        public float RepresentedStemNumberFromHeight(float heightInM)
        {
            Debug.Assert(heightInM <= Constant.RegenerationLayerHeight);
            return this.RepresentedClasses[Maths.Limit((int)MathF.Round(heightInM / Constant.Sapling.HeightClassSizeInM) + 1, 0, this.RepresentedClasses.Count - 1)];
        }

        /// represented stem number by one cohort (using Reinekes Law):
        public float RepresentedStemNumberFromDiameter(float dbh)
        {
            return this.ReinekeR * MathF.Pow(dbh / 25.0F, -1.605F) / Constant.Grid.LightCellsPerHectare;
        }

        public void SetupReinekeLookup()
        {
            this.RepresentedClasses.Clear();
            Debug.Assert(Constant.Sapling.MinimumHeightInM > 0.0F);
            this.RepresentedClasses.Add(0.0F); // saplings cannot have zero height, so there are zero saplings per hectare in the first height class
            for (int heightClass = 1; heightClass < this.RepresentedClasses.Capacity; heightClass++)
            {
                float heightInM = Constant.Sapling.HeightClassSizeInM * heightClass;
                float dbh = 100.0F * heightInM / this.HeightDiameterRatio;
                // cap Reineke stem counts as they become unplausible at very small dbh
                if (dbh < Constant.Sapling.MinimumDbhInCm)
                {
                    dbh = Constant.Sapling.MinimumDbhInCm;
                }
                this.RepresentedClasses.Add(this.RepresentedStemNumberFromDiameter(dbh));
            }
        }
    }
}
