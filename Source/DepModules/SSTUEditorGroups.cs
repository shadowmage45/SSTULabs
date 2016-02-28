using System.Collections.Generic;
using UnityEngine;

namespace SSTUTools
{
    //[KSPAddon(KSPAddon.Startup.MainMenu, true)]
    public class SSTUEditorGroups : MonoBehaviour
    {
        ////https://github.com/KospY/KIS/blob/master/Plugins/Source/KISAddonEditorFilter.cs
        ////https://github.com/BobPalmer/UmbraSpaceIndustries/blob/master/USITools/USITools/PartCatalog.cs
        //private static List<AvailablePart> groupParts = new List<AvailablePart>();
        //internal string category = "Filter by Function";
        //internal string subCategoryTitle = "SSTU - Engine Clusters";
        //internal string defaultTitle = "SSTU";
        //internal string iconName = "R&D_node_icon_advrocketry";

        //void Awake()
        //{
        //    GameEvents.onGUIEditorToolbarReady.Add(SubCategories);
        //    groupParts.Clear();
        //    SSTUEngineCluster engineClusterModule;
        //    foreach (AvailablePart availablePart in PartLoader.LoadedPartsList)
        //    {
        //        if (availablePart.partPrefab == null) { continue; }
        //        engineClusterModule = availablePart.partPrefab.GetComponent<SSTUEngineCluster>();
        //        if (engineClusterModule != null && availablePart.category==PartCategories.none)
        //        {
        //            groupParts.Add(availablePart);
        //        }
        //    }
        //}

        //private bool EditorItemsFilter(AvailablePart avPart)
        //{
        //    if (groupParts.Contains(avPart))
        //    {
        //        return true;
        //    }
        //    else
        //    {
        //        return false;
        //    }
        //}

        //private void SubCategories()
        //{
        //    RUI.Icons.Selectable.Icon icon = PartCategorizer.Instance.iconLoader.GetIcon(iconName);
        //    PartCategorizer.Category Filter = PartCategorizer.Instance.filters.Find(f => f.button.categoryName == category);
        //    PartCategorizer.AddCustomSubcategoryFilter(Filter, subCategoryTitle, icon, p => EditorItemsFilter(p));

        //    RUIToggleButtonTyped button = Filter.button.activeButton;
        //    button.SetFalse(button, RUIToggleButtonTyped.ClickType.FORCED);
        //    button.SetTrue(button, RUIToggleButtonTyped.ClickType.FORCED);
        //}
    }
}

