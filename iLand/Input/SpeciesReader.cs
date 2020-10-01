﻿using Microsoft.Data.Sqlite;
using System;

namespace iLand.Input
{
    public class SpeciesReader : IDisposable
    {
        private bool isDisposed;
        private readonly SqliteDataReader reader;

        private readonly int active;
        private readonly int id;
        private readonly int name;
        private readonly int lipFile;
        private readonly int isConiferous;
        private readonly int isEvergreen;
        private readonly int bmFoliageA;
        private readonly int bfoliageB;
        private readonly int bmWoodyA;
        private readonly int bmWoodyB;
        private readonly int bmRootA;
        private readonly int bmRootB;
        private readonly int bmBranchA;
        private readonly int bmBranchB;
        private readonly int specificLeafArea;
        private readonly int finerootFoliageRatio;
        private readonly int barkThickness;
        private readonly int cnFoliage;
        private readonly int cnFineroot;
        private readonly int cnWood;
        private readonly int turnoverLeaf;
        private readonly int turnoverRoot;
        private readonly int hdLow;
        private readonly int hdHigh;
        private readonly int woodDensity;
        private readonly int formFactor;
        private readonly int snagKsw;
        private readonly int snagHalflife;
        private readonly int snagKyl;
        private readonly int snagKyr;
        private readonly int maximumAge;
        private readonly int maximumHeight;
        private readonly int aging;
        private readonly int probIntrinsic;
        private readonly int probStress;
        private readonly int respVpdExponent;
        private readonly int respTempMin;
        private readonly int respTempMax;
        private readonly int respNitrogenClass;
        private readonly int phenologyClass;
        private readonly int maxCanopyConductance;
        private readonly int psiMin;
        private readonly int lightResponseClass;
        private readonly int seedYearInterval;
        private readonly int maturityYears;
        private readonly int seedKernelAs1;
        private readonly int seedKernelAs2;
        private readonly int seedKernelKs0;
        private readonly int fecundityM2;
        private readonly int nonSeedYearFraction;
        private readonly int serotinyFormula;
        private readonly int fecunditySerotiny;
        private readonly int establishmentParametersMinTemp;
        private readonly int establishmentParametersChillRequirement;
        private readonly int establishmentParametersGddMin;
        private readonly int establishmentParametersGddMax;
        private readonly int establishmentParametersGddBaseTemperature;
        private readonly int establishmentParametersGddBudBurst;
        private readonly int establishmentParametersMinFrostFree;
        private readonly int establishmentParametersFrostTolerance;
        private readonly int establishmentParametersPsiMin;
        private readonly int saplingGrowthParametersHeightGrowthPotential;
        private readonly int saplingGrowthParametersHdSapling;
        private readonly int saplingGrowthParametersStressThreshold;
        private readonly int saplingGrowthParametersMaxStressYears;
        private readonly int saplingGrowthParametersReferenceRatio;
        private readonly int saplingGrowthParametersReinekesR;
        private readonly int saplingGrowthParametersBrowsingProbability;
        private readonly int saplingGrowthParametersSproutGrowth;

