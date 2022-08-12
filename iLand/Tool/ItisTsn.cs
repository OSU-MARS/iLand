using System;

namespace iLand.Tool
{
    /// <summary>
    /// ITIS (Integrated Taxonomic Information System, https://itis.gov/) TSNs (taxonomic serial numbers)
    /// </summary>
    internal enum ItisTsn : UInt32
    {
        Default = 0,
        AbiesAlba = 506607, // abal
        AbiesAmabilis = 181824, // abam
        AbiesGrandis = 183284, // abgr
        AbiesProcera = 181835, // abpr
        AcerCampestre = 28739, // acca
        AcerMacrophyllum = 28748, // acma
        AcerPlatanoides = 28755, // acpl
        AcerPseudoplatanus = 28756, // acps
        AlnusGlutinosa = 19470, // algl
        AlnusIncana = 19471, // alin
        AlnusRubra = 19474, // alru
        AlnusViridis = 181892, // alvi
        BetulaPendula = 19495, // bepe
        CarpinusBetulus = 184204, // cabe
        CastaneaSativa = 506541, // casa
        CorylusAvellana = 501642, // coav
        FagusSylvatica = 502590, // fasy
        FraxinusExcelsior = 502663, // frex
        LarixDecidua = 183410, // lade
        PiceaAbies = 183289, // piab
        PiceaSitchensis = 183309, // pisi
        PinusCembra = 506605, // pice
        PinusNigra = 183364, // pini
        PinusPonderosa = 183365, // pipo
        PinusSylvestris = 183389, // pisy
        PopulusNigra = 22468, // poni
        PopulusTremula = 22473, // potr
        PseudotsugaMenziesii = 183424, // psme
        QuercusPetraea = 506539, // qupe
        // QuercusPubescens = , // qupu, no search hits in ITIS, inquiry sent to ITIS webmaster
        QuercusRobur = 19405, // quro
        RobiniaPseudoacacia = 504804, // rops
        SalixCaprea = 22515, // saca
        SorbusAria = 836433, // soar
        SorbusAucuparia = 25320, // soau
        ThujaPlicata = 18044, // thpl
        TiliaCordata = 505507, // tico
        TiliaPlatyphyllos = 21541, // tipl
        TsugaHeterophylla = 183400, // tshe
        TsugaMertensiana = 183402, // tsme
        UlmusGlabra = 19053, // ulgl -> Ulmus
        Unknown = UInt32.MaxValue
    }
}
