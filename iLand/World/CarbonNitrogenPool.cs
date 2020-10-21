namespace iLand.World
{
    /// <summary>
    /// Provides (in addition to <see cref="CarbonNitrogenPool"/>) also a decay rate.
    /// </summary>
    public class CarbonNitrogenPool : CarbonNitrogenTuple
    {
        public float DecompositionRate { get; set; } // get weighting parameter

        public CarbonNitrogenPool()
        {
            this.C = 0.0F;
            this.N = 0.0F;
            this.DecompositionRate = 0.0F;
        }

        public CarbonNitrogenPool(float c, float n, float weight)
        {
            this.C = c;
            this.N = n;
            this.DecompositionRate = weight;
        }

        public new void Clear()
        {
            base.Clear();
            this.DecompositionRate = 0.0F;
        }

        public void Add(CarbonNitrogenTuple s, float parameter_value)
        {
            CarbonNitrogenPool pool = new CarbonNitrogenPool() { C = s.C, N = s.N, DecompositionRate = parameter_value };
            this.DecompositionRate = this.GetWeightedParameter(pool);
            this.C += s.C;
            this.N += s.N;
        } // convenience function

        // increase pool (and weight the value)
        public static CarbonNitrogenPool operator +(CarbonNitrogenPool p1, CarbonNitrogenPool p2)
        {
            CarbonNitrogenPool pool = new CarbonNitrogenPool() { C = p1.C, N = p1.N, DecompositionRate = p1.DecompositionRate };
            pool.GetWeightedParameter(p2);
            pool.C += p2.C;
            pool.N += p2.N;
            return pool;
        }

        // return the pool multiplied with 'factor'
        public static CarbonNitrogenPool operator *(CarbonNitrogenPool pool, float factor)
        {
            return new CarbonNitrogenPool()
            {
                C = pool.C * factor,
                N = pool.N * factor,
                DecompositionRate = pool.DecompositionRate
            };
        }

        /// add biomass and weigh the parameter_value with the current C-content of the pool
        /// add biomass with a specific 'CNRatio' and 'parameter_value'
        public void AddBiomass(float biomass, float CNratio, float decompositionRate)
        {
            if (biomass == 0.0)
            {
                return;
            }
            float newC = biomass * Constant.BiomassCFraction;
            float oldCfraction = C / (newC + C);
            this.DecompositionRate = this.DecompositionRate * oldCfraction + decompositionRate * (1.0F - oldCfraction);
            base.AddBiomass(biomass, CNratio);
        }

        // 'simulate' weighting (get weighted param value of 's' with the current content)
        public float GetWeightedParameter(CarbonNitrogenPool s)
        {
            if (s.C == 0.0F)
            {
                return DecompositionRate;
            }
            float p_old = C / (s.C + C);
            float result = DecompositionRate * p_old + s.DecompositionRate * (1.0F - p_old);
            return result;
        }
    }
}
