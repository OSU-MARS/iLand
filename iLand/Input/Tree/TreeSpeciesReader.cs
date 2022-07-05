using Microsoft.Data.Sqlite;
using System;

namespace iLand.Input.Tree
{
    public class TreeSpeciesReader : IDisposable
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
        private readonly int saplingGrowthReferenceRatio;
        private readonly int saplingGrowthParametersReinekesR;
        private readonly int saplingGrowthParametersBrowsingProbability;
        private readonly int saplingGrowthParametersSproutGrowth;

        public TreeSpeciesReader(SqliteDataReader reader)
        {
            isDisposed = false;
            this.reader = reader;

            active = reader.GetOrdinal("active");
            id = reader.GetOrdinal("shortName");
            name = reader.GetOrdinal("name");
            lipFile = reader.GetOrdinal("LIPfile");
            isConiferous = reader.GetOrdinal("isConiferous");
            isEvergreen = reader.GetOrdinal("isEvergreen");
            bmFoliageA = reader.GetOrdinal("bmFoliage_a");
            bfoliageB = reader.GetOrdinal("bmFoliage_b");
            bmWoodyA = reader.GetOrdinal("bmWoody_a");
            bmWoodyB = reader.GetOrdinal("bmWoody_b");
            bmRootA = reader.GetOrdinal("bmRoot_a");
            bmRootB = reader.GetOrdinal("bmRoot_b");
            bmBranchA = reader.GetOrdinal("bmBranch_a");
            bmBranchB = reader.GetOrdinal("bmBranch_b");
            specificLeafArea = reader.GetOrdinal("specificLeafArea");
            finerootFoliageRatio = reader.GetOrdinal("finerootFoliageRatio");
            barkThickness = reader.GetOrdinal("barkThickness");
            cnFoliage = reader.GetOrdinal("cnFoliage");
            cnFineroot = reader.GetOrdinal("cnFineroot");
            cnWood = reader.GetOrdinal("cnWood");
            turnoverLeaf = reader.GetOrdinal("turnoverLeaf");
            turnoverRoot = reader.GetOrdinal("turnoverRoot");
            hdLow = reader.GetOrdinal("HDlow");
            hdHigh = reader.GetOrdinal("HDhigh");
            woodDensity = reader.GetOrdinal("woodDensity");
            formFactor = reader.GetOrdinal("formFactor");
            snagKsw = reader.GetOrdinal("snagKSW"); // decay rate of SWD
            snagHalflife = reader.GetOrdinal("snagHalfLife");
            snagKyl = reader.GetOrdinal("snagKYL"); // decay rate labile
            snagKyr = reader.GetOrdinal("snagKYR"); // decay rate refractory matter
            maximumAge = reader.GetOrdinal("maximumAge");
            maximumHeight = reader.GetOrdinal("maximumHeight");
            aging = reader.GetOrdinal("aging");
            probIntrinsic = reader.GetOrdinal("probIntrinsic");
            probStress = reader.GetOrdinal("probStress");
            respVpdExponent = reader.GetOrdinal("respVpdExponent");
            respTempMin = reader.GetOrdinal("respTempMin");
            respTempMax = reader.GetOrdinal("respTempMax");
            respNitrogenClass = reader.GetOrdinal("respNitrogenClass");
            phenologyClass = reader.GetOrdinal("phenologyClass");
            maxCanopyConductance = reader.GetOrdinal("maxCanopyConductance");
            psiMin = reader.GetOrdinal("psiMin");
            lightResponseClass = reader.GetOrdinal("lightResponseClass");
            seedYearInterval = reader.GetOrdinal("seedYearInterval");
            maturityYears = reader.GetOrdinal("maturityYears");
            seedKernelAs1 = reader.GetOrdinal("seedKernel_as1");
            seedKernelAs2 = reader.GetOrdinal("seedKernel_as2");
            seedKernelKs0 = reader.GetOrdinal("seedKernel_ks0");
            fecundityM2 = reader.GetOrdinal("fecundity_m2");
            nonSeedYearFraction = reader.GetOrdinal("nonSeedYearFraction");
            serotinyFormula = reader.GetOrdinal("serotinyFormula");
            fecunditySerotiny = reader.GetOrdinal("serotinyFecundity");
            establishmentParametersMinTemp = reader.GetOrdinal("estMinTemp");
            establishmentParametersChillRequirement = reader.GetOrdinal("estChillRequirement");
            establishmentParametersGddMin = reader.GetOrdinal("estGDDMin");
            establishmentParametersGddMax = reader.GetOrdinal("estGDDMax");
            establishmentParametersGddBaseTemperature = reader.GetOrdinal("estGDDBaseTemp");
            establishmentParametersGddBudBurst = reader.GetOrdinal("estBudBirstGDD");
            establishmentParametersMinFrostFree = reader.GetOrdinal("estFrostFreeDays");
            establishmentParametersFrostTolerance = reader.GetOrdinal("estFrostTolerance");
            establishmentParametersPsiMin = reader.GetOrdinal("estPsiMin");
            saplingGrowthParametersHeightGrowthPotential = reader.GetOrdinal("sapHeightGrowthPotential");
            saplingGrowthParametersHdSapling = reader.GetOrdinal("sapHDSapling");
            saplingGrowthParametersStressThreshold = reader.GetOrdinal("sapStressThreshold");
            saplingGrowthParametersMaxStressYears = reader.GetOrdinal("sapMaxStressYears");
            saplingGrowthReferenceRatio = reader.GetOrdinal("sapReferenceRatio");
            saplingGrowthParametersReinekesR = reader.GetOrdinal("sapReinekesR");
            saplingGrowthParametersBrowsingProbability = reader.GetOrdinal("browsingProbability");
            saplingGrowthParametersSproutGrowth = reader.GetOrdinal("sapSproutGrowth");
        }

