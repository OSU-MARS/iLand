namespace iLand.Core
{
    /** CNPair stores a duple of carbon and nitrogen (tons/ha)
        use addBiomass(biomass, cnratio) to add biomass; use operators (+, +=, *, *=) for simple operations. */
    public class CNPair
    {
        public double C { get; set; } // carbon pool
        public double N { get; set; } // nitrogen pool

        public CNPair()
        {
            C = 0.0;
            N = 0.0;
        }

        public CNPair(double carbonTonsHa, double nitrogenTonsHa) 
        { 
            C = carbonTonsHa; 
            N = nitrogenTonsHa; 
        }

        public bool IsEmpty() { return C == 0.0; } ///< returns true if pool is empty
        public bool IsValid() { return C >= 0.0 && N >= 0.0; } ///< return true if pool is valid (content of C or N >=0)
        public double CNratio() { return N > 0 ? C / N : 0.0; } ///< current CN ratio
                                                           /// retrieve the amount of biomass (kg/ha). Uses the global C-fraciton. Soil pools are in t/ha!!!
        public double Biomass() { return C / Constant.BiomassCFraction; }
        // some simple operators
        public static CNPair operator +(CNPair p1, CNPair p2) { return new CNPair(p1.C + p2.C, p1.N + p2.N); } ///< return the sum of two pools
        public static CNPair operator -(CNPair p1, CNPair p2) { return new CNPair(p1.C - p2.C, p1.N - p2.N); } ///< return the difference of two pools
        public static CNPair operator *(CNPair p, double factor) { return new CNPair(p.C * factor, p.N * factor); } ///< return the pool multiplied with 'factor'

        /// add biomass to the pool (kg dry mass/ha); CNratio is used to calculate the N-Content, the global C-Fraction of biomass is used to
        /// calculate the amount of carbon of 'biomass'.
        public void AddBiomass(double biomass, double cnRatio)
        { 
            this.C += Constant.BiomassCFraction * biomass; 
            this.N += Constant.BiomassCFraction * biomass / cnRatio;
        }

        public void Clear() 
        {
            C = 0.0; 
            N = 0.0; 
        }
    }
}
