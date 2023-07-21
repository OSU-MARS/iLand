// This file is used by Code Analysis to maintain SuppressMessage
// attributes that are applied to this project.
// Project-level suppressions either have no target or are given
// a specific target and scoped to a namespace, type, member, etc.

using System.Diagnostics.CodeAnalysis;

[assembly: SuppressMessage("Style", "IDE0042:Deconstruct variable declaration", Justification = "Roslyn bug VS 17.2.4", Scope = "namespaceanddescendants", Target = "~N:iLand")]
[assembly: SuppressMessage("Style", "IDE0066:Convert switch statement to expression", Justification = "readability", Scope = "member", Target = "~M:iLand.Input.ProjectFile.Browsing.ReadStartElement(System.Xml.XmlReader)")]
[assembly: SuppressMessage("Style", "IDE0066:Convert switch statement to expression", Justification = "readability", Scope = "member", Target = "~M:iLand.Input.ProjectFile.ConditionAnnualOutput.ReadStartElement(System.Xml.XmlReader)")]
[assembly: SuppressMessage("Style", "IDE0066:Convert switch statement to expression", Justification = "readability", Scope = "member", Target = "~M:iLand.Input.ProjectFile.FilterAnnualOutput.ReadStartElement(System.Xml.XmlReader)")]
[assembly: SuppressMessage("Style", "IDE0220:Add explicit cast", Justification = "https://github.com/dotnet/roslyn/issues/63470", Scope = "member", Target = "~M:iLand.Output.Sql.DynamicStandAnnualOutput.Setup(iLand.Input.ProjectFile.Project,iLand.Simulation.SimulationState)")]
[assembly: SuppressMessage("Style", "IDE0057:Use range operator", Justification = "readability", Scope = "member", Target = "~M:iLand.Extensions.ArrayExtensions.Slice``1(``0[],System.Int32)~System.Span{``0}")]
[assembly: SuppressMessage("Style", "IDE0031:Use null propagation", Justification = "clarity", Scope = "member", Target = "~M:iLand.Input.Tree.IndividualTreeReaderFeather.#ctor(System.String)")]
[assembly: SuppressMessage("Style", "IDE0031:Use null propagation", Justification = "clarity", Scope = "member", Target = "~M:iLand.Output.Outputs.Dispose(System.Boolean)")]
[assembly: SuppressMessage("Style", "IDE0031:Use null propagation", Justification = "clarity", Scope = "member", Target = "~M:iLand.Simulation.Model.RunYear")]
[assembly: SuppressMessage("Style", "IDE0031:Use null propagation", Justification = "clarity", Scope = "member", Target = "~M:iLand.Tree.SaplingStatistics.AfterSaplingGrowth(iLand.World.ResourceUnit,iLand.Tree.TreeSpecies)")]
[assembly: SuppressMessage("Style", "IDE0031:Use null propagation", Justification = "clarity", Scope = "member", Target = "~M:iLand.Tree.TreeListSpatial.OnTreeRemoved(iLand.Simulation.Model,System.Int32,iLand.Tree.MortalityCause)")]
[assembly: SuppressMessage("Style", "IDE0031:Use null propagation", Justification = "clarity", Scope = "member", Target = "~M:iLand.World.ResourceUnit.OnStartYear")]
[assembly: SuppressMessage("Style", "IDE0031:Use null propagation", Justification = "clarity", Scope = "member", Target = "~M:iLand.World.ResourceUnit.CalculateCarbonCycle")]
[assembly: SuppressMessage("Style", "IDE0031:Use null propagation", Justification = "clarity", Scope = "member", Target = "~M:iLand.World.Landscape.MarkSaplingCellsAndScaleSnags(System.Threading.Tasks.ParallelOptions)")]
[assembly: SuppressMessage("Style", "IDE0031:Use null propagation", Justification = "clarity", Scope = "member", Target = "~M:iLand.Tree.TreeListSpatial.Remove(iLand.Simulation.Model,System.Int32,System.Single,System.Single,System.Single)")]
[assembly: SuppressMessage("Style", "IDE0031:Use null propagation", Justification = "clarity", Scope = "member", Target = "~M:iLand.Tree.TreeListSpatial.MarkTreeAsDead(iLand.Simulation.Model,System.Int32)")]
[assembly: SuppressMessage("Style", "IDE0031:Use null propagation", Justification = "clarity", Scope = "member", Target = "~M:iLand.Tree.TreeListSpatial.PartitionBiomass(iLand.Tree.TreeGrowthData,iLand.Simulation.Model,System.Int32)")]
[assembly: SuppressMessage("Style", "IDE0270:Use coalesce expression", Justification = "readability", Scope = "type", Target = "~T:iLand.Tree.TreeSpeciesStamps")]
[assembly: SuppressMessage("Style", "IDE0270:Use coalesce expression", Justification = "readability", Scope = "type", Target = "~T:iLand.World.Landscape")]
[assembly: SuppressMessage("Style", "IDE0270:Use coalesce expression", Justification = "readability", Scope = "type", Target = "~T:iLand.World.TreePopulator")]