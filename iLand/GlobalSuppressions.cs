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