        public SpeciesReader(SqliteDataReader reader)
        {
            this.isDisposed = false;
            this.reader = reader;

            this.active = reader.GetOrdinal("active");
            this.id = reader.GetOrdinal("shortName");
            this.name = reader.GetOrdinal("name");
            this.lipFile = reader.GetOrdinal("LIPfile");
            this.isConiferous = reader.GetOrdinal("isConiferous");
            this.isEvergreen = reader.GetOrdinal("isEvergreen");
            this.bmFoliageA = reader.GetOrdinal("bmFoliage_a");
            this.bfoliageB = reader.GetOrdinal("bmFoliage_b");
            this.bmWoodyA = reader.GetOrdinal("bmWoody_a");
            this.bmWoodyB = reader.GetOrdinal("bmWoody_b");
            this.bmRootA = reader.GetOrdinal("bmRoot_a");
            this.bmRootB = reader.GetOrdinal("bmRoot_b");
            this.bmBranchA = reader.GetOrdinal("bmBranch_a");
            this.bmBranchB = reader.GetOrdinal("bmBranch_b");
            this.specificLeafArea = reader.GetOrdinal("specificLeafArea");
            this.finerootFoliageRatio = reader.GetOrdinal("finerootFoliageRatio");
            this.barkThickness = reader.GetOrdinal("barkThickness");
            this.cnFoliage = reader.GetOrdinal("cnFoliage");
            this.cnFineroot = reader.GetOrdinal("cnFineroot");
            this.cnWood = reader.GetOrdinal("cnWood");
            this.turnoverLeaf = reader.GetOrdinal("turnoverLeaf");
            this.turnoverRoot = reader.GetOrdinal("turnoverRoot");
            this.hdLow = reader.GetOrdinal("HDlow");
            this.hdHigh = reader.GetOrdinal("HDhigh");
            this.woodDensity = reader.GetOrdinal("woodDensity");
            this.formFactor = reader.GetOrdinal("formFactor");
            this.snagKsw = reader.GetOrdinal("snagKSW"); // decay rate of SWD
            this.snagHalflife = reader.GetOrdinal("snagHalfLife");
            this.snagKyl = reader.GetOrdinal("snagKYL"); // decay rate labile
            this.snagKyr = reader.GetOrdinal("snagKYR"); // decay rate refractory matter
            this.maximumAge = reader.GetOrdinal("maximumAge");
            this.maximumHeight = reader.GetOrdinal("maximumHeight");
            this.aging = reader.GetOrdinal("aging");
            this.probIntrinsic = reader.GetOrdinal("probIntrinsic");
            this.probStress = reader.GetOrdinal("probStress");
            this.respVpdExponent = reader.GetOrdinal("respVpdExponent");
            this.respTempMin = reader.GetOrdinal("respTempMin");
            this.respTempMax = reader.GetOrdinal("respTempMax");
            this.respNitrogenClass = reader.GetOrdinal("respNitrogenClass");
            this.phenologyClass = reader.GetOrdinal("phenologyClass");
            this.maxCanopyConductance = reader.GetOrdinal("maxCanopyConductance");
            this.psiMin = reader.GetOrdinal("psiMin");
            this.lightResponseClass = reader.GetOrdinal("lightResponseClass");
            this.seedYearInterval = reader.GetOrdinal("seedYearInterval");
            this.maturityYears = reader.GetOrdinal("maturityYears");
            this.seedKernelAs1 = reader.GetOrdinal("seedKernel_as1");
            this.seedKernelAs2 = reader.GetOrdinal("seedKernel_as2");
            this.seedKernelKs0 = reader.GetOrdinal("seedKernel_ks0");
            this.fecundityM2 = reader.GetOrdinal("fecundity_m2");
            this.nonSeedYearFraction = reader.GetOrdinal("nonSeedYearFraction");
            this.serotinyFormula = reader.GetOrdinal("serotinyFormula");
            this.fecunditySerotiny = reader.GetOrdinal("serotinyFecundity");
            this.establishmentParametersMinTemp = reader.GetOrdinal("estMinTemp");
            this.establishmentParametersChillRequirement = reader.GetOrdinal("estChillRequirement");
            this.establishmentParametersGddMin = reader.GetOrdinal("estGDDMin");
            this.establishmentParametersGddMax = reader.GetOrdinal("estGDDMax");
            this.establishmentParametersGddBaseTemperature = reader.GetOrdinal("estGDDBaseTemp");
            this.establishmentParametersGddBudBurst = reader.GetOrdinal("estBudBirstGDD");
            this.establishmentParametersMinFrostFree = reader.GetOrdinal("estFrostFreeDays");
            this.establishmentParametersFrostTolerance = reader.GetOrdinal("estFrostTolerance");
            this.establishmentParametersPsiMin = reader.GetOrdinal("estPsiMin");
            this.saplingGrowthParametersHeightGrowthPotential = reader.GetOrdinal("sapHeightGrowthPotential");
            this.saplingGrowthParametersHdSapling = reader.GetOrdinal("sapHDSapling");
            this.saplingGrowthParametersStressThreshold = reader.GetOrdinal("sapStressThreshold");
            this.saplingGrowthParametersMaxStressYears = reader.GetOrdinal("sapMaxStressYears");
            this.saplingGrowthParametersReferenceRatio = reader.GetOrdinal("sapReferenceRatio");
            this.saplingGrowthParametersReinekesR = reader.GetOrdinal("sapReinekesR");
            this.saplingGrowthParametersBrowsingProbability = reader.GetOrdinal("browsingProbability");
            this.saplingGrowthParametersSproutGrowth = reader.GetOrdinal("sapSproutGrowth");
        }

