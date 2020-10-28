namespace iLand.World
{
    /// <summary>
    /// Provides (in addition to <see cref="CarbonNitrogenPool"/>) also a decay rate.
    /// </summary>
    public class CarbonNitrogenPool : CarbonNitrogenTuple
    {
        public float DecompositionRate { get; set; } // get weighting parameter

        public CarbonNitrogenPool()
            : base()
        {
            this.DecompositionRate = 0.0F;
        }

        public CarbonNitrogenPool(float carbonTonsPerHa, float nitrogenTonsPerHa, float decompositionRate)
            : base(carbonTonsPerHa, nitrogenTonsPerHa)
        {
            this.DecompositionRate = decompositionRate;
        }

        public new void Zero()
        {
            base.Zero();
            this.DecompositionRate = 0.0F;
        }

        public void Add(CarbonNitrogenTuple cnTuple, float decompositionRate)
        {
            CarbonNitrogenPool pool = new CarbonNitrogenPool() { C = cnTuple.C, N = cnTuple.N, DecompositionRate = decompositionRate };
            this.DecompositionRate = this.GetWeightedDecomposiitonRate(pool);
            this.C += cnTuple.C;
            this.N += cnTuple.N;
        } // convenience function

        // increase pool (and weight the value)
        public static CarbonNitrogenPool operator +(CarbonNitrogenPool pool1, CarbonNitrogenPool pool2)
        {
            CarbonNitrogenPool sum = new CarbonNitrogenPool() { C = pool1.C, N = pool1.N, DecompositionRate = pool1.DecompositionRate };
            sum.GetWeightedDecomposiitonRate(pool2);
            sum.C += pool2.C;
            sum.N += pool2.N;
            return sum;
        }

        // return the pool multiplied with 'factor'
        public static CarbonNitrogenPool operator *(CarbonNitrogenPool pool, float cnFactor)
        {
            return new CarbonNitrogenPool()
            {
                C = pool.C * cnFactor,
                N = pool.N * cnFactor,
                DecompositionRate = pool.DecompositionRate
            };
        }

        /// add biomass and weigh the parameter_value with the current C-content of the pool
        /// add biomass with a specific 'CNRatio' and 'parameter_value'
        public void AddBiomass(float biomass, float cnRatio, float decompositionRate)
        {
            if (biomass == 0.0)
            {
                return;
            }
            float newC = biomass * Constant.BiomassCFraction;
            float thisCfraction = this.C / (newC + this.C);
            this.DecompositionRate = this.DecompositionRate * thisCfraction + decompositionRate * (1.0F - thisCfraction);
            base.AddBiomass(biomass, cnRatio);
        }

        // 'simulate' weighting (get weighted param value of 's' with the current content)
        public float GetWeightedDecomposiitonRate(CarbonNitrogenPool other)
        {
            if (other.C == 0.0F)
            {
                return DecompositionRate;
            }
            float thisCarbonFraction = this.C / (other.C + this.C);
            float result = this.DecompositionRate * thisCarbonFraction + other.DecompositionRate * (1.0F - thisCarbonFraction);
            return result;
        }
    }
}
