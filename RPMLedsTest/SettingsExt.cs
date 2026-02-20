using MSCLoader;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.IO;
using System.Xml.Linq;
namespace RPMLeds
{
    public static class SettingsTranslationExtensions
    {
        static Dictionary<string, string> _TranslateDict = new Dictionary<string, string>();
        static bool _TranslateFound = false;
        public static bool LoadTranslateDictionary(string translatePath)
        {
            string filePath = Path.Combine(translatePath, "lang.json");
            if (!File.Exists(filePath)) return false;
            try
            {
                var json = File.ReadAllText(filePath);
                _TranslateDict = JsonConvert.DeserializeObject<Dictionary<string, string>>(json);
                _TranslateFound = true;
                return _TranslateFound;
            }
            catch
            {
                ModConsole.Log("RPMLed - Translation lang.json file found but not loaded.");
                return false;
            }
        }
        public static void GetTranslation(string key, ref string translation)
        {
            if (_TranslateFound && _TranslateDict.TryGetValue(key, out var value))
                translation = value;
        }
        // -------- Slider --------
        public static SettingsSliderInt AddSlider(string settingID, string name, int minValue, int maxValue, int value = 0, Action onValueChanged = null, string[] textValues = null, bool visibleByDefault = true)
        {
           GetTranslation(settingID, ref name);
           return Settings.AddSlider(settingID, name, minValue,maxValue, value,onValueChanged,textValues,visibleByDefault);
        }
        public static SettingsSlider AddSlider(string settingID, string name, float minValue, float maxValue, float value = 0f, Action onValueChanged = null, int decimalPoints = 2, bool visibleByDefault = true)
        {
            GetTranslation(settingID, ref name);
            return Settings.AddSlider(settingID, name, minValue, maxValue, value, onValueChanged, decimalPoints, visibleByDefault);
        }
        // -------- CheckBox --------
        public static SettingsCheckBox AddCheckBox(string settingID, string name, bool value = false, Action onValueChanged = null, bool visibleByDefault = true)
        {
            GetTranslation(settingID, ref name);
            return Settings.AddCheckBox(settingID, name, value, onValueChanged, visibleByDefault);
        }

        // -------- Header --------
        public static SettingsHeader AddHeader(string HeaderTitle, bool collapsedByDefault = false, bool visibleByDefault = true)
        {
            GetTranslation(HeaderTitle, ref HeaderTitle);
            return Settings.AddHeader(HeaderTitle, collapsedByDefault, visibleByDefault);
        }

        // -------- Text --------
        public static SettingsText AddText(string text, bool visibleByDefault = true)
        {
            GetTranslation(text, ref text);
            return Settings.AddText(text, visibleByDefault);
        }

        
    }
}
