using iLand.Tool;

namespace iLand.Extensions
{
    internal static class FiaCodeExtensions
    {
        public static bool TryParse(string iLandFourLetterSpeciesCode, out FiaCode fiaCode)
        {
            fiaCode = iLandFourLetterSpeciesCode switch
            {
                // "abal" => FiaCode.AbiesAlba, // no FIA code
                "abam" => FiaCode.AbiesAmabilis,
                "abgr" => FiaCode.AbiesGrandis,
                "abpr" => FiaCode.AbiesProcera,
                "acca" => FiaCode.AcerCampestre,
                "acma" => FiaCode.AcerMacrophyllum,
                "acpl" => FiaCode.AcerPlatanoides,
                "acps" => FiaCode.AcerPseudoplatanus,
                "algl" => FiaCode.AlnusGlutinosa,
                "alin" => FiaCode.AlnusIncana,
                "alru" => FiaCode.AlnusRubra,
                "alvi" => FiaCode.AlnusViridis,
                "bepe" => FiaCode.BetulaPendula,
                "cabe" => FiaCode.CarpinusBetulus,
                "casa" => FiaCode.CastaneaSativa,
                // "coav" => FiaCode.CorylusAvellana, // no FIA code
                "fasy" => FiaCode.FagusSylvatica,
                "frex" => FiaCode.FraxinusExcelsior,
                "lade" => FiaCode.LarixDecidua,
                "piab" => FiaCode.PiceaAbies,
                // "pice" => FiaCode.PinusCembra, // no FIA code
                "pini" => FiaCode.PinusNigra,
                "pipo" => FiaCode.PinusPonderosa,
                "pisi" => FiaCode.PiceaSitchensis,
                "pisy" => FiaCode.PinusSylvestris,
                "poni" => FiaCode.PopulusNigra,
                // "potr" => FiaCode.PopulusTremula, // no FIA code
                "psme" => FiaCode.PseudotsugaMenziesii,
                "qupe" => FiaCode.QuercusPetraea,
                // "qupu" => FiaCode.QuercusPubescence, // no FIA code
                "quro" => FiaCode.QuercusRobur,
                "rops" => FiaCode.RobiniaPseudoacacia,
                // "saca" => FiaCode.SalixCaprea, // no FIA code
                // "soar" => FiaCode.SorbusAria, // no FIA code
                "soau" => FiaCode.SorbusAucuparia,
                "thpl" => FiaCode.ThujaPlicata,
                "tico" => FiaCode.TiliaCordata,
                "tipl" => FiaCode.TiliaPlatyphyllos,
                "tshe" => FiaCode.TsugaHeterophylla,
                "tsme" => FiaCode.TsugaMertensiana,
                // "ulgl" => FiaCode.UlmusGlabra, // no FIA code
                _ => FiaCode.Unknown
            };

            return fiaCode != FiaCode.Unknown;
        }
    }
}
