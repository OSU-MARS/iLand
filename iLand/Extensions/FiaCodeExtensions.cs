using iLand.Tree;

namespace iLand.Extensions
{
    internal static class FiaCodeExtensions
    {
        public static bool TryConvert(WorldFloraID speciesID, out FiaCode fiaCode)
        {
            fiaCode = speciesID switch
            {
                // FiaCode.Abies,
                WorldFloraID.AbiesAmabilis => FiaCode.AbiesAmabilis,
                WorldFloraID.AbiesGrandis => FiaCode.AbiesGrandis,
                WorldFloraID.AbiesProcera => FiaCode.AbiesProcera,
                WorldFloraID.AcerCampestre => FiaCode.AcerCampestre,
                WorldFloraID.AcerMacrophyllum => FiaCode.AcerMacrophyllum,
                WorldFloraID.AcerPlatanoides => FiaCode.AcerPlatanoides,
                WorldFloraID.AcerPseudoplatanus => FiaCode.AcerPseudoplatanus,
                WorldFloraID.AlnusGlutinosa => FiaCode.AlnusGlutinosa,
                WorldFloraID.AlnusIncana => FiaCode.AlnusIncana,
                WorldFloraID.AlnusRubra => FiaCode.AlnusRubra,
                WorldFloraID.AlnusAlnobetula => FiaCode.AlnusViridis, // Alnus viridis	(Chaix) DC. is a synonym of Alnus alnobetula (Ehrh.) K.Koch, https://wfoplantlist.org/plant-list/taxon/wfo-0000944137-2022-06
                WorldFloraID.BetulaPendula => FiaCode.BetulaPendula,
                WorldFloraID.CarpinusBetulus => FiaCode.CarpinusBetulus,
                WorldFloraID.CastaneaSativa => FiaCode.CastaneaSativa,
                WorldFloraID.Default => FiaCode.Default, // debatable
                WorldFloraID.FagusSylvatica => FiaCode.FagusSylvatica,
                WorldFloraID.FraxinusExcelsior => FiaCode.FraxinusExcelsior,
                WorldFloraID.LarixDecidua => FiaCode.LarixDecidua,
                WorldFloraID.PiceaAbies => FiaCode.PiceaAbies,
                WorldFloraID.PiceaSitchensis => FiaCode.PiceaSitchensis,
                // FiaCode.Pinus,
                WorldFloraID.PinusNigra => FiaCode.PinusNigra,
                WorldFloraID.PinusPonderosa => FiaCode.PinusPonderosa,
                WorldFloraID.PinusSylvestris => FiaCode.PinusSylvestris,
                // FiaCode.Populus,
                WorldFloraID.PopulusNigra => FiaCode.PopulusNigra,
                WorldFloraID.PseudotsugaMenziesii => FiaCode.PseudotsugaMenziesii,
                WorldFloraID.QuercusPetraea => FiaCode.QuercusPetraea,
                WorldFloraID.QuercusRobur => FiaCode.QuercusRobur,
                WorldFloraID.RobiniaPseudoacacia => FiaCode.RobiniaPseudoacacia,
                // FiaCode.Salix,
                // FiaCode.Sorbus,
                WorldFloraID.SorbusAucuparia => FiaCode.SorbusAucuparia,
                WorldFloraID.ThujaPlicata => FiaCode.ThujaPlicata,
                WorldFloraID.TiliaCordata => FiaCode.TiliaCordata,
                WorldFloraID.TiliaPlatyphyllos => FiaCode.TiliaPlatyphyllos,
                WorldFloraID.TsugaHeterophylla => FiaCode.TsugaHeterophylla,
                WorldFloraID.TsugaMertensiana => FiaCode.TsugaMertensiana,
                // FiaCode.Ulmus,
                // no FIA code
                WorldFloraID.AbiesAlba or 
                WorldFloraID.CorylusAvellana or
                WorldFloraID.PinusCembra or
                WorldFloraID.PopulusTremula or
                WorldFloraID.QuercusPubescens or
                WorldFloraID.SalixCaprea or
                WorldFloraID.SorbusAria or
                WorldFloraID.UlmusGlabra or
                WorldFloraID.Unknown or
                _ => FiaCode.Unknown
            };

            return fiaCode != FiaCode.Unknown;
        }

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
                // "qupu" => FiaCode.QuercusPubescens, // no FIA code
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
