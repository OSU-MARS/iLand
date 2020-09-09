using iLand.tools;

namespace iLand.core
{
    /** CNPair stores a duple of carbon and nitrogen (kg/ha)
        use addBiomass(biomass, cnratio) to add biomass; use operators (+, +=, *, *=) for simple operations. */
    internal class CNPair
    {
        public static double biomassCFraction = Constant.biomassCFraction; // TODO: remove dup

        public double C; // carbon pool (kg C/ha)
        public double N; // nitrogen pool (kg N/ha)

        public CNPair()
        {
            C = 0.0;
            N = 0.0;
        }

        public CNPair(double c, double n) { C = c; N = n; }

        public void clear() { C = 0.0; N = 0.0; }
        public static void setCFraction(double fraction) { biomassCFraction = fraction; } ///< set the global fraction of carbon of biomass
        public bool isEmpty() { return C == 0.0; } ///< returns true if pool is empty
        public bool isValid() { return C >= 0.0 && N >= 0.0; } ///< return true if pool is valid (content of C or N >=0)
        public double CN() { return N > 0 ? C / N : 0.0; } ///< current CN ratio
                                                           /// retrieve the amount of biomass (kg/ha). Uses the global C-fraciton. Soil pools are in t/ha!!!
        public double biomass() { return C / biomassCFraction; }
        /// add biomass to the pool (kg dry mass/ha); CNratio is used to calculate the N-Content, the global C-Fraction of biomass is used to
        /// calculate the amount of carbon of 'biomass'.
        public void addBiomass(double biomass, double CNratio) { C += biomass * biomassCFraction; N += biomass * biomassCFraction / CNratio; }
        // some simple operators
        public static CNPair operator +(CNPair p1, CNPair p2) { return new CNPair(p1.C + p2.C, p1.N + p2.N); } ///< return the sum of two pools
        public static CNPair operator -(CNPair p1, CNPair p2) { return new CNPair(p1.C - p2.C, p1.N - p2.N); } ///< return the difference of two pools
        public static CNPair operator *(CNPair p, double factor) { return new CNPair(p.C * factor, p.N * factor); } ///< return the pool multiplied with 'factor'
    }
}
