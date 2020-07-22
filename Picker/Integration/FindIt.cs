using ColossalFramework.UI;
using System;
using System.Collections.Generic;
using System.Reflection;
using UnityEngine;

namespace Picker
{
    public partial class PickerTool
    {
        public static Dictionary<String, int> MenuIndex = new Dictionary<string, int>
        {
            ["All"] = 0,
            ["Ploppable"] = 2,
            ["Growable"] = 3,
            ["RICO"] = 4,
            ["Prop"] = 4,
            ["Decal"] = 5
        };

        private UIComponent _findItSearchbox = null;
        private UIComponent FISearchbox
        {
            get
            {
                if (_findItSearchbox == null)
                {
                    UIComponent searchBoxPanel = UIView.Find("UISearchBox");
                    if (searchBoxPanel == null)
                    {
                        Debug.Log($"Find It not found");
                        return null;
                    }
                    _findItSearchbox = searchBoxPanel;
                }
                return _findItSearchbox;
            }
        }

        private UIDropDown _filterDropdown = null;
        private UIDropDown FIFilterDropdown
        {
            get
            {
                if (_filterDropdown == null)
                {
                    UIDropDown filterDropdown = FISearchbox.Find<UIDropDown>("UIDropDown");
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

        private void SetFIDropdown(string entry)
        {
            //Debug.Log($"Dropdown: {entry}");
            FIFilterDropdown.selectedIndex = MenuIndex[entry];
        }

        private void ReflectIntoFindIt(PrefabInfo info)
        {
            Type FindItType = Type.GetType("FindIt.FindIt, FindIt");
            object FindItInstance = FindItType.GetField("instance").GetValue(null);
            Type SearchBoxType = Type.GetType("FindIt.GUI.UISearchBox, FindIt");
            object UISearchbox = FindItType.GetField("searchBox").GetValue(FindItInstance);

            // Turn on workshop or vanilla filter (Find It 2 only)
            FieldInfo checkBoxType = SearchBoxType.GetField(info.m_isCustomContent ? "workshopFilter" : "vanillaFilter");
            if (checkBoxType != null)
            {
                UICheckBox checkBox = (UICheckBox)checkBoxType.GetValue(UISearchbox);
                checkBox.isChecked = true;
            }

            // Reset building level and size filters to 'All' (both Find It and Find It 2)
            if (info is BuildingInfo)
            {
                FieldInfo levelFilterType = SearchBoxType.GetField("levelFilter");
                if (levelFilterType != null)
                {
                    UIDropDown levelFilter = (UIDropDown)levelFilterType.GetValue(UISearchbox);
                    levelFilter.selectedIndex = 0;
                }
                FieldInfo sizeFilterXType = SearchBoxType.GetField("sizeFilterX");
                if (sizeFilterXType != null)
                {
                    UIDropDown sizeFilterX = (UIDropDown)sizeFilterXType.GetValue(UISearchbox);
                    sizeFilterX.selectedIndex = 0;
                }
                FieldInfo sizeFilterYType = SearchBoxType.GetField("sizeFilterY");
                if (sizeFilterYType != null)
                {
                    UIDropDown sizeFilterY = (UIDropDown)sizeFilterYType.GetValue(UISearchbox);
                    sizeFilterY.selectedIndex = 0;
                }
            }

            // Turn off custom tag filter (Find It 2 only)
            Type FilterTagType = Type.GetType("FindIt.GUI.UIFilterTag, FindIt");
            if (FilterTagType != null) 
            { 
                object UIFilterTag = SearchBoxType.GetField("tagPanel").GetValue(UISearchbox);
                FieldInfo customTagFilterType = FilterTagType.GetField("tagDropDownCheckBox");
                if (customTagFilterType != null)
                {
                    UICheckBox customTagFilterCheckBox = (UICheckBox)customTagFilterType.GetValue(UIFilterTag);
                    customTagFilterCheckBox.isChecked = false;
                }
            }

            if (FISearchbox == null)
                return;

            // Open Find It
            if (!FISearchbox.isVisible)
            {
                UIButton FIButton = UIView.Find<UIButton>("FindItMainButton");
                if (FIButton == null) return;
                FIButton.SimulateClick();
            }

            StartCoroutine(ReflectIntoFindItProcess(info, false));
        }

        private IEnumerator<object> ReflectIntoFindItProcess(PrefabInfo info, bool tryingPRICO)
        {
            yield return new WaitForSeconds(0.10f);

            bool found = false;

            Type FindItType = Type.GetType("FindIt.FindIt, FindIt");
            object FindItInstance = FindItType.GetField("instance").GetValue(null);
            object UIScrollPanel = FindItType.GetField("scrollPanel").GetValue(FindItInstance);
            Type ScrollPanelType = Type.GetType("FindIt.GUI.UIScrollPanel, FindIt");

            // Get all the item data...
            PropertyInfo iData = ScrollPanelType.GetProperty("itemsData");
            object itemsData = iData.GetValue(UIScrollPanel, null);
            object[] itemDataBuffer = itemsData.GetType().GetMethod("ToArray").Invoke(itemsData, null) as object[];

            for (int i = 0; i < itemDataBuffer.Length; i++)
            {
                object itemData = itemDataBuffer[i];

                // Get the actual asset data of this prefab instance in the Find It scrollable panel
                Type UIScrollPanelItemData = itemData.GetType();
                object itemData_currentData_asset = UIScrollPanelItemData.GetField("asset").GetValue(itemData);
                PrefabInfo itemData_currentData_asset_info = itemData_currentData_asset.GetType().GetProperty("prefab").GetValue(itemData_currentData_asset, null) as PrefabInfo;

                // Display data at this position. Return.
                if (itemData_currentData_asset_info != null && itemData_currentData_asset_info.name == info.name)
                {
                    //Debug.Log("Found data at position " + i + " in Find it ScrollablePanel");
                    ScrollPanelType.GetMethod("DisplayAt").Invoke(UIScrollPanel, new object[] { i });

                    string itemDataName = UIScrollPanelItemData.GetField("name").GetValue(itemData) as string;
                    UIComponent test = UIScrollPanel as UIComponent;
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
                if (info is BuildingInfo && !tryingPRICO)
                {
                    // If it's a building and it hasn't been found, try again for Ploppable RICO
                    SetFIDropdown("RICO");
                    StartCoroutine(ReflectIntoFindItProcess(info, true));
                }
                else
                {
                    Debug.Log($"Object {info.name} not found");
                }
            }
        }
    }
}
