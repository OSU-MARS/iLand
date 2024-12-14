using iLand.Tool;
using iLand.Tree;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Drawing;
using System.Text;
using Model = iLand.Simulation.Model;

namespace iLand.World
{
    // TODO: rewrite
    public class SvdStates
    {
        private static readonly ReadOnlyCollection<Point> mid_points = new List<Point>() {
            new(-1, -3), new(0, -3), new(1, -3),
            new(-2, -2), new(-1, -2), new(0, -2), new(1, -2), new(2, -2),
            new(-3, -1), new(-2, -1), new(-1, -1), new(0, -1), new(1, -1), new(2, -1), new(3, -1),
            new(-3, 0), new(-2, 0), new(-1, 0), new(1, 0), new(2, 0), new(3, 0),
            new(-3, 1), new(-2, 1), new(-1, 1), new(0, 1), new(1, 1), new(2, 1), new(3, 1),
            new(-2, 2), new(-1, 2), new(0, 2), new(1, 2), new(2, 2),
            new(-1, 3), new(0, 3), new(1, 3) }.AsReadOnly();
        private static readonly ReadOnlyCollection<Point> close_points = new List<Point>() { 
            new(-1, -1),  new(0, -1),  new(1, -1),
            new(-1, 0),  new(1, 0),
            new(-1, 1),  new(0, 1),  new(1, 1) }.AsReadOnly();

        private readonly StructureGranularity mStructureClassification;
        private readonly LaiClasses mFunctioningClassification;

        private readonly List<SvdState> states;
        private readonly List<string> compositionString;
        private readonly Dictionary<SvdState, int> stateLookup;

        public SvdStates(Model model)
        {
            this.states = [];
            this.compositionString = [];
            this.stateLookup = [];

            // add an empty state ("unforested") to the machine
            SvdState s = new();
            this.states.Add(s);
            this.compositionString.Add("unforested");
            this.stateLookup[states[0]] = 0;
            // s.svd = this;

            string svdStructure = model.Project.Model.Settings.SvdStructure;
            if (svdStructure == "4m")
            {
                this.mStructureClassification = StructureGranularity.FourM;
            }
            else if (svdStructure == "2m")
            {
                this.mStructureClassification = StructureGranularity.TwoM;
            }
            else
            {
                throw new ArgumentOutOfRangeException(nameof(model), "Setup of SVD States: invalid value for 'structure': '" + svdStructure + "', allowed values are '2m', '4m'.");
            }

            int svdFunction = model.Project.Model.Settings.SvdFunction;
            if (svdFunction == 3)
            {
                mFunctioningClassification = LaiClasses.Three;
            }
            else if (svdFunction == 5)
            {
                mFunctioningClassification = LaiClasses.Five;
            }
            else
            {
                throw new ArgumentOutOfRangeException(nameof(model), "Setup of SVD States: invalid value for 'functioning': " + svdFunction + "', allowed values are '3', '5'.");
            }

            // qDebug() << "setup of SvdStates completed.";
        }

        /// get a string with the main species on the resource unit
        /// dominant species is uppercase, all other lowercase
        public string CompositionString(int id) // C++: SVDState::compositionString()
        {
            return this.compositionString[id];
        }

