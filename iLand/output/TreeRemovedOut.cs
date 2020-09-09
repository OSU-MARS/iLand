using iLand.core;
using iLand.tools;

namespace iLand.output
{
    internal class TreeRemovedOut : Output
    {
        private Expression mFilter;

        public TreeRemovedOut()
        {
            setName("Tree Removed Output", "treeremoved");
            setDescription("Output of removed indivdual trees. Use the ''filter'' property to reduce amount of data (filter by resource-unit, year, species, ...)." + System.Environment.NewLine +
                       "The output is triggered immediately when a tree is removed due to mortality or management. ");
            columns().Add(OutputColumn.year());
            columns().Add(OutputColumn.ru());
            columns().Add(OutputColumn.id());
            columns().Add(OutputColumn.species());
            columns().Add(new OutputColumn("id", "id of the tree", OutputDatatype.OutInteger));
            columns().Add(new OutputColumn("reason", "reason of removal: 0: mortality, 1: management, 2: disturbance ", OutputDatatype.OutInteger));
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

        public void execRemovedTree(Tree t, int reason)
        {
            if (!mFilter.isEmpty())
            { // skip trees if filter is present
                TreeWrapper tw = new TreeWrapper();
                mFilter.setModelObject(tw);
                tw.setTree(t);
                if (mFilter.execute() == 0.0)
                {
                    return;
                }
            }

            this.add(currentYear());
            this.add(t.ru().index());
            this.add(t.ru().id());
            this.add(t.species().id());
            this.add(t.id());
            this.add(reason);
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

        public override void exec()
        {
            // do nothing here
            return;
        }

        public void setup()
        {
            string filter = settings().value(".filter", "");
            mFilter.setExpression(filter);
            Tree.setTreeRemovalOutput(this);
        }
    }
}
