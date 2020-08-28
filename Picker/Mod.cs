using ColossalFramework;
using ColossalFramework.Globalization;
using ColossalFramework.Plugins;
using ColossalFramework.UI;
using ICities;
using Picker.Localization;
using System;
using System.Globalization;
using System.Reflection;
using UnityEngine;

namespace Picker
{
    public class Picker : LoadingExtensionBase, IUserMod
    {
        static CultureInfo Culture => new CultureInfo(SingletonLite<LocaleManager>.instance.language == "zh" ? "zh-cn" : SingletonLite<LocaleManager>.instance.language);

        public string Name => "Picker 1.4";
        public string Description => Localize.mod_Description;
        public const string settingsFileName = "Picker";

        private static GameObject GO_FindIt;

        private static int findItVersion = -1;
        internal static int FindItVersion
        {
            get
            {
                if (findItVersion == -1)
                {
                    findItVersion = GetFindItVersion();
                }
                return findItVersion;
            }
        }

        private static int moveItVersion = -1;
        internal static int MoveItVersion
        {
            get
            {
                if (moveItVersion == -1)
                {
                    moveItVersion = GetMoveItVersion();
                }
                return moveItVersion;
            }
        }

        public Picker()
        {
            try
            {
                // Creating setting file
                if (GameSettings.FindSettingsFileByName(settingsFileName) == null)
                {
                    GameSettings.AddSettingsFile(new SettingsFile[] { new SettingsFile() { fileName = settingsFileName } });
                }
            }
            catch (Exception e)
            {
                Debug.Log($"Could not load/create the setting file.\n{e}");
            }
        }

        public override void OnLevelLoaded(LoadMode mode)
        {
            if (!(mode == LoadMode.LoadGame || mode == LoadMode.NewGame || mode == LoadMode.NewGameFromScenario || mode == LoadMode.LoadScenario || mode == LoadMode.NewMap || mode == LoadMode.LoadMap))
            {
                return;
            }

            InstallMod();
        }

        public override void OnLevelUnloading()
        {
            UninstallMod();
        }

        public static void InstallMod()
        {
            if (PickerTool.instance == null)
            {
                ToolController toolController = ToolsModifierControl.toolController;
                PickerTool.instance = toolController.gameObject.AddComponent<PickerTool>();
                PickerTool.instance.enabled = false;
            }
            else
            {
                Debug.Log($"Picker: InstallMod with existing instance!");
            }

            GO_FindIt = new GameObject();
            PickerTool.FindIt = GO_FindIt.AddComponent<FindItManager>();
        }

        public static void UninstallMod()
        {
            if (ToolsModifierControl.toolController.CurrentTool is PickerTool)
                ToolsModifierControl.SetTool<DefaultTool>();

            if (PickerTool.instance != null)
            {
                PickerTool.instance.enabled = false;
                UnityEngine.Object.Destroy(PickerTool.instance.m_button);
                PickerTool.instance.m_button = null;
            }

            UnityEngine.Object.Destroy(GO_FindIt);
            PickerTool.FindIt = null;
            UnityEngine.Object.Destroy(PickerTool.instance);
            PickerTool.instance = null;

            LocaleManager.eventLocaleChanged -= LocaleChanged;
        }

