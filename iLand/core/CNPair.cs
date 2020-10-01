namespace iLand.Core
{
    /** CNPair stores a duple of carbon and nitrogen (kg/ha)
        use addBiomass(biomass, cnratio) to add biomass; use operators (+, +=, *, *=) for simple operations. */
    public class CNPair
    {
        ///< set the global fraction of carbon of biomass
        public static double BiomassCFraction { get; set; }

        public double C; // carbon pool (kg C/ha)
        public double N; // nitrogen pool (kg N/ha)

        static CNPair()
        {
            CNPair.BiomassCFraction = Constant.BiomassCFraction;
        }

        public CNPair()
        {
            C = 0.0;
            N = 0.0;
        }

        public CNPair(double c, double n) { C = c; N = n; }

        public bool IsEmpty() { return C == 0.0; } ///< returns true if pool is empty
        public bool IsValid() { return C >= 0.0 && N >= 0.0; } ///< return true if pool is valid (content of C or N >=0)
        public double CNratio() { return N > 0 ? C / N : 0.0; } ///< current CN ratio
                                                           /// retrieve the amount of biomass (kg/ha). Uses the global C-fraciton. Soil pools are in t/ha!!!
        public double Biomass() { return C / BiomassCFraction; }
        // some simple operators
        public static CNPair operator +(CNPair p1, CNPair p2) { return new CNPair(p1.C + p2.C, p1.N + p2.N); } ///< return the sum of two pools
        public static CNPair operator -(CNPair p1, CNPair p2) { return new CNPair(p1.C - p2.C, p1.N - p2.N); } ///< return the difference of two pools
        public static CNPair operator *(CNPair p, double factor) { return new CNPair(p.C * factor, p.N * factor); } ///< return the pool multiplied with 'factor'

        /// add biomass to the pool (kg dry mass/ha); CNratio is used to calculate the N-Content, the global C-Fraction of biomass is used to
        /// calculate the amount of carbon of 'biomass'.
        public void AddBiomass(double biomass, double CNratio)
        { 
            this.C += biomass * BiomassCFraction; 
            this.N += biomass * BiomassCFraction / CNratio;
        }

        public void Clear() 
        {
            C = 0.0; 
            N = 0.0; 
        }
    }
}
