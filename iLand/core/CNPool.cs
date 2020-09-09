namespace iLand.core
{
    /** CNPool provides (in addition to CNPair) also a weighted parameter value (e.g. a decay rate) */
    internal class CNPool : CNPair
    {
        private double mParameter;

        public double parameter() { return mParameter; } ///< get weighting parameter
        public void setParameter(double value) { mParameter = value; }

        public CNPool()
        {
            mParameter = 0.0;
        }

        public CNPool(double c, double n, double param_value)
        {
            C = c; 
            N = n; 
            mParameter = param_value;
        }

        public new void clear()
        {
            base.clear();
            mParameter = 0.0;
        }

        public void add(CNPair s, double parameter_value)
        {
            CNPool pool = new CNPool() { C = s.C, N = s.N, mParameter = parameter_value };
            this.mParameter = this.parameter(pool);
            this.C += s.C;
            this.N += s.N;
        } ///< convenience function

        // increase pool (and weight the value)
        public static CNPool operator +(CNPool p1, CNPool p2)
        {
            CNPool pool = new CNPool() { C = p1.C, N = p1.N, mParameter = p1.mParameter };
            pool.parameter(p2);
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
                mParameter = pool.mParameter
            };
        }

        /// add biomass and weigh the parameter_value with the current C-content of the pool
        /// add biomass with a specific 'CNRatio' and 'parameter_value'
        public void addBiomass(double biomass, double CNratio, double parameter_value)
        {
            if (biomass == 0.0)
            {
                return;
            }
            double new_c = biomass * biomassCFraction;
            double p_old = C / (new_c + C);
            mParameter = mParameter * p_old + parameter_value * (1.0 - p_old);
            base.addBiomass(biomass, CNratio);
        }

        ///< 'simulate' weighting (get weighted param value of 's' with the current content)
        public double parameter(CNPool s)
        {
            if (s.C == 0.0)
            {
                return parameter();
            }
            double p_old = C / (s.C + C);
            double result = mParameter * p_old + s.parameter() * (1.0 - p_old);
            return result;
        }
    }
}
