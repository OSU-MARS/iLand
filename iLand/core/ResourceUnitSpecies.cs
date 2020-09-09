using iLand.tools;
using System.Diagnostics;

namespace iLand.core
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
    internal class ResourceUnitSpecies
    {
        private double mLAIfactor; ///< relative amount of this species' LAI on this resource unit (0..1). Is calculated once a year.
        private double mRemovedGrowth; ///< m3 volume of trees removed/managed (to calculate GWL) (m3/ha)
        private StandStatistics mStatistics; ///< statistics of a species on this resource unit
        private StandStatistics mStatisticsDead; ///< statistics of died trees (this year) of a species on this resource unit
        private StandStatistics mStatisticsMgmt; ///< statistics of removed trees (this year) of a species on this resource unit
        private Production3PG m3PG; ///< NPP prodution unit of this species
        private SpeciesResponse mResponse; ///< calculation and storage of species specific respones on this resource unit
        private Establishment mEstablishment; ///< establishment for seedlings and sapling growth
        private SaplingStat mSaplingStat; ///< statistics on saplings
        private Species mSpecies; ///< link to speices
        private ResourceUnit mRU; ///< link to resource unit
        private int mLastYear;

        // access
        public SpeciesResponse speciesResponse() { return mResponse; }
        public Species species() { return mSpecies; } ///< return pointer to species
        public ResourceUnit ru() { return mRU; } ///< return pointer to resource unit
        public Production3PG prod3PG() { return m3PG; } ///< the 3pg production model of this speies x resourceunit

        public SaplingStat saplingStat() { return mSaplingStat; } ///< statistics for the sapling sub module
        public SaplingStat constSaplingStat() { return mSaplingStat; } ///< statistics for the sapling sub module

        public Establishment establishment() { return mEstablishment; } ///< establishment submodel
        public StandStatistics statistics() { return mStatistics; } ///< statistics of this species on the resourceunit
        public StandStatistics statisticsDead() { return mStatisticsDead; } ///< statistics of died trees
        public StandStatistics statisticsMgmt() { return mStatisticsMgmt; } ///< statistics of removed trees
        public StandStatistics constStatistics() { return mStatistics; } ///< accessor
        public StandStatistics constStatisticsDead() { return mStatisticsDead; } ///< accessor
        public StandStatistics constStatisticsMgmt() { return mStatisticsMgmt; } ///< accessor

        // actions
        public double removedVolume() { return mRemovedGrowth; } ///< sum of volume with was remvoved because of death/management (m3/ha)
                                                                 /// relative fraction of LAI of this species (0..1) (if total LAI on resource unit is >= 1, then the sum of all LAIfactors of all species = 1)
        public double LAIfactor() { return mLAIfactor; }
        public void setLAIfactor(double newLAIfraction)
        {
            mLAIfactor = newLAIfraction;
            if (mLAIfactor < 0 || mLAIfactor > 1.00001)
            {
                Debug.WriteLine("invalid LAIfactor " + mLAIfactor);
            }
        }

        public double leafArea()
        {
            // Leaf area of the species:
            // total leaf area on the RU * fraction of leafarea
            return mLAIfactor * ru().leafAreaIndex();
        }

        public void setup(Species species, ResourceUnit ru)
        {
            mSpecies = species;
            mRU = ru;
            mResponse.setup(this);
            m3PG.setResponse(mResponse);
            mEstablishment.setup(ru.climate(), this);
            mStatistics.setResourceUnitSpecies(this);
            mStatisticsDead.setResourceUnitSpecies(this);
            mStatisticsMgmt.setResourceUnitSpecies(this);

            mRemovedGrowth = 0.0;
            mLastYear = -1;

            Debug.WriteLineIf(mSpecies.index() > 1000 || mSpecies.index() < 0, "suspicious species?? in RUS::setup()");
        }

        public void calculate(bool fromEstablishment = false)
        {
            // if *not* called from establishment, clear the species-level-stats
            if (!fromEstablishment)
            {
                statistics().clear();
            }

            // if already processed in this year, do not repeat
            if (mLastYear == GlobalSettings.instance().currentYear())
            {
                return;
            }

            if (mLAIfactor > 0.0 || fromEstablishment == true)
            {
                // execute the water calculation...
                if (fromEstablishment)
                {
                    mRU.waterCycle().run(); // run the water sub model (only if this has not be done already)
                }
                using DebugTimer rst = new DebugTimer("response+3pg");
                mResponse.calculate();// calculate environmental responses per species (vpd, temperature, ...)
                m3PG.calculate();// production of NPP
                mLastYear = GlobalSettings.instance().currentYear(); // mark this year as processed
            }
            else
            {
                // if no LAI is present, then just clear the respones.
                mResponse.clear();
                m3PG.clear();
            }
        }

        public void updateGWL()
        {
            // removed growth is the running sum of all removed
            // tree volume. the current "GWL" therefore is current volume (standing) + mRemovedGrowth.
            // important: statisticsDead() and statisticsMgmt() need to calculate() before -> volume() is already scaled to ha
            mRemovedGrowth += statisticsDead().volume() + statisticsMgmt().volume();
        }
    }
}
