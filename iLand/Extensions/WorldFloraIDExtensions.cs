using iLand.Tree;
using System;

namespace iLand.Extensions
{
    internal static class WorldFloraIDExtensions
    {
        public static WorldFloraID Convert(FiaCode fiaCode)
        {
            return fiaCode switch
            {
                // WorldFloraID.AbiesAlba, no FIA code
                FiaCode.AbiesAmabilis => WorldFloraID.AbiesAmabilis,
                FiaCode.AbiesGrandis => WorldFloraID.AbiesGrandis,
                FiaCode.AbiesProcera => WorldFloraID.AbiesProcera,
                FiaCode.AcerCampestre => WorldFloraID.AcerCampestre,
                FiaCode.AcerMacrophyllum => WorldFloraID.AcerMacrophyllum,
                FiaCode.AcerPlatanoides => WorldFloraID.AcerPlatanoides,
                FiaCode.AcerPseudoplatanus => WorldFloraID.AcerPseudoplatanus,
                FiaCode.AlnusGlutinosa => WorldFloraID.AlnusGlutinosa,
                FiaCode.AlnusIncana => WorldFloraID.AlnusIncana,
                FiaCode.AlnusRubra => WorldFloraID.AlnusRubra,
                FiaCode.AlnusViridis => WorldFloraID.AlnusAlnobetula,
                FiaCode.BetulaPendula => WorldFloraID.BetulaPendula,
                FiaCode.CarpinusBetulus => WorldFloraID.CarpinusBetulus,
                FiaCode.CastaneaSativa => WorldFloraID.CastaneaSativa,
                // WorldFloraID.CorylusAvellana, no FIA code
                FiaCode.FagusSylvatica => WorldFloraID.FagusSylvatica,
                FiaCode.FraxinusExcelsior => WorldFloraID.FraxinusExcelsior,
                FiaCode.LarixDecidua => WorldFloraID.LarixDecidua,
                FiaCode.PiceaAbies => WorldFloraID.PiceaAbies,
                // WorldFloraID.PinusCembra, no FIA code
                FiaCode.PinusNigra => WorldFloraID.PinusNigra,
                FiaCode.PinusPonderosa => WorldFloraID.PinusPonderosa,
                FiaCode.PiceaSitchensis => WorldFloraID.PiceaSitchensis,
                FiaCode.PinusSylvestris => WorldFloraID.PinusSylvestris,
                FiaCode.PopulusNigra => WorldFloraID.PopulusNigra,
                // WorldFloraID.PopulusTremula, no FIA code
                FiaCode.PseudotsugaMenziesii => WorldFloraID.PseudotsugaMenziesii,
                FiaCode.QuercusPetraea => WorldFloraID.QuercusPetraea,
                // WorldFloraID.QuercusPubescens, // no FIA code
                FiaCode.QuercusRobur => WorldFloraID.QuercusRobur,
                FiaCode.RobiniaPseudoacacia => WorldFloraID.RobiniaPseudoacacia,
                // WorldFloraID.SalixCaprea, no FIA code
                // WorldFloraID.SorbusAria, no FIA code
                FiaCode.SorbusAucuparia => WorldFloraID.SorbusAucuparia,
                FiaCode.ThujaPlicata => WorldFloraID.ThujaPlicata,
                FiaCode.TiliaCordata => WorldFloraID.TiliaCordata,
                FiaCode.TiliaPlatyphyllos => WorldFloraID.TiliaPlatyphyllos,
                FiaCode.TsugaHeterophylla => WorldFloraID.TsugaHeterophylla,
                FiaCode.TsugaMertensiana => WorldFloraID.TsugaMertensiana,
                // WorldFloraID.UlmusGlabra, no FIA code
                _ => throw new ArgumentOutOfRangeException(nameof(fiaCode), "A World Flora Online identifier for FIA code " + fiaCode + " is not known.")
            };
        }

        public static WorldFloraID Parse(string iLandFourLetterSpeciesCode)
        {
            if (WorldFloraIDExtensions.TryParse(iLandFourLetterSpeciesCode, out WorldFloraID speciesID) == false)
            {
                throw new ArgumentOutOfRangeException(nameof(iLandFourLetterSpeciesCode), "A World Flora Online identifier for species abbreviation '" + iLandFourLetterSpeciesCode + "' is not known.");
            }

            return speciesID;
        }

