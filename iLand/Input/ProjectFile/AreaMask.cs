﻿using System;
using System.Xml;

namespace iLand.Input.ProjectFile
{
    public class AreaMask : Enablable
    {
        public string? ImageFile { get; private set; }

        public AreaMask()
            : base("areaMask")
        {
            this.ImageFile = null;
        }

        protected override void ReadStartElement(XmlReader reader)
        {
            if (reader.AttributeCount != 0)
            {
                this.ReadEnabled(reader);
            }
            else if (String.Equals(reader.Name, "imageFile", StringComparison.Ordinal))
            {
                this.ImageFile = reader.ReadElementContentAsString().Trim();
            }
            else
            {
                throw new XmlException("Element '" + reader.Name + "' is unknown, has unexpected attributes, or is missing expected attributes.");
            }
        }
    }
}
