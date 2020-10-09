namespace iLand.Core
{
    /** CNPool provides (in addition to CNPair) also a weighted parameter value (e.g. a decay rate) */
    public class CNPool : CNPair
    {
        public double Weight { get; set; } ///< get weighting parameter

        public CNPool()
        {
            this.C = 0.0;
            this.N = 0.0;
            this.Weight = 0.0;
        }

        public CNPool(double c, double n, double weight)
        {
            this.C = c;
            this.N = n;
            this.Weight = weight;
        }

        public new void Clear()
        {
            base.Clear();
            this.Weight = 0.0;
        }

        public void Add(CNPair s, double parameter_value)
        {
            CNPool pool = new CNPool() { C = s.C, N = s.N, Weight = parameter_value };
            this.Weight = this.GetWeightedParameter(pool);
            this.C += s.C;
            this.N += s.N;
        } ///< convenience function

        // increase pool (and weight the value)
        public static CNPool operator +(CNPool p1, CNPool p2)
        {
            CNPool pool = new CNPool() { C = p1.C, N = p1.N, Weight = p1.Weight };
            pool.GetWeightedParameter(p2);
            pool.C += p2.C;
            pool.N += p2.N;
            return pool;
        }

        ///< return the pool multiplied with 'factor'
        public static CNPool operator *(CNPool pool, double factor)
        {
            return new CNPool()
            {
                C = pool.C * factor,
                N = pool.N * factor,
                Weight = pool.Weight
            };
        }

        /// add biomass and weigh the parameter_value with the current C-content of the pool
        /// add biomass with a specific 'CNRatio' and 'parameter_value'
        public void AddBiomass(double biomass, double CNratio, double parameterValue)
        {
            if (biomass == 0.0)
            {
                return;
            }
            double newC = biomass * Constant.BiomassCFraction;
            double oldCfraction = C / (newC + C);
            Weight = Weight * oldCfraction + parameterValue * (1.0 - oldCfraction);
            base.AddBiomass(biomass, CNratio);
        }

        ///< 'simulate' weighting (get weighted param value of 's' with the current content)
        public double GetWeightedParameter(CNPool s)
        {
            if (s.C == 0.0)
            {
                return Weight;
            }
            double p_old = C / (s.C + C);
            double result = Weight * p_old + s.Weight * (1.0 - p_old);
            return result;
        }
    }
}
