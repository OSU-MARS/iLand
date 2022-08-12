using iLand.Tool;

namespace iLand.Extensions
{
    internal static class ItisTsnExtensions
    {
        public static bool TryParse(string iLandFourLetterSpeciesCode, out ItisTsn itisTsn)
        {
            itisTsn = iLandFourLetterSpeciesCode switch
            {
                "abal" => ItisTsn.AbiesAlba,
                "abam" => ItisTsn.AbiesAmabilis,
                "abgr" => ItisTsn.AbiesGrandis,
                "abpr" => ItisTsn.AbiesProcera,
                "acca" => ItisTsn.AcerCampestre,
                "acma" => ItisTsn.AcerMacrophyllum,
                "acpl" => ItisTsn.AcerPlatanoides,
                "acps" => ItisTsn.AcerPseudoplatanus,
                "algl" => ItisTsn.AlnusGlutinosa,
                "alin" => ItisTsn.AlnusIncana,
                "alru" => ItisTsn.AlnusRubra,
                "alvi" => ItisTsn.AlnusViridis,
                "bepe" => ItisTsn.BetulaPendula,
                "cabe" => ItisTsn.CarpinusBetulus,
                "casa" => ItisTsn.CastaneaSativa,
                "coav" => ItisTsn.CorylusAvellana,
                "fasy" => ItisTsn.FagusSylvatica,
                "frex" => ItisTsn.FraxinusExcelsior,
                "lade" => ItisTsn.LarixDecidua,
                "piab" => ItisTsn.PiceaAbies,
                "pice" => ItisTsn.PinusCembra,
                "pini" => ItisTsn.PinusNigra,
                "pipo" => ItisTsn.PinusPonderosa,
                "pisi" => ItisTsn.PiceaSitchensis,
                "pisy" => ItisTsn.PinusSylvestris,
                "poni" => ItisTsn.PopulusNigra,
                "potr" => ItisTsn.PopulusTremula,
                "psme" => ItisTsn.PseudotsugaMenziesii,
                "qupe" => ItisTsn.QuercusPetraea,
                // "qupu" => ItisTsn.QuercusPubescens,
                "quro" => ItisTsn.QuercusRobur,
                "rops" => ItisTsn.RobiniaPseudoacacia,
                "saca" => ItisTsn.SalixCaprea,
                "soar" => ItisTsn.SorbusAria,
                "soau" => ItisTsn.SorbusAucuparia,
                "thpl" => ItisTsn.ThujaPlicata,
                "tico" => ItisTsn.TiliaCordata,
                "tipl" => ItisTsn.TiliaPlatyphyllos,
                "tshe" => ItisTsn.TsugaHeterophylla,
                "tsme" => ItisTsn.TsugaMertensiana,
                "ulgl" => ItisTsn.UlmusGlabra,
                _ => ItisTsn.Unknown
            };

            return itisTsn != ItisTsn.Unknown;
        }
    }
}
