// C++/output/{ treeout.h, treeout.cpp }
using iLand.Input.ProjectFile;
using iLand.Simulation;
using iLand.Tool;
using iLand.Tree;
using iLand.World;
using Microsoft.Data.Sqlite;
using System;
using System.Drawing;
using Model = iLand.Simulation.Model;

namespace iLand.Output.Sql
{
    public class IndividualTreeAnnualOutput : AnnualOutput
    {
        private readonly Expression treeFilter;

        public IndividualTreeAnnualOutput()
        {
            this.treeFilter = new();

            this.Name = "Tree Output";
            this.TableName = "tree";
            this.Description = "Output of indivdual trees. Use the 'filter' property to reduce amount of data (filter by resource-unit, year, species, ...)." + System.Environment.NewLine +
                               "The output is triggered after the growth of the current season. " +
                               "Initial values (without any growth) are output as 'startyear-1'." + Environment.NewLine +
                               "The 'treeFlags' is a binary combination of individual flags; see iLand.Tree.TreeFlags.";
            this.Columns.Add(SqlColumn.CreateYear());
            this.Columns.Add(SqlColumn.CreateResourceUnitID());
            this.Columns.Add(SqlColumn.CreateTreeSpeciesID());
            this.Columns.Add(new("id", "ID of the tree", SqliteType.Integer));
            this.Columns.Add(new("xLight", "Tree's approximate GIS coordinate in the x direction (quantized to the light grid), m", SqliteType.Real));
            this.Columns.Add(new("yLight", "Tree's approximate GIS coordinate in the y direction (quantized to the light grid), m", SqliteType.Real));
            this.Columns.Add(new("dbh", "Tree's DBH (cm) of the tree", SqliteType.Real));
            this.Columns.Add(new("height", "Tree's height (m) of the tree", SqliteType.Real));
            this.Columns.Add(new("basalArea", "Tree's basal area, m².", SqliteType.Real));
            this.Columns.Add(new("volume_m3", "Tree's stem volume, m³.", SqliteType.Real));
            this.Columns.Add(new("age", "tree age (years)", SqliteType.Integer));
            this.Columns.Add(new("leafArea_m2", "Tree's leaf area of the tree, m².", SqliteType.Real));
            this.Columns.Add(new("foliageMass", "Foliage biomass, kg", SqliteType.Real));
            this.Columns.Add(new("stemMass", "Biomass in woody department (tree stem, without reserve pool), kg.", SqliteType.Real));
            this.Columns.Add(new("branchMass", "Biomass in branches, kg.", SqliteType.Real));
            this.Columns.Add(new("fineRootMass", "Fine root biomass, kg", SqliteType.Real));
            this.Columns.Add(new("coarseRootMass", "Coarse root biomass, kg.", SqliteType.Real));
            this.Columns.Add(new("lri", "Light resource index of the tree (raw light index from iLand, without applying resource-unit modifications).", SqliteType.Real));
            this.Columns.Add(new("lightResponse", "Light response value (including species specific response to the light level)", SqliteType.Real));
            this.Columns.Add(new("stressIndex", "Tree's stress level, 0..1 (see [Mortality]).", SqliteType.Real));
            this.Columns.Add(new("reserve_kg", "NPP currently available in the tree's reserve pool, kg biomass.", SqliteType.Real));
            this.Columns.Add(new("treeFlags", "Tree's individual bit flags (see iLand.Tree.TreeFlags).", SqliteType.Integer));
        }

        public override void Setup(Project projectFile, SimulationState simulationState)
        {
            this.treeFilter.SetExpression(projectFile.Output.Sql.IndividualTree.Filter);
        }

        protected override void LogYear(Model model, SqliteCommand insertRow)
        {
            Landscape landscape = model.Landscape;
            AllTreesEnumerator allTreeEnumerator = new(landscape);
            TreeVariableAccessor treeWrapper = new(model.SimulationState);
            this.treeFilter.Wrapper = treeWrapper;
            while (allTreeEnumerator.MoveNext())
            {
                TreeListSpatial treesOfSpecies = allTreeEnumerator.CurrentTrees;
                int treeIndex = allTreeEnumerator.CurrentTreeIndex;
                if (this.treeFilter.IsEmpty == false)
                { 
                    // nothing to log if tree is excluded by filter
                    treeWrapper.Trees = treesOfSpecies;
                    treeWrapper.TreeIndex = treeIndex;
                    if (this.treeFilter.Execute() == 0.0F)
                    {
                        continue;
                    }
                }

                PointF approximateTreeProjectCoordinate = landscape.LightGrid.GetCellProjectCentroid(treesOfSpecies.LightCellIndexXY[treeIndex]);
                PointF approximateTreeGisCoordinate = landscape.ToGisCoordinate(approximateTreeProjectCoordinate);

                insertRow.Parameters[0].Value = model.SimulationState.CurrentCalendarYear; // year
                insertRow.Parameters[1].Value = treesOfSpecies.ResourceUnit.ID; // resourceUnit
                insertRow.Parameters[2].Value = treesOfSpecies.Species.WorldFloraID; // species
                insertRow.Parameters[3].Value = treesOfSpecies.TreeID[treeIndex]; // id
                insertRow.Parameters[4].Value = approximateTreeGisCoordinate.X; // xLite
                insertRow.Parameters[5].Value = approximateTreeGisCoordinate.Y; // yLight
                insertRow.Parameters[6].Value = treesOfSpecies.DbhInCm[treeIndex]; // dbh
                insertRow.Parameters[7].Value = treesOfSpecies.HeightInM[treeIndex]; // height
                insertRow.Parameters[8].Value = treesOfSpecies.GetBasalArea(treeIndex); // basalArea
                insertRow.Parameters[9].Value = treesOfSpecies.GetStemVolume(treeIndex); // volume_m3
                insertRow.Parameters[10].Value = treesOfSpecies.AgeInYears[treeIndex]; // age
                insertRow.Parameters[11].Value = treesOfSpecies.LeafAreaInM2[treeIndex]; // leafArea_m2
                insertRow.Parameters[12].Value = treesOfSpecies.FoliageMassInKg[treeIndex]; // foliageMass
                insertRow.Parameters[13].Value = treesOfSpecies.StemMassInKg[treeIndex]; // stemMass
                insertRow.Parameters[14].Value = treesOfSpecies.GetBranchBiomass(treeIndex); // branchMass
                insertRow.Parameters[15].Value = treesOfSpecies.FineRootMassInKg[treeIndex]; // fineRootMass
                insertRow.Parameters[16].Value = treesOfSpecies.CoarseRootMassInKg[treeIndex]; // coarseRootMass
                insertRow.Parameters[17].Value = treesOfSpecies.LightResourceIndex[treeIndex]; // lri
                insertRow.Parameters[18].Value = treesOfSpecies.LightResponse[treeIndex]; // lightResponse
                insertRow.Parameters[19].Value = treesOfSpecies.StressIndex[treeIndex]; // stressIndex
                insertRow.Parameters[20].Value = treesOfSpecies.NppReserveInKg[treeIndex]; // reserve_kg
                insertRow.Parameters[21].Value = treesOfSpecies.Flags[treeIndex]; // treeFlags
                insertRow.ExecuteNonQuery();
            }
        }
    }
}
