﻿using iLand.Extensions;
using iLand.Tree;

namespace iLand.Output.Memory
{
    public class ResourceUnitIndividualTreeTrajectories
    {
        public int LengthInYears { get; private set; }
        public TreeListBiometric?[] TreesByYear { get; private set; }

        public ResourceUnitIndividualTreeTrajectories(int initialCapacityInYears)
        {
            this.LengthInYears = 0;
            this.TreesByYear = new TreeListBiometric[initialCapacityInYears];
        }

        public int CapacityInYears
        {
            get { return this.TreesByYear.Length; }
        }

        public void AddYear(TreeListSpatial treesOfSpecies)
        {
            if (this.LengthInYears == this.CapacityInYears)
            {
                this.Extend();
            }

            this.TreesByYear[this.LengthInYears] = new TreeListBiometric(treesOfSpecies);
            ++this.LengthInYears;
        }

        public void AddYearWithoutSpecies(ResourceUnitTreeSpecies speciesNoLongerPresentOnResourceUnit)
        {
            if (this.LengthInYears == this.CapacityInYears)
            {
                this.Extend();
            }

            this.TreesByYear[this.LengthInYears] = speciesNoLongerPresentOnResourceUnit.Species.EmptyTreeList;
            ++this.LengthInYears;
        }

        private void Extend()
        {
            int newCapacityInYears = this.CapacityInYears + Constant.Data.DefaultAnnualAllocationIncrement;

            this.TreesByYear = this.TreesByYear.Resize(newCapacityInYears);
        }
    }
}
