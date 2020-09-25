using System;

namespace iLand.core
{
    internal class SaplingCell
    {
        public const int SaplingCells = 5; // TODO: cells per what?

        public enum ECellState { CellInvalid = 0, CellFree = 1, CellFull = 2 };
        public SaplingTree[] Saplings { get; private set; }
        public ECellState State { get; set; }

        public SaplingCell()
        {
            State = ECellState.CellInvalid;
            Saplings = new SaplingTree[SaplingCells];
        }

        public void CheckState()
        {
            if (State == ECellState.CellInvalid)
            {
                return;
            }

            bool free = false;
            for (int i = 0; i < SaplingCells; ++i)
            {
                // locked for all species, if a sapling of one species >1.3m
                if (Saplings[i].Height > 1.3F)
                {
                    State = ECellState.CellFull;
                    return;
                }
                // locked, if all slots are occupied.
                if (!Saplings[i].IsOccupied())
                {
                    free = true;
                }
            }
            State = free ? ECellState.CellFree : ECellState.CellFull;
        }

        /// get an index to an open slot in the cell, or -1 if all slots are occupied
        public int GetFreeIndex()
        {
            for (int i = 0; i < SaplingCells; ++i)
            {
                if (!Saplings[i].IsOccupied())
                {
                    return i;
                }
            }
            return -1;
        }

        /// count the number of occupied slots on the pixel
        public int GetOccupiedSlotCount()
        {
            int n = 0;
            for (int i = 0; i < SaplingCells; ++i)
            {
                n += Saplings[i].IsOccupied() ? 1 : 0;
            }
            return n;
        }

        /// add a sapling to this cell, return a pointer to the tree on success, or 0 otherwise
        public SaplingTree AddSapling(float h_m, int age_yrs, int species_idx)
        {
            int idx = GetFreeIndex();
            if (idx == -1)
            {
                return null;
            }
            Saplings[idx].SetSapling(h_m, age_yrs, species_idx);
            return Saplings[idx];
        }

        /// return the maximum height on the pixel
        public float MaxHeight()
        {
            if (State == ECellState.CellInvalid)
            {
                return 0.0F;
            }
            float h_max = 0.0F;
            for (int i = 0; i < SaplingCells; ++i)
            {
                h_max = Math.Max(Saplings[i].Height, h_max);
            }
            return h_max;
        }

        public bool HasNewSaplings()
        {
            if (State == ECellState.CellInvalid)
            {
                return false;
            }
            for (int i = 0; i < SaplingCells; ++i)
            {
                if (Saplings[i].IsOccupied() && Saplings[i].Age < 2)
                {
                    return true;
                }
            }
            return false;
        }

        /// return the sapling tree of the requested species, or 0
        public SaplingTree Sapling(int species_index)
        {
            if (State == ECellState.CellInvalid)
            {
                return null;
            }
            for (int i = 0; i < SaplingCells; ++i)
            {
                if (Saplings[i].SpeciesIndex == species_index)
                {
                    return Saplings[i];
                }
            }
            return null;
        }
    }
}
