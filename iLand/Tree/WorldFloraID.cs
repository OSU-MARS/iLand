using System;

namespace iLand.Tree
{
    /// <summary>
    /// World Flora Online plant identifiers (https://wfoplantlist.org/) as of 2022-06.
    /// </summary>
    public enum WorldFloraID : UInt32
    {
        Default = 0, // not assigned by WFO
        AbiesAlba = 0000510976, // abal
        AbiesAmabilis = 0000510989, // abam
        AbiesGrandis = 0000511178, // abgr
        AbiesProcera = 0000511367, // abpr
        AcerCampestre = 0000514040, // acca
        AcerMacrophyllum = 0000514511, // acma
        AcerPlatanoides = 0000514884, // acpl
        AcerPseudoplatanus = 0000514908, // acps
        AlnusGlutinosa = 0000945215, // algl
        AlnusIncana = 0000945749, // alin
        AlnusRubra = 0000947467, // alru
        AlnusAlnobetula = 0000944137, // alal
        BetulaPendula = 0000335449, // bepe
        CarpinusBetulus = 0000804581, // cabe
        CastaneaSativa = 0000812271, // casa
        CorylusAvellana = 0000925259, // coav
        FagusSylvatica = 0000966507, // fasy
        FraxinusExcelsior = 502663, // frex
        LarixDecidua = 0000832453, // lade
        PiceaAbies = 0000482030, // piab
        PiceaSitchensis = 0000482639, // pisi
        PinusCembra = 0000482273, // pice
        PinusNigra = 0000481696, // pini (J.F.Arnold), also 0000481695 (Aiton)
        PinusPonderosa = 0000481903, // pipo
        PinusSylvestris = 0000481648, // pisy
        PopulusNigra = 0000928297, // poni
        PopulusTremula = 0000928205, // potr
        PseudotsugaMenziesii = 0000478194, // psme
        QuercusPetraea = 0000292459, // qupe
        QuercusPubescens = 0000292685, // qupu
        QuercusRobur = 0000292858, // quro
        RobiniaPseudoacacia = 0000213931, // rops
        SalixCaprea = 0000929313, // saca
        SorbusAria = 0000996176, // soar ((L.) Crantz)
        SorbusAucuparia = 0001016186, // soau (L.)
        ThujaPlicata = 0000407856, // thpl
        TiliaCordata = 0000457451, // tico
        TiliaPlatyphyllos = 0000456948, // tipl
        TsugaHeterophylla = 0000456392, // tshe
        TsugaMertensiana = 0000456459, // tsme
        UlmusGlabra = 0000416717, // ulgl (Huds.)
        Unknown = UInt32.MaxValue // not assigned by WFO
    }
}