        public bool Active() { return reader.GetBoolean(this.active); }
        public string ID() { return reader.GetString(this.id); }
        public string Name() { return reader.GetString(this.name); }
        public string LipFile() { return reader.GetString(this.lipFile); }
        public bool IsConiferous() { return reader.GetBoolean(this.isConiferous); }
        public bool IsEvergreen() { return reader.GetBoolean(this.isEvergreen); }
        public double BmFoliageA() { return reader.GetDouble(this.bmFoliageA); }
        public double BmFoliageB() { return reader.GetDouble(this.bfoliageB); }
        public double BmWoodyA() { return reader.GetDouble(this.bmWoodyA); }
        public double BmWoodyB() { return reader.GetDouble(this.bmWoodyB); }
        public double BmRootA() { return reader.GetDouble(this.bmRootA); }
        public double BmRootB() { return reader.GetDouble(this.bmRootB); }
        public double BmBranchA() { return reader.GetDouble(this.bmBranchA); }
        public double BmBranchB() { return reader.GetDouble(this.bmBranchB); }
        public double SpecificLeafArea() { return reader.GetDouble(this.specificLeafArea); }
        public double FinerootFoliageRatio() { return reader.GetDouble(this.finerootFoliageRatio); }
        public double BarkThickness() { return reader.GetDouble(this.barkThickness); }
        public double CnFoliage() { return reader.GetDouble(this.cnFoliage); }
        public double CnFineroot() { return reader.GetDouble(this.cnFineroot); }
        public double CnWood() { return reader.GetDouble(this.cnWood); }
        public double TurnoverLeaf() { return reader.GetDouble(this.turnoverLeaf); }
        public double TurnoverRoot() { return reader.GetDouble(this.turnoverRoot); }
        public string HdLow() { return reader.GetString(this.hdLow); }
        public string HdHigh() { return reader.GetString(this.hdHigh); }
        public double WoodDensity() { return reader.GetDouble(this.woodDensity); }
        public double FormFactor() { return reader.GetDouble(this.formFactor); }
        public double SnagKsw() { return reader.GetDouble(this.snagKsw); }
        public double SnagHalflife() { return reader.GetDouble(this.snagHalflife); }
        public double SnagKyl() { return reader.GetDouble(this.snagKyl); }
        public double SnagKyr() { return reader.GetDouble(this.snagKyr); }
        public double MaximumAge() { return reader.GetDouble(this.maximumAge); }
        public double MaximumHeight() { return reader.GetDouble(this.maximumHeight); }
        public string Aging() { return reader.GetString(this.aging); }
        public double ProbIntrinsic() { return reader.GetDouble(this.probIntrinsic); }
        public double ProbStress() { return reader.GetDouble(this.probStress); }
        public double RespVpdExponent() { return reader.GetDouble(this.respVpdExponent); }
        public double RespTempMin() { return reader.GetDouble(this.respTempMin); }
        public double RespTempMax() { return reader.GetDouble(this.respTempMax); }
        public double RespNitrogenClass() { return reader.GetDouble(this.respNitrogenClass); }
        public int PhenologyClass() { return reader.GetInt32(this.phenologyClass); }
        public double MaxCanopyConductance() { return reader.GetDouble(this.maxCanopyConductance); }
        public double PsiMin() { return -Math.Abs(reader.GetDouble(this.psiMin)); } // force a negative value
        public double LightResponseClass() { return reader.GetDouble(this.lightResponseClass); }
        public int SeedYearInterval() { return reader.GetInt32(this.seedYearInterval); }
        public int MaturityYears() { return reader.GetInt32(this.maturityYears); }
        public double SeedKernelAs1() { return reader.GetDouble(this.seedKernelAs1); }
        public double SeedKernelAs2() { return reader.GetDouble(this.seedKernelAs2); }
        public double SeedKernelKs0() { return reader.GetDouble(this.seedKernelKs0); }
        public double FecundityM2() { return reader.GetDouble(this.fecundityM2); }
        public double NonSeedYearFraction() { return reader.GetDouble(this.nonSeedYearFraction); }

