using iLand.Tools;
using System.Diagnostics;

namespace iLand.Core
{
    /** @class ResourceUnitSpecies
      @ingroup core
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
        private int mLastYear;

        public Production3PG BiomassGrowth { get; private set; } ///< the 3pg production model of this species x resourceunit
        public Establishment Establishment { get; private set; } ///< establishment submodel
        /// relative fraction of LAI of this species (0..1) (if total LAI on resource unit is >= 1, then the sum of all LAIfactors of all species = 1)
        public double LaiFraction { get; private set; }
        public double RemovedVolume { get; private set; } ///< sum of volume with was remvoved because of death/management (m3/ha)
        public SpeciesResponse Response { get; private set; }
        public ResourceUnit RU { get; private set; } ///< return pointer to resource unit
        public SaplingStat SaplingStats { get; private set; } ///< statistics for the sapling sub module
        public Species Species { get; private set; } ///< return pointer to species
        public StandStatistics Statistics { get; private set; } ///< statistics of this species on the resourceunit
        public StandStatistics StatisticsDead { get; private set; } ///< statistics of died trees
        public StandStatistics StatisticsMgmt { get; private set; } ///< statistics of removed trees
        
        public void SetLaiFactor(double laiFraction)
        {
            if (laiFraction < 0 || laiFraction > 1.00001)
            {
                Debug.WriteLine("invalid LAIfactor " + LaiFraction);
            }
            this.LaiFraction = laiFraction;
        }

        public ResourceUnitSpecies(Species species, ResourceUnit ru)
        {
            this.BiomassGrowth = new Production3PG();
            this.Establishment = new Establishment();
            this.mLastYear = -1;
            this.RemovedVolume = 0.0;
            this.Response = new SpeciesResponse();
            this.RU = ru;
            this.SaplingStats = new SaplingStat();
            this.Species = species;
            this.Statistics = new StandStatistics();
            this.StatisticsDead = new StandStatistics();
            this.StatisticsMgmt = new StandStatistics();

            this.BiomassGrowth.SpeciesResponse = this.Response;
            this.Establishment.Setup(ru.Climate, this);
            this.Response.Setup(this);
            this.Statistics.ResourceUnitSpecies = this;
            this.StatisticsDead.ResourceUnitSpecies = this;
            this.StatisticsMgmt.ResourceUnitSpecies = this;

            Debug.WriteLineIf(Species.Index > 1000 || Species.Index < 0, "suspicious species?? in RUS::setup()");
        }

        public void Calculate(bool fromEstablishment = false)
        {
            // if *not* called from establishment, clear the species-level-stats
            if (!fromEstablishment)
            {
                Statistics.Clear();
            }

            // if already processed in this year, do not repeat
            if (mLastYear == GlobalSettings.Instance.CurrentYear)
            {
                return;
            }

            if (LaiFraction > 0.0 || fromEstablishment == true)
            {
                // execute the water calculation...
                if (fromEstablishment)
                {
                    RU.WaterCycle.Run(); // run the water sub model (only if this has not be done already)
                }
                using DebugTimer rst = new DebugTimer("ResourceUnitSpecies.Calculate(Response + BiomassGrowth)");
                Response.Calculate();// calculate environmental responses per species (vpd, temperature, ...)
                BiomassGrowth.Calculate();// production of NPP
                mLastYear = GlobalSettings.Instance.CurrentYear; // mark this year as processed
            }
            else
            {
                // if no LAI is present, then just clear the respones.
                Response.Clear();
                BiomassGrowth.Clear();
            }
        }

        public double LeafArea()
        {
            // Leaf area of the species:
            // total leaf area on the RU * fraction of leafarea
            return LaiFraction * RU.LeafAreaIndex();
        }

        public void UpdateGwl()
        {
            // removed growth is the running sum of all removed
            // tree volume. the current "GWL" therefore is current volume (standing) + mRemovedGrowth.
            // important: statisticsDead() and statisticsMgmt() need to calculate() before -> volume() is already scaled to ha
            RemovedVolume += StatisticsDead.Volume + StatisticsMgmt.Volume;
        }
    }
}
