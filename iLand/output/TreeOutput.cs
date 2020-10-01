using iLand.Core;
using iLand.Tools;
using Microsoft.Data.Sqlite;
using System;
using System.Diagnostics;

namespace iLand.Output
{
    internal class TreeOutput : Output
    {
        private readonly Expression mFilter;

        public TreeOutput()
        {
            this.mFilter = new Expression();

            Name = "Tree Output";
            TableName = "tree";
            Description = "Output of indivdual trees. Use the ''filter'' property to reduce amount of data (filter by resource-unit, year, species, ...)." + System.Environment.NewLine +
                          "The output is triggered after the growth of the current season. " +
                          "Initial values (without any growth) are output as 'startyear-1'.";
            Columns.Add(SqlColumn.CreateYear());
            Columns.Add(SqlColumn.CreateResourceUnit());
            Columns.Add(SqlColumn.CreateID());
            Columns.Add(SqlColumn.CreateSpecies());
            Columns.Add(new SqlColumn("id", "id of the tree", OutputDatatype.OutInteger));
            Columns.Add(new SqlColumn("x", "position of the tree, x-direction (m)", OutputDatatype.OutDouble));
            Columns.Add(new SqlColumn("y", "position of the tree, y-direction (m)", OutputDatatype.OutDouble));
            Columns.Add(new SqlColumn("dbh", "dbh (cm) of the tree", OutputDatatype.OutDouble));
            Columns.Add(new SqlColumn("height", "height (m) of the tree", OutputDatatype.OutDouble));
            Columns.Add(new SqlColumn("basalArea", "basal area of tree in m2", OutputDatatype.OutDouble));
            Columns.Add(new SqlColumn("volume_m3", "volume of tree (m3)", OutputDatatype.OutDouble));
            Columns.Add(new SqlColumn("leafArea_m2", "current leaf area of the tree (m2)", OutputDatatype.OutDouble));
            Columns.Add(new SqlColumn("foliageMass", "current mass of foliage (kg)", OutputDatatype.OutDouble));
            Columns.Add(new SqlColumn("woodyMass", "kg Biomass in woody department", OutputDatatype.OutDouble));
            Columns.Add(new SqlColumn("fineRootMass", "kg Biomass in fine-root department", OutputDatatype.OutDouble));
            Columns.Add(new SqlColumn("coarseRootMass", "kg Biomass in coarse-root department", OutputDatatype.OutDouble));
            Columns.Add(new SqlColumn("lri", "LightResourceIndex of the tree (raw light index from iLand, without applying resource-unit modifications)", OutputDatatype.OutDouble));
            Columns.Add(new SqlColumn("lightResponse", "light response value (including species specific response to the light level)", OutputDatatype.OutDouble));
            Columns.Add(new SqlColumn("stressIndex", "scalar (0..1) indicating the stress level (see [Mortality]).", OutputDatatype.OutDouble));
            Columns.Add(new SqlColumn("reserve_kg", "NPP currently available in the reserve pool (kg Biomass)", OutputDatatype.OutDouble));
        }

        public override void Setup()
        {
            if (!this.Settings().IsValid())
            {
                throw new NotSupportedException("No parameter section in project file.");
            }
            string filter = Settings().GetString(".filter", "");
            mFilter.SetExpression(filter);
        }

        protected override void LogYear(SqliteCommand insertRow)
        {
            AllTreeIterator at = new AllTreeIterator(GlobalSettings.Instance.Model);
            using DebugTimer dt = new DebugTimer("TreeOutput.LogYear()");
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
                this.WriteRow(insertRow);
            }
        }
    }
}
