using iLand.Input;
using iLand.Tool;
using System;
using System.Diagnostics;

namespace iLand.World
{
    /** @class Soil provides an implementation of the ICBM/2N soil carbon and nitrogen dynamics model.
        The ICBM/2N model was developed by Kaetterer and Andren (2001) and merged with 3-PG as 3-PGN by Xenakis et al. 2008.
        See http://iland-model.org/soil+C+and+N+cycling for a model overview and the rationale of the model choice.
        */
    public class ResourceUnitSoil
    {
        public SoilParameters Parameters { get; private init; }
        public ResourceUnit RU { get; private init; } // link to containing resource unit

        public float ClimateDecompositionFactor { get; set; } // set the climate decomposition factor for the current year
        public CarbonNitrogenTuple FluxToAtmosphere { get; private set; } // total flux due to heterotrophic respiration kg/ha
        public CarbonNitrogenTuple FluxToDisturbance { get; private set; } // total flux due to disturbance events (e.g. fire) kg/ha
        public CarbonNitrogenPool InputLabile { get; private set; } // input pool of labile matter (t/ha)
        public CarbonNitrogenPool InputRefractory { get; private set; } // input pool of refractory matter (t/ha)
        public CarbonNitrogenTuple OrganicMatter { get; private set; } // soil organic matter (SOM; humified maaterial) (t/ha)
        public float PlantAvailableNitrogen { get; private set; } // root accessible nitrogen (kg/ha*yr)
        public CarbonNitrogenPool YoungLabile { get; private set; } // young labile matter (litter) (t/ha)
        public CarbonNitrogenPool YoungRefractory { get; private set; } // young refractory (woody debris)  matter (t/ha)

        public ResourceUnitSoil(ResourceUnit ru, ResourceUnitEnvironment environment)
        {
            this.RU = ru;

            // see Xenakis 2008 for parameter definitions
            // Xenakis G, Raya D, Maurizio M. 2008. Sensitivity and uncertainty analysis from a coupled 3-PG and soil organic matter 
            //   decomposition model. Ecological Modelling 219(1–2):1-16. https://doi.org/10.1016/j.ecolmodel.2008.07.020
            this.Parameters = new SoilParameters()
            {
                AnnualNitrogenDeposition = environment.AnnualNitrogenDeposition,
                El = environment.SoilEl,
                Er = environment.SoilEr,
                Hc = environment.SoilHumificationRate,
                Ko = environment.SoilOrganicDecompositionRate,
                Kyl = environment.SoilYoungLabileDecompositionRate,
                Kyr = environment.SoilYoungRefractoryDecompositionRate,
                Leaching = environment.SoilLeaching,
                Qb = environment.SoilQb,
                Qh = environment.SoilQh,
                UseDynamicAvailableNitrogen = environment.UseDynamicAvailableNitrogen
            };
            if (this.Parameters.Kyl <= 0.0 || this.Parameters.Kyr <= 0.0)
            {
                throw new NotSupportedException(String.Format("Kyl or kyr less than zero: kyl: {0} (young labile decomposition rate), kyr: {1} (young refractory decomposition rate)", this.Parameters.Kyl, this.Parameters.Kyr));
            }

            this.ClimateDecompositionFactor = 0.0F;
            this.FluxToAtmosphere = new CarbonNitrogenTuple();
            this.FluxToDisturbance = new CarbonNitrogenTuple();
            this.InputLabile = new CarbonNitrogenPool();
            this.InputRefractory = new CarbonNitrogenPool();
            // ICBM/2 "old" carbon pool: humified soil organic content
            this.OrganicMatter = new CarbonNitrogenTuple(0.001F * environment.SoilOrganicC, // environment values are in kg/ha, pool sizes are in t/ha
                                                         0.001F * environment.SoilOrganicN);
            this.PlantAvailableNitrogen = environment.SoilAvailableNitrogen; // TODO: gets overwritten rather than modified in NewYear()?
            // ICBM/2 litter layer
            this.YoungLabile = new CarbonNitrogenPool(0.001F * environment.SoilYoungLabileC,
                                                      0.001F * environment.SoilYoungLabileN,
                                                      this.Parameters.Kyl);
            // ICBM/2 coarse woody debris
            this.YoungRefractory = new CarbonNitrogenPool(0.001F * environment.SoilYoungRefractoryC,
                                                          0.001F * environment.SoilYoungRefractoryN,
                                                          this.Parameters.Kyr);

            if (!this.OrganicMatter.HasCarbonAndNitrogen())
            {
                throw new NotSupportedException(String.Format("Organic matter invalid: c: {0} n: {1}", OrganicMatter.C, OrganicMatter.N));
            }
            if (!this.YoungLabile.HasCarbonAndNitrogen())
            {
                throw new NotSupportedException(String.Format("Young labile invalid: c: {0} n: {1}", YoungLabile.C, YoungLabile.N));
            }
            if (!this.YoungRefractory.HasCarbonAndNitrogen())
            {
                throw new NotSupportedException(String.Format("Young refractory invalid: c: {0} n: {1}", YoungRefractory.C, YoungRefractory.N));
            }
        }

        // reset of bookkeeping variables
        public void OnStartYear()
        {
            this.FluxToAtmosphere.Zero();
            this.FluxToDisturbance.Zero();
        }

        /// set soil inputs of current year (litter and deadwood)
        public void SetSoilInput(CarbonNitrogenPool labile_input_kg_ha, CarbonNitrogenPool refractory_input_kg_ha)
        {
            // stockable area:
            // if the stockable area is < 1ha, then
            // scale the soil inputs to a full hectare
            float area_ha = RU != null ? RU.AreaInLandscape / Constant.ResourceUnitAreaInM2 : 1.0F;
            if (area_ha <= 0.0)
            {
                throw new NotSupportedException("Resource unit's stockable area is zero or negative.");
            }
            // for the carbon input flow from snags/trees we assume a minimum size of the "stand" of 0.1ha
            // this reduces rapid input pulses (e.g. if one large tree dies).
            // Put differently: for resource units with stockable area < 0.1ha, we add a "blank" area.
            // the soil module always calculates per ha values, so nothing else needs to be done here.
            // area_ha = std::max(area_ha, 0.1);

            this.InputLabile = labile_input_kg_ha * (0.001F / area_ha); // transfer from kg/ha -> tons/ha and scale to 1 ha
            this.InputRefractory = refractory_input_kg_ha * (0.001F / area_ha);
            // calculate the decomposition rates
            this.Parameters.Kyl = this.YoungLabile.GetWeightedDecomposiitonRate(this.InputLabile);
            this.Parameters.Kyr = this.YoungRefractory.GetWeightedDecomposiitonRate(this.InputRefractory);
            if (Double.IsNaN(this.Parameters.Kyr) || Double.IsNaN(this.YoungRefractory.C))
            {
                throw new ArgumentException("Kyr or refractory carbon is NAN");
            }
        }

        // must be called after snag dyanmics (i.e. to ensure input fluxes are available)
        public void CalculateYear()
        {
            // checks
            if (this.ClimateDecompositionFactor == 0.0)
            {
                throw new NotSupportedException("Climate decomposition factor is zero for resource unit " + RU.ResourceUnitGridIndex + ".");
            }

            float timestep = Constant.TimeStepInYears; // 1 year (annual)
            CarbonNitrogenTuple totalBefore = this.YoungLabile + this.YoungRefractory + this.OrganicMatter;
            CarbonNitrogenTuple totalInput = this.InputLabile + this.InputRefractory;
            if (Double.IsNaN(totalInput.C) || Double.IsNaN(this.Parameters.Kyr))
            {
                Debug.Fail("Input carbon or decomposition rate is NaN.");
            }

            // Xenakis 2008?
            float kyl = this.Parameters.Kyl; // for readability
            float kyr = this.Parameters.Kyr;
            float el = this.Parameters.El;
            float er = this.Parameters.Er;
            float hc = this.Parameters.Hc;
            float ko = this.Parameters.Ko;
            float qb = this.Parameters.Qb;
            float qh = this.Parameters.Qh;

            float ylss = this.InputLabile.C / (kyl * this.ClimateDecompositionFactor); // Yl steady state C
            float etal = el * (1.0F - hc) / qb - hc * (1.0F - el) / qh; // eta l in the paper
            float ynlss = 0.0F;
            if (this.InputLabile.HasNoCarbon() == false)
            {
                ynlss = this.InputLabile.C / (kyl * this.ClimateDecompositionFactor * (1.0F - hc)) * ((1.0F - el) / this.InputLabile.GetCNRatio() + etal); // Yl steady state N
            }
            float yrss = this.InputRefractory.C / (kyr * this.ClimateDecompositionFactor); // Yr steady state C
            float etar = er * (1.0F - hc) / qb - hc * (1.0F - er) / qh; // eta r in the paper
            float ynrss = 0.0F;
            if (this.InputRefractory.HasNoCarbon() == false)
            {
                ynrss = this.InputRefractory.C / (kyr * this.ClimateDecompositionFactor * (1.0F - hc)) * ((1.0F - er) / this.InputRefractory.GetCNRatio() + etar); // Yr steady state N
            }
            float oss = hc * totalInput.C / (ko * this.ClimateDecompositionFactor); // O steady state C
            float onss = hc * totalInput.C / (qh * ko * this.ClimateDecompositionFactor); // O steady state N

            float al = hc * (kyl * this.ClimateDecompositionFactor * this.YoungLabile.C - this.InputLabile.C) / ((ko - kyl) * this.ClimateDecompositionFactor);
            float ar = hc * (kyr * this.ClimateDecompositionFactor * this.YoungRefractory.C - this.InputRefractory.C) / ((ko - kyr) * this.ClimateDecompositionFactor);

            // update of state variables
            // precalculations
            float lfactor = MathF.Exp(-kyl * this.ClimateDecompositionFactor * timestep);
            float rfactor = MathF.Exp(-kyr * this.ClimateDecompositionFactor * timestep);
            // young labile pool
            CarbonNitrogenTuple yl = this.YoungLabile;
            this.YoungLabile.C = ylss + (yl.C - ylss) * lfactor;
            this.YoungLabile.N = ynlss + (yl.N - ynlss - etal / (el - hc) * (yl.C - ylss)) * MathF.Exp(-kyl * ClimateDecompositionFactor * (1.0F - hc) * timestep / (1.0F - el)) + etal / (el - hc) * (yl.C - ylss) * lfactor;
            this.YoungLabile.DecompositionRate = kyl; // update decomposition rate
            // young refractory pool
            CarbonNitrogenTuple yr = this.YoungRefractory;
            this.YoungRefractory.C = yrss + (yr.C - yrss) * rfactor;
            this.YoungRefractory.N = ynrss + (yr.N - ynrss - etar / (er - hc) * (yr.C - yrss)) * MathF.Exp(-kyr * ClimateDecompositionFactor * (1.0F - hc) * timestep / (1.0F - er)) + etar / (er - hc) * (yr.C - yrss) * rfactor;
            this.YoungRefractory.DecompositionRate = kyr; // update decomposition rate
            // soil organic matter pool (old)
            CarbonNitrogenTuple som = this.OrganicMatter;
            this.OrganicMatter.C = oss + (som.C - oss - al - ar) * MathF.Exp(-ko * this.ClimateDecompositionFactor * timestep) + al * lfactor + ar * rfactor;
            this.OrganicMatter.N = onss + (som.N - onss - (al + ar) / qh) * MathF.Exp(-ko * this.ClimateDecompositionFactor * timestep) + al / qh * lfactor + ar / qh * rfactor;

            // calculate delta (i.e. flux to atmosphere)
            CarbonNitrogenTuple totalAfter = this.YoungLabile + this.YoungRefractory + this.OrganicMatter;
            CarbonNitrogenTuple flux = totalBefore + totalInput - totalAfter;
            if (flux.C < 0.0)
            {
                Debug.Fail("Negative flux to atmosphere.");
                flux.Zero();
            }
            this.FluxToAtmosphere += flux;

            // plant available nitrogen from Xenakis 2008 equation 2
            if (this.Parameters.UseDynamicAvailableNitrogen)
            {
                // TODO: why is ICBM/2N formulated with no memory of previously available nitrogen?
                float leaching = this.Parameters.Leaching;
                float litterNitrogen = kyl * this.ClimateDecompositionFactor * (1.0F - hc) / (1.0F - el) * (this.YoungLabile.N - el * this.YoungLabile.C / qb);  // N from labile...
                float coarseWoodyNitrogen = kyr * this.ClimateDecompositionFactor * (1.0F - hc) / (1.0F - er) * (this.YoungRefractory.N - er * this.YoungRefractory.C / qb); // + N from refractory...
                float humusNitrogen = ko * this.ClimateDecompositionFactor * this.OrganicMatter.N * (1.0F - leaching); // + N from SOM pool (reduced by leaching (leaching modeled only from slow SOM Pool))
                this.PlantAvailableNitrogen = 1000.0F * (litterNitrogen + coarseWoodyNitrogen + humusNitrogen); // t/ha -> kg/ha

                if (this.PlantAvailableNitrogen < 0.0F)
                {
                    // TODO: should this check follow deposition?
                    this.PlantAvailableNitrogen = 0.0F;
                }
                if (Double.IsNaN(this.PlantAvailableNitrogen) || Double.IsNaN(this.YoungRefractory.C))
                {
                    throw new ApplicationException("Plant available nitrogen or coarse woody carbon is NaN.");
                }

                // add nitrogen deposition
                this.PlantAvailableNitrogen += this.Parameters.AnnualNitrogenDeposition;

                // steady state for n-available
                //    float navss = kyl * mRE * (1.0F - h)/(1.0F - el)*(ynlss - el * ylss / qb); // available nitrogen (steady state)
                //    navss += kyr * mRE * (1.0F - h)/(1.0F - er)*(ynrss - Er * yrss/ qb);
                //    navss += ko * mRE * onss*(1.0F - leaching);
            }
        }

