using iLand.core;
using iLand.tools;
using System;
using System.Diagnostics;

namespace iLand.output
{
    internal class TreeOut : Output
    {
        private Expression mFilter;

        public TreeOut()
        {
            setName("Tree Output", "tree");
            setDescription("Output of indivdual trees. Use the ''filter'' property to reduce amount of data (filter by resource-unit, year, species, ...)." + System.Environment.NewLine +
                       "The output is triggered after the growth of the current season. " +
                       "Initial values (without any growth) are output as 'startyear-1'.");
            columns().Add(OutputColumn.year());
            columns().Add(OutputColumn.ru());
            columns().Add(OutputColumn.id());
            columns().Add(OutputColumn.species());
            columns().Add(new OutputColumn("id", "id of the tree", OutputDatatype.OutInteger));
            columns().Add(new OutputColumn("x", "position of the tree, x-direction (m)", OutputDatatype.OutDouble));
            columns().Add(new OutputColumn("y", "position of the tree, y-direction (m)", OutputDatatype.OutDouble));
            columns().Add(new OutputColumn("dbh", "dbh (cm) of the tree", OutputDatatype.OutDouble));
            columns().Add(new OutputColumn("height", "height (m) of the tree", OutputDatatype.OutDouble));
            columns().Add(new OutputColumn("basalArea", "basal area of tree in m2", OutputDatatype.OutDouble));
            columns().Add(new OutputColumn("volume_m3", "volume of tree (m3)", OutputDatatype.OutDouble));
            columns().Add(new OutputColumn("leafArea_m2", "current leaf area of the tree (m2)", OutputDatatype.OutDouble));
            columns().Add(new OutputColumn("foliageMass", "current mass of foliage (kg)", OutputDatatype.OutDouble));
            columns().Add(new OutputColumn("woodyMass", "kg Biomass in woody department", OutputDatatype.OutDouble));
            columns().Add(new OutputColumn("fineRootMass", "kg Biomass in fine-root department", OutputDatatype.OutDouble));
            columns().Add(new OutputColumn("coarseRootMass", "kg Biomass in coarse-root department", OutputDatatype.OutDouble));
            columns().Add(new OutputColumn("lri", "LightResourceIndex of the tree (raw light index from iLand, without applying resource-unit modifications)", OutputDatatype.OutDouble));
            columns().Add(new OutputColumn("lightResponse", "light response value (including species specific response to the light level)", OutputDatatype.OutDouble));
            columns().Add(new OutputColumn("stressIndex", "scalar (0..1) indicating the stress level (see [Mortality]).", OutputDatatype.OutDouble));
            columns().Add(new OutputColumn("reserve_kg", "NPP currently available in the reserve pool (kg Biomass)", OutputDatatype.OutDouble));
        }

        public void setup()
        {
            Debug.WriteLine("treeout::setup() called");
            if (!settings().isValid())
            {
                throw new NotSupportedException("setup(): no parameter section in init file!");
            }
            string filter = settings().value(".filter", "");
            mFilter.setExpression(filter);
        }

        public override void exec()
        {
            AllTreeIterator at = new AllTreeIterator(GlobalSettings.instance().model());
            using DebugTimer dt = new DebugTimer("exec()");
            TreeWrapper tw = new TreeWrapper();
            mFilter.setModelObject(tw);
            for (Tree t = at.next(); t != null; t = at.next())
            {
                if (!mFilter.isEmpty())
                { // skip fields
                    tw.setTree(t);
                    if (mFilter.execute() == 0.0)
                    {
                        continue;
                    }
                }
                this.add(currentYear());
                this.add(t.ru().index());
                this.add(t.ru().id());
                this.add(t.species().id());
                this.add(t.id());
                this.add(t.position().X);
                this.add(t.position().Y);
                this.add(t.dbh());
                this.add(t.height());
                this.add(t.basalArea());
                this.add(t.volume());
                this.add(t.leafArea());
                this.add(t.mFoliageMass);
                this.add(t.mWoodyMass);
                this.add(t.mFineRootMass);
                this.add(t.mCoarseRootMass);
                this.add(t.lightResourceIndex());
                this.add(t.mLightResponse);
                this.add(t.mStressIndex);
                this.add(t.mNPPReserve);
                writeRow();
            }
        }
    }
}
