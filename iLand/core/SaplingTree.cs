using System;

namespace iLand.core
{
    internal class SaplingTree
    {
        public UInt16 age;  // number of consectuive years the sapling suffers from dire conditions
        public Int16 species_index; // index of the species within the resource-unit-species container
        public byte stress_years; // number of consecutive years that a sapling suffers from stress
        public byte flags; // flags, e.g. whether sapling stems from sprouting
        public float height; // height of the sapling in meter

        public SaplingTree() { clear(); }

        public bool is_occupied() { return height > 0.0F; }

        public void clear()
        {
            age = 0;
            species_index = -1;
            stress_years = 0;
            flags = 0;
            height = 0.0F;
        }

        public void setSapling(float h_m, int age_yrs, int species_idx)
        {
            height = h_m;
            age = (UInt16)age_yrs;
            stress_years = 0;
            species_index = (Int16)species_idx;
        }

        // flags
        public bool is_sprout() { return (flags & 1) != 0; }
        public void set_sprout(bool sprout) 
        {
            if (sprout)
            {
                flags |= 1;
            }
            else
            {
                flags &= 0xfe;
            }
        }

        // get resource unit species of the sapling tree
        public ResourceUnitSpecies resourceUnitSpecies(ResourceUnit ru)
        {
            if (ru == null || !is_occupied())
            {
                return null;
            }
            ResourceUnitSpecies rus = ru.resourceUnitSpecies(species_index);
            return rus;
        }
    }
}
