using System.Xml;

namespace iLand.Input.ProjectFile
{
    public class DefaultSoil : XmlSerializable
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

        public DefaultSoil()
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

            if (reader.IsStartElement("defaultSoil"))
            {
                reader.Read();
            }
            else if (reader.IsStartElement("el"))
            {
                this.El = reader.ReadElementContentAsFloat();
                if (this.El < 0.0F)
                {
                    throw new XmlException("El is negative.");
                }
            }
            else if (reader.IsStartElement("er"))
            {
                this.Er = reader.ReadElementContentAsFloat();
                if (this.Er < 0.0F)
                {
                    throw new XmlException("Er is negative.");
                }
            }
            else if (reader.IsStartElement("qb"))
            {
                this.Qb = reader.ReadElementContentAsFloat();
                if (this.Qb < 0.0F)
                {
                    throw new XmlException("Qb is negative.");
                }
            }
            else if (reader.IsStartElement("qh"))
            {
                this.Qh = reader.ReadElementContentAsFloat();
                if (this.Qh < 0.0F)
                {
                    throw new XmlException("Qh is negative.");
                }
            }
            else if (reader.IsStartElement("swdDBHClass12"))
            {
                this.SwdDbhClass12 = reader.ReadElementContentAsFloat();
                if (this.SwdDbhClass12 < 0.0F)
                {
                    throw new XmlException("Breakpoint between DBH classes 1 and 2 is negative.");
                }
            }
            else if (reader.IsStartElement("swdDBHClass23"))
            {
                this.SwdDdhClass23 = reader.ReadElementContentAsFloat();
                if (this.SwdDdhClass23 < 0.0F)
                {
                    throw new XmlException("Breakpoint between DBH classes 2 and 3 is negative.");
                }
            }
            else if (reader.IsStartElement("useDynamicAvailableNitrogen"))
            {
                this.UseDynamicAvailableNitrogen = reader.ReadElementContentAsBoolean();
            }
            else if (reader.IsStartElement("leaching"))
            {
                this.Leaching = reader.ReadElementContentAsFloat();
                if (this.Leaching < 0.0F)
                {
                    throw new XmlException("Leaching rate is negative.");
                }
            }
            else if (reader.IsStartElement("nitrogenDeposition"))
            {
                this.NitrogenDeposition = reader.ReadElementContentAsFloat();
                if (this.NitrogenDeposition < 0.0F)
                {
                    throw new XmlException("Nitrogen deposition rate is negative.");
                }
            }
            else
            {
                throw new XmlException("Encountered unknown element '" + reader.Name + "'.");
            }
        }
    }
}
