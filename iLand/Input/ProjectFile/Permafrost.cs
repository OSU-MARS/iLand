using System.Xml;

namespace iLand.Input.ProjectFile
{
    public class Permafrost : Enablable
    {
        // physical constants
        public float LatentHeatOfFusion { get; private set; } ///< latent heat of fusion, i.e. energy requied to thaw/freeze 1l of water
        public float MaxPermafrostDepth { get; private set; } ///< maximum depth (m) of active layer

        public float LambdaSnow { get; private set; } ///< thermal conductivity [W/m*K] of snow
        public float LambdaOrganicLayer { get; private set; } ///< thermal conductivity [W/m*K] of the organic layer

        public float SoilOrganicLayerDensity { get; private set; } ///< density (kg/m3) of the organic layer
        public float SoilOrganicLayerDefaultDepth { get; private set; } ///< 0.1m default depth of soil organic litter

        // parameters
        public float GroundBaseDepth { get; private set; } ///< depth (m) of the zone below the active layer from where the secondary heat flux originates
        public float MaxFreezeThawPerDayInMMH2O { get; private set; } ///< maximum amount of freeze/thaw (mm watercolumn) per day
        public float InitialGroundTemperature { get; private set; }
        public float InitialDepthFrozenInM { get; private set; } // m
        // TODO: remove unused
        // public float OrganicLayerDefaultDepthInM { get; private set; }

        public bool SimulateOnly { get; private set; } ///< if true, permafrost has no effect on the available water (and soil depth)

        public Moss Moss { get; private init; }

        public Permafrost()
            : base("permafrost")
        {
            this.LatentHeatOfFusion = 0.333F; // MJ/litre H₂O
            this.GroundBaseDepth = 5.0F; // m
            this.InitialDepthFrozenInM = 1.0F; // m
            this.InitialGroundTemperature = 0.0F; // °C?
            this.LambdaOrganicLayer = 0.0F; // units?
            this.LambdaSnow = 0.3F; // units?
            this.MaxFreezeThawPerDayInMMH2O = 10.0F; // units?
            this.MaxPermafrostDepth = 2.0F; // m
            this.Moss = new();
            this.SimulateOnly = false;
            // this.OrganicLayerDefaultDepthInM = 0.1F; // m?
            this.SoilOrganicLayerDensity = 50.0F; // units?
            this.SoilOrganicLayerDefaultDepth = 0.1F; // m?
        }

        protected override void ReadStartElement(XmlReader reader)
        {
            if (reader.AttributeCount != 0)
            {
                this.ReadEnabled(reader);
            }
            else
            {
                switch (reader.Name)
                {
                    case "permafrost":
                        reader.Read();
                        break;
                    case "groundBaseDepth":
                        this.GroundBaseDepth = reader.ReadElementContentAsFloat();
                        break;
                    case "initialDepthFrozen":
                        this.InitialDepthFrozenInM = reader.ReadElementContentAsFloat();
                        break;
                    case "initialGroundTemperature":
                        this.InitialGroundTemperature = reader.ReadElementContentAsFloat();
                        break;
                    case "lambdaOrganicLayer":
                        this.LambdaOrganicLayer = reader.ReadElementContentAsFloat();
                        break;
                    case "lambdaSnow":
                        this.LambdaSnow = reader.ReadElementContentAsFloat();
                        break;
                    case "maxFreezeThawPerDay":
                        this.MaxFreezeThawPerDayInMMH2O = reader.ReadElementContentAsFloat();
                        break;
                    case "moss":
                        this.Moss.ReadXml(reader);
                        break;
                    //case "organicLayerDefaultDepth":
                    //    this.OrganicLayerDefaultDepthInM = reader.ReadElementContentAsFloat();
                    //    break;
                    case "organicLayerDensity":
                        this.SoilOrganicLayerDensity = reader.ReadElementContentAsFloat();
                        break;
                    case "onlySimulate":
                        this.SimulateOnly = reader.ReadElementContentAsBoolean();
                        break;
                    default:
                        throw new XmlException("Element '" + reader.Name + "' is unknown, has unexpected attributes, or is missing expected attributes.");
                }
            }
        }

        public void Validate()
        {
            if (this.Enabled == false)
            {
                // do no checking unless enabled as default values are invalid
                return;
            }

            if (this.LambdaSnow * this.LambdaOrganicLayer == 0.0F)
            {
                throw new XmlException("lambdaSnow or lambdaOrganicLayer is invalid (0).");
            }
        }
    }
}
