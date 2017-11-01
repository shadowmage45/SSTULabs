using UnityEngine;

namespace KSPShaderTools
{

    public interface IRecolorable
    {
        string[] getSectionNames();
        Color[] getSectionColors(string name);
        void setSectionColors(string name, Color[] colors);
    }

    public interface IPartTextureUpdated
    {
        void textureUpdated(Part part);
    }

    public interface IPartGeometryUpdated
    {
        void geometryUpdated(Part part);
    }
}