        //public List<object> DebugList()
        //{
        //    List<object> list = new List<object>() 
        //    {
        //        // (1) inputs of the year
        //        mInputLab.C, mInputLab.N, mInputLab.DecompositionRate, mInputRef.C, mInputRef.N, mInputRef.DecompositionRate, ClimateDecompositionFactor,
        //        // (2) states
        //        mKyl, mKyr, YoungLabile.C, YoungLabile.N, YoungRefractory.C, YoungRefractory.N, OrganicMatter.C, OrganicMatter.N,
        //        // (3) nav
        //        PlantAvailableNitrogen, mAvailableNitrogenFromLabile, mAvailableNitrogenFromRefractory, (PlantAvailableNitrogen - mAvailableNitrogenFromLabile - mAvailableNitrogenFromRefractory)
        //    };
        //    return list;
        //}

        /// <summary>
        /// Remove biomass from the soil layer (e.g.: due to fire).
        /// </summary>
        /// <param name="downWoodInKgHa">Downed woody debris (yR) to remove kg/ha.</param>
        /// <param name="litterLossKgHa">Biomass in litter pools (yL) to remove kg/ha.</param>
        /// <param name="soilOrganicLossKgHa">Biomass in soil pool (SOM) to remove kg/ha.</param>
        public void RemoveBiomass(float downWoodInKgHa, float litterLossKgHa, float soilOrganicLossKgHa)
        {
            float downWoodFraction = 0.0F;
            float litterFraction = 0.0F;
            float soilOrganicMatterFraction = 0.0F;
            if (this.YoungRefractory.HasNoCarbon() == false)
            {
                downWoodFraction = 0.001F * downWoodInKgHa / YoungRefractory.GetBiomass();
            }
            if (this.YoungLabile.HasNoCarbon() == false)
            {
                litterFraction = 0.001F * litterLossKgHa / YoungLabile.GetBiomass();
            }
            if (this.OrganicMatter.HasNoCarbon() == false)
            {
                soilOrganicMatterFraction = 0.001F * soilOrganicLossKgHa / OrganicMatter.GetBiomass();
            }

            this.RemoveBiomassFractions(downWoodFraction, litterFraction, soilOrganicMatterFraction);
        }

        /// <summary>
        /// Remove part of the biomass (e.g.: due to fire).
        /// </summary>
        /// <param name="downWoodFraction">Fraction of downed woody debris (yR) to remove (0: nothing, 1: remove 100% percent).</param>
        /// <param name="litterFraction">Fraction of litter pools (yL) to remove (0: nothing, 1: remove 100% percent).</param>
        /// <param name="soilFraction">Fraction of soil pool (SOM) to remove (0: nothing, 1: remove 100% percent).</param>
        public void RemoveBiomassFractions(float downWoodFraction, float litterFraction, float soilFraction)
        {
            if (downWoodFraction < 0.0 || downWoodFraction > 1.0)
            {
                throw new ArgumentOutOfRangeException(nameof(downWoodFraction));
            }
            if (litterFraction < 0.0 || litterFraction > 1.0)
            {
                throw new ArgumentOutOfRangeException(nameof(litterFraction));
            }
            if (soilFraction < 0.0 || soilFraction > 1.0)
            {
                throw new ArgumentOutOfRangeException(nameof(soilFraction));

            }
            // down woody debris
            this.FluxToDisturbance += this.YoungRefractory * Maths.Limit(downWoodFraction, 0.0F, 1.0F);
            this.YoungRefractory *= 1.0F - downWoodFraction;
            // litter
            this.FluxToDisturbance += this.YoungLabile * Maths.Limit(litterFraction, 0.0F, 1.0F);
            this.YoungLabile *= 1.0F - litterFraction;
            // old soil organic matter
            this.FluxToDisturbance += this.OrganicMatter * Maths.Limit(soilFraction, 0.0F, 1.0F);
            this.OrganicMatter *= 1.0F - soilFraction;

            Debug.Assert((Single.IsNaN(this.PlantAvailableNitrogen) == false) && (Single.IsNaN(this.YoungRefractory.C) == false));
        }
    }
}
