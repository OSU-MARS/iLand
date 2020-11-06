﻿using System.Xml;

namespace iLand.Input.ProjectFile
{
    public class Soil : XmlSerializable
    {
        public float El { get; private set; }
        public float Er { get; private set; }
        public float Qb { get; private set; }
        public float Qh { get; private set; }
        public float SwdDbhClass12 { get; private set; }
        public float SwdDdhClass23 { get; private set; }
        public bool UseDynamicAvailableNitrogen { get; private set; }
        public float Leaching { get; private set; }
        public float NitrogenDeposition { get; private set; }

        public Soil()
        {
            this.Leaching = 0.15F;
            this.NitrogenDeposition = 0.0F;
            this.Qb = 5.0F;
            this.UseDynamicAvailableNitrogen = false;
        }

        protected override void ReadStartElement(XmlReader reader)
        {
            if (reader.AttributeCount != 0)
            {
                throw new XmlException("Encountered unexpected attributes.");
            }

            if (reader.IsStartElement("soil"))
            {
                reader.Read();
            }
            else if (reader.IsStartElement("el"))
            {
                this.El = reader.ReadElementContentAsFloat();
            }
            else if (reader.IsStartElement("er"))
            {
                this.Er = reader.ReadElementContentAsFloat();
            }
            else if (reader.IsStartElement("qb"))
            {
                this.Qb = reader.ReadElementContentAsFloat();
            }
            else if (reader.IsStartElement("qh"))
            {
                this.Qh = reader.ReadElementContentAsFloat();
            }
            else if (reader.IsStartElement("swdDBHClass12"))
            {
                this.SwdDbhClass12 = reader.ReadElementContentAsFloat();
            }
            else if (reader.IsStartElement("swdDBHClass23"))
            {
                this.SwdDdhClass23 = reader.ReadElementContentAsFloat();
            }
            else if (reader.IsStartElement("useDynamicAvailableNitrogen"))
            {
                this.UseDynamicAvailableNitrogen = reader.ReadElementContentAsBoolean();
            }
            else if (reader.IsStartElement("leaching"))
            {
                this.Leaching = reader.ReadElementContentAsFloat();
            }
            else if (reader.IsStartElement("nitrogenDeposition"))
            {
                this.NitrogenDeposition = reader.ReadElementContentAsFloat();
            }
            else
            {
                throw new XmlException("Encountered unknown element '" + reader.Name + "'.");
            }
        }
    }
}