        public static string ToSpeciesAbbreviation(this WorldFloraID speciesID)
        {
            return speciesID switch
            {
                WorldFloraID.AbiesAlba => "abal",
                WorldFloraID.AbiesAmabilis => "abam",
                WorldFloraID.AbiesGrandis => "abgr",
                WorldFloraID.AbiesProcera => "abpr",
                WorldFloraID.AcerCampestre => "acca",
                WorldFloraID.AcerMacrophyllum => "acma",
                WorldFloraID.AcerPlatanoides => "acpl",
                WorldFloraID.AcerPseudoplatanus => "acps",
                WorldFloraID.AlnusAlnobetula => "alal",
                WorldFloraID.AlnusGlutinosa => "algl",
                WorldFloraID.AlnusIncana => "alin",
                WorldFloraID.AlnusRubra => "alru",
                WorldFloraID.BetulaPendula => "bepe",
                WorldFloraID.CarpinusBetulus => "cabe",
                WorldFloraID.CastaneaSativa => "casa",
                WorldFloraID.CorylusAvellana => "coav",
                WorldFloraID.FagusSylvatica => "fasy",
                WorldFloraID.FraxinusExcelsior => "frex",
                WorldFloraID.LarixDecidua => "lade",
                WorldFloraID.PiceaAbies => "piab",
                WorldFloraID.PiceaSitchensis => "pisi",
                WorldFloraID.PinusCembra => "pice",
                WorldFloraID.PinusNigra => "pini",
                WorldFloraID.PinusPonderosa => "pipo",
                WorldFloraID.PinusSylvestris => "pisy",
                WorldFloraID.PopulusNigra => "poni",
                WorldFloraID.PopulusTremula => "potr",
                WorldFloraID.PseudotsugaMenziesii => "psme",
                WorldFloraID.QuercusPetraea => "qupe",
                WorldFloraID.QuercusPubescens => "qupu",
                WorldFloraID.QuercusRobur => "quro",
                WorldFloraID.RobiniaPseudoacacia => "rops",
                WorldFloraID.SalixCaprea => "saca",
                WorldFloraID.SorbusAria => "soar",
                WorldFloraID.SorbusAucuparia => "soau",
                WorldFloraID.ThujaPlicata => "thpl",
                WorldFloraID.TiliaCordata => "tico",
                WorldFloraID.TiliaPlatyphyllos => "tipl",
                WorldFloraID.TsugaHeterophylla => "tshe",
                WorldFloraID.TsugaMertensiana => "tsme",
                WorldFloraID.UlmusGlabra => "ulgl",
                WorldFloraID.Default or
                WorldFloraID.Unknown or
                _ => throw new ArgumentOutOfRangeException(nameof(speciesID), "An iLand species abbreviation for World Flora Online identifier " + speciesID + " is not known.")
            };
        }

        public static bool TryParse(string iLandFourLetterSpeciesCode, out WorldFloraID speciesID)
        {
            // switch (ReadOnlySpan<char>) is in preview as of VS 17.3.0 (https://github.com/dotnet/csharplang/issues/1881)
            speciesID = iLandFourLetterSpeciesCode switch
            {
                "abal" => WorldFloraID.AbiesAlba,
                "abam" => WorldFloraID.AbiesAmabilis,
                "abgr" => WorldFloraID.AbiesGrandis,
                "abpr" => WorldFloraID.AbiesProcera,
                "acca" => WorldFloraID.AcerCampestre,
                "acma" => WorldFloraID.AcerMacrophyllum,
                "acpl" => WorldFloraID.AcerPlatanoides,
                "acps" => WorldFloraID.AcerPseudoplatanus,
                "alal" => WorldFloraID.AlnusAlnobetula,
                "algl" => WorldFloraID.AlnusGlutinosa,
                "alin" => WorldFloraID.AlnusIncana,
                "alru" => WorldFloraID.AlnusRubra,
                "bepe" => WorldFloraID.BetulaPendula,
                "cabe" => WorldFloraID.CarpinusBetulus,
                "casa" => WorldFloraID.CastaneaSativa,
                "coav" => WorldFloraID.CorylusAvellana,
                "fasy" => WorldFloraID.FagusSylvatica,
                "frex" => WorldFloraID.FraxinusExcelsior,
                "lade" => WorldFloraID.LarixDecidua,
                "piab" => WorldFloraID.PiceaAbies,
                "pice" => WorldFloraID.PinusCembra,
                "pini" => WorldFloraID.PinusNigra,
                "pipo" => WorldFloraID.PinusPonderosa,
                "pisi" => WorldFloraID.PiceaSitchensis,
                "pisy" => WorldFloraID.PinusSylvestris,
                "poni" => WorldFloraID.PopulusNigra,
                "potr" => WorldFloraID.PopulusTremula,
                "psme" => WorldFloraID.PseudotsugaMenziesii,
                "qupe" => WorldFloraID.QuercusPetraea,
                "qupu" => WorldFloraID.QuercusPubescens,
                "quro" => WorldFloraID.QuercusRobur,
                "rops" => WorldFloraID.RobiniaPseudoacacia,
                "saca" => WorldFloraID.SalixCaprea,
                "soar" => WorldFloraID.SorbusAria,
                "soau" => WorldFloraID.SorbusAucuparia,
                "thpl" => WorldFloraID.ThujaPlicata,
                "tico" => WorldFloraID.TiliaCordata,
                "tipl" => WorldFloraID.TiliaPlatyphyllos,
                "tshe" => WorldFloraID.TsugaHeterophylla,
                "tsme" => WorldFloraID.TsugaMertensiana,
                "ulgl" => WorldFloraID.UlmusGlabra,
                _ => WorldFloraID.Unknown
            };

            return speciesID != WorldFloraID.Unknown;
        }
    }
}
