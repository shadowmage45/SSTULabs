using UnityEngine;

namespace KSPShaderTools
{

    public interface IRecolorable
    {
        string[] getSectionNames();
        RecoloringData[] getSectionColors(string name);
        TextureSet getSectionTexture(string name);
        void setSectionColors(string name, RecoloringData[] colors);
    }

    public interface IPartTextureUpdated
    {
        void textureUpdated(Part part);
    }

    public interface IPartGeometryUpdated
    {
        void geometryUpdated(Part part);
    }

    public struct RecoloringDataPreset
    {
        public string name;
        public string title;
        public Color color;
        public float specular;
        public float metallic;

        public RecoloringDataPreset(ConfigNode node)
        {
            name = node.GetStringValue("name");
            title = node.GetStringValue("title");
            color = Utils.parseColorFromBytes(node.GetStringValue("color"));
            specular = node.GetFloatValue("specular");
            metallic = node.GetFloatValue("metallic");
        }

        public RecoloringData getRecoloringData()
        {
            return new RecoloringData(color, specular, metallic);
        }
    }

    public struct RecoloringData
    {

        public Color color;
        public float specular;
        public float metallic;

        public RecoloringData(string data)
        {
            if (data.Contains(","))//CSV value, parse from floats
            {
                string[] values = data.Split(',');
                int len = values.Length;
                if (len < 3)
                {
                    MonoBehaviour.print("ERROR: Not enough data in: " + data + " to construct color values.");
                    values = new string[] { "255", "255", "255", "0", "0"};
                }
                string redString = values[0];
                string greenString = values[1];
                string blueString = values[2];
                string specString = len > 3 ? values[3] : "0";
                string metalString = len > 4 ? values[4] : "0";
                float r = Utils.safeParseFloat(redString);
                float g = Utils.safeParseFloat(greenString);
                float b = Utils.safeParseFloat(blueString);
                color = new Color(r, g, b);
                specular = Utils.safeParseFloat(specString);
                metallic = Utils.safeParseFloat(metalString);
            }
            else //preset color, load from string value
            {
                RecoloringDataPreset preset = PresetColor.getColor(data);
                color = preset.color;
                specular = preset.specular;
                metallic = preset.metallic;
            }
        }

        public RecoloringData(Color color, float spec, float metal)
        {
            this.color = color;
            specular = spec;
            metallic = metal;
        }

        public RecoloringData(RecoloringData data)
        {
            color = data.color;
            specular = data.specular;
            metallic = data.metallic;
        }
        
        public Color getShaderColor()
        {
            color.a = specular;
            return color;
        }

        public string getPersistentData()
        {
            return color.r + "," + color.g + "," + color.b + "," + specular + "," + metallic;
        }

    }

    /// <summary>
    /// Wraps a persistent data field in a PartModule/etc to support load/save operations for recoloring data in a consistent and encapsulated fashion.
    /// </summary>
    public class RecoloringHandler
    {
        private BaseField persistentDataField;

        private RecoloringData[] colorData;

        public RecoloringHandler(BaseField persistentDataField)
        {
            this.persistentDataField = persistentDataField;
            int len = 3;
            colorData = new RecoloringData[len];
            string[] channelData = this.persistentDataField.GetValue<string>(persistentDataField.host).Split(';');
            for (int i = 0; i < len; i++)
            {
                colorData[i] = new RecoloringData(channelData[i]);
            }
        }

        public RecoloringData getColorData(int index)
        {
            return colorData[index];
        }

        public RecoloringData[] getColorData()
        {
            return colorData;
        }

        public void setColorData(RecoloringData[] data)
        {
            this.colorData = data;
            save();
        }

        public void save()
        {
            int len = colorData.Length;
            string data = "";
            for (int i = 0; i < len; i++)
            {
                if (i > 0)
                {
                    data = data + ";";
                }
                data = data + colorData[i].getPersistentData();
            }
            persistentDataField.SetValue(data, persistentDataField.host);
        }

    }

}
