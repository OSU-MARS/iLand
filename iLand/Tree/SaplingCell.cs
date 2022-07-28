using System;

namespace iLand.Tree
{
    public class SaplingCell
    {
        // 5 slots per cell = max of 5 saplings per 2 x 2 m cell => maximum of 8000 saplings per hectare
        public const int SaplingsPerCell = 5;

        public Sapling[] Saplings { get; private init; }
        public SaplingCellState State { get; set; }

        public SaplingCell()
        {
            this.State = SaplingCellState.NotOnLandscape;
            this.Saplings = new Sapling[SaplingCell.SaplingsPerCell];
            for (int slotIndex = 0; slotIndex < this.Saplings.Length; ++slotIndex)
            {
                // TODO: change storage to SoA instead of AoS
                this.Saplings[slotIndex] = new Sapling();
            }
        }

        public void CheckState()
        {
            if (this.State == SaplingCellState.NotOnLandscape)
            {
                return;
            }

            bool hasFreeSlot = false;
            for (int slotIndex = 0; slotIndex < this.Saplings.Length; ++slotIndex)
            {
                // locked for all species if a sapling of one species >1.3m
                if (this.Saplings[slotIndex].HeightInM > 1.3F)
                {
                    this.State = SaplingCellState.Full;
                    return;
                }
                // locked if all slots are occupied.
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
        public Sapling? AddSaplingIfSlotFree(float heightInM, int ageInYears, int speciesIndex)
        {
            int freeIndex = this.GetFreeIndex();
            if (freeIndex == -1)
            {
                return null;
            }
            this.Saplings[freeIndex].SetSapling(heightInM, ageInYears, speciesIndex);
            return this.Saplings[freeIndex];
        }

        /// return the maximum height on the pixel
        public float MaxHeight()
        {
            if (this.State == SaplingCellState.NotOnLandscape)
            {
                return 0.0F;
            }
            float tallestSapling = 0.0F;
            for (int slotIndex = 0; slotIndex < this.Saplings.Length; ++slotIndex)
            {
                tallestSapling = MathF.Max(this.Saplings[slotIndex].HeightInM, tallestSapling);
            }
            return tallestSapling;
        }

        public bool HasNewSaplings()
        {
            if (this.State == SaplingCellState.NotOnLandscape)
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
        public Sapling? FirstOrDefault(int speciesIndex)
        {
            if (this.State == SaplingCellState.NotOnLandscape)
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
