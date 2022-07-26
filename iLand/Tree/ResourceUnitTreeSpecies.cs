using iLand.Input.ProjectFile;
using iLand.World;
using System;

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
        /// relative fraction of LAI of this species (0..1) (if total LAI on resource unit is >= 1, then the sum of all LAIfactors of all species = 1)
        public float LaiFraction { get; private set; }
        public float RemovedStemVolume { get; private set; } // sum of volume with was remvoved because of death/management (m3/ha)
        public ResourceUnit RU { get; private init; } // return pointer to resource unit
        public SaplingEstablishment SaplingEstablishment { get; private init; } // establishment submodel
        public SaplingProperties SaplingStats { get; private init; } // statistics for the sapling sub module
        public TreeSpecies Species { get; private init; } // return pointer to species
        public ResourceUnitTreeStatistics StatisticsLive { get; private init; } // statistics of this species on the resource unit
        public ResourceUnitTreeStatistics StatisticsManagement { get; private init; } // statistics of removed trees
        public ResourceUnitTreeStatistics StatisticsSnag { get; private init; } // statistics of trees that have died
        public ResourceUnitTreeSpeciesGrowth TreeGrowth { get; private init; } // the 3-PG production model of this species on this resource unit

        public ResourceUnitTreeSpecies(TreeSpecies treeSpecies, ResourceUnit ru)
        {
            if ((treeSpecies.Index < 0) || (treeSpecies.Index > 1000))
            {
                throw new ArgumentOutOfRangeException(nameof(treeSpecies), "Implausible tree species index.");
            }
            this.RU = ru;
            this.Species = treeSpecies;

            this.RemovedStemVolume = 0.0F;
            this.SaplingEstablishment = new SaplingEstablishment();
            this.SaplingStats = new SaplingProperties();
            this.StatisticsLive = new ResourceUnitTreeStatistics(ru, this);
            this.StatisticsManagement = new ResourceUnitTreeStatistics(ru, this);
            this.StatisticsSnag = new ResourceUnitTreeStatistics(ru, this);
            this.TreeGrowth = new ResourceUnitTreeSpeciesGrowth(ru, this); // requires this.Species be set
        }

        public void SetRULaiFraction(float laiFraction)
        {
            if (laiFraction < 0.0F || laiFraction > 1.00001F)
            {
                throw new ArgumentOutOfRangeException("Invalid LAI fraction " + laiFraction + ".");
            }
            this.LaiFraction = laiFraction;
        }

        public void CalculateBiomassGrowthForYear(Project projectFile, bool fromSaplingEstablishmentOrGrowth = false)
        {
            // if *not* called from establishment, clear the species-level-stats
            if (fromSaplingEstablishmentOrGrowth == false)
            {
                this.StatisticsLive.Zero();
            }

            if ((this.LaiFraction > 0.0F) || (fromSaplingEstablishmentOrGrowth == true))
            {
                // assumes the water cycle is already updated for the current year
                this.TreeGrowth.Modifiers.CalculateMonthlyGrowthModifiers(this.RU.Weather);// calculate environmental responses per species (vpd, temperature, ...)
                this.TreeGrowth.CalculateGppForYear(projectFile);// production of NPP
            }
            else
            {
                // if no leaf area is present, then just clear the respones
                this.TreeGrowth.Modifiers.ZeroMonthlyAndAnnualModifiers();
                this.TreeGrowth.ZeroMonthlyAndAnnualValues();
            }
        }

        public float LeafArea()
        {
            // Leaf area of the species:
            // total leaf area on the RU * fraction of leafarea
            return this.LaiFraction * this.RU.GetLeafAreaIndex();
        }

        public void UpdateGwl()
        {
            // removed growth is the running sum of all removed
            // tree volume. the current "GWL" therefore is current volume (standing) + mRemovedGrowth.
            // important: statisticsDead() and statisticsMgmt() need to calculate() before -> volume() is already scaled to ha
            this.RemovedStemVolume += this.StatisticsSnag.StemVolume + this.StatisticsManagement.StemVolume;
        }
    }
}
