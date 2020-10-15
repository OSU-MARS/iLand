using iLand.Tools;
using System;
using System.Collections.Generic;
using System.Diagnostics;

namespace iLand.World
{
    /** @class Soil provides an implementation of the ICBM/2N soil carbon and nitrogen dynamics model.
        @ingroup core
        The ICBM/2N model was developed by Kaetterer and Andren (2001) and used by others (e.g. Xenakis et al, 2008).
        See http://iland.boku.ac.at/soil+C+and+N+cycling for a model overview and the rationale of the model choice.
        */
    public class Soil
    {
        private readonly SoilParams mParams;
        private double mNitrogenDeposition = 0.0; ///< annual nitrogen deposition (kg N/ha*yr)

        public ResourceUnit mRU; ///< link to containing resource unit
        // variables
        private double mAvailableNitrogenFromLabile; ///< plant available nitrogen from labile pool (kg/ha)
        private double mAvailableNitrogenFromRefractory; ///< plant available nitrogen from refractory pool (kg/ha)
        public double mKyl; ///< litter decomposition rate
        public double mKyr; ///< downed woody debris (dwd) decomposition rate
        private double mKo; ///< decomposition rate for soil organic matter (i.e. the "old" pool sensu ICBM)
        private double mH; ///< humification rate

        public CNPool mInputLab; ///< input pool labile matter (t/ha)
        public CNPool mInputRef; ///< input pool refractory matter (t/ha)

        public double AvailableNitrogen { get; private set; } ///< return available Nitrogen (kg/ha*yr)
        public double ClimateFactor { get; set; } ///< set the climate decomposition factor for the current year
        public CNPair FluxToAtmosphere { get; private set; } ///< total flux due to heterotrophic respiration kg/ha
        public CNPair FluxToDisturbance { get; private set; } ///< total flux due to disturbance events (e.g. fire) kg/ha
        public CNPair OrganicMatter { get; private set; } ///< soil organic matter (SOM) (t/ha)
        public CNPool YoungLabile { get; private set; } ///< young labile matter (litter) (t/ha)
        public CNPool YoungRefractory { get; private set; } ///< young refractory (woody debris)  matter (t/ha)

        // site-specific parameters
        // i.e. parameters that need to be specified in the environment file
        // note that leaching is not actually influencing soil dynamics but reduces availability of N to plants by assuming that some N
        // (proportional to its mineralization in the mineral soil horizon) is leached
        // see separate wiki-page (http://iland.boku.ac.at/soil+parametrization+and+initialization)
        // and R-script on parameter estimation and initialization
        private class SoilParams
        {
            public double qb; ///< C/N ratio of soil microbes
            public double qh; ///< C/N ratio of SOM
            public double leaching; ///< how many percent of the mineralized nitrogen in O is not available for plants but is leached
            public double el; ///< microbal efficiency in the labile pool, auxiliary parameter (see parameterization example)
            public double er; ///< microbal efficiency in the refractory pool, auxiliary parameter (see parameterization example)
            public bool isSetup;

            // ICBM/2N parameters
            public SoilParams()
            {
                qb = 5.0;
                qh = 25.0;
                leaching = 0.15;
                el = 0.0577;
                er = 0.073;
                isSetup = false;
            }
        }

        public Soil(GlobalSettings globalSettings, ResourceUnit ru = null)
        {
            this.mKo = 0.0;
            this.mKyl = 0.0;
            this.mKyr = 0.0;
            this.mH = 0.0;
            this.mRU = ru;

            this.mInputLab = null;
            this.mInputRef = null;
            this.mParams = new SoilParams();

            this.AvailableNitrogen = 0.0;
            this.ClimateFactor = 0.0;
            this.FluxToAtmosphere = new CNPair();
            this.FluxToDisturbance = new CNPair();
            this.OrganicMatter = new CNPair();
            this.YoungLabile = new CNPool();
            this.YoungRefractory = new CNPool();

            this.FetchParameters(globalSettings);
        }

        private void FetchParameters(GlobalSettings globalSettings)
        {
            XmlHelper xml_site = new XmlHelper(globalSettings.Settings.Node("model.site"));
            mKo = xml_site.GetDouble("somDecompRate", 0.02);
            mH = xml_site.GetDouble("soilHumificationRate", 0.3);

            if (mParams.isSetup)
            {
                return;
            }
            XmlHelper xml = new XmlHelper(globalSettings.Settings.Node("model.settings.soil"));
            mParams.qb = xml.GetDouble("qb", 5.0);
            mParams.qh = xml.GetDouble("qh", 25.0);
            mParams.leaching = xml.GetDouble("leaching", 0.15);
            mParams.el = xml.GetDouble("el", 0.0577);
            mParams.er = xml.GetDouble("er", 0.073);

            mParams.isSetup = true;

            mNitrogenDeposition = xml.GetDouble("nitrogenDeposition", 0.0);
        }

