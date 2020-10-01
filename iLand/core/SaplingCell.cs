﻿using System;

namespace iLand.Core
{
    public class SaplingCell
    {
        public const int SaplingSlots = 5; // TODO: 5 slots per cell -> max of 5 saplings per 2 x 2 m cell -> upper limit of only 8000 TPH regen?

        public enum SaplingCellState { Invalid = 0, Free = 1, Full = 2 };

        public SaplingTree[] Saplings { get; private set; }
        public SaplingCellState State { get; set; }

        public SaplingCell()
        {
            this.State = SaplingCellState.Invalid;
            this.Saplings = new SaplingTree[SaplingCell.SaplingSlots];
            for (int slotIndex = 0; slotIndex < this.Saplings.Length; ++slotIndex)
            {
                // BUGBUG: SoA
                this.Saplings[slotIndex] = new SaplingTree();
            }
        }

        public void CheckState()
        {
            if (this.State == SaplingCellState.Invalid)
            {
                return;
            }

            bool hasFreeSlot = false;
            for (int slotIndex = 0; slotIndex < this.Saplings.Length; ++slotIndex)
            {
                // locked for all species, if a sapling of one species >1.3m
                if (this.Saplings[slotIndex].Height > 1.3F)
                {
                    this.State = SaplingCellState.Full;
                    return;
                }
                // locked, if all slots are occupied.
                if (!this.Saplings[slotIndex].IsOccupied())
                {
                    hasFreeSlot = true;
                }
            }
            this.State = hasFreeSlot ? SaplingCellState.Free : SaplingCellState.Full;
        }

        /// get an index to an open slot in the cell, or -1 if all slots are occupied
        public int GetFreeIndex()
        {
            for (int slotIndex = 0; slotIndex < this.Saplings.Length; ++slotIndex)
            {
                if (!Saplings[slotIndex].IsOccupied())
                {
                    return slotIndex;
                }
            }
            return -1;
        }

        /// count the number of occupied slots on the pixel
        public int GetOccupiedSlotCount()
        {
            int slotsOccupied = 0;
            for (int slotIndex = 0; slotIndex < this.Saplings.Length; ++slotIndex)
            {
                slotsOccupied += this.Saplings[slotIndex].IsOccupied() ? 1 : 0;
            }
            return slotsOccupied;
        }

        /// add a sapling to this cell, return a pointer to the tree on success, or 0 otherwise
        public SaplingTree AddSapling(float h_m, int age_yrs, int species_idx)
        {
            int freeIndex = GetFreeIndex();
            if (freeIndex == -1)
            {
                return null;
            }
            this.Saplings[freeIndex].SetSapling(h_m, age_yrs, species_idx);
            return Saplings[freeIndex];
        }

        /// return the maximum height on the pixel
        public float MaxHeight()
        {
            if (this.State == SaplingCellState.Invalid)
            {
                return 0.0F;
            }
            float h_max = 0.0F;
            for (int slotIndex = 0; slotIndex < this.Saplings.Length; ++slotIndex)
            {
                h_max = Math.Max(Saplings[slotIndex].Height, h_max);
            }
            return h_max;
        }

        public bool HasNewSaplings()
        {
            if (this.State == SaplingCellState.Invalid)
            {
                return false;
            }
            for (int slotIndex = 0; slotIndex < this.Saplings.Length; ++slotIndex)
            {
                if (this.Saplings[slotIndex].IsOccupied() && this.Saplings[slotIndex].Age < 2)
                {
                    return true;
                }
            }
            return false;
        }

        /// return the sapling tree of the requested species, or 0
        public SaplingTree FirstOrDefault(int speciesIndex)
        {
            if (this.State == SaplingCellState.Invalid)
            {
                return null;
            }
            for (int slotIndex = 0; slotIndex < this.Saplings.Length; ++slotIndex)
            {
                if (this.Saplings[slotIndex].SpeciesIndex == speciesIndex)
                {
                    return this.Saplings[slotIndex];
                }
            }
            return null;
        }
    }
}
