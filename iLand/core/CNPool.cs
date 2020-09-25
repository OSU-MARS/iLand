namespace iLand.core
{
    /** CNPool provides (in addition to CNPair) also a weighted parameter value (e.g. a decay rate) */
    internal class CNPool : CNPair
    {
        public double Parameter { get; set; } ///< get weighting parameter

        public CNPool()
        {
            Parameter = 0.0;
        }

        public CNPool(double c, double n, double param_value)
        {
            C = c; 
            N = n; 
            Parameter = param_value;
        }

        public new void Clear()
        {
            base.Clear();
            Parameter = 0.0;
        }

        public void Add(CNPair s, double parameter_value)
        {
            CNPool pool = new CNPool() { C = s.C, N = s.N, Parameter = parameter_value };
            this.Parameter = this.GetWeightedParameter(pool);
            this.C += s.C;
            this.N += s.N;
        } ///< convenience function

        // increase pool (and weight the value)
        public static CNPool operator +(CNPool p1, CNPool p2)
        {
            CNPool pool = new CNPool() { C = p1.C, N = p1.N, Parameter = p1.Parameter };
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
                Parameter = pool.Parameter
            };
        }

        /// add biomass and weigh the parameter_value with the current C-content of the pool
        /// add biomass with a specific 'CNRatio' and 'parameter_value'
        public void AddBiomass(double biomass, double CNratio, double parameter_value)
        {
            if (biomass == 0.0)
            {
                return;
            }
            double new_c = biomass * BiomassCFraction;
            double p_old = C / (new_c + C);
            Parameter = Parameter * p_old + parameter_value * (1.0 - p_old);
            base.AddBiomass(biomass, CNratio);
        }

        ///< 'simulate' weighting (get weighted param value of 's' with the current content)
        public double GetWeightedParameter(CNPool s)
        {
            if (s.C == 0.0)
            {
                return Parameter;
            }
            double p_old = C / (s.C + C);
            double result = Parameter * p_old + s.Parameter * (1.0 - p_old);
            return result;
        }
    }
}
