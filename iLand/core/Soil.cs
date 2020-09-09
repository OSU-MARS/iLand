using iLand.tools;
using System;
using System.Collections.Generic;
using System.Diagnostics;

namespace iLand.core
{
    /** @class Soil provides an implementation of the ICBM/2N soil carbon and nitrogen dynamics model.
        @ingroup core
        The ICBM/2N model was developed by Kaetterer and Andren (2001) and used by others (e.g. Xenakis et al, 2008).
        See http://iland.boku.ac.at/soil+C+and+N+cycling for a model overview and the rationale of the model choice.
        */
    internal class Soil
    {
        private static readonly SoilParams global_soilpar = new SoilParams();
        private static readonly SoilParams mParams = global_soilpar;
        private static double mNitrogenDeposition = 0.0; ///< annual nitrogen deposition (kg N/ha*yr)

        public ResourceUnit mRU; ///< link to containing resource unit
        // variables
        private double mRE; ///< climate factor 're' (see Snag::calculateClimateFactors())
        private double mAvailableNitrogen; ///< plant available nitrogen (kg/ha)
        private double mAvailableNitrogenFromLabile; ///< plant available nitrogen from labile pool (kg/ha)
        private double mAvailableNitrogenFromRefractory; ///< plant available nitrogen from refractory pool (kg/ha)
        public double mKyl; ///< litter decomposition rate
        public double mKyr; ///< downed woody debris (dwd) decomposition rate
        private double mKo; ///< decomposition rate for soil organic matter (i.e. the "old" pool sensu ICBM)
        private double mH; ///< humification rate

        public CNPool mInputLab; ///< input pool labile matter (t/ha)
        public CNPool mInputRef; ///< input pool refractory matter (t/ha)
        // state variables
        public CNPool mYL; ///< C/N Pool for young labile matter (i.e. litter) (t/ha)
        public CNPool mYR; ///< C/N Pool for young refractory matter (i.e. downed woody debris) (t/ha)
        public CNPair mSOM; ///< C/N Pool for old matter (t/ha) (i.e. soil organic matter, SOM)

        private CNPair mTotalToDisturbance; ///< book-keeping pool for heterotrophic respiration (kg/*ha)
        private CNPair mTotalToAtmosphere; ///< book-keeping disturbance envents (fire) (kg/ha)

        public void setClimateFactor(double climate_factor_re) { mRE = climate_factor_re; } ///< set the climate decomposition factor for the current year

        public CNPool youngLabile() { return mYL; } ///< young labile matter (t/ha)
        public CNPool youngRefractory() { return mYR; } ///< young refractory matter (t/ha)
        public CNPair oldOrganicMatter() { return mSOM; } ///< old matter (SOM) (t/ha)
        public double availableNitrogen() { return mAvailableNitrogen; } ///< return available Nitrogen (kg/ha*yr)

        public CNPair fluxToAtmosphere() { return mTotalToAtmosphere; } ///< total flux due to heterotrophic respiration kg/ha
        public CNPair fluxToDisturbance() { return mTotalToDisturbance; } ///< total flux due to disturbance events (e.g. fire) kg/ha

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
            public bool is_setup;

            // ICBM/2N parameters
            public SoilParams()
            {
                qb = 5.0;
                qh = 25.0;
                leaching = 0.15;
                el = 0.0577;
                er = 0.073;
                is_setup = false;
            }
        }

        private void fetchParameters()
        {
            XmlHelper xml_site = new XmlHelper(GlobalSettings.instance().settings().node("model.site"));
            mKo = xml_site.valueDouble("somDecompRate", 0.02);
            mH = xml_site.valueDouble("soilHumificationRate", 0.3);

            if (mParams.is_setup || GlobalSettings.instance().model() != null)
            {
                return;
            }
            XmlHelper xml = new XmlHelper(GlobalSettings.instance().settings().node("model.settings.soil"));
            mParams.qb = xml.valueDouble("qb", 5.0);
            mParams.qh = xml.valueDouble("qh", 25.0);
            mParams.leaching = xml.valueDouble("leaching", 0.15);
            mParams.el = xml.valueDouble("el", 0.0577);
            mParams.er = xml.valueDouble("er", 0.073);

            mParams.is_setup = true;

            mNitrogenDeposition = xml.valueDouble("nitrogenDeposition", 0.0);
        }


