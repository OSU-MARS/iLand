// C++/core/{ resourceunitspecies.h, resourceunitspecies.cpp }
using iLand.World;
using System;
using Model = iLand.Simulation.Model;

namespace iLand.Tree
{
    /** The class contains data available at ResourceUnit x Species scale.
        Data stored is either statistical (i.e. number of trees per species) or used
        within the model (e.g. fraction of utilizable Radiation).
        Important submodules are:
        * 3-PG production (Production3-PG)
        * Establishment
        * Growth and Recruitment of Saplings
        * Snag dynamics
      */
    public class ResourceUnitTreeSpecies
    {
        private int yearOfMostRecentBiomassCalculation;

        public ResourceUnit ResourceUnit { get; private init; } // return pointer to resource unit
        public SaplingEstablishment SaplingEstablishment { get; private init; } // establishment submodel
        public SaplingStatistics SaplingStats { get; private init; } // statistics for the sapling sub module, C++ constSaplingStat(), mSaplingStat
        public TreeSpecies Species { get; private init; } // return pointer to species
        public LiveTreeAndSaplingStatistics StatisticsLive { get; private init; } // statistics of this species on the resource unit, C++ statistics(), mStatistics
        public LiveTreeStatistics StatisticsManagement { get; private init; } // statistics of removed trees
        public LiveTreeStatistics StatisticsSnag { get; private init; } // statistics of trees that have died, maintained here for now since resource unit snags are tracked by size class and not by species
        public ResourceUnitTreeSpeciesGrowth TreeGrowth { get; private init; } // the 3-PG production model of this species on this resource unit

        public ResourceUnitTreeSpecies(TreeSpecies treeSpecies, ResourceUnit resourceUnit)
        {
            if ((treeSpecies.Index < 0) || (treeSpecies.Index > 1000))
            {
                throw new ArgumentOutOfRangeException(nameof(treeSpecies), "Implausible tree species index.");
            }

            this.yearOfMostRecentBiomassCalculation = Int32.MinValue;

            this.ResourceUnit = resourceUnit;
            this.Species = treeSpecies;

            this.SaplingEstablishment = new();
            this.SaplingStats = new();
            this.StatisticsLive = new();
            this.StatisticsManagement = new();
            this.StatisticsSnag = new();
            this.TreeGrowth = new(resourceUnit, this); // requires this.Species be set
        }

        public void CalculateBiomassGrowthForYear(Model model, bool fromSaplingEstablishmentOrGrowth = false) // C++: ResourceUnitSpecies::calculate()
        {
            // TODO: simplify annual growth state machine so this function's called once, rather than up to three times from different places, and this.yearOfMostRecentBiomassCalculation can be removed
            // if *not* called from establishment, clear the species-level-stats
            bool hasLeafArea = this.StatisticsLive.LeafAreaIndex > 0.0F;
            if (fromSaplingEstablishmentOrGrowth == false)
            {
                this.StatisticsLive.Zero();
            }

            // if already processed in this year, do not repeat
            // TODO: but if this is a no op call why can statistics still be zeroed above?
            if (this.yearOfMostRecentBiomassCalculation == model.SimulationState.CurrentCalendarYear)
            {
                return;
            }

            if (hasLeafArea || (fromSaplingEstablishmentOrGrowth == true))
            {
                // calculate environmental responses per species (vpd, temperature, ...)
                // assumes the water cycle is already updated for the current year
                this.TreeGrowth.Modifiers.CalculateMonthlyGrowthModifiers(model.Landscape);
                this.TreeGrowth.CalculateGppForYear(model.Project);// production of NPP
                this.yearOfMostRecentBiomassCalculation = model.SimulationState.CurrentCalendarYear;
            }
            else
            {
                // if no leaf area is present, then just clear the respones
                this.TreeGrowth.Modifiers.ZeroMonthlyAndAnnualModifiers();
                this.TreeGrowth.ZeroMonthlyAndAnnualValues();
                // TODO: why doesn't C++ update the year here?
            }
        }

        // TODO: remove unused API
        public float LeafAreaIndexSaplings()
        {
            return this.ResourceUnit.AreaInLandscapeInM2 > 0.0F ? this.SaplingStats.LeafArea / this.ResourceUnit.AreaInLandscapeInM2 : 0.0F;
        }
    }
}
