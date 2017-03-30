using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using UnityEngine;

namespace SSTUTools
{
    public interface IRecolorable
    {
        string[] getSectionNames();
        Color[] getSectionColors(string name);
        void setSectionColors(string name, Color color1, Color color2, Color color3);
    }
}
