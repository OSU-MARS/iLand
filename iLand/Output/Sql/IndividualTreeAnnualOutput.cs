﻿using iLand.Input.ProjectFile;
using iLand.Simulation;
using iLand.Tool;
using iLand.Tree;
using iLand.World;
using Microsoft.Data.Sqlite;
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
                               "Initial values (without any growth) are output as 'startyear-1'.";
            this.Columns.Add(SqlColumn.CreateYear());
            this.Columns.Add(SqlColumn.CreateResourceUnitID());
            this.Columns.Add(SqlColumn.CreateTreeSpeciesID());
            this.Columns.Add(new("id", "ID of the tree", SqliteType.Integer));
            this.Columns.Add(new("xLight", "Tree's approximate GIS coordinate in the x direction (quantized to the light grid), m", SqliteType.Real));
            this.Columns.Add(new("yLight", "Tree's approximate GIS coordinate in the y direction (quantized to the light grid), m", SqliteType.Real));
            this.Columns.Add(new("dbh", "Tree's DBH (cm) of the tree", SqliteType.Real));
            this.Columns.Add(new("height", "Tree's height (m) of the tree", SqliteType.Real));
            this.Columns.Add(new("basalArea", "Tree's basal area, m².", SqliteType.Real));
            this.Columns.Add(new("volumeM3", "Tree's stem volume, m³.", SqliteType.Real));
            this.Columns.Add(new("leafAreaM2", "Tree's leaf area of the tree, m².", SqliteType.Real));
            this.Columns.Add(new("foliageMass", "Foliage biomass, kg", SqliteType.Real));
            this.Columns.Add(new("woodyMass", "Woody biomass, kg.", SqliteType.Real));
            this.Columns.Add(new("fineRootMass", "Fine root biomass, kg", SqliteType.Real));
            this.Columns.Add(new("coarseRootMass", "Coarse root biomass, kg.", SqliteType.Real));
            this.Columns.Add(new("lri", "Light resource index of the tree (raw light index from iLand, without applying resource-unit modifications).", SqliteType.Real));
            this.Columns.Add(new("lightResponse", "Light response value (including species specific response to the light level)", SqliteType.Real));
            this.Columns.Add(new("stressIndex", "Tree's stress level, 0..1 (see [Mortality]).", SqliteType.Real));
            this.Columns.Add(new("reserve_kg", "NPP currently available in the tree's reserve pool, kg biomass.", SqliteType.Real));
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

                insertRow.Parameters[0].Value = model.SimulationState.CurrentCalendarYear;
                insertRow.Parameters[1].Value = treesOfSpecies.ResourceUnit.ID;
                insertRow.Parameters[2].Value = treesOfSpecies.Species.WorldFloraID;
                insertRow.Parameters[3].Value = treesOfSpecies.TreeID[treeIndex];
                insertRow.Parameters[4].Value = approximateTreeGisCoordinate.X;
                insertRow.Parameters[5].Value = approximateTreeGisCoordinate.Y;
                insertRow.Parameters[6].Value = treesOfSpecies.DbhInCm[treeIndex];
                insertRow.Parameters[7].Value = treesOfSpecies.HeightInM[treeIndex];
                insertRow.Parameters[8].Value = treesOfSpecies.GetBasalArea(treeIndex);
                insertRow.Parameters[9].Value = treesOfSpecies.GetStemVolume(treeIndex);
                insertRow.Parameters[10].Value = treesOfSpecies.LeafAreaInM2[treeIndex];
                insertRow.Parameters[11].Value = treesOfSpecies.FoliageMassInKg[treeIndex];
                insertRow.Parameters[12].Value = treesOfSpecies.StemMassInKg[treeIndex];
                insertRow.Parameters[13].Value = treesOfSpecies.FineRootMassInKg[treeIndex];
                insertRow.Parameters[14].Value = treesOfSpecies.CoarseRootMassInKg[treeIndex];
                insertRow.Parameters[15].Value = treesOfSpecies.LightResourceIndex[treeIndex];
                insertRow.Parameters[16].Value = treesOfSpecies.LightResponse[treeIndex];
                insertRow.Parameters[17].Value = treesOfSpecies.StressIndex[treeIndex];
                insertRow.Parameters[18].Value = treesOfSpecies.NppReserveInKg[treeIndex];
                insertRow.ExecuteNonQuery();
            }
        }
    }
}