        public bool Active() { return reader.GetBoolean(active); }
        public string ID() { return reader.GetString(id); }
        public string Name() { return reader.GetString(name); }
        public string LipFile() { return reader.GetString(lipFile); }
        public bool IsConiferous() { return reader.GetBoolean(isConiferous); }
        public bool IsEvergreen() { return reader.GetBoolean(isEvergreen); }
        public float BmFoliageA() { return reader.GetFloat(bmFoliageA); }
        public float BmFoliageB() { return reader.GetFloat(bfoliageB); }
        public float BmWoodyA() { return reader.GetFloat(bmWoodyA); }
        public float BmWoodyB() { return reader.GetFloat(bmWoodyB); }
        public float BmRootA() { return reader.GetFloat(bmRootA); }
        public float BmRootB() { return reader.GetFloat(bmRootB); }
        public float BmBranchA() { return reader.GetFloat(bmBranchA); }
        public float BmBranchB() { return reader.GetFloat(bmBranchB); }
        public float SpecificLeafArea() { return reader.GetFloat(specificLeafArea); }
        public float FinerootFoliageRatio() { return reader.GetFloat(finerootFoliageRatio); }
        public float BarkThickness() { return reader.GetFloat(barkThickness); }
        public float CnFoliage() { return reader.GetFloat(cnFoliage); }
        public float CnFineroot() { return reader.GetFloat(cnFineroot); }
        public float CnWood() { return reader.GetFloat(cnWood); }
        public float TurnoverLeaf() { return reader.GetFloat(turnoverLeaf); }
        public float TurnoverRoot() { return reader.GetFloat(turnoverRoot); }
        public string HdLow() { return reader.GetString(hdLow); }
        public string HdHigh() { return reader.GetString(hdHigh); }
        public float WoodDensity() { return reader.GetFloat(woodDensity); }
        public float FormFactor() { return reader.GetFloat(formFactor); }
        public float SnagKsw() { return reader.GetFloat(snagKsw); }
        public float SnagHalflife() { return reader.GetFloat(snagHalflife); }
        public float SnagKyl() { return reader.GetFloat(snagKyl); }
        public float SnagKyr() { return reader.GetFloat(snagKyr); }
        public float MaximumAge() { return reader.GetFloat(maximumAge); }
        public float MaximumHeight() { return reader.GetFloat(maximumHeight); }
        public string Aging() { return reader.GetString(aging); }
        public float ProbIntrinsic() { return reader.GetFloat(probIntrinsic); }
        public float ProbStress() { return reader.GetFloat(probStress); }
        public float RespVpdExponent() { return reader.GetFloat(respVpdExponent); }
        public float RespTempMin() { return reader.GetFloat(respTempMin); }
        public float RespTempMax() { return reader.GetFloat(respTempMax); }
        public float RespNitrogenClass() { return reader.GetFloat(respNitrogenClass); }
        public int PhenologyClass() { return reader.GetInt32(phenologyClass); }
        public float MaxCanopyConductance() { return reader.GetFloat(maxCanopyConductance); }
        public float PsiMin() { return -Math.Abs(reader.GetFloat(psiMin)); } // force a negative value
        public float LightResponseClass() { return reader.GetFloat(lightResponseClass); }
        public int SeedYearInterval() { return reader.GetInt32(seedYearInterval); }
        public int MaturityYears() { return reader.GetInt32(maturityYears); }
        public float SeedKernelAs1() { return reader.GetFloat(seedKernelAs1); }
        public float SeedKernelAs2() { return reader.GetFloat(seedKernelAs2); }
        public float SeedKernelKs0() { return reader.GetFloat(seedKernelKs0); }
        public float FecundityM2() { return reader.GetFloat(fecundityM2); }
        public float NonSeedYearFraction() { return reader.GetFloat(nonSeedYearFraction); }

