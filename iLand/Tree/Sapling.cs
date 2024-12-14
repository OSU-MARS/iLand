// C++/core/{ saplings.h, saplings.cpp }: SaplingTree
using iLand.World;
using System;

namespace iLand.Tree
{
    public class Sapling
    {
        private SaplingStatus status;

        public UInt16 Age { get; set; } // age of the cohort in years
        public float HeightInM { get; set; } // height of the sapling in meter
        public Int16 SpeciesIndex { get; private set; } // index of the species within the resource-unit-species container
        public byte StressYears { get; set; } // number of consecutive years that a sapling suffers from stress

        public Sapling() 
        {
            this.Clear();
        }

        public void Clear()
        {
            this.status = SaplingStatus.None;

            this.Age = 0;
            this.HeightInM = 0.0F;
            this.IsSprout = false;
            this.SpeciesIndex = -1;
            this.StressYears = 0;
        }

        public bool IsBrowsed 
        { 
            get { return (this.status & SaplingStatus.Browsed) == SaplingStatus.Browsed; }
            set 
            {
                if (value) 
                {
                    this.status |= SaplingStatus.Browsed; 
                } 
                else 
                { 
                    this.status &= ~SaplingStatus.Browsed; 
                }
            }
        }

        public bool IsSprout 
        { 
            get { return (this.status & SaplingStatus.Sprout) == SaplingStatus.Sprout; }
            set
            {
                if (value)
                {
                    this.status |= SaplingStatus.Sprout;
                }
                else
                {
                    this.status &= ~SaplingStatus.Sprout;
                }
            }
        }

        public bool IsOccupied()
        {
            return this.HeightInM > 0.0F;
        }

        public void SetSapling(float heightInM, int ageInYears, int speciesIndex)
        {
            this.HeightInM = heightInM;
            this.Age = (UInt16)ageInYears;
            this.StressYears = 0;
            this.SpeciesIndex = (Int16)speciesIndex;
        }

        // get resource unit species of the sapling tree
        public ResourceUnitTreeSpecies GetResourceUnitSpecies(ResourceUnit resourceUnit)
        {
            return resourceUnit.Trees.SpeciesAvailableOnResourceUnit[this.SpeciesIndex];
        }
    }
}
