﻿using System;
using System.Xml;

namespace iLand.Input.ProjectFile
{
    public class DynamicStandOutput : ConditionOutput
    {
        public bool BySpecies { get; private set; }
        public bool ByResourceUnit { get; private set; }

        public string? Columns { get; protected set; }
        public string? ResourceUnitFilter { get; protected set; }
        public string? TreeFilter { get; protected set; }

        public DynamicStandOutput()
        {
            this.ByResourceUnit = true;
            this.BySpecies = true;
            this.Columns = null;
            this.ResourceUnitFilter = null;
            this.TreeFilter = null;
        }

        protected override void ReadStartElement(XmlReader reader)
        {
            if (reader.AttributeCount != 0)
            {
                throw new XmlException("Encountered unexpected attributes.");
            }

            if (String.Equals(reader.Name, "dynamicStand", StringComparison.Ordinal))
            {
                reader.Read();
            }
            else if (String.Equals(reader.Name, "enabled", StringComparison.Ordinal))
            {
                this.Enabled = reader.ReadElementContentAsBoolean();
            }
            else if (String.Equals(reader.Name, "condition", StringComparison.Ordinal))
            {
                this.Condition = reader.ReadElementContentAsString().Trim();
            }
            else if (String.Equals(reader.Name, "rufilter", StringComparison.Ordinal))
            {
                this.ResourceUnitFilter = reader.ReadElementContentAsString().Trim();
            }
            else if (String.Equals(reader.Name, "columns", StringComparison.Ordinal))
            {
                this.Columns = reader.ReadElementContentAsString().Trim();
            }
            else if (String.Equals(reader.Name, "treefilter", StringComparison.Ordinal))
            {
                this.TreeFilter = reader.ReadElementContentAsString().Trim();
            }
            else if (String.Equals(reader.Name, "bySpecies", StringComparison.Ordinal))
            {
                this.BySpecies = reader.ReadElementContentAsBoolean();
            }
            else if (String.Equals(reader.Name, "byResourceUnit", StringComparison.Ordinal))
            {
                this.ByResourceUnit = reader.ReadElementContentAsBoolean();
            }
            else
            {
                throw new XmlException("Encountered unknown element '" + reader.Name + "'.");
            }
        }
    }
}