        public string? SerotinyFormula()
        {
            return reader.IsDBNull(serotinyFormula) ? null : reader.GetString(serotinyFormula);
        }

        public float FecunditySerotiny()
        {
            return reader.IsDBNull(fecunditySerotiny) ? 0.0F : reader.GetFloat(fecunditySerotiny);
        }

        public float EstablishmentParametersMinTemp() { return reader.GetFloat(establishmentParametersMinTemp); }
        public int EstablishmentParametersChillRequirement() { return reader.GetInt32(establishmentParametersChillRequirement); }
        public int EstablishmentParametersGddMin() { return reader.GetInt32(establishmentParametersGddMin); }
        public int EstablishmentParametersGddMax() { return reader.GetInt32(establishmentParametersGddMax); }
        public float EstablishmentParametersGddBaseTemperature() { return reader.GetFloat(establishmentParametersGddBaseTemperature); }
        public int EstablishmentParametersGddBudBurst() { return reader.GetInt32(establishmentParametersGddBudBurst); }
        public int EstablishmentParametersMinFrostFree() { return reader.GetInt32(establishmentParametersMinFrostFree); }
        public float EstablishmentParametersFrostTolerance() { return reader.GetFloat(establishmentParametersFrostTolerance); }

        public float EstablishmentParametersPsiMin()
        {
            if (reader.IsDBNull(establishmentParametersPsiMin))
            {
                return float.NaN;
            }

            return -MathF.Abs(reader.GetFloat(establishmentParametersPsiMin)); // force negative value
        }

        public string SaplingGrowthParametersHeightGrowthPotential() { return reader.GetString(saplingGrowthParametersHeightGrowthPotential); }
        public float SaplingGrowthParametersHdSapling() { return reader.GetFloat(saplingGrowthParametersHdSapling); }
        public float SaplingGrowthParametersStressThreshold() { return reader.GetFloat(saplingGrowthParametersStressThreshold); }
        public int SaplingGrowthParametersMaxStressYears() { return reader.GetInt32(saplingGrowthParametersMaxStressYears); }
        public float SaplingGrowthParametersReferenceRatio() { return reader.GetFloat(saplingGrowthReferenceRatio); }
        public float SaplingGrowthParametersReinekesR() { return reader.GetFloat(saplingGrowthParametersReinekesR); }
        public float SaplingGrowthParametersBrowsingProbability() { return reader.GetFloat(saplingGrowthParametersBrowsingProbability); }

        public float SaplingGrowthParametersSproutGrowth()
        {
            if (reader.IsDBNull(saplingGrowthParametersSproutGrowth))
            {
                return float.NaN;
            }

            return reader.GetFloat(saplingGrowthParametersSproutGrowth);
        }

        public void Dispose()
        {
            // Do not change this code. Put cleanup code in 'Dispose(bool disposing)' method
            Dispose(disposing: true);
            GC.SuppressFinalize(this);
        }

        protected virtual void Dispose(bool disposing)
        {
            if (!isDisposed)
            {
                if (disposing)
                {
                    reader.Dispose();
                }

                isDisposed = true;
            }
        }

        public bool Read()
        {
            return reader.Read();
        }
    }
}