        public string SerotinyFormula() 
        { 
            return reader.IsDBNull(this.serotinyFormula) ? null : reader.GetString(this.serotinyFormula); 
        }

        public double FecunditySerotiny()
        { 
            return reader.IsDBNull(this.fecunditySerotiny) ? 0.0 : reader.GetDouble(this.fecunditySerotiny); 
        }

        public double EstablishmentParametersMinTemp() { return reader.GetDouble(this.establishmentParametersMinTemp); }
        public int EstablishmentParametersChillRequirement() { return reader.GetInt32(this.establishmentParametersChillRequirement); }
        public int EstablishmentParametersGddMin() { return reader.GetInt32(this.establishmentParametersGddMin); }
        public int EstablishmentParametersGddMax() { return reader.GetInt32(this.establishmentParametersGddMax); }
        public double EstablishmentParametersGddBaseTemperature() { return reader.GetDouble(this.establishmentParametersGddBaseTemperature); }
        public int EstablishmentParametersGddBudBurst() { return reader.GetInt32(this.establishmentParametersGddBudBurst); }
        public int EstablishmentParametersMinFrostFree() { return reader.GetInt32(this.establishmentParametersMinFrostFree); }
        public double EstablishmentParametersFrostTolerance() { return reader.GetDouble(this.establishmentParametersFrostTolerance); }
        
        public double EstablishmentParametersPsiMin()
        {
            return reader.IsDBNull(this.establishmentParametersPsiMin) ? Double.NaN : -Math.Abs(reader.GetDouble(this.establishmentParametersPsiMin)); // force negative value
        }
        
        public string SaplingGrowthParametersHeightGrowthPotential() { return reader.GetString(this.saplingGrowthParametersHeightGrowthPotential); }
        public float SaplingGrowthParametersHdSapling() { return reader.GetFloat(this.saplingGrowthParametersHdSapling); }
        public double SaplingGrowthParametersStressThreshold() { return reader.GetDouble(this.saplingGrowthParametersStressThreshold); }
        public int SaplingGrowthParametersMaxStressYears() { return reader.GetInt32(this.saplingGrowthParametersMaxStressYears); }
        public double SaplingGrowthParametersReferenceRatio() { return reader.GetDouble(this.saplingGrowthParametersReferenceRatio); }
        public double SaplingGrowthParametersReinekesR() { return reader.GetDouble(this.saplingGrowthParametersReinekesR); }
        public double SaplingGrowthParametersBrowsingProbability() { return reader.GetDouble(this.saplingGrowthParametersBrowsingProbability); }
        
        public double SaplingGrowthParametersSproutGrowth() 
        { 
            return reader.IsDBNull(this.saplingGrowthParametersSproutGrowth) ? Double.NaN : reader.GetDouble(this.saplingGrowthParametersSproutGrowth); 
        }

        public void Dispose()
        {
            // Do not change this code. Put cleanup code in 'Dispose(bool disposing)' method
            this.Dispose(disposing: true);
            GC.SuppressFinalize(this);
        }

        protected virtual void Dispose(bool disposing)
        {
            if (!isDisposed)
            {
                if (disposing)
                {
                    this.reader.Dispose();
                }

                isDisposed = true;
            }
        }

        public bool Read()
        {
            return this.reader.Read();
        }
    }
}