        public Soil(ResourceUnit ru = null)
        {
            mRU = ru;
            mRE = 0.0;
            mAvailableNitrogen = 0.0;
            mKyl = 0.0;
            mKyr = 0.0;
            mH = 0.0;
            mKo = 0.0;
            fetchParameters();
        }

        // reset of bookkeeping variables
        public void newYear()
        {
            mTotalToDisturbance.clear();
            mTotalToAtmosphere.clear();
        }

        /// setup initial content of the soil pool (call before model start)
        public void setInitialState(CNPool young_labile_kg_ha, CNPool young_refractory_kg_ha, CNPair SOM_kg_ha)
        {
            mYL = young_labile_kg_ha * 0.001; // pool sizes are stored in t/ha
            mYR = young_refractory_kg_ha * 0.001;
            mSOM = SOM_kg_ha * 0.001;

            mKyl = young_labile_kg_ha.parameter();
            mKyr = young_refractory_kg_ha.parameter();

            if (mKyl <= 0.0 || mKyr <= 0.0)
            {
                throw new NotSupportedException(String.Format("setup of Soil: kyl or kyr invalid: kyl: {0} kyr: {1}", mKyl, mKyr));
            }
            if (!mYL.isValid())
            {
                throw new NotSupportedException(String.Format("setup of Soil: yl-pool invalid: c: {0} n: {1}", mYL.C, mYL.N));
            }
            if (!mYL.isValid())
            {
                throw new NotSupportedException(String.Format("setup of Soil: yr-pool invalid: c: {0} n: {1}", mYR.C, mYR.N));
            }
            if (!mYL.isValid())
            {
                throw new NotSupportedException(String.Format("setup of Soil: som-pool invalid: c: {0} n: {1}", mSOM.C, mSOM.N));
            }
        }

        /// set soil inputs of current year (litter and deadwood)
        public void setSoilInput(CNPool labile_input_kg_ha, CNPool refractory_input_kg_ha)
        {
            // stockable area:
            // if the stockable area is < 1ha, then
            // scale the soil inputs to a full hectare
            double area_ha = mRU != null ? mRU.stockableArea() / Constant.cRUArea : 1.0;

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
            mKyl = mYL.parameter(mInputLab);
            mKyr = mYR.parameter(mInputRef);
            if (Double.IsNaN(mKyr) || Double.IsNaN(mYR.C))
            {
                Debug.WriteLine("mKyr is NAN");
            }
        }