        /// calculate and returns the Id ofthe state that
        /// the resource unit is currently in
        public int EvaluateState(Model model, ResourceUnit ru) // C++: SVDState::evaluateState()
        {
            SvdState s = new();
            float h = ru.GetCanopyHeight90(model, out bool rIrregular);

            switch (this.mStructureClassification)
            {
                case StructureGranularity.FourM:
                    {
                        // normal height classes: 4m classes
                        int hcls = Maths.Limit((int)(h / 4.0F), 0, 20); // 21 classes (0..20)
                        if (h == 4.0F)
                        {
                            hcls = 0; // <4m: heightgrid height==4m
                        }

                        if (rIrregular)
                        {
                            // irregular height classes: 12m steps:
                            // 21: <12m, 22: 12-24m, 23: 24-36m, 24: 38-48m, 25: 48-60m, 26: >60m
                            // irregular: >50% of the area < 50% of top height (which is 90th percentile of 10m pixels)
                            hcls = 21 + Maths.Limit((int)(h / 12), 0, 5);
                        }
                        s.Structure = hcls;
                    }
                    break;
                case StructureGranularity.TwoM:
                    {
                        // normal height classes: 2m classes
                        int hcls = Maths.Limit((int)(h / 2), 0, 30); // 31 classes (0..30)
                        if (h == 4.0F)
                        {
                            // 0-4m: differentiate: if saplings >2m exist, then class 1, otherwise class 0.
                            if (h > 2.0F)
                            {
                                hcls = 1;
                            }
                            else
                            {
                                hcls = 0;
                            }
                        }
                        if (rIrregular)
                        {
                            // irregular classes: 8m steps, max 56m: 31: <8m, 32: 8-16m, ... 37: 48-56m, 38: >=56m
                            hcls = 31 + Maths.Limit((int)(h / 8), 0, 7);
                        }
                        s.Structure = hcls;
                    }
                    break;
                default:
                    throw new NotSupportedException("Unhandled structure classification " + this.mStructureClassification + ".");
            }

            double lai = ru.Trees.LiveTreeAndSaplingStatisticsForAllSpecies.LeafAreaIndex;
            lai += ru.Trees.LiveTreeAndSaplingStatisticsForAllSpecies.SaplingLeafAreaIndex;

            switch (this.mFunctioningClassification)
            {
                case LaiClasses.Three:
                    s.Function = 0;
                    if (lai > 2.0F) 
                    {
                        s.Function = 1;
                    }
                    if (lai > 4.0F)
                    {
                        s.Function = 2;
                    }
                    break;
                case LaiClasses.Five:
                    s.Function = Maths.Limit((int)lai, 0, 4);
                    break;
                default:
                    throw new NotSupportedException("Unhandled functional classification " + this.mFunctioningClassification + ".");
            }

            // species
            int other_i = 0;
            double total_ba = ru.Trees.LiveTreeAndSaplingStatisticsForAllSpecies.BasalAreaInM2PerHa + ru.Trees.LiveTreeAndSaplingStatisticsForAllSpecies.SaplingBasalArea;
            if (total_ba > 0.0F)
            {
                for (int it = 0; it < ru.Trees.SpeciesAvailableOnResourceUnit.Count; ++it)
                {
                    ResourceUnitTreeSpecies species = ru.Trees.SpeciesAvailableOnResourceUnit[it];
                    double rel_ba = (species.StatisticsLive.BasalAreaInM2PerHa + species.SaplingStats.BasalArea) / total_ba;
                    if (rel_ba > 0.66)
                    {
                        s.DominantSpeciesIndex = species.Species.Index;
                    }
                    else if (rel_ba > 0.2)
                    {
                        s.AdmixedSpeciesIndices[other_i++] = species.Species.Index;
                    }
                }
                if (other_i >= s.AdmixedSpeciesIndices.Length)
                {
                    throw new InvalidOperationException("SvdStates: too many species!");
                }
                // generate +- unique number: this is mostly used for hashing, uniqueness is not strictly required
                s.Composition = s.DominantSpeciesIndex;
                for (int i = 0; i < s.AdmixedSpeciesIndices.Length; ++i)
                {
                    if (s.AdmixedSpeciesIndices[i] > -1)
                    {
                        s.Composition = (s.Composition << 6) + s.AdmixedSpeciesIndices[i];
                    }
                }
            }

            // lookup state in the hash table and return
            if (this.stateLookup.ContainsKey(s) == false)
            {
                s.UniqueID = this.states.Count;
                this.states.Add(s);
                this.compositionString.Add(GetCompositionString(ru, s));
                this.stateLookup[s] = s.UniqueID; // add to hash
            }

            return stateLookup[s]; // which is a unique index
        }

        /// access the state with the id 'index'
        public SvdState GetState(int index) // C++: SVDState::state()
        { 
            return this.states[index]; 
        }

        /// return true if 'state' is a valid state Id
        public bool IsValidStateIndex(int stateIndex) // C++: SVDState::isStateValid()
        { 
            return (stateIndex >= 0) && (stateIndex < this.states.Count);
        }

        /// return the number of states
        public int StateCount() // C++: SVDState::count()
        { 
            return this.states.Count; 
        }

        /// evaluate the species composition in the neighborhood of the cell
        /// this is executed in parallel.
        public void EvaluateNeighborhood(Model model, ResourceUnit ru) // C++: SVDState::evaluateNeighborhood()
        {
            List<float> local = ru.SvdState.LocalComposition;
            List<float> midrange = ru.SvdState.MidDistanceComposition;

            // clear the vectors
            local.Clear();
            midrange.Clear();

            // get the center point
            Grid<ResourceUnit?> grid = model.Landscape.ResourceUnitGrid;
            Point cp = grid.GetCellXYIndex(ru.ResourceUnitGridIndex);

            // do the work:
            this.EvaluateNeighborhood(local, cp, close_points, grid);
            this.EvaluateNeighborhood(midrange, cp, mid_points, grid);
        }

