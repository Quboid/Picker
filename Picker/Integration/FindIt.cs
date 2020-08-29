using ColossalFramework.UI;
using System;
using System.Collections.Generic;
using System.Reflection;
using UnityEngine;

namespace Picker
{
    public class FindItManager : MonoBehaviour
    {
        // Presently dependant on FindIt version
        public static Dictionary<String, int> MenuIndex = new Dictionary<string, int>
        {
            ["All"] = 0,
            ["Growable"] = 3,
            ["RICO"] = 4,
            ["Prop"] = 4,
            ["Decal"] = 5
        };

        private UIComponent _Searchbox = null;
        internal UIComponent Searchbox
        {
            get
            {
                if (_Searchbox == null)
                {
                    UIComponent searchBoxPanel = UIView.Find("UISearchBox");
                    if (searchBoxPanel == null)
                    {
                        Debug.Log($"Find It not found");
                        return null;
                    }
                    _Searchbox = searchBoxPanel;
                }
                return _Searchbox;
            }
        }

        private UIDropDown _filterDropdown = null;
        internal UIDropDown FilterDropdown
        {
            get
            {
                if (_filterDropdown == null)
                {
                    UIDropDown filterDropdown = Searchbox.Find<UIDropDown>("FindIt_AssetTypeFilter") ?? Searchbox.Find<UIDropDown>("UIDropDown");
                    if (filterDropdown == null)
                    {
                        Debug.Log($"Find It filters not found");
                        return null;
                    }
                    _filterDropdown = filterDropdown;
                }
                return _filterDropdown;
            }
        }

        public void Awake()
        {

            if (Picker.IsRicoEnabled)
            { // Will need changed if/when Find It 2 adds more filter types
                MenuIndex["Prop"] += Picker.FindItVersion;
                MenuIndex["Decal"] += Picker.FindItVersion;
            }
        }

        internal void Find(string filterEntry, PrefabInfo info)
        {
            //Debug.Log($"\nFIND filterEntry:{filterEntry}, info:{info.name}");
            if (Searchbox == null)
                return;

            // Open Find It
            if (!Searchbox.isVisible)
            {
                UIButton FIButton = UIView.Find<UIButton>("FindItMainButton");
                if (FIButton == null) return;
                FIButton.SimulateClick();
            }

            // Find It 2 handles all this, thanks to sway
            if (Picker.FindItVersion == 2)
            {
                FilterDropdown.selectedIndex = MenuIndex[filterEntry];

                MethodInfo picker = Searchbox.GetType().GetMethod("Picker", new Type[] { typeof(PrefabInfo) });
                if (!(bool)picker.Invoke(Searchbox, new object[] { info }))
                {
                    if (filterEntry == "Growable")
                    {
                        FilterDropdown.selectedIndex = MenuIndex["RICO"];
                        if (!(bool)picker.Invoke(Searchbox, new object[] { info }))
                        {
                            Debug.Log($"Object {info.name} not found [P04]");
                        }
                        return;
                    }
                    Debug.Log($"Object {info.name} not found [P02]");
                }
                return;
            }

            // Clear the text box
            UITextField TextField = Searchbox.Find<UITextField>("UITextField");
            TextField.text = "";

            StartCoroutine(ClearFilters(filterEntry, info, false, 0));
        }

        private IEnumerator<object> ClearFilters(string filterEntry, PrefabInfo info, bool tryingPRICO, int step)
        {
            //Debug.Log($"\nFILTER {step}: {info.name} <{filterEntry}> {tryingPRICO}");

            FilterDropdown.selectedIndex = MenuIndex[filterEntry];

            yield return new WaitForSeconds(0.05f);

            // Reset the filters
            if (Picker.FindItVersion == 1)
            {
                if (filterEntry == "Growable" || filterEntry == "RICO")
                {
                    UIComponent UIFilterGrowable = Searchbox.Find("UIFilterGrowable");
                    UIFilterGrowable.GetComponentInChildren<UIButton>().SimulateClick();

                    UIDropDown[] dropDowns = Searchbox.GetComponentsInChildren<UIDropDown>();
                    foreach (UIDropDown d in dropDowns)
                    {
                        if (d.items.Length < 7)
                        { // Reset all except categories menu
                            d.selectedIndex = 0;
                        }
                    }
                }
            }
            else
            {
                throw new Exception($"Find It called but not available (version:{Picker.FindItVersion})! [P03]");
            }

            StartCoroutine(FindProcess(filterEntry, info, false, step));
        }

        private IEnumerator<object> FindProcess(string filterEntry, PrefabInfo info, bool tryingPRICO, int step)
        {
            //Debug.Log($"\nPROCESS {step}: {info.name} <{filterEntry}> {tryingPRICO}");

            yield return new WaitForSeconds(0.05f);

            bool found = false;

            Type FindItType = Type.GetType("FindIt.FindIt, FindIt");
            Type ScrollPanelType = Type.GetType("FindIt.GUI.UIScrollPanel, FindIt");
            object FindItInstance = FindItType.GetField("instance").GetValue(null);
            object ScrollPanel = FindItType.GetField("scrollPanel").GetValue(FindItInstance);

            // Get all the item data...
            PropertyInfo iData = ScrollPanelType.GetProperty("itemsData");
            object itemsData = iData.GetValue(ScrollPanel, null);
            object[] itemDataBuffer = itemsData.GetType().GetMethod("ToArray").Invoke(itemsData, null) as object[];

            for (int i = 0; i < itemDataBuffer.Length; i++)
            {
                object itemData = itemDataBuffer[i];

                // Get the actual asset data of this prefab instance in the Find It scrollable panel
                Type ItemDataType = itemData.GetType();
                object itemData_currentData_asset = ItemDataType.GetField("asset").GetValue(itemData);
                PrefabInfo itemData_currentData_asset_info = itemData_currentData_asset.GetType().GetProperty("prefab").GetValue(itemData_currentData_asset, null) as PrefabInfo;

                // Display data at this position. Return.
                if (itemData_currentData_asset_info != null && itemData_currentData_asset_info.name == info.name)
                {
                    //Debug.Log("Found data at position " + i + " in Find it ScrollablePanel");
                    ScrollPanelType.GetMethod("DisplayAt").Invoke(ScrollPanel, new object[] { i });

                    string itemDataName = ItemDataType.GetField("name").GetValue(itemData) as string;
                    UIComponent test = ScrollPanel as UIComponent;
                    UIButton[] fYou = test.GetComponentsInChildren<UIButton>();
                    foreach (UIButton mhmBaby in fYou)
                    {
                        if (mhmBaby.name == itemDataName)
                        {
                            mhmBaby.SimulateClick();
                            found = true;
                        }
                    }
                    if (found)
                    {
                        break;
                    }
                }
            }

            if (!found)
            {
                if (step > 5)
                {
                    Debug.Log($"Object {info.name} not found [P01]");
                    yield break;
                }
                if (Picker.IsRicoEnabled && info is BuildingInfo)
                {
                    // If it's a building and it hasn't been found, try again for Ploppable RICO
                    StartCoroutine(ClearFilters(tryingPRICO ? "Growable" : "RICO", info, !tryingPRICO, ++step));
                }
                else
                {
                    StartCoroutine(FindProcess(filterEntry, info, false, ++step));
                }
            }
        }
    }
}