        // reset of bookkeeping variables
        public void NewYear()
        {
            FluxToAtmosphere.Clear();
            FluxToDisturbance.Clear();
        }

        /// setup initial content of the soil pool (call before model start)
        public void SetInitialState(CNPool young_labile_kg_ha, CNPool young_refractory_kg_ha, CNPair SOM_kg_ha)
        {
            YoungLabile = young_labile_kg_ha * 0.001; // pool sizes are stored in t/ha
            YoungRefractory = young_refractory_kg_ha * 0.001;
            OrganicMatter = SOM_kg_ha * 0.001;

            mKyl = young_labile_kg_ha.Weight;
            mKyr = young_refractory_kg_ha.Weight;

            if (mKyl <= 0.0 || mKyr <= 0.0)
            {
                throw new NotSupportedException(String.Format("setup of Soil: kyl or kyr invalid: kyl: {0} kyr: {1}", mKyl, mKyr));
            }
            if (!YoungLabile.IsValid())
            {
                throw new NotSupportedException(String.Format("setup of Soil: yl-pool invalid: c: {0} n: {1}", YoungLabile.C, YoungLabile.N));
            }
            if (!YoungLabile.IsValid())
            {
                throw new NotSupportedException(String.Format("setup of Soil: yr-pool invalid: c: {0} n: {1}", YoungRefractory.C, YoungRefractory.N));
            }
            if (!YoungLabile.IsValid())
            {
                throw new NotSupportedException(String.Format("setup of Soil: som-pool invalid: c: {0} n: {1}", OrganicMatter.C, OrganicMatter.N));
            }
        }

        /// set soil inputs of current year (litter and deadwood)
        public void SetSoilInput(CNPool labile_input_kg_ha, CNPool refractory_input_kg_ha)
        {
            // stockable area:
            // if the stockable area is < 1ha, then
            // scale the soil inputs to a full hectare
            double area_ha = mRU != null ? mRU.StockableArea / Constant.RUArea : 1.0;

            if (area_ha == 0.0)
            {
                Debug.WriteLine("setSoilInput: stockable area is 0!");
                return;
                //throw new NotSupportedException("setSoilInput: stockable area is 0!");
            }
            // for the carbon input flow from snags/trees we assume a minimum size of the "stand" of 0.1ha
            // this reduces rapid input pulses (e.g. if one large tree dies).
            // Put differently: for resource units with stockable area < 0.1ha, we add a "blank" area.
            // the soil module always calculates per ha values, so nothing else needs to be done here.
            // area_ha = std::max(area_ha, 0.1);

            mInputLab = labile_input_kg_ha * (0.001 / area_ha); // transfer from kg/ha -> tons/ha and scale to 1 ha
            mInputRef = refractory_input_kg_ha * (0.001 / area_ha);
            // calculate the decomposition rates
            mKyl = YoungLabile.GetWeightedParameter(mInputLab);
            mKyr = YoungRefractory.GetWeightedParameter(mInputRef);
            if (Double.IsNaN(mKyr) || Double.IsNaN(YoungRefractory.C))
            {
                Debug.WriteLine("mKyr is NAN");
            }
        }

