using iLand.core;

namespace iLand.abe
{
    internal class SSpeciesStand
    {
        public Species species; ///< the ID of the species (ie a pointer)
        public double basalArea; ///< basal area m2
        public double relBasalArea; ///< fraction [0..1] fraction of species based on basal area.

        public SSpeciesStand()
        {
            species = null;
            basalArea = 0.0;
            relBasalArea = 0.0;
        }
    }
}
