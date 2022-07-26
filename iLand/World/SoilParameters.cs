namespace iLand.World
{
    // site-specific soil parameters specified in the environment file
    // Note that leaching is not actually influencing soil dynamics but reduces availability of N to plants by assuming that some N
    // (proportional to its mineralization in the mineral soil horizon) is leached
    // see separate wiki-page (http://iland-model.org/soil+parametrization+and+initialization)
    // and R-script on parameter estimation and initialization
    public class SoilParameters
    {
        public float AnnualNitrogenDeposition { get; set; } // kg N/ha-yr
        public float El { get; set; } // microbal efficiency in the labile pool, auxiliary parameter (see parameterization example)
        public float Er { get; set; } // microbal efficiency in the refractory pool, auxiliary parameter (see parameterization example)
        public float Hc { get; set; } // humification rate
        public float Ko { get; set; } // decomposition rate for soil organic matter (ICBM/2N "old" pool)
        public float Kyl { get; set; } // litter decomposition rate
        public float Kyr { get; set; } // downed woody debris (DWD) decomposition rate
        public float NitrogenLeachingFraction { get; set; } // fraction of mineralized nitrogen in the O layer no longer available for plants due to being leached away
        public float Qb { get; set; } // C/N ratio of soil microbes
        public float Qh { get; set; } // C/N ratio of soil organic matter
        public bool UseDynamicAvailableNitrogen { get; set; }
    }
}
