using System;

namespace iLand.core
{
    internal class SaplingTree
    {
        public UInt16 Age { get; set; }  // number of consectuive years the sapling suffers from dire conditions
        public byte Flags { get; set; } // flags, e.g. whether sapling stems from sprouting
        public float Height { get; set; } // height of the sapling in meter
        public Int16 SpeciesIndex { get; private set; } // index of the species within the resource-unit-species container
        public byte StressYears { get; set; } // number of consecutive years that a sapling suffers from stress

        public SaplingTree() 
        { 
            Clear(); 
        }

        public bool IsOccupied() { return Height > 0.0F; }

        // flags
        public bool IsSprout() { return (Flags & 1) != 0; }
        public void SetSprout(bool sprout)
        {
            if (sprout)
            {
                Flags |= 1;
            }
            else
            {
                Flags &= 0xfe;
            }
        }

        public void Clear()
        {
            Age = 0;
            SpeciesIndex = -1;
            StressYears = 0;
            Flags = 0;
            Height = 0.0F;
        }

        public void SetSapling(float h_m, int age_yrs, int species_idx)
        {
            Height = h_m;
            Age = (UInt16)age_yrs;
            StressYears = 0;
            SpeciesIndex = (Int16)species_idx;
        }

        // get resource unit species of the sapling tree
        public ResourceUnitSpecies ResourceUnitSpecies(ResourceUnit ru)
        {
            if (ru == null || !IsOccupied())
            {
                return null;
            }
            ResourceUnitSpecies rus = ru.ResourceUnitSpecies(SpeciesIndex);
            return rus;
        }
    }
}
