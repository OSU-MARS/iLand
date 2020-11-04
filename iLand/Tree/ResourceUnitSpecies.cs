using iLand.Input.ProjectFile;
using iLand.Simulation;
using iLand.World;
using System;
using System.Diagnostics;

namespace iLand.Tree
{
    /** @class ResourceUnitSpecies
        The class contains data available at ResourceUnit x Species scale.
        Data stored is either statistical (i.e. number of trees per species) or used
        within the model (e.g. fraction of utilizable Radiation).
        Important submodules are:
        * 3PG production (Production3PG)
        * Establishment
        * Growth and Recruitment of Saplings
        * Snag dynamics
      */
    public class ResourceUnitSpecies
    {
        public ResourceUnitSpeciesGrowth BiomassGrowth { get; private set; } // the 3pg production model of this species x resourceunit
        public Establishment Establishment { get; private set; } // establishment submodel
        /// relative fraction of LAI of this species (0..1) (if total LAI on resource unit is >= 1, then the sum of all LAIfactors of all species = 1)
        public float LaiFraction { get; private set; }
        public double RemovedStemVolume { get; private set; } // sum of volume with was remvoved because of death/management (m3/ha)
        public ResourceUnitSpeciesResponse Response { get; private set; }
        public ResourceUnit RU { get; private set; } // return pointer to resource unit
        public SaplingProperties SaplingStats { get; private set; } // statistics for the sapling sub module
        public TreeSpecies Species { get; private set; } // return pointer to species
        public ResourceUnitSpeciesStatistics Statistics { get; private set; } // statistics of this species on the resource unit
        public ResourceUnitSpeciesStatistics StatisticsDead { get; private set; } // statistics of trees that have died
        public ResourceUnitSpeciesStatistics StatisticsManagement { get; private set; } // statistics of removed trees
        
        public ResourceUnitSpecies(TreeSpecies treeSpecies, ResourceUnit ru)
        {
            if ((treeSpecies.Index < 0) || (treeSpecies.Index > 1000))
            {
                throw new ArgumentOutOfRangeException(nameof(treeSpecies), "Implausible tree species index.");
            }
            this.RU = ru;
            this.Species = treeSpecies;

            this.BiomassGrowth = new ResourceUnitSpeciesGrowth();
            this.Establishment = new Establishment(ru.Climate, this); // requires this.Species and this.RU be set
            this.RemovedStemVolume = 0.0;
            this.Response = new ResourceUnitSpeciesResponse();
            this.SaplingStats = new SaplingProperties();
            this.Statistics = new ResourceUnitSpeciesStatistics();
            this.StatisticsDead = new ResourceUnitSpeciesStatistics();
            this.StatisticsManagement = new ResourceUnitSpeciesStatistics();

            this.BiomassGrowth.SpeciesResponse = this.Response;
            this.Response.Setup(this);
            this.Statistics.ResourceUnitSpecies = this;
            this.StatisticsDead.ResourceUnitSpecies = this;
            this.StatisticsManagement.ResourceUnitSpecies = this;
        }

        public void SetRULaiFraction(float laiFraction)
        {
            if (laiFraction < 0.0F || laiFraction > 1.00001F)
            {
                throw new ArgumentOutOfRangeException("Invalid LAI fraction " + laiFraction + ".");
            }
            this.LaiFraction = laiFraction;
        }

        public void CalculateBiomassGrowthForYear(Project projectFile, bool fromEstablishment = false)
        {
            // if *not* called from establishment, clear the species-level-stats
            if (fromEstablishment == false)
            {
                this.Statistics.Zero();
            }

            if (this.LaiFraction > 0.0F || fromEstablishment == true)
            {
                // assumes the water cycle is already updated for the current year
                //using DebugTimer rst = model.DebugTimers.Create("ResourceUnitSpecies.Calculate(Response + BiomassGrowth)");
                this.Response.CalculateUtilizableRadiation(this.RU.Climate);// calculate environmental responses per species (vpd, temperature, ...)
                this.BiomassGrowth.CalculateGppForYear(projectFile);// production of NPP
            }
            else
            {
                // if no leaf area is present, then just clear the respones.
                this.Response.Zero();
                this.BiomassGrowth.Zero();
            }
        }

        public double LeafArea()
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
            this.RemovedStemVolume += this.StatisticsDead.StemVolume + this.StatisticsManagement.StemVolume;
        }
    }
}
