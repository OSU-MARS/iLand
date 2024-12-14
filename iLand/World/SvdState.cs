using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;

namespace iLand.World
{
    // TODO: rewrite
    public class SvdState
    {
        public int Composition { get; set; } // a kind of hash number combining all species (can be negative), C++ composition
        public int Structure { get; set; } // C++ structure
        public int Function { get; set; } // C++ function
        public int DominantSpeciesIndex { get; set; } // C++ dominant_species_index
        public int[] AdmixedSpeciesIndices { get; private init; } // C++ admixed_species_index
        /// the unique Id of the state within the current simulation.
        public int UniqueID { get; set; } // C++ Id

        public SvdState()
        {
            this.Composition = 0;
            this.Structure = 0;
            this.Function = 0;
            this.DominantSpeciesIndex = -1;
            this.UniqueID = 0;
            this.AdmixedSpeciesIndices = new int[5];
            for (int i = 0; i < this.AdmixedSpeciesIndices.Length; ++i)
            {
                this.AdmixedSpeciesIndices[i] = -1;
            }
        }

        // functions for the hashing of states
        public static bool operator ==(SvdState s1, SvdState s2)
        {
            // this does not include comparing the 'Id'!
            bool equal = s1.Composition == s2.Composition && s1.Structure == s2.Structure && s1.Function == s2.Function && s1.DominantSpeciesIndex == s2.DominantSpeciesIndex;
            if (!equal) 
            {
                return false;
            }
            for (int i = 0; i < s1.AdmixedSpeciesIndices.Length; ++i)
            {
                if (s1.AdmixedSpeciesIndices[i] != s2.AdmixedSpeciesIndices[i])
                {
                    return false;
                }
            }

            return true;
        }

        public static bool operator !=(SvdState s1, SvdState s2)
        { 
            return !(s1 == s2); 
        }

        public override bool Equals([NotNullWhen(true)] object? obj)
        {
            if (obj is SvdState other)
            {
                return this == other;
            }

            return false;
        }

        public override int GetHashCode()
        {
            return HashCode.Combine(this.Composition, this.Structure, this.Function);
        }

        /// calculate neighborhood population, return total weight added to the vector of species
        public float EvaluateNeighborhood(List<float> v) // C++: SVDState::neighborhoodAnalysis()
        {
            /* apply some rules:
            * (a) only 1 dominant species: 100%
            * (b) 1 dom. and 1 other: 67/33
            * (c) only 1 other: 50
            * (d) two other: 50/50
            * (e) three other: 33/33/33
            * (f) 4 other: 4x 25
            * none: 0
            * */
            float total_weight = 1.0F;
            if (this.DominantSpeciesIndex > -1)
            {
                if (this.AdmixedSpeciesIndices[0] == -1)
                {
                    v[DominantSpeciesIndex] += 1.0F; // (a)
                }
                else
                {
                    // max 1 other species: >66% + >20% . at least 86% . no other species possible
                    v[this.DominantSpeciesIndex] += 0.67f; // (b)
                    v[this.AdmixedSpeciesIndices[0]] += 0.33f;
                }
            }
            else
            {
                // no dominant species
                int n_s = 0;
                for (int i = 0; i < this.AdmixedSpeciesIndices.Length; ++i)
                {
                    if (this.AdmixedSpeciesIndices[i] > -1)
                    {
                        ++n_s;
                    }
                }

                float f;
                switch (n_s)
                {
                    case 0: 
                        return 0.0F; // (f)
                    case 1: 
                        f = 0.5f; 
                        break;
                    case 2: 
                        f = 0.5f; 
                        break;
                    case 3: 
                        f = 0.33f; 
                        total_weight = 0.99f; 
                        break;
                    case 4: 
                        f = 0.25f; 
                        break;
                    default:
                        throw new NotSupportedException("Unhandled species index " + n_s + ".");
                }

                // apply cases
                for (int i = 0; i < n_s; ++i)
                {
                    v[this.AdmixedSpeciesIndices[i]] += f;
                }
            }
            return total_weight;
        }
    }
}
