using System;

namespace iLand.Tool
{
    /// <summary>
    /// USFS FIA species codes
    /// </summary>
    /// <remarks>
    /// From FIA master tree species list v9.1 (https://www.fia.fs.fed.us/library/field-guides-methods-proc/)
    /// </remarks>
    internal enum FiaCode : UInt16
    {
        Default = 0,
        Abies = 10, // (abal)
        AbiesAmabilis = 11, // abam
        AbiesGrandis = 17, // abgr
        AbiesProcera = 22, // abpr
        AcerCampestre = 5145, // acca
        AcerMacrophyllum = 312, // acma
        AcerPlatanoides = 320, // acpl
        AcerPseudoplatanus = 5151, // acps
        AlnusGlutinosa = 355, // algl
        AlnusIncana = 5188, // alin
        AlnusRubra = 351, // alru
        AlnusViridis = 5192, // alvi
        BetulaPendula = 5279, // bepe
        CarpinusBetulus = 5378, // cabe
        CastaneaSativa = 5402, // casa
        // only FIA code for Corylus is C. colurna, (coav)
        FagusSylvatica = 5983, // fasy
        FraxinusExcelsior = 5492, // frex
        LarixDecidua = 6212, // lade
        PiceaAbies = 91, // piab
        PiceaSitchensis = 98, // pisi
        Pinus = 100, // (pice)
        PinusNigra = 136, // pini
        PinusPonderosa = 122, // pipo
        PinusSylvestris = 130, // pisy
        PopulusNigra = 753, // poni
        Populus = 740, // (potr)
        PseudotsugaMenziesii = 202, // psme
        QuercusPetraea = 6794, // qupe
        Quercus = 800, // (qupu)
        QuercusRobur = 6797, // quro
        RobiniaPseudoacacia = 901, // rops
        Salix = 920, // (saca)
        Sorbus = 934, // (soar)
        SorbusAucuparia = 936, // soau
        ThujaPlicata = 242, // thpl
        TiliaCordata = 8813, // tico
        TiliaPlatyphyllos = 950, // tipl
        TsugaHeterophylla = 263, // tshe
        TsugaMertensiana = 264, // tsme
        Ulmus = 970, // (ulgl)
        Unknown = UInt16.MaxValue
    }
}