        /// Main calculation function
        /// must be called after snag dyanmics (i.e. to ensure input fluxes are available)
        public void CalculateYear()
        {
            SoilParams sp = mParams;
            // checks
            if (ClimateFactor == 0.0)
            {
                throw new NotSupportedException("calculateYear(): Invalid value for 're' (=0) for RU(index): " + mRU.Index);
            }
            double t = 1.0; // timestep (annual)
                            // auxiliary calculations
            CNPair total_before = YoungLabile + YoungRefractory + OrganicMatter;

            CNPair total_in = mInputLab + mInputRef;
            if (Double.IsNaN(total_in.C) || Double.IsNaN(mKyr))
            {
                Debug.WriteLine("soil input is NAN.");
            }
            double ylss = mInputLab.C / (mKyl * ClimateFactor); // Yl stedy state C
            double cl = sp.el * (1.0 - mH) / sp.qb - mH * (1.0 - sp.el) / sp.qh; // eta l in the paper
            double ynlss = 0.0;
            if (!mInputLab.IsEmpty())
            {
                ynlss = mInputLab.C / (mKyl * ClimateFactor * (1.0 - mH)) * ((1.0 - sp.el) / mInputLab.CNratio() + cl); // Yl steady state N
            }
            double yrss = mInputRef.C / (mKyr * ClimateFactor); // Yr steady state C
            double cr = sp.er * (1.0 - mH) / sp.qb - mH * (1.0 - sp.er) / sp.qh; // eta r in the paper
            double ynrss = 0.0;
            if (!mInputRef.IsEmpty())
            {
                ynrss = mInputRef.C / (mKyr * ClimateFactor * (1.0 - mH)) * ((1.0 - sp.er) / mInputRef.CNratio() + cr); // Yr steady state N
            }
            double oss = mH * total_in.C / (mKo * ClimateFactor); // O steady state C
            double onss = mH * total_in.C / (sp.qh * mKo * ClimateFactor); // O steady state N

            double al = mH * (mKyl * ClimateFactor * YoungLabile.C - mInputLab.C) / ((mKo - mKyl) * ClimateFactor);
            double ar = mH * (mKyr * ClimateFactor * YoungRefractory.C - mInputRef.C) / ((mKo - mKyr) * ClimateFactor);

            // update of state variables
            // precalculations
            double lfactor = Math.Exp(-mKyl * ClimateFactor * t);
            double rfactor = Math.Exp(-mKyr * ClimateFactor * t);
            // young labile pool
            CNPair yl = YoungLabile;
            YoungLabile.C = ylss + (yl.C - ylss) * lfactor;
            YoungLabile.N = ynlss + (yl.N - ynlss - cl / (sp.el - mH) * (yl.C - ylss)) * Math.Exp(-mKyl * ClimateFactor * (1.0 - mH) * t / (1.0 - sp.el)) + cl / (sp.el - mH) * (yl.C - ylss) * lfactor;
            YoungLabile.Weight = mKyl; // update decomposition rate
            // young ref. pool
            CNPair yr = YoungRefractory;
            YoungRefractory.C = yrss + (yr.C - yrss) * rfactor;
            YoungRefractory.N = ynrss + (yr.N - ynrss - cr / (sp.er - mH) * (yr.C - yrss)) * Math.Exp(-mKyr * ClimateFactor * (1.0 - mH) * t / (1.0 - sp.er)) + cr / (sp.er - mH) * (yr.C - yrss) * rfactor;
            YoungRefractory.Weight = mKyr; // update decomposition rate
            // SOM pool (old)
            CNPair o = OrganicMatter;
            OrganicMatter.C = oss + (o.C - oss - al - ar) * Math.Exp(-mKo * ClimateFactor * t) + al * lfactor + ar * rfactor;
            OrganicMatter.N = onss + (o.N - onss - (al + ar) / sp.qh) * Math.Exp(-mKo * ClimateFactor * t) + al / sp.qh * lfactor + ar / sp.qh * rfactor;

            // calculate delta (i.e. flux to atmosphere)
            CNPair total_after = YoungLabile + YoungRefractory + OrganicMatter;
            CNPair flux = total_before + total_in - total_after;
            if (flux.C < 0.0)
            {
                Debug.WriteLine("negative flux to atmosphere?!?");
                flux.Clear();
            }
            FluxToAtmosphere += flux;

            // calculate plant available nitrogen
            mAvailableNitrogenFromLabile = mKyl * ClimateFactor * (1.0 - mH) / (1.0 - sp.el) * (YoungLabile.N - sp.el * YoungLabile.C / sp.qb);  // N from labile...
            mAvailableNitrogenFromRefractory = mKyr * ClimateFactor * (1 - mH) / (1.0 - sp.er) * (YoungRefractory.N - sp.er * YoungRefractory.C / sp.qb); // + N from refractory...
            double nav_from_som = mKo * ClimateFactor * OrganicMatter.N * (1.0 - sp.leaching); // + N from SOM pool (reduced by leaching (leaching modeled only from slow SOM Pool))

            mAvailableNitrogenFromLabile *= 1000.0; // t/ha -> kg/ha
            mAvailableNitrogenFromRefractory *= 1000.0; // t/ha -> kg/ha
            nav_from_som *= 1000.0; // t/ha -> kg/ha

            AvailableNitrogen = mAvailableNitrogenFromLabile + mAvailableNitrogenFromRefractory + nav_from_som;

            if (AvailableNitrogen < 0.0)
            {
                AvailableNitrogen = 0.0;
            }
            if (Double.IsNaN(AvailableNitrogen) || Double.IsNaN(YoungRefractory.C))
            {
                Debug.WriteLine("Available Nitrogen is NAN.");
            }

            // add nitrogen deposition
            AvailableNitrogen += mNitrogenDeposition;

            // stedy state for n-available
            //    double navss = mKyl*mRE*(1.0 -mH)/(1.0 -sp.el)*(ynlss-sp.el*ylss/sp.qb); // available nitrogen (steady state)
            //    navss += mKyr*mRE*(1.0 -mH)/(1.0 -sp.er)*(ynrss - sp.er*yrss/sp.qb);
            //    navss += mKo*mRE*onss*(1.0 -sp.leaching);

        }

