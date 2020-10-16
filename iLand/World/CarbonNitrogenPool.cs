namespace iLand.World
{
    /// <summary>
    /// Provides (in addition to <see cref="CarbonNitrogenPool"/>) also a decay rate.
    /// </summary>
    public class CarbonNitrogenPool : CarbonNitrogenTuple
    {
        public double DecompositionRate { get; set; } // get weighting parameter

        public CarbonNitrogenPool()
        {
            this.C = 0.0;
            this.N = 0.0;
            this.DecompositionRate = 0.0;
        }

        public CarbonNitrogenPool(double c, double n, double weight)
        {
            this.C = c;
            this.N = n;
            this.DecompositionRate = weight;
        }

        public new void Clear()
        {
            base.Clear();
            this.DecompositionRate = 0.0;
        }

        public void Add(CarbonNitrogenTuple s, double parameter_value)
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
        public static CarbonNitrogenPool operator *(CarbonNitrogenPool pool, double factor)
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
        public void AddBiomass(double biomass, double CNratio, double decompositionRate)
        {
            if (biomass == 0.0)
            {
                return;
            }
            double newC = biomass * Constant.BiomassCFraction;
            double oldCfraction = C / (newC + C);
            DecompositionRate = DecompositionRate * oldCfraction + decompositionRate * (1.0 - oldCfraction);
            base.AddBiomass(biomass, CNratio);
        }

        // 'simulate' weighting (get weighted param value of 's' with the current content)
        public double GetWeightedParameter(CarbonNitrogenPool s)
        {
            if (s.C == 0.0)
            {
                return DecompositionRate;
            }
            double p_old = C / (s.C + C);
            double result = DecompositionRate * p_old + s.DecompositionRate * (1.0 - p_old);
            return result;
        }
    }
}
