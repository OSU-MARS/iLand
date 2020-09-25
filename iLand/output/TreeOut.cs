using iLand.core;
using iLand.tools;
using System;
using System.Diagnostics;

namespace iLand.output
{
    internal class TreeOut : Output
    {
        private readonly Expression mFilter;

        public TreeOut()
        {
            this.mFilter = new Expression();

            Name = "Tree Output";
            TableName = "tree";
            Description = "Output of indivdual trees. Use the ''filter'' property to reduce amount of data (filter by resource-unit, year, species, ...)." + System.Environment.NewLine +
                          "The output is triggered after the growth of the current season. " +
                          "Initial values (without any growth) are output as 'startyear-1'.";
            Columns.Add(OutputColumn.CreateYear());
            Columns.Add(OutputColumn.CreateResourceUnit());
            Columns.Add(OutputColumn.CreateID());
            Columns.Add(OutputColumn.CreateSpecies());
            Columns.Add(new OutputColumn("id", "id of the tree", OutputDatatype.OutInteger));
            Columns.Add(new OutputColumn("x", "position of the tree, x-direction (m)", OutputDatatype.OutDouble));
            Columns.Add(new OutputColumn("y", "position of the tree, y-direction (m)", OutputDatatype.OutDouble));
            Columns.Add(new OutputColumn("dbh", "dbh (cm) of the tree", OutputDatatype.OutDouble));
            Columns.Add(new OutputColumn("height", "height (m) of the tree", OutputDatatype.OutDouble));
            Columns.Add(new OutputColumn("basalArea", "basal area of tree in m2", OutputDatatype.OutDouble));
            Columns.Add(new OutputColumn("volume_m3", "volume of tree (m3)", OutputDatatype.OutDouble));
            Columns.Add(new OutputColumn("leafArea_m2", "current leaf area of the tree (m2)", OutputDatatype.OutDouble));
            Columns.Add(new OutputColumn("foliageMass", "current mass of foliage (kg)", OutputDatatype.OutDouble));
            Columns.Add(new OutputColumn("woodyMass", "kg Biomass in woody department", OutputDatatype.OutDouble));
            Columns.Add(new OutputColumn("fineRootMass", "kg Biomass in fine-root department", OutputDatatype.OutDouble));
            Columns.Add(new OutputColumn("coarseRootMass", "kg Biomass in coarse-root department", OutputDatatype.OutDouble));
            Columns.Add(new OutputColumn("lri", "LightResourceIndex of the tree (raw light index from iLand, without applying resource-unit modifications)", OutputDatatype.OutDouble));
            Columns.Add(new OutputColumn("lightResponse", "light response value (including species specific response to the light level)", OutputDatatype.OutDouble));
            Columns.Add(new OutputColumn("stressIndex", "scalar (0..1) indicating the stress level (see [Mortality]).", OutputDatatype.OutDouble));
            Columns.Add(new OutputColumn("reserve_kg", "NPP currently available in the reserve pool (kg Biomass)", OutputDatatype.OutDouble));
        }

        public override void Setup()
        {
            Debug.WriteLine("treeout::setup() called");
            if (!Settings().IsValid())
            {
                throw new NotSupportedException("setup(): no parameter section in init file!");
            }
            string filter = Settings().Value(".filter", "");
            mFilter.SetExpression(filter);
        }

        public override void Exec()
        {
            AllTreeIterator at = new AllTreeIterator(GlobalSettings.Instance.Model);
            using DebugTimer dt = new DebugTimer("exec()");
            TreeWrapper tw = new TreeWrapper();
            mFilter.Wrapper = tw;
            for (Tree t = at.MoveNext(); t != null; t = at.MoveNext())
            {
                if (!mFilter.IsEmpty)
                { // skip fields
                    tw.Tree = t;
                    if (mFilter.Execute() == 0.0)
                    {
                        continue;
                    }
                }
                this.Add(CurrentYear());
                this.Add(t.RU.Index);
                this.Add(t.RU.ID);
                this.Add(t.Species.ID);
                this.Add(t.ID);
                this.Add(t.GetCellCenterPoint().X);
                this.Add(t.GetCellCenterPoint().Y);
                this.Add(t.Dbh);
                this.Add(t.Height);
                this.Add(t.BasalArea());
                this.Add(t.Volume());
                this.Add(t.LeafArea);
                this.Add(t.FoliageMass);
                this.Add(t.StemMass);
                this.Add(t.FineRootMass);
                this.Add(t.CoarseRootMass);
                this.Add(t.LightResourceIndex);
                this.Add(t.mLightResponse);
                this.Add(t.StressIndex);
                this.Add(t.mNPPReserve);
                WriteRow();
            }
        }
    }
}
