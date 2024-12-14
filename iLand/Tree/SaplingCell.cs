// C++/core/{ saplings.h, saplings.cpp }: SaplingCell
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

        /// add a sapling to this cell, return a pointer to the tree on success, or 0 otherwise
        // TODO: recontract to TryAddSaplingIfSlotFree()
        public Sapling? AddSaplingIfSlotFree(float heightInM, int ageInYears, int speciesIndex)
        {
            int freeIndex = this.GetFreeIndex();
            if (freeIndex == -1)
            {
                return null;
            }
            this.Saplings[freeIndex].SetSapling(heightInM, ageInYears, speciesIndex);
            // TODO: this.State is not update for the sapling just added; isn't this a bug?
            return this.Saplings[freeIndex];
        }

        // TODO: fix naming to reflect updating of this.State
        public void CheckState()
        {
            if (this.State == SaplingCellState.NotOnLandscape)
            {
                return;
            }

            bool hasFreeSlot = false;
            bool hasOccupiedCell = false;
            for (int slotIndex = 0; slotIndex < this.Saplings.Length; ++slotIndex)
            {
                // locked for all species if a sapling of one species >1.3m
                if (this.Saplings[slotIndex].HeightInM > 1.3F)
                {
                    this.State = SaplingCellState.Full;
                    return;
                }
                // locked if all slots are occupied.
                if (this.Saplings[slotIndex].IsOccupied() == false)
                {
                    hasFreeSlot = true;
                }
                else
                {
                    hasOccupiedCell = true;
                }
            }

            if (hasFreeSlot)
            {
                this.State = hasOccupiedCell ? SaplingCellState.Free : SaplingCellState.Empty;
            }
            else
            {
                this.State = SaplingCellState.Full;
            }
        }

        public void Clear()
        {
            for (int saplingIndex = 0; saplingIndex < this.Saplings.Length; ++saplingIndex)
            {
                Sapling sapling = this.Saplings[saplingIndex];
                sapling.Clear();
            }
            this.CheckState();
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

        public bool HasFreeSlots()
        {
            return (SaplingCellState.Empty <= this.State) && (this.State < SaplingCellState.Full);
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

        /// <returns>height, in m, of the tallest sapling within the cell</returns>
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

        public Sapling? TryGetSaplingOfSpecies(int species_index)
        {
            if (this.State == SaplingCellState.NotOnLandscape)
            {
                return null;
            }

            for (int saplingIndex = 0; saplingIndex < this.Saplings.Length; ++saplingIndex)
            {
                Sapling sapling = this.Saplings[saplingIndex];
                if (sapling.SpeciesIndex == species_index)
                {
                    return sapling;
                }
            }
            return null;
        }
    }
}