        public void OnSettingsUI(UIHelperBase helper)
        {
            LocaleManager.eventLocaleChanged -= LocaleChanged;
            LocaleChanged();
            LocaleManager.eventLocaleChanged += LocaleChanged;

            UIHelperBase group = helper.AddGroup(Name);

            //Assembly assembly = null;
            //foreach (Assembly a in AppDomain.CurrentDomain.GetAssemblies())
            //{
            //    if (a.FullName.Length >= 12 && a.FullName.Substring(0, 12) == "NetworkSkins")
            //    {
            //        assembly = a;
            //        break;
            //    }
            //}

            UICheckBox checkBox = (UICheckBox)group.AddCheckbox(Localize.options_SetFRTMode, PickerTool.doSetFRTMode.value, (b) =>
            {
                PickerTool.doSetFRTMode.value = b;
            });
            checkBox.tooltip = Localize.options_SetFRTMode_Tooltip;

            group.AddSpace(10);

            checkBox = (UICheckBox)group.AddCheckbox(Localize.options_OpenMenu, PickerTool.openMenu.value, (b) =>
            {
                PickerTool.openMenu.value = b;
            });
            checkBox.tooltip = Localize.options_OpenMenu_Tooltip;

            group.AddSpace(10);

            ((UIPanel)((UIHelper)group).self).gameObject.AddComponent<OptionsKeymappingMain>();
            UIPanel panel = ((UIHelper)group).self as UIPanel;

            group.AddSpace(20);

            UIButton button = (UIButton)group.AddButton(Localize.options_ResetButtonPosition, () =>
            {
                UIPickerButton.savedX.value = -1000;
                UIPickerButton.savedY.value = -1000;
                PickerTool.instance?.m_button?.ResetPosition();
            });

            group.AddSpace(20);

            panel = ((UIHelper)group).self as UIPanel;
            UILabel fitLabel = panel.AddUIComponent<UILabel>();
            fitLabel.name = "fitLabel";
            fitLabel.text = $"Find It: ";
            switch (GetFindItVersion())
            {
                case 0:
                    fitLabel.text += Localize.options_NotFound;
                    break;
                case 1:
                    fitLabel.text += Localize.options_Found + " (v1)";
                    break;
                case 2:
                    fitLabel.text += Localize.options_Found + " (v2)";
                    break;
                default:
                    fitLabel.text += Localize.options_Unknown;
                    break;
            }

            UILabel mitLabel = panel.AddUIComponent<UILabel>();
            mitLabel.name = "mitLabel";
            mitLabel.text = $"Move It: ";
            switch (GetMoveItVersion())
            {
                case 0:
                    mitLabel.text += Localize.options_NotFound;
                    break;
                case 1:
                    mitLabel.text += Localize.options_Found;
                    break;
                default:
                    mitLabel.text += Localize.options_Unknown;
                    break;
            }

            UILabel ns2Label = panel.AddUIComponent<UILabel>();
            ns2Label.name = "ns2Label";
            ns2Label.text = $"Network Skins 2: ";
            switch (PickerTool.isNS2Installed())
            {
                case false:
                    ns2Label.text += Localize.options_NotFound;
                    break;
                case true:
                    ns2Label.text += Localize.options_Found;
                    break;
            }

            group.AddSpace(20);
        }

        internal static void LocaleChanged()
        {
            Debug.Log($"Picker Locale changed {Localize.Culture?.Name}->{Culture.Name}");
            Localize.Culture = Culture;
        }

        internal static Assembly GetAssembly(string name)
        {
            Assembly assembly = null;
            foreach (PluginManager.PluginInfo plugin in PluginManager.instance.GetPluginsInfo())
            {
                foreach (Assembly a in plugin.GetAssemblies())
                {
                    if (plugin.isEnabled && a.GetName().Name.ToLower() == name)
                    {
                        //Debug.Log($"Assembly {name} found");
                        assembly = a;
                        break;
                    }
                }
            }
            return assembly;
        }

        internal static int GetFindItVersion()
        {
            Assembly assembly = GetAssembly("findit");

            if (assembly == null)
            {
                return 0;
            }

            if (assembly.FullName.StartsWith("FindIt, Version=1.0"))
            {
                return 1;
            }

            return 2;
        }

        internal static int GetMoveItVersion()
        {
            Assembly assembly = GetAssembly("moveit");

            if (assembly == null)
            {
                return 0;
            }
            return 1;
        }

        private static int _isRicoEnabled = -1;
        internal static bool IsRicoEnabled
        {
            get
            {
                if (_isRicoEnabled == -1)
                {
                    foreach (PluginManager.PluginInfo plugin in PluginManager.instance.GetPluginsInfo())
                    {
                        foreach (Assembly assembly in plugin.GetAssemblies())
                        {
                            if (assembly.GetName().Name.ToLower() == "ploppablerico")
                            {
                                Debug.Log("Ploppable RICO found");
                                _isRicoEnabled = plugin.isEnabled ? 1 : 0;
                                return _isRicoEnabled == 1;
                            }
                        }
                    }
                }

                return _isRicoEnabled == 1 ? true : false;
            }
        }

        public void OnEnabled()
        {
            if (LoadingManager.exists && LoadingManager.instance.m_loadingComplete)
            {
                InstallMod();
            }
        }

        public void OnDisabled()
        {
            if (LoadingManager.exists && LoadingManager.instance.m_loadingComplete)
            {
                UninstallMod();
            }
        }
    }
}
