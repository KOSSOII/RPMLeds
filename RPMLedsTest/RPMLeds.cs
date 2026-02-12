using HutongGames.PlayMaker;
using MSCLoader;
using System.Linq;
using System.Runtime.InteropServices;
using UnityEngine;


namespace RPMLeds
{
    public class RPMLeds : Mod
    {
        public override string ID => "RPMLeds"; // Your (unique) mod ID 
        public override string Name => "RPMLeds"; // Your mod name
        public override string Author => "Izuko"; // Name of the Author (your name)
        public override string Version => "1.0"; // Version
        public override string Description => "RPM Leds for Logitech G923"; // Short description of your mod 
        public override Game SupportedGames => Game.MyWinterCar;
        internal static class LogitechNative
        {
            private const string DLL = @"\mywintercar_Data\Plugins\LogitechSteeringWheel.dll";

            [DllImport(DLL, CallingConvention = CallingConvention.Cdecl)]
            public static extern bool LogiSteeringInitialize(bool ignoreXInput);

            [DllImport(DLL, CallingConvention = CallingConvention.Cdecl)]
            public static extern bool LogiIsConnected(int index);

            [DllImport(DLL, CallingConvention = CallingConvention.Cdecl)]
            public static extern void LogiUpdate();

            [DllImport(DLL, CallingConvention = CallingConvention.Cdecl)]
            public static extern void LogiSteeringShutdown();

            [DllImport(DLL, CallingConvention = CallingConvention.Cdecl)]
            public static extern bool LogiPlayLeds(
                int index,
                float currentRPM,
                float rpmFirstLedTurnsOn,
                float rpmRedLine
            );
        }
        internal static class LogitechManager
        {
            private static bool initialized = false;

            public static bool Initialize()
            {
                if (initialized)
                    return true;

                bool ok = LogitechNative.LogiSteeringInitialize(false);
                if (ok)
                    initialized = true;

                return ok;
            }

            public static void Shutdown()
            {
                if (!initialized)
                    return;

                LogitechNative.LogiSteeringShutdown();
                initialized = false;
            }
        }
        public override void ModSetup()
        {
            SetupFunction(Setup.OnLoad, Mod_OnLoad);
            SetupFunction(Setup.Update, Mod_Update);
            SetupFunction(Setup.ModSettings, Mod_Settings);
            SetupFunction(Setup.OnMenuLoad, Mod_OnMenuLoad);
        }
        public class PartInfo
        {
            public string Path;
            public string Name;
            public FsmFloat PartValue;
            public FsmBool Installed;
        }
        SettingsCheckBox _showDebugMSG;
        SettingsDropDownList _maxRPMSource;
        SettingsSliderInt _manualMaxRPM;
        SettingsSlider _startPointPercent;
        SettingsSlider _maxPointPercent;
        private void Mod_Settings()
        {
            string[] sourceDDSettings = new string[] { "Auto", "Race Tachometer", "Rev Limiter", "Manual" };
            _maxRPMSource = Settings.AddDropDownList("Max RPM Source", "Max RPM Source", sourceDDSettings, OnSelectionChanged: UpdateSource);
            _startPointPercent = Settings.AddSlider("Percent Appear", "Start Point", 1f, 100F, 70F);
            _maxPointPercent = Settings.AddSlider("Max Point", "Max Point Shift", 1f, 100F, 90F);
            _manualMaxRPM = Settings.AddSlider("Manual MaxRPM", "Manual MaxRPM", 650, 10000, 7000, visibleByDefault: false);
            _showDebugMSG = Settings.AddCheckBox("_showDebugMSG", "Show debug messages", false);
        }
        private void UpdateSource()
        {
            if(_maxRPMSource.GetSelectedItemName()== "Manual")
            {
                _manualMaxRPM.SetVisibility(true);
            }
            else
            {
                _manualMaxRPM.SetVisibility(false);
            }
        }
        private PartInfo InitPartValue(string Name, string Path, string VaribleName, string FSMName = "Data")
        {
            var gameObj = GameObject.Find(Path);
            if (gameObj == null)
            {
                if (_showDebugMSG.GetValue())
                    ModConsole.Log($"Part {Name}\n On Path {Path}\n Not found. Skip");
                return null;
            }


            var dataFSM = gameObj.GetComponents<PlayMakerFSM>().Where(x => x.FsmName == FSMName).First();
            if (dataFSM == null)
            {
                if (_showDebugMSG.GetValue())
                    ModConsole.Log($"For Part {Name} Data FSM Not found. Skip");
                return null;
            }
            var value = dataFSM.GetVariable<FsmFloat>(VaribleName);
            var installed = dataFSM.GetVariable<FsmBool>("Installed");
            if (value == null)
            {
                if (_showDebugMSG.GetValue())
                    ModConsole.Log($"Value with {VaribleName} Not found. Skip");
                return null;
            }
            return new PartInfo
            {
                Name = Name,
                Path = Path,
                PartValue = value,
                Installed = installed, 
            };
        }

        FsmFloat carRPM;
        PartInfo revLimit;
        PartInfo raceTachot;
        FsmString currentVeh;
        const float RPM_MAX_DEFAULT = 7000f;
        const float RPM_FIRST_DEFAULT = 5000f;
        private void Mod_OnLoad()
        {
            carRPM = FsmVariables.GlobalVariables.FindFsmFloat("RPM");
            raceTachot = InitPartValue("Tacho", "VINP_Tachometer","SettingRPM");
            revLimit = InitPartValue("RevLimiter", "VINP_Revlimiter", "SettingRPM");
            currentVeh = FsmVariables.GlobalVariables.GetFsmString("PlayerCurrentVehicle");
            if (_maxRPMSource.GetSelectedItemName() == "Manual")
            {
                _manualMaxRPM.SetVisibility(true);
            }
        }
        private void Mod_OnMenuLoad()
        {
            if (LogitechManager.Initialize())
            {
                if (_showDebugMSG.GetValue())
                    ModConsole.Print("Logitech initialized successfully");
            }
            else
            {
                if (_showDebugMSG.GetValue())
                    ModConsole.Print("Logitech init failed");
            }
            
        }
        private void Mod_Update()
        {
            if(carRPM.Value < 50) return;

            if (currentVeh.Value != "Corris") return;

            LogitechNative.LogiUpdate();

            if (!LogitechNative.LogiIsConnected(0))
                return;

            float rpm_MAX = RPM_MAX_DEFAULT;
            float rpm_FIRST_LED = RPM_FIRST_DEFAULT;
            float startPercent = _startPointPercent.GetValue();
            float maxPercent = _maxPointPercent.GetValue();
            switch (_maxRPMSource.GetSelectedItemIndex())
            {
                case 0: //Auto
                    if (raceTachot.Installed.Value && revLimit.Installed.Value)
                    {
                        rpm_MAX = (raceTachot.PartValue.Value <= revLimit.PartValue.Value) ? raceTachot.PartValue.Value : revLimit.PartValue.Value;
                    }
                    else if (raceTachot.Installed.Value)
                    {
                        rpm_MAX = raceTachot.PartValue.Value;
                    }
                    else if (revLimit.Installed.Value)
                    {
                        rpm_MAX = revLimit.PartValue.Value;
                    }
                    break;
                case 1: //Tacho
                    if(raceTachot.Installed.Value)
                        rpm_MAX = raceTachot.PartValue.Value;
                    break;
                case 2: //Rev Limiter
                    if(revLimit.Installed.Value)
                        rpm_MAX = revLimit.PartValue.Value;
                    break;
                case 3: //Manual
                    rpm_MAX = _manualMaxRPM.GetValue();
                    break;
            }
            rpm_FIRST_LED = rpm_MAX * (startPercent / 100f);
            float shiftPoint = rpm_MAX * (maxPercent / 100f);
            if (_showDebugMSG.GetValue())
                ModConsole.Log($"Led Set - RPM:{carRPM.Value} FL:{rpm_FIRST_LED} SP:{shiftPoint} Max:{rpm_MAX}");
            var plays = LogitechNative.LogiPlayLeds(0, carRPM.Value, rpm_FIRST_LED, shiftPoint);
        }
    }
}
