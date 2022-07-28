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
            : this(0.0F, 0.0F)
        {
        }

        public CarbonNitrogenTuple(float carbonTonsPerHa, float nitrogenTonsPerHa)
        {
            this.C = carbonTonsPerHa;
            this.N = nitrogenTonsPerHa; 
        }

        public bool HasNoCarbon() { return this.C == 0.0F; } // returns true if pool is empty
        public bool HasCarbonAndNitrogen() { return this.C >= 0.0F && N >= 0.0F; } // return true if pool is valid (content of C or N >=0)
        public float GetCNRatio() { return this.N > 0.0F ? this.C / this.N : 0.0F; } // current CN ratio
        // retrieve the amount of biomass (kg/ha). Uses the global C-fraction. Soil pools are in t/ha!!!
        public float GetBiomass() { return this.C / Constant.DryBiomassCarbonFraction; }
        
        public static CarbonNitrogenTuple operator +(CarbonNitrogenTuple tuple1, CarbonNitrogenTuple tuple2) 
        { 
            return new CarbonNitrogenTuple(tuple1.C + tuple2.C, tuple1.N + tuple2.N); 
        }
        public static CarbonNitrogenTuple operator -(CarbonNitrogenTuple tuple1, CarbonNitrogenTuple tuple2)
        { 
            return new CarbonNitrogenTuple(tuple1.C - tuple2.C, tuple1.N - tuple2.N); 
        }
        public static CarbonNitrogenTuple operator *(CarbonNitrogenTuple tuple, float factor) 
        { 
            return new CarbonNitrogenTuple(tuple.C * factor, tuple.N * factor); 
        } 

        /// add biomass to the pool (kg dry mass/ha); CNratio is used to calculate the N-Content, the global C-Fraction of biomass is used to
        /// calculate the amount of carbon of 'biomass'.
        public void AddBiomass(float biomass, float cnRatio)
        { 
            this.C += Constant.DryBiomassCarbonFraction * biomass; 
            this.N += Constant.DryBiomassCarbonFraction * biomass / cnRatio;
        }

        public void Zero() 
        {
            this.C = 0.0F; 
            this.N = 0.0F; 
        }
    }
}