        /// Main calculation function
        /// must be called after snag dyanmics (i.e. to ensure input fluxes are available)
        public void calculateYear()
        {
            SoilParams sp = mParams;
            // checks
            if (mRE == 0.0)
            {
                throw new NotSupportedException("calculateYear(): Invalid value for 're' (=0) for RU(index): " + mRU.index());
            }
            double t = 1.0; // timestep (annual)
                            // auxiliary calculations
            CNPair total_before = mYL + mYR + mSOM;

            CNPair total_in = mInputLab + mInputRef;
            if (Double.IsNaN(total_in.C) || Double.IsNaN(mKyr))
            {
                Debug.WriteLine("soil input is NAN.");
            }
            double ylss = mInputLab.C / (mKyl * mRE); // Yl stedy state C
            double cl = sp.el * (1.0 - mH) / sp.qb - mH * (1.0 - sp.el) / sp.qh; // eta l in the paper
            double ynlss = 0.0;
            if (!mInputLab.isEmpty())
            {
                ynlss = mInputLab.C / (mKyl * mRE * (1.0 - mH)) * ((1.0 - sp.el) / mInputLab.CN() + cl); // Yl steady state N
            }
            double yrss = mInputRef.C / (mKyr * mRE); // Yr steady state C
            double cr = sp.er * (1.0 - mH) / sp.qb - mH * (1.0 - sp.er) / sp.qh; // eta r in the paper
            double ynrss = 0.0;
            if (!mInputRef.isEmpty())
            {
                ynrss = mInputRef.C / (mKyr * mRE * (1.0 - mH)) * ((1.0 - sp.er) / mInputRef.CN() + cr); // Yr steady state N
            }
            double oss = mH * total_in.C / (mKo * mRE); // O steady state C
            double onss = mH * total_in.C / (sp.qh * mKo * mRE); // O steady state N

            double al = mH * (mKyl * mRE * mYL.C - mInputLab.C) / ((mKo - mKyl) * mRE);
            double ar = mH * (mKyr * mRE * mYR.C - mInputRef.C) / ((mKo - mKyr) * mRE);

            // update of state variables
            // precalculations
            double lfactor = Math.Exp(-mKyl * mRE * t);
            double rfactor = Math.Exp(-mKyr * mRE * t);
            // young labile pool
            CNPair yl = mYL;
            mYL.C = ylss + (yl.C - ylss) * lfactor;
            mYL.N = ynlss + (yl.N - ynlss - cl / (sp.el - mH) * (yl.C - ylss)) * Math.Exp(-mKyl * mRE * (1.0 - mH) * t / (1.0 - sp.el)) + cl / (sp.el - mH) * (yl.C - ylss) * lfactor;
            mYL.setParameter(mKyl); // update decomposition rate
                                    // young ref. pool
            CNPair yr = mYR;
            mYR.C = yrss + (yr.C - yrss) * rfactor;
            mYR.N = ynrss + (yr.N - ynrss - cr / (sp.er - mH) * (yr.C - yrss)) * Math.Exp(-mKyr * mRE * (1.0 - mH) * t / (1.0 - sp.er)) + cr / (sp.er - mH) * (yr.C - yrss) * rfactor;
            mYR.setParameter(mKyr); // update decomposition rate
                                    // SOM pool (old)
            CNPair o = mSOM;
            mSOM.C = oss + (o.C - oss - al - ar) * Math.Exp(-mKo * mRE * t) + al * lfactor + ar * rfactor;
            mSOM.N = onss + (o.N - onss - (al + ar) / sp.qh) * Math.Exp(-mKo * mRE * t) + al / sp.qh * lfactor + ar / sp.qh * rfactor;

            // calculate delta (i.e. flux to atmosphere)
            CNPair total_after = mYL + mYR + mSOM;
            CNPair flux = total_before + total_in - total_after;
            if (flux.C < 0.0)
            {
                Debug.WriteLine("negative flux to atmosphere?!?");
                flux.clear();
            }
            mTotalToAtmosphere += flux;

            // calculate plant available nitrogen
            mAvailableNitrogenFromLabile = mKyl * mRE * (1.0 - mH) / (1.0 - sp.el) * (mYL.N - sp.el * mYL.C / sp.qb);  // N from labile...
            mAvailableNitrogenFromRefractory = mKyr * mRE * (1 - mH) / (1.0 - sp.er) * (mYR.N - sp.er * mYR.C / sp.qb); // + N from refractory...
            double nav_from_som = mKo * mRE * mSOM.N * (1.0 - sp.leaching); // + N from SOM pool (reduced by leaching (leaching modeled only from slow SOM Pool))

            mAvailableNitrogenFromLabile *= 1000.0; // t/ha -> kg/ha
            mAvailableNitrogenFromRefractory *= 1000.0; // t/ha -> kg/ha
            nav_from_som *= 1000.0; // t/ha -> kg/ha

            mAvailableNitrogen = mAvailableNitrogenFromLabile + mAvailableNitrogenFromRefractory + nav_from_som;

            if (mAvailableNitrogen < 0.0)
            {
                mAvailableNitrogen = 0.0;
            }
            if (Double.IsNaN(mAvailableNitrogen) || Double.IsNaN(mYR.C))
            {
                Debug.WriteLine("Available Nitrogen is NAN.");
            }

            // add nitrogen deposition
            mAvailableNitrogen += mNitrogenDeposition;

            // stedy state for n-available
            //    double navss = mKyl*mRE*(1.0 -mH)/(1.0 -sp.el)*(ynlss-sp.el*ylss/sp.qb); // available nitrogen (steady state)
            //    navss += mKyr*mRE*(1.0 -mH)/(1.0 -sp.er)*(ynrss - sp.er*yrss/sp.qb);
            //    navss += mKo*mRE*onss*(1.0 -sp.leaching);

        }

