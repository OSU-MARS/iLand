using System;

namespace iLand.core
{
    internal class SaplingCell
    {
        public const int NSAPCELLS = 5;

        public enum ECellState { CellInvalid = 0, CellFree = 1, CellFull = 2 };
        public SaplingTree[] saplings;

        public ECellState state;

        public SaplingCell()
        {
            state = ECellState.CellInvalid;
            saplings = new SaplingTree[NSAPCELLS];
        }

        public void checkState()
        {
            if (state == ECellState.CellInvalid)
            {
                return;
            }

            bool free = false;
            for (int i = 0; i < NSAPCELLS; ++i)
            {
                // locked for all species, if a sapling of one species >1.3m
                if (saplings[i].height > 1.3F)
                {
                    state = ECellState.CellFull;
                    return;
                }
                // locked, if all slots are occupied.
                if (!saplings[i].is_occupied())
                {
                    free = true;
                }
            }
            state = free ? ECellState.CellFree : ECellState.CellFull;
        }

        /// get an index to an open slot in the cell, or -1 if all slots are occupied
        public int free_index()
        {
            for (int i = 0; i < NSAPCELLS; ++i)
            {
                if (!saplings[i].is_occupied())
                {
                    return i;
                }
            }
            return -1;
        }

        /// count the number of occupied slots on the pixel
        public int n_occupied()
        {
            int n = 0;
            for (int i = 0; i < NSAPCELLS; ++i)
            {
                n += saplings[i].is_occupied() ? 1 : 0;
            }
            return n;
        }

        /// add a sapling to this cell, return a pointer to the tree on success, or 0 otherwise
        public SaplingTree addSapling(float h_m, int age_yrs, int species_idx)
        {
            int idx = free_index();
            if (idx == -1)
            {
                return null;
            }
            saplings[idx].setSapling(h_m, age_yrs, species_idx);
            return saplings[idx];
        }

        /// return the maximum height on the pixel
        public float max_height()
        {
            if (state == ECellState.CellInvalid)
            {
                return 0.0F;
            }
            float h_max = 0.0F;
            for (int i = 0; i < NSAPCELLS; ++i)
            {
                h_max = Math.Max(saplings[i].height, h_max);
            }
            return h_max;
        }

        public bool has_new_saplings()
        {
            if (state == ECellState.CellInvalid)
            {
                return false;
            }
            for (int i = 0; i < NSAPCELLS; ++i)
            {
                if (saplings[i].is_occupied() && saplings[i].age < 2)
                {
                    return true;
                }
            }
            return false;
        }

        /// return the sapling tree of the requested species, or 0
        public SaplingTree sapling(int species_index)
        {
            if (state == ECellState.CellInvalid)
            {
                return null;
            }
            for (int i = 0; i < NSAPCELLS; ++i)
            {
                if (saplings[i].species_index == species_index)
                {
                    return saplings[i];
                }
            }
            return null;
        }
    }
}