        public List<object> DebugList()
        {
            List<object> list = new List<object>() 
            {
                // (1) inputs of the year
                mInputLab.C, mInputLab.N, mInputLab.Weight, mInputRef.C, mInputRef.N, mInputRef.Weight, ClimateFactor,
                // (2) states
                mKyl, mKyr, YoungLabile.C, YoungLabile.N, YoungRefractory.C, YoungRefractory.N, OrganicMatter.C, OrganicMatter.N,
                // (3) nav
                AvailableNitrogen, mAvailableNitrogenFromLabile, mAvailableNitrogenFromRefractory, (AvailableNitrogen - mAvailableNitrogenFromLabile - mAvailableNitrogenFromRefractory)
            };
            return list;
        }

        /// remove part of the biomass (e.g.: due to fire).
        /// @param DWDfrac fraction of downed woody debris (yR) to remove (0: nothing, 1: remove 100% percent)
        /// @param litterFrac fraction of litter pools (yL) to remove (0: nothing, 1: remove 100% percent)
        /// @param soilFrac fraction of soil pool (SOM) to remove (0: nothing, 1: remove 100% percent)
        public void Disturbance(double DWDfrac, double litterFrac, double soilFrac)
        {
            if (DWDfrac < 0.0 || DWDfrac > 1.0)
            {
                Debug.WriteLine("warning: Soil:disturbance: DWD-fraction invalid " + DWDfrac);
            }
            if (litterFrac < 0.0 || litterFrac > 1.0)
            {
                Debug.WriteLine("warning: Soil:disturbance: litter-fraction invalid " + litterFrac);
            }
            if (soilFrac < 0.0 || soilFrac > 1.0)
            {
                Debug.WriteLine("warning: Soil:disturbance: soil-fraction invalid " + soilFrac);
            }
            // dwd
            FluxToDisturbance += YoungRefractory * Global.Limit(DWDfrac, 0.0, 1.0);
            YoungRefractory *= (1.0 - DWDfrac);
            // litter
            FluxToDisturbance += YoungLabile * Global.Limit(litterFrac, 0.0, 1.0);
            YoungLabile *= (1.0 - litterFrac);
            // old soil organic matter
            FluxToDisturbance += OrganicMatter * Global.Limit(soilFrac, 0.0, 1.0);
            OrganicMatter *= (1.0 - soilFrac);
            if (Double.IsNaN(AvailableNitrogen) || Double.IsNaN(YoungRefractory.C))
            {
                Debug.WriteLine("Available Nitrogen is NAN.");
            }
        }

        /// remove biomass from the soil layer (e.g.: due to fire).
        /// @param DWD_kg_ha downed woody debris (yR) to remove kg/ha
        /// @param litter_kg_ha biomass in litter pools (yL) to remove kg/ha
        /// @param soil_kg_ha biomass in soil pool (SOM) to remove kg/ha
        public void DisturbanceBiomass(double DWD_kg_ha, double litter_kg_ha, double soil_kg_ha)
        {
            double frac_dwd = 0.0;
            double frac_litter = 0.0;
            double frac_som = 0.0;
            if (!YoungRefractory.IsEmpty())
            {
                frac_dwd = DWD_kg_ha / 1000.0 / YoungRefractory.Biomass();
            }
            if (!YoungLabile.IsEmpty())
            {
                frac_litter = litter_kg_ha / 1000.0 / YoungLabile.Biomass();
            }
            if (!OrganicMatter.IsEmpty())
            {
                frac_som = soil_kg_ha / 1000.0 / OrganicMatter.Biomass();
            }

            Disturbance(frac_dwd, frac_litter, frac_som);
        }
    }
}