        public List<object> debugList()
        {
            List<object> list = new List<object>() {
        // (1) inputs of the year
        mInputLab.C, mInputLab.N, mInputLab.parameter(), mInputRef.C, mInputRef.N, mInputRef.parameter(), mRE,
            // (2) states
            mKyl, mKyr, mYL.C, mYL.N, mYR.C, mYR.N, mSOM.C, mSOM.N,
            // (3) nav
            mAvailableNitrogen, mAvailableNitrogenFromLabile, mAvailableNitrogenFromRefractory, (mAvailableNitrogen - mAvailableNitrogenFromLabile - mAvailableNitrogenFromRefractory)
                };
            return list;
        }

        /// remove part of the biomass (e.g.: due to fire).
        /// @param DWDfrac fraction of downed woody debris (yR) to remove (0: nothing, 1: remove 100% percent)
        /// @param litterFrac fraction of litter pools (yL) to remove (0: nothing, 1: remove 100% percent)
        /// @param soilFrac fraction of soil pool (SOM) to remove (0: nothing, 1: remove 100% percent)
        public void disturbance(double DWDfrac, double litterFrac, double soilFrac)
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
            mTotalToDisturbance += mYR * Global.limit(DWDfrac, 0.0, 1.0);
            mYR *= (1.0 - DWDfrac);
            // litter
            mTotalToDisturbance += mYL * Global.limit(litterFrac, 0.0, 1.0);
            mYL *= (1.0 - litterFrac);
            // old soil organic matter
            mTotalToDisturbance += mSOM * Global.limit(soilFrac, 0.0, 1.0);
            mSOM *= (1.0 - soilFrac);
            if (Double.IsNaN(mAvailableNitrogen) || Double.IsNaN(mYR.C))
            {
                Debug.WriteLine("Available Nitrogen is NAN.");
            }
        }

        /// remove biomass from the soil layer (e.g.: due to fire).
        /// @param DWD_kg_ha downed woody debris (yR) to remove kg/ha
        /// @param litter_kg_ha biomass in litter pools (yL) to remove kg/ha
        /// @param soil_kg_ha biomass in soil pool (SOM) to remove kg/ha
        public void disturbanceBiomass(double DWD_kg_ha, double litter_kg_ha, double soil_kg_ha)
        {
            double frac_dwd = 0.0;
            double frac_litter = 0.0;
            double frac_som = 0.0;
            if (!mYR.isEmpty())
            {
                frac_dwd = DWD_kg_ha / 1000.0 / mYR.biomass();
            }
            if (!mYL.isEmpty())
            {
                frac_litter = litter_kg_ha / 1000.0 / mYL.biomass();
            }
            if (!mSOM.isEmpty())
            {
                frac_som = soil_kg_ha / 1000.0 / mSOM.biomass();
            }

            disturbance(frac_dwd, frac_litter, frac_som);
        }
    }
}