        /// create a human readable string representation of the string
        public string GetStateLabel(ResourceUnit ru, int index) // C++: SVDState::stateLabel()
        {
            if (index < 0 || index >= this.states.Count)
            {
                return "invalid";
            }

            SvdState s = GetState(index);
            StringBuilder label = new();
            if (s.DominantSpeciesIndex >= 0)
            {
                label.Append(ru.Trees.TreeSpeciesSet[s.DominantSpeciesIndex].Name + " ");
            }
            for (int i = 0; i < s.AdmixedSpeciesIndices.Length; ++i)
            {
                if (s.AdmixedSpeciesIndices[i] >= 0)
                {
                    label.Append(ru.Trees.TreeSpeciesSet[s.AdmixedSpeciesIndices[i]].Name.ToLowerInvariant() + " ");
                }
            }

            string hlabel;
            switch (this.mStructureClassification)
            {
                case StructureGranularity.FourM:
                    if (s.Structure < 21)
                    {
                        hlabel = (s.Structure * 4) + "-" + (s.Structure * 4 + 4);
                    }
                    else
                    {
                        hlabel = "Irr: " + ((s.Structure - 21) * 12) + "-" + ((s.Structure - 21 + 1) * 12);
                    }
                    break;
                case StructureGranularity.TwoM:
                    if (s.Structure < 31)
                    {
                        hlabel = (s.Structure * 2) + "-" + (s.Structure * 2 + 2);
                    }
                    else
                    {
                        hlabel = "Irr: " + ((s.Structure - 31) * 8) + "-" + ((s.Structure - 31 + 1) * 8);
                    }
                    break;
                default:
                    throw new NotSupportedException("Unhandled structure classification " + this.mStructureClassification + ".");
            }
            string flabel;
            switch (this.mFunctioningClassification)
            {
                case LaiClasses.Three:
                    flabel = s.Function == 0 ? "<2" : (s.Function == 1 ? "2-4" : ">4");
                    break;
                case LaiClasses.Five:
                    if (s.Function == 0)
                    {
                        flabel = "<1";
                    }
                    else if (s.Function == 4)
                    {
                        flabel = ">=4";
                    }
                    else
                    {
                        flabel = s.Function + "-" + (s.Function + 1);
                    }
                    break;
                default:
                    throw new NotSupportedException("Unhandled functional classification " + this.mFunctioningClassification + ".");
            }

            label.Append(hlabel + " (LAI " + flabel + ")");
            return label.ToString();
        }

        /// run the neighborhood evaluation; list: points to check, vec: a vector with a slot for each species, grid: the resource unit grid,
        /// center_point: the coordinates of the focal resource unit
        private void EvaluateNeighborhood(List<float> vec, Point center_point, ReadOnlyCollection<Point> list, Grid<ResourceUnit?> grid) // C++: SVDState::executeNeighborhood()
        {
            // evaluate the mid range neighborhood
            float n = 0.0F;
            for (int i = 0; i < list.Count; ++i)
            {
                if (grid.IsIndexValid(center_point.X + i, center_point.Y + i))
                {
                    ResourceUnit? nb = grid[center_point.X + i, center_point.Y + i];
                    if (nb != null)
                    {
                        int s = nb.SvdState.StateID;
                        if (this.IsValidStateIndex(s))
                        {
                            this.states[s].EvaluateNeighborhood(vec);
                            ++n;
                        }
                    }
                }
            }
            if (n > 0)
            {
                for (int i = 0; i < vec.Count; ++i)
                {
                    vec[i] /= n;
                }
            }
        }

        private static string GetCompositionString(ResourceUnit ru, SvdState s) // C++: SVDStates::createCompositionString()
        {
            StringBuilder label = new();
            if (s.DominantSpeciesIndex >= 0)
            {
                label.Append(ru.Trees.TreeSpeciesSet[s.DominantSpeciesIndex].Name.ToUpperInvariant() + " ");
            }
            for (int i = 0; i < 5; ++i)
            {
                if (s.AdmixedSpeciesIndices[i] >= 0)
                {
                    label.Append(ru.Trees.TreeSpeciesSet[s.AdmixedSpeciesIndices[i]].Name.ToLowerInvariant() + " ");
                }
            }
            if (label.Length < 1)
            {
                if (s.Structure > 0)
                {
                    return "mix";
                }
                else
                {
                    return "unforested";
                }
            }

            label.Remove(label.Length - 1, 1); // remove the trailing space
            return label.ToString();
        }

        private enum StructureGranularity
        {
            FourM, // 0-4, 4-8, 8-12, ... + irregular
            TwoM  // 0-2, 2-4, 4-6, .... + irregular
        }

        private enum LaiClasses
        {
            Three, // LAI 0-2, 2-4, >4
            Five   // LAI 0-1, 1-2, 2-3, 3-4, >4
        }
    }
}