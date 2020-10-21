namespace iLand.World
{
    /// <summary>
    /// Stores a duple of carbon and nitrogen (tons/ha).
    /// </summary>
    /// <remarks>
    /// Use AddBiomass(biomass, cnratio) to add biomass, use operators(+, +=, *, *=) for simple operations.
    /// </remarks>
    public class CarbonNitrogenTuple
    {
        public float C { get; set; } // carbon pool
        public float N { get; set; } // nitrogen pool

        public CarbonNitrogenTuple()
        {
            C = 0.0F;
            N = 0.0F;
        }

        public CarbonNitrogenTuple(float carbonTonsHa, float nitrogenTonsHa) 
        { 
            C = carbonTonsHa; 
            N = nitrogenTonsHa; 
        }

        public bool IsEmpty() { return C == 0.0F; } // returns true if pool is empty
        public bool IsValid() { return C >= 0.0F && N >= 0.0F; } // return true if pool is valid (content of C or N >=0)
        public float CNratio() { return N > 0.0F ? C / N : 0.0F; } // current CN ratio
                                                           /// retrieve the amount of biomass (kg/ha). Uses the global C-fraciton. Soil pools are in t/ha!!!
        public float Biomass() { return C / Constant.BiomassCFraction; }
        // some simple operators
        public static CarbonNitrogenTuple operator +(CarbonNitrogenTuple p1, CarbonNitrogenTuple p2) { return new CarbonNitrogenTuple(p1.C + p2.C, p1.N + p2.N); } // return the sum of two pools
        public static CarbonNitrogenTuple operator -(CarbonNitrogenTuple p1, CarbonNitrogenTuple p2) { return new CarbonNitrogenTuple(p1.C - p2.C, p1.N - p2.N); } // return the difference of two pools
        public static CarbonNitrogenTuple operator *(CarbonNitrogenTuple p, float factor) { return new CarbonNitrogenTuple(p.C * factor, p.N * factor); } // return the pool multiplied with 'factor'

        /// add biomass to the pool (kg dry mass/ha); CNratio is used to calculate the N-Content, the global C-Fraction of biomass is used to
        /// calculate the amount of carbon of 'biomass'.
        public void AddBiomass(float biomass, float cnRatio)
        { 
            this.C += Constant.BiomassCFraction * biomass; 
            this.N += Constant.BiomassCFraction * biomass / cnRatio;
        }

        public void Clear() 
        {
            C = 0.0F; 
            N = 0.0F; 
        }
    }
}
