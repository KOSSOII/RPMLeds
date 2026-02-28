using Harmony;
using HutongGames.PlayMaker;
using HutongGames.PlayMaker.Actions;
using MSCLoader;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using UnityEngine;



namespace RPMLeds
{
    public class RPMLeds : Mod
    {
        public override string ID => "RPMLeds"; // Your (unique) mod ID 
        public override string Name => "RPM Leds And Advanced FFB"; // Your mod name
        public override string Author => "Izuko"; // Name of the Author (your name)
        public override string Version => "1.7.7"; // Version
        public override string Description => "FFB Advanced And RPM Leds for Logitech Steering Wheels"; // Short description of your mod 
        public override Game SupportedGames => Game.MyWinterCar;
        public static bool Patch = true;
        public bool UseDirectInputFFB = false;
        public bool DIFFBInit = false;
        public bool DETECTMODCARS = false;
        private HarmonyInstance harmony;
        #region Dirtect Input
        [DllImport("user32")]
        private static extern int GetForegroundWindow();

        [DllImport("UnityForceFeedback")]
        private static extern int InitDirectInput(int HWND);

        [DllImport("UnityForceFeedback")]
        private static extern void Aquire();

        [DllImport("UnityForceFeedback")]
        private static extern int SetDeviceForcesXY(int x, int y);

        [DllImport("UnityForceFeedback")]
        private static extern bool StartEffect();

        [DllImport("UnityForceFeedback")]
        private static extern bool StopEffect();

        [DllImport("UnityForceFeedback")]
        private static extern bool SetAutoCenter(bool autoCentre);

        [DllImport("UnityForceFeedback")]
        private static extern void FreeDirectInput();

        #endregion

        public class PartInfo
        {
            public string Path;
            public string Name;
            public FsmFloat PartValue;
            public FsmBool Installed;
        }
        [StructLayout(LayoutKind.Sequential)]
        public struct LogiControllerPropertiesData
        {
            public int forceEnable;
            public int overallGain;
            public int springGain;
            public int damperGain;
            public int defaultSpringEnabled;
            public int defaultSpringGain;
            public int combinePedals;
            public int wheelRange;
            public int gameSettingsEnabled;
            public int allowControllerProperties;
        }
        #region SettingsVars

        SettingsCheckBox _showDebugMSG;
        SettingsCheckBox _enableAdvancedFFB;
        SettingsDropDownList _maxRPMSource;
        SettingsSliderInt _manualMaxRPM;
        SettingsSlider _startPointPercent;
        SettingsSlider _maxPointPercent;
        SettingsSliderInt _controllerIndex;
        SettingsCheckBox _modEnabled;
        SettingsCheckBox _rpmLedsEnabled;
        SettingsCheckBox _profilerEnabled;
        SettingsSliderInt _profilerWheelMaxRange;
        SettingsCheckBox _profilerForceEnabled;
        SettingsSliderInt _profilerOverallGain;
        SettingsSliderInt _profilerSpringllGain;
        SettingsSliderInt _profilerDamperGain;
        SettingsCheckBox _profilerAllowGameSettings;
        SettingsCheckBox _profilerCombinedPedals;
        SettingsCheckBox _profilerDefaultSpringEnabled;
        SettingsSliderInt _profilerDefaultSpringGain;
        SettingsSlider _ffbMaxForce;
        SettingsButton _applyProffilerSettings;

        #endregion SettingsVars

        public class ToggledCar
        {
            public string CarName;
            public SettingsCheckBox CB;
        }
        Dictionary<string,bool> CARSTOGGLE = new Dictionary<string, bool>();
        List<ToggledCar> _ffbTogledCarsList = new List<ToggledCar>();

        SettingsSliderInt _DIMultyply;
        SettingsCheckBox _UseDIFFB;
        SettingsCheckBox _SendMozaTelemtry;
        FsmGameObject VehicleSpawn;
        SettingsCheckBox _detectModCars;
        int _DIFFBMltpy = 1000;
        bool _SendMozaTelemetry = false;
        #region SettingsVars_FFBAdvanced
        // Advanced ConstantForce FFB settings (your new system)
        SettingsKeybind keybind;
        SettingsSlider _ffbTorqueScale;
        SettingsCheckBox _ffbToglePerCar;
        SettingsCheckBox _ffbEnableLUT;
        SettingsSlider _ffbbumpGain;
        SettingsSliderInt _ffbbumpDead;
        SettingsSlider _ffbbumpClamp;
        SettingsSlider _ffbbumpSmoothing;
        SettingsSlider _ffbrearSlipStartDeg;
        SettingsSlider _ffbrearSlipFullDeg;
        SettingsSliderInt _ffbrearSlipMinSpeed;
        SettingsSlider _ffbdriftDamperMul;
        SettingsSlider _ffbdriftMzMul;
        SettingsSlider _ffbdriftTrailMul;

        SettingsSlider _ffbSteeringFriction;
        SettingsSlider _ffbFrictionDeadVel;
        SettingsSlider _ffbDamperStop;
        SettingsSlider _ffbDamperRoll;
        SettingsSlider _ffbDamperFast;
        SettingsSlider _ffbDamperLowSpeed;
        SettingsSlider _ffbDamperHighSpeed;

        SettingsSlider _ffbMzNormalize;
        SettingsSlider _ffbMzSoftPower;

        SettingsSlider _ffbTorqueSmoothing;
        SettingsSlider _ffbSoftLimitK;
        SettingsSlider _ffbRateUpPerSec;
        SettingsSliderInt _ConstantDamper;
        SettingsSliderInt _ConstantSoftStop;
        SettingsSlider _ffbRateDownPerSec;

        SettingsSlider _ffbMinNormalForceForFFB;
        SettingsSlider _ffbAirHardReleasePerSec;
        SettingsSlider _ffbAirFilterReset;
        SettingsSlider _ffbLandInTime;
        List<SettingsText> FFBLables = new List<SettingsText>();
        SettingsSlider _ffbMzGain;
        SettingsSlider _ffbTrailMeters;
        SettingsCheckBox _ffbInvertForce;

        SettingsButton _LUTShow;
        SettingsButton _LUTReload;
        #endregion

        #region Vars
        private FfbLut _lut = new FfbLut();
        bool LUTEnabled = false;
        bool _IsToggleByCar = false;
        FsmFloat maxSteeringAngle;
        PartInfo revLimit;
        PartInfo raceTachot;
        FsmString currentVeh;
        int _CONSTANTSOFTSTOP = 100;
        public static Dictionary<string, RegularCarInfo> CARS = new Dictionary<string, RegularCarInfo>();
        public static Dictionary<GameObject, RegularCarInfo> CARSMOD = new Dictionary<GameObject, RegularCarInfo>();
        int profilerOperatinRange = 900;
        const float RPM_MAX_DEFAULT = 7000f;
        const float RPM_FIRST_DEFAULT = 5000f;
        public static bool ffbColisionsEnabled = true;
        bool ledsEnabled = true;
        bool debugIsEnabled = false;
        static bool advancedFFBOn = true;
        float shiftPoint = 0;
        bool forceFuncFinish = false;
        private string propertiesEdit;
        private string actualState;
        private string activeForces;
        public static int _CONTROLLERINDEX = 0;
        public bool forcesIsZero = true;
        Dictionary<string, float> _DEBUGVALS = new Dictionary<string, float>();
        RPMLeds.LogiControllerPropertiesData logiControllerPropertiesData = new RPMLeds.LogiControllerPropertiesData();
        int _CONSTANTDAMPER = 0;
        float rpm_MAX = RPM_MAX_DEFAULT;
        float rpm_FIRST_LED = RPM_FIRST_DEFAULT;
        float startPercent = 0;
        float maxPercent = 0;
        float currentRPM = 0;
        float currentForce = 0;
        float currentSpeed = 0;
        float manualRPMMax = RPM_MAX_DEFAULT;
        int settingsRPMSource = 0;

        public static int collisionForceSetted = 0;
        public bool countersteeringEnabled = false;


        RegularCarInfo CURRENTCAR;
        #endregion
        private FfbLutDebugGraph _lutUi;
        private void ToggleLutUI()
        {
            if (_lutUi == null)
            {
                var go = new GameObject("RPMLeds_FFB_LUT_UI");
                _lutUi = go.AddComponent<FfbLutDebugGraph>();
                _lutUi.lutFolderAbsolute = ModLoader.GetModSettingsFolder(this);
                _lutUi.lutFileName = "ffb_lut.lut";
                _lutUi.showGraph = true;
                _lutUi.Reload();
            }
            else
            {
                UnityEngine.Object.Destroy(_lutUi.gameObject);
                _lutUi = null;
            }
        }
        internal static class LogitechManager
        {
            private static bool initialized = false;

            public static bool Initialize()
            {
                bool ok = false;
                if (initialized)
                    return true;
                try
                {
                    ok = LogitechGSDK.LogiSteeringInitialize(false);
                }
                catch (UnityException ex)
                {
                    ModConsole.Error($"Logi Steering Initialize function failed:\n{ex.Message}");
                    return false;
                }

                if (ok)
                    initialized = true;

                return ok;
            }

            public static void Shutdown()
            {
                if (!initialized)
                    return;

                LogitechGSDK.LogiSteeringShutdown();
                initialized = false;
            }
        }

        #region Start() Custom
        [HarmonyPatch(typeof(ForceFeedback), "Start")]
        class Patch_Block_Start
        {
            static bool Prefix(ForceFeedback __instance, ref CarDynamics ___cardynamics)
            {
                if (!Patch)
                    return true;
                ___cardynamics = __instance.GetComponent<CarDynamics>();
                return false;
            }
        }
        #endregion

        #region Update() Custom
        [HarmonyPatch(typeof(ForceFeedback), "Update")]
        class Patch_Block_Update
        {
            static bool Prefix(ref int ___sign, ref bool ___invertForceFeedback, ref float ___forceFeedback, ref int ___force, ref CarDynamics ___cardynamics, ref int ___clampValue, ref float ___multiplier, ref int ___factor)
            {
                if (!Patch)
                    return true;

                ___sign = 1;
                if (___invertForceFeedback)
                {
                    ___sign = -1;
                }

                ___forceFeedback = ___cardynamics.forceFeedback;
                if (Mathf.Abs(___forceFeedback) > (float)___clampValue)
                {
                    ___forceFeedback = (float)___clampValue * Mathf.Sign(___forceFeedback);
                }

                ___force = (int)(___forceFeedback * ___multiplier) * ___factor * ___sign;
                return false;
            }
        }
        #endregion

        #region DI FFB Functions
        private void InitDIFFB()
        {
            if (DIFFBInit)
            {
                ModConsole.Log("RPMLeds - DIFFB force feedback attempted to initialise but was aleady running!");
                return;
            }

            int foregroundWindow = GetForegroundWindow();
            InitDirectInput(foregroundWindow);
            Aquire();
            DIFFBInit = true;
            SetAutoCenter(autoCentre: false);
        }
        #endregion

        public override void ModSetup()
        {
            SetupFunction(Setup.OnLoad, Mod_OnLoad);
            SetupFunction(Setup.FixedUpdate, Mod_FixedUpdate);
            SetupFunction(Setup.Update, Update);
            SetupFunction(Setup.ModSettings, Mod_Settings);
            SetupFunction(Setup.OnMenuLoad, Mod_OnMenuLoad);
            SetupFunction(Setup.OnGUI, OnGui);
            SetupFunction(Setup.ModSettingsLoaded, SettingsLoaded);
        }
        private void CarToggleChanged()
        {
            _IsToggleByCar = _ffbToglePerCar.GetValue();
            CARSTOGGLE.Clear();
            foreach (var carCB in _ffbTogledCarsList)
            {
                carCB.CB.SetVisibility(_IsToggleByCar);
                CARSTOGGLE.Add(carCB.CarName, carCB.CB.GetValue());
            }

        }
        private void SettingsLoaded()
        {
            SettingChanged();
        }
        private void FFBSettingsChanged()
        {
            bool advOn = _enableAdvancedFFB.GetValue();
            foreach (var lable in FFBLables)
                lable.SetVisibility(advOn);

            _ffbbumpGain.SetVisibility(advOn);
            _ffbbumpDead.SetVisibility(advOn);
            _ffbbumpClamp.SetVisibility(advOn);
            _ffbbumpSmoothing.SetVisibility(advOn);
            _ffbrearSlipStartDeg.SetVisibility(advOn);
            _ffbrearSlipFullDeg.SetVisibility(advOn);
            _ffbrearSlipMinSpeed.SetVisibility(advOn);
            _ffbdriftDamperMul.SetVisibility(advOn);
            _ffbdriftMzMul.SetVisibility(advOn);
            _ffbdriftTrailMul.SetVisibility(advOn);

            rearSlipStartDeg = _ffbrearSlipStartDeg.GetValue();
            rearSlipFullDeg = _ffbrearSlipFullDeg.GetValue();
            rearSlipMinSpeed = _ffbrearSlipMinSpeed.GetValue();


            driftDamperMul = _ffbdriftDamperMul.GetValue();
            driftMzMul = _ffbdriftMzMul.GetValue();
            driftTrailMul = _ffbdriftTrailMul.GetValue();


            bumpGain = _ffbbumpGain.GetValue();
            bumpDead = _ffbbumpDead.GetValue();
            bumpClamp = _ffbbumpClamp.GetValue();
            bumpSmoothing = _ffbbumpSmoothing.GetValue();


            _ffbMzGain.SetVisibility(advOn);
            _ffbTrailMeters.SetVisibility(advOn);
            _ConstantSoftStop.SetVisibility(advOn);
            _CONSTANTSOFTSTOP = _ConstantSoftStop.GetValue();
            _ConstantDamper.SetVisibility(advOn);
            _CONSTANTDAMPER = _ConstantDamper.GetValue();
            softStopEnable = false;
            mzGain = _ffbMzGain.GetValue();
            trailMeters = _ffbTrailMeters.GetValue();


            _ffbTorqueScale.SetVisibility(advOn);
            _ffbMaxForce.SetVisibility(advOn);
            _ffbInvertForce.SetVisibility(advOn);

            _ffbSteeringFriction.SetVisibility(advOn);
            _ffbFrictionDeadVel.SetVisibility(advOn);

            _ffbDamperStop.SetVisibility(advOn);
            _ffbDamperRoll.SetVisibility(advOn);
            _ffbDamperFast.SetVisibility(advOn);
            _ffbDamperLowSpeed.SetVisibility(advOn);
            _ffbDamperHighSpeed.SetVisibility(advOn);

            _ffbMzNormalize.SetVisibility(advOn);
            _ffbMzSoftPower.SetVisibility(advOn);

            _ffbTorqueSmoothing.SetVisibility(advOn);
            _ffbSoftLimitK.SetVisibility(advOn);
            _ffbRateUpPerSec.SetVisibility(advOn);
            _ffbRateDownPerSec.SetVisibility(advOn);

            _ffbMinNormalForceForFFB.SetVisibility(advOn);
            _ffbAirHardReleasePerSec.SetVisibility(advOn);
            _ffbAirFilterReset.SetVisibility(advOn);
            _ffbLandInTime.SetVisibility(advOn);


            steeringFriction = _ffbSteeringFriction.GetValue();
            frictionDeadVel = _ffbFrictionDeadVel.GetValue();

            damperStop = _ffbDamperStop.GetValue();
            damperRoll = _ffbDamperRoll.GetValue();
            damperFast = _ffbDamperFast.GetValue();
            damperLowSpeed = _ffbDamperLowSpeed.GetValue();
            damperHighSpeed = _ffbDamperHighSpeed.GetValue();

            mzNormalize = _ffbMzNormalize.GetValue();
            mzSoftPower = _ffbMzSoftPower.GetValue();

            torqueScale = _ffbTorqueScale.GetValue();
            maxForce = _ffbMaxForce.GetValue();
            torqueSmoothing = _ffbTorqueSmoothing.GetValue();
            softLimitK = _ffbSoftLimitK.GetValue();
            rateUpPerSec = _ffbRateUpPerSec.GetValue();
            rateDownPerSec = _ffbRateDownPerSec.GetValue();

            minNormalForceForFFB = _ffbMinNormalForceForFFB.GetValue();
            airHardReleasePerSec = _ffbAirHardReleasePerSec.GetValue();
            airFilterReset = _ffbAirFilterReset.GetValue();
            landInTime = _ffbLandInTime.GetValue();

            forceInvert = _ffbInvertForce.GetValue();
        }
        private void OnGui()
        {
            if (UseDirectInputFFB) return;
            if (!debugIsEnabled) return;
            float width = 350f;
            float height = 400f;
            float margin = 20f;

            for (int i = 0; i < 3; i++)
            {
                Rect rect = new Rect(
                    margin + i * (width + margin),
                    margin,
                    width,
                    height
                );

                if (i == 0)
                    activeForces = GUI.TextArea(rect, activeForces, 1000);
                else if (i == 1)
                    propertiesEdit = GUI.TextArea(rect, propertiesEdit, 1000);
                else
                    actualState = GUI.TextArea(rect, actualState, 1000);
            }
        }
        private void FFBAdvancedSettings()
        {

            SettingsTranslationExtensions.AddHeader("Advanced FFB Settings");
            _ConstantSoftStop = SettingsTranslationExtensions.AddSlider("_ConstantSoftStop", "Wheel Soft Stop at range %", 0, 100, 98, SettingChanged, visibleByDefault: false);
            _ConstantDamper = SettingsTranslationExtensions.AddSlider("_ConstantDamper", "Constant Damper (St. Wheel rotate resistance)", 0, 100, 15, SettingChanged, visibleByDefault: false);

            _ffbTorqueScale = SettingsTranslationExtensions.AddSlider("_ffbTorqueScale", "Torque Scale", 0.001f, 2f, 0.08f, SettingChanged, visibleByDefault: false);

            _ffbMaxForce = SettingsTranslationExtensions.AddSlider("_ffbMaxForce", "Max Force", 0f, 100f, 98f, SettingChanged, visibleByDefault: false);

            _ffbInvertForce = Settings.AddCheckBox("_ffbInvertForce", "Invert Force", true, SettingChanged, visibleByDefault: false);

            _ffbSteeringFriction = SettingsTranslationExtensions.AddSlider("_ffbSteeringFriction", "Steering Friction", 0f, 2f, 0.4f, SettingChanged, visibleByDefault: false);

            _ffbFrictionDeadVel = SettingsTranslationExtensions.AddSlider("_ffbFrictionDeadVel", "Friction Dead Vel", 0f, 5f, 0.1f, SettingChanged, visibleByDefault: false);

            _ffbDamperStop = SettingsTranslationExtensions.AddSlider("_ffbDamperStop", "Damper Stationary", 0f, 2f, 0.15f, SettingChanged, visibleByDefault: false);

            _ffbDamperRoll = SettingsTranslationExtensions.AddSlider("_ffbDamperRoll", "Damper Roll", 0f, 2f, 0.08f, SettingChanged, visibleByDefault: false);

            _ffbDamperFast = SettingsTranslationExtensions.AddSlider("_ffbDamperFast", "Damper Fast", 0f, 10f, 4.85f, SettingChanged, visibleByDefault: false);

            _ffbDamperLowSpeed = SettingsTranslationExtensions.AddSlider("_ffbDamperLowSpeed", "Damper Low Speed Stationary → Roll  (m/s)", 0f, 50f, 0.85f, SettingChanged, visibleByDefault: false);

            _ffbDamperHighSpeed = SettingsTranslationExtensions.AddSlider("_ffbDamperHighSpeed", "Damper High Speed (m/s)", 0f, 80f, 20f, SettingChanged, visibleByDefault: false);

            _ffbMzNormalize = SettingsTranslationExtensions.AddSlider("_ffbMzNormalize", "Mz Normalize", 0f, 10000f, 810, SettingChanged, visibleByDefault: false);

            _ffbMzSoftPower = SettingsTranslationExtensions.AddSlider("_ffbMzSoftPower", "Mz Soft Power", 0.1f, 8f, 1.17f, SettingChanged, visibleByDefault: false);

            _ffbTorqueSmoothing = SettingsTranslationExtensions.AddSlider("_ffbTorqueSmoothing", "Torque Smoothing", 0f, 1f, 0.04f, SettingChanged, visibleByDefault: false);

            _ffbSoftLimitK = SettingsTranslationExtensions.AddSlider("_ffbSoftLimitK", "Soft Limit K", 0f, 0.500f, 0f, SettingChanged, visibleByDefault: false);

            _ffbRateUpPerSec = SettingsTranslationExtensions.AddSlider("_ffbRateUpPerSec", "Rate Up / sec", 0f, 2000f, 450f, SettingChanged, visibleByDefault: false);

            _ffbRateDownPerSec = SettingsTranslationExtensions.AddSlider("_ffbRateDownPerSec", "Rate Down / sec", 0f, 2000f, 1200f, SettingChanged, visibleByDefault: false);

            _ffbMinNormalForceForFFB = SettingsTranslationExtensions.AddSlider("_ffbMinNormalForceForFFB", "Min Normal Force", 0f, 10000f, 312f, SettingChanged, visibleByDefault: false);

            _ffbAirHardReleasePerSec = SettingsTranslationExtensions.AddSlider("_ffbAirHardReleasePerSec", "Air Hard Release / sec", 0f, 10000f, 2564, SettingChanged, visibleByDefault: false);

            _ffbAirFilterReset = SettingsTranslationExtensions.AddSlider("_ffbAirFilterReset", "Air Filter Reset", 0f, 2f, 0.2f, SettingChanged, visibleByDefault: false);

            _ffbLandInTime = SettingsTranslationExtensions.AddSlider("_ffbLandInTime", "Land In Time", 0f, 2f, 0.18f, SettingChanged, visibleByDefault: false);

            _ffbMzGain = SettingsTranslationExtensions.AddSlider("_ffbMzGain", "Aligning Torque Gain (Mz)", 0.5f, 3.0f, 1.2f, SettingChanged, visibleByDefault: false);

            _ffbTrailMeters = SettingsTranslationExtensions.AddSlider("_ffbTrailMeters", "Steering Trail (meters)", 0.0f, 0.55f, 0.02f, SettingChanged, visibleByDefault: false);

            _ffbbumpGain = SettingsTranslationExtensions.AddSlider("_ffbbumpGain", "Bump Gain", 0.0f, 0.2f, 0.0628f, SettingChanged, visibleByDefault: false, decimalPoints: 4);

            _ffbbumpDead = SettingsTranslationExtensions.AddSlider("_ffbbumpDead", "Bump Dead Zone (N/s)", 0, 15000, 3900, SettingChanged, visibleByDefault: false);

            _ffbbumpClamp = SettingsTranslationExtensions.AddSlider("_ffbbumpClamp", "Bump Clamp", 0.0f, 5000f, 4070, SettingChanged, visibleByDefault: false);

            _ffbbumpSmoothing = SettingsTranslationExtensions.AddSlider("_ffbbumpSmoothing", "Bump Smoothing", 0.0f, 1f, 0.85f, SettingChanged, visibleByDefault: false);

            _ffbrearSlipStartDeg = SettingsTranslationExtensions.AddSlider("_ffbrearSlipStartDeg", "Start modulation Deg", 0.0f, 10f, 3f, SettingChanged, visibleByDefault: false);

            _ffbrearSlipFullDeg = SettingsTranslationExtensions.AddSlider("_ffbrearSlipFullDeg", "Rear Slip Full Deg", 0.0f, 50, 18, SettingChanged, visibleByDefault: false);

            _ffbrearSlipMinSpeed = SettingsTranslationExtensions.AddSlider("_ffbrearSlipMinSpeed", "Rear Slip Min Speed (m/s)", 0, 15, 3, SettingChanged, visibleByDefault: false);
          
            _ffbdriftDamperMul = SettingsTranslationExtensions.AddSlider("_ffbdriftDamperMul", "Drift Damper Mul", 0.0f, 1, 0.80f, SettingChanged, visibleByDefault: false);

            _ffbdriftMzMul = SettingsTranslationExtensions.AddSlider("_ffbdriftMzMul", "Drift Mz Mul", 0.0f, 5, 1.2f, SettingChanged, visibleByDefault: false);

            _ffbdriftTrailMul = SettingsTranslationExtensions.AddSlider("_ffbdriftTrailMul", "Drift Trail Mul", 0.0f, 5, 1.15f, SettingChanged, visibleByDefault: false);

            _ffbEnableLUT = SettingsTranslationExtensions.AddCheckBox("_ffbEnableLUT", "Enable LUT", false, SettingChanged, visibleByDefault: false);
            _LUTShow = Settings.AddButton("Show LUT Curve", ShowLUTGui, false);
            _LUTReload = Settings.AddButton("Reload LUT Curve", ReloadLUT, false);


        }
        private void Mod_Settings()
        {
            SettingsTranslationExtensions.LoadTranslateDictionary(ModLoader.GetModSettingsFolder(this));

            Keybind.AddHeader("Keybind");
            keybind = Keybind.Add("KB1", "Set all forces to 0", KeyCode.F6);

            SettingsTranslationExtensions.AddHeader("RPM Leds");
            _rpmLedsEnabled = SettingsTranslationExtensions.AddCheckBox("_rpmLedsEnabled", "LEDs Enabled", true, SettingChanged);

            string[] sourceDDSettings = new string[] { "Auto", "Race Tachometer", "Rev Limiter", "Manual" };
            _maxRPMSource = Settings.AddDropDownList("Max RPM Source", "Max Corris RPM Source", sourceDDSettings, OnSelectionChanged: SettingChanged);

            _startPointPercent = SettingsTranslationExtensions.AddSlider("Percent Appear", "Start Point", 1f, 100F, 70F, SettingChanged);
            _maxPointPercent = SettingsTranslationExtensions.AddSlider("Max Point", "Max Point Shift", 1f, 100F, 90F, SettingChanged);
            _manualMaxRPM = SettingsTranslationExtensions.AddSlider("Manual MaxRPM", "Manual MaxRPM", 650, 10000, 7000, SettingChanged, visibleByDefault: false);

            SettingsTranslationExtensions.AddHeader("Enable Advanced FFB");
            _enableAdvancedFFB = Settings.AddCheckBox("_AdvancedFFB", "Enable", false, SettingChanged);


            FFBAdvancedSettings();

            _ffbToglePerCar = Settings.AddCheckBox("_ffbToglePerCar", "Toggle Advanced FFB by car", false, CarToggleChanged);

            _ffbTogledCarsList.Clear();

            _ffbTogledCarsList.Add(new ToggledCar { CarName = "Corris", CB = Settings.AddCheckBox("_TGCorris", "Corris", true, CarToggleChanged, visibleByDefault: false) });
            _ffbTogledCarsList.Add(new ToggledCar { CarName = "Sorbet", CB = Settings.AddCheckBox("_TGSorbet", "Sorbet", true, CarToggleChanged, visibleByDefault: false) });
            _ffbTogledCarsList.Add(new ToggledCar { CarName = "Taxi", CB = Settings.AddCheckBox("_TGTaxi", "Taxi", true, CarToggleChanged, visibleByDefault: false) });
            _ffbTogledCarsList.Add(new ToggledCar { CarName = "Kekmet", CB = Settings.AddCheckBox("_TGKekmet", "Kekmet", true, CarToggleChanged, visibleByDefault: false) });
            _ffbTogledCarsList.Add(new ToggledCar { CarName = "Gifu", CB = Settings.AddCheckBox("_TGGifu", "Gifu", true, CarToggleChanged, visibleByDefault: false) });
            _ffbTogledCarsList.Add(new ToggledCar { CarName = "Bachglotz", CB = Settings.AddCheckBox("_TGBachglotz", "Bachglotz", true, CarToggleChanged, visibleByDefault: false) });

            SettingsTranslationExtensions.AddHeader("Properties for Profiler (LGS)");
            SettingsTranslationExtensions.AddText("*BETA MAY CAUSE CRASH* Use if your wheel is set up via Profiler (Logitech Gaming Software) *BETA* Need Testers for LGS");
            _profilerEnabled = Settings.AddCheckBox("_profilerEnabled", "Settings Enabled", false, SettingChanged, visibleByDefault: true);
            _profilerWheelMaxRange = SettingsTranslationExtensions.AddSlider("_profilerWheelMaxRange", "Wheel Max Range", 90, 900, 900, SettingChanged, visibleByDefault: false);
            _profilerForceEnabled = SettingsTranslationExtensions.AddCheckBox("_profilerForceEnabled", "Force Feedback Enabled", true, SettingChanged, visibleByDefault: false);
            _profilerOverallGain = SettingsTranslationExtensions.AddSlider("_profilerOverallGain", "Overall Gain", 0, 100, 80, SettingChanged, visibleByDefault: false);
            _profilerSpringllGain = SettingsTranslationExtensions.AddSlider("_profilerSpringllGain", "Spring Gain", 0, 100, 80, SettingChanged, visibleByDefault: false);
            _profilerDamperGain = SettingsTranslationExtensions.AddSlider("_profilerDamperGain", "Damper Gain", 0, 100, 80, SettingChanged, visibleByDefault: false);
            _profilerAllowGameSettings = SettingsTranslationExtensions.AddCheckBox("_profilerAllowGameSettings", "Allow Game Settings", true, SettingChanged, visibleByDefault: false);
            _profilerCombinedPedals = SettingsTranslationExtensions.AddCheckBox("_profilerCombinedPedals", "Combined Pedals", true, SettingChanged, visibleByDefault: false);
            _profilerDefaultSpringEnabled = SettingsTranslationExtensions.AddCheckBox("_profilerDefaultSpringEnabled", "Default Spring Enabled", true, SettingChanged, visibleByDefault: false);
            _profilerDefaultSpringGain = SettingsTranslationExtensions.AddSlider("_profilerDefaultSpringGain", "Default Spring Gain", 0, 100, 80, SettingChanged, visibleByDefault: false);
            _applyProffilerSettings = Settings.AddButton("Save profiler settings", applyProfiler, visibleByDefault: false);

            Settings.AddHeader("Debug and Controller");
            _showDebugMSG = SettingsTranslationExtensions.AddCheckBox("_showDebugMSG", "Show debug window", false, SettingChanged);
            SettingsTranslationExtensions.AddText("If the controller shown in the debug window is incorrect, try changing the controller index used for detection. After adjusting the index, restart the game and check again.");
            _controllerIndex = SettingsTranslationExtensions.AddSlider("_controllerIndex", "Controller index", 0, 10, 0, SettingChanged, visibleByDefault: true);

            _UseDIFFB = SettingsTranslationExtensions.AddCheckBox("_UseDIFFB", "Use Direct Input FFB (!GAME RESTART REQUIRED!)", false, SettingChanged);
            _DIMultyply = SettingsTranslationExtensions.AddSlider("_DIMultyply", "Direct Input FFB Force Multiply", 1, 10000, 1000, SettingChanged, visibleByDefault: false);
            _SendMozaTelemtry = SettingsTranslationExtensions.AddCheckBox("_SendMozaTelemtry", "Send Telemetry to Pit House", false, SettingChanged, visibleByDefault: false);
            _detectModCars = Settings.AddCheckBox("_detectModCars","Detect Mod Cars", false, SettingChanged,visibleByDefault:true);
            _modEnabled = SettingsTranslationExtensions.AddCheckBox("_modEnabled", "Patch Vanilla FFB (Restart req)", true, SettingChanged, visibleByDefault: false);

        }
        private void ShowLUTGui()
        {
            ToggleLutUI();
        }
        private void ReloadLUT()
        {
            LoadLut();
        }
        private void SettingChanged()
        {
            CarToggleChanged();
            FFBSettingsChanged();

            if (_maxRPMSource.GetSelectedItemName() == "Manual")
            {
                _manualMaxRPM.SetVisibility(true);
            }

            _modEnabled.SetVisibility(_showDebugMSG.GetValue());
            _CONTROLLERINDEX = _controllerIndex.GetValue();
            ledsEnabled = _rpmLedsEnabled.GetValue();
            debugIsEnabled = _showDebugMSG.GetValue();
            advancedFFBOn = _enableAdvancedFFB.GetValue();

            _ffbEnableLUT.SetVisibility(advancedFFBOn);

            LUTEnabled = _ffbEnableLUT.GetValue();

            _LUTShow.SetVisibility(LUTEnabled);
            _LUTReload.SetVisibility(LUTEnabled);

            _manualMaxRPM.SetVisibility(_maxRPMSource.GetSelectedItemName() == "Manual");

            startPercent = _startPointPercent.GetValue();
            maxPercent = _maxPointPercent.GetValue();
            manualRPMMax = _manualMaxRPM.GetValue();
            settingsRPMSource = _maxRPMSource.GetSelectedItemIndex();

            var profilerEnablde = _profilerEnabled.GetValue();
            _profilerWheelMaxRange.SetVisibility(profilerEnablde);
            _profilerForceEnabled.SetVisibility(profilerEnablde);
            _profilerOverallGain.SetVisibility(profilerEnablde);
            _profilerSpringllGain.SetVisibility(profilerEnablde);
            _profilerDamperGain.SetVisibility(profilerEnablde);
            _profilerAllowGameSettings.SetVisibility(false);
            _profilerCombinedPedals.SetVisibility(profilerEnablde);
            _profilerDefaultSpringEnabled.SetVisibility(profilerEnablde);
            _profilerDefaultSpringGain.SetVisibility(profilerEnablde);

            if (!debugIsEnabled)
            {
                propertiesEdit = string.Empty;
                actualState = string.Empty;
            }

            UseDirectInputFFB = _UseDIFFB.GetValue();
            _DIMultyply.SetVisibility(UseDirectInputFFB);
            _SendMozaTelemtry.SetVisibility(UseDirectInputFFB);

            _SendMozaTelemetry = _SendMozaTelemtry.GetValue();
            _DIFFBMltpy = _DIMultyply.GetValue();

            DETECTMODCARS = _detectModCars.GetValue();

            if (logiInit)
                SetOldWheelProperties();
        }

        private PartInfo InitPartValue(string Name, string Path, string VaribleName, string FSMName = "Data")
        {
            var gameObj = GameObject.Find(Path);
            if (gameObj == null)
            {
                if (debugIsEnabled)
                    ModConsole.Log($"Part {Name}\n On Path {Path}\n Not found. Skip");
                return null;
            }


            var dataFSM = gameObj.GetComponents<PlayMakerFSM>().Where(x => x.FsmName == FSMName).First();
            if (dataFSM == null)
            {
                if (debugIsEnabled)
                    ModConsole.Log($"For Part {Name} Data FSM Not found. Skip");
                return null;
            }
            var value = dataFSM.GetVariable<FsmFloat>(VaribleName);
            var installed = dataFSM.GetVariable<FsmBool>("Installed");
            if (value == null)
            {
                if (debugIsEnabled)
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
        public class RegularCarInfo
        {
            public ForceFeedback FFBComp;
            public Drivetrain Drivetrain;
            public GameObject CarObject;
            public Rigidbody Rigidbody;
            public Axles Axles;
            public float LastSteeringAngle; //Set By ffb to calculat steering velocity
            public AxisCarController AxisCarController;
            public bool IsCorris;
            public static RegularCarInfo initCar(string carName)
            {
                var car = GameObject.Find(carName);
                var ffbComp = car.GetComponent<ForceFeedback>();
                var drivetrainComp = car.GetComponent<Drivetrain>();
                return new RegularCarInfo
                {
                    FFBComp = ffbComp,
                    Drivetrain = drivetrainComp,
                    CarObject = car,
                    Rigidbody = car.GetComponent<Rigidbody>(),
                    Axles = car.GetComponent<Axles>(),
                    AxisCarController = car.GetComponent<AxisCarController>(),
                    IsCorris = carName == "CORRIS"
                };
            }
        }
        bool setSteerAngle = false;
        private void Mod_OnLoad()
        {
            Patch = _modEnabled.GetValue();

            currentVeh = FsmVariables.GlobalVariables.GetFsmString("PlayerCurrentVehicle");
            VehicleSpawn = FsmVariables.GlobalVariables.GetFsmGameObject("VehicleSpawn");
            maxSteeringAngle = GameObject.Find("Systems/OptionsDB").GetComponents<PlayMakerFSM>().Where(x => x.FsmName == "Controls").First().GetVariable<FsmFloat>("SteeringRotationFull");

            raceTachot = InitPartValue("Tacho", "VINP_Tachometer", "SettingRPM");
            revLimit = InitPartValue("RevLimiter", "VINP_Revlimiter", "SettingRPM");
            CARS.Clear();
            CARS.Add("Corris", RegularCarInfo.initCar("CORRIS"));
            CARS.Add("Sorbet", RegularCarInfo.initCar("SORBET(190-200psi)"));
            CARS.Add("Taxi", RegularCarInfo.initCar("JOBS/TAXIJOB/MACHTWAGEN"));
            CARS.Add("Kekmet", RegularCarInfo.initCar("KEKMET(350-400psi)"));
            CARS.Add("Gifu", RegularCarInfo.initCar("GIFU(750/450psi)"));
            CARS.Add("Bachglotz", RegularCarInfo.initCar("BACHGLOTZ(1905kg)"));

            
            SettingChanged();
            harmony = HarmonyInstance.Create("izuko.rpmledffb");
            harmony.PatchAll();
            ModConsole.Print("RPMLed - Harmony FFB patches applied. Default FFB Disabled");
            if (UseDirectInputFFB)
                InitDIFFB();

        }
        private void LoadLut()
        {
            _lut.LoadFromFile(Path.Combine(ModLoader.GetModSettingsFolder(this), "ffb_lut.lut"));
        }

        private bool logiInit = false;
        bool softStopEnable = false;
        private void Mod_OnMenuLoad()
        {
            SettingChanged();

            if (!UseDirectInputFFB)
                InitLogi();
            else
                InitDIFFB();

            LoadLut();
        }
        private void InitLogi()
        {
            if (LogitechManager.Initialize())
            {
                ModConsole.Print("RPMLed - Logitech initialized successfully");
                if (_profilerEnabled.GetValue())
                {
                    logiInit = true;
                    if (LoadProfilerWheelProperties(_CONTROLLERINDEX, ref logiControllerPropertiesData))
                    {

                        SetOldWheelProperties();
                        ApplyProfilerWheelProperties(_CONTROLLERINDEX, ref logiControllerPropertiesData);

                    }
                }
            }
            else
            {
                ModConsole.Error("RPMLed - Logitech init failed");
            }
        }
        private void SetForcesToZero()
        {
            if (!UseDirectInputFFB)
            {
                if (!LogitechGSDK.LogiIsConnected(_CONTROLLERINDEX)) return;
                LogitechGSDK.LogiStopSoftstopForce(_CONTROLLERINDEX);
                LogitechGSDK.LogiStopConstantForce(_CONTROLLERINDEX);
                LogitechGSDK.LogiStopDamperForce(_CONTROLLERINDEX);
                LogitechGSDK.LogiStopSpringForce(_CONTROLLERINDEX);
                softStopEnable = false;
            }
            else
            {
                SetDeviceForcesXY(0, 0);
            }
            forcesIsZero = true;
        }
        private void PlayLogiRPMLeds()
        {
            rpm_MAX = RPM_MAX_DEFAULT;
            rpm_FIRST_LED = RPM_FIRST_DEFAULT;

            if (CURRENTCAR.IsCorris)
                switch (settingsRPMSource)
                {
                    case 0: // Auto
                        if (revLimit != null && raceTachot != null && raceTachot.Installed.Value && revLimit.Installed.Value)
                            rpm_MAX = Mathf.Min(raceTachot.PartValue.Value, revLimit.PartValue.Value);
                        else if (raceTachot != null && raceTachot.Installed.Value)
                            rpm_MAX = raceTachot.PartValue.Value;
                        else if (revLimit != null && revLimit.Installed.Value)
                            rpm_MAX = revLimit.PartValue.Value;
                        break;
                    case 1: // Tacho
                        if (raceTachot != null && raceTachot.Installed.Value)
                            rpm_MAX = raceTachot.PartValue.Value;
                        break;
                    case 2: // Rev Limiter
                        if (revLimit != null && revLimit.Installed.Value)
                            rpm_MAX = revLimit.PartValue.Value;
                        break;
                    case 3: // Manual
                        rpm_MAX = manualRPMMax;
                        break;
                }

            rpm_FIRST_LED = rpm_MAX * (startPercent / 100f);
            shiftPoint = rpm_MAX * (maxPercent / 100f);
            PlayLedsCall(_CONTROLLERINDEX, currentRPM, rpm_FIRST_LED, shiftPoint);

        }
        private void PlayLedsCall(int controllerIndex, float currentRpm, float startRpm, float endRpm)
        {
            if (!UseDirectInputFFB)
                LogitechGSDK.LogiPlayLeds(controllerIndex, currentRpm, startRpm, endRpm);
        }
        private bool LogiOK()
        {
            var logiOK = LogitechGSDK.LogiIsConnected(_CONTROLLERINDEX) && LogitechGSDK.LogiUpdate();

            if (!setSteerAngle)
            {
                LogitechGSDK.LogiSetOperatingRange(_CONTROLLERINDEX, profilerOperatinRange);
                setSteerAngle = true;
            }
            return logiOK;
        }
        private void Mod_FixedUpdate()
        {
            if (string.IsNullOrEmpty(currentVeh.Value))
            {
                if (!forcesIsZero)
                    SetForcesToZero();
                return;
            }
            if (!UseDirectInputFFB)
            {
                if (!LogiOK()) return;
            }

            if(DETECTMODCARS)
            {
                var carGet = CARSMOD.TryGetValue(VehicleSpawn.Value, out CURRENTCAR);
                if(!carGet)
                {
                    CARSMOD.Add(VehicleSpawn.Value, RegularCarInfo.initCar(VehicleSpawn.Value.name));
                    ModConsole.Log($"RPMLed - Init Car {VehicleSpawn.Value.name}");
                }
                carGet = CARSMOD.TryGetValue(VehicleSpawn.Value, out CURRENTCAR);
            }
            else
            {
                CURRENTCAR = CARS[currentVeh.Value];
            }
            if (CURRENTCAR == null)
            {
                ModConsole.Error("Current Car not found it cars list");
                return;
            }

            currentRPM = CURRENTCAR.Drivetrain.rpm;
            currentSpeed = Mathf.Abs(CURRENTCAR.Drivetrain.differentialSpeed);


            if (currentRPM > 50 && ledsEnabled)
            {
                if (!UseDirectInputFFB)
                    PlayLogiRPMLeds();
            }
            
            var vanillaForce = CURRENTCAR.FFBComp.force;
            var advanced = advancedFFBOn && !_IsToggleByCar || advancedFFBOn && _IsToggleByCar && CARSTOGGLE[currentVeh.Value];
            if (!UseDirectInputFFB)
            {
                PlayLogiFFB(vanillaForce, advanced);
            }
            else
            {
                PlayDirectInputFFB(vanillaForce, advanced);
            }

        }
        private void PlayDirectInputFFB(int vanillaForce, bool advanced)
        {
            if (forcesIsZero)
                StartEffect();
            var force = vanillaForce;
            if (advanced)
            {
                float steer = Input.GetAxisRaw("Joy1 Axis 1");
                int DISteerValue = Mathf.RoundToInt(steer * 32767f);
                force = CalculateForces(CURRENTCAR, DISteerValue) * _DIFFBMltpy;
            }
            SetDeviceForcesXY(force, 0);
        }
        private void PlayLogiFFB(int vanillaForce, bool advanced)
        {
            var force = vanillaForce / 100;
            if (advanced)
            {
                if (!softStopEnable)
                {
                    LogitechGSDK.LogiPlaySoftstopForce(_CONTROLLERINDEX, _CONSTANTSOFTSTOP);
                    LogitechGSDK.LogiPlayDamperForce(_CONTROLLERINDEX, _CONSTANTDAMPER); // 5–15 good range
                    softStopEnable = true;
                }
                var state = LogitechGSDK.LogiGetStateCSharp(_CONTROLLERINDEX);
                force = CalculateForces(CURRENTCAR, state.lX);
            }

            LogitechGSDK.LogiPlayConstantForce(_CONTROLLERINDEX, force);
        }
        #region FFB Settings

        // Rear slip -> parameter modulation (NOT assist torque)
        private float rearSlipStartDeg = 3f;     // start modulation
        private float rearSlipFullDeg = 18f;    // full modulation
        private float rearSlipMinSpeed = 3f;     // m/s

      
        private float driftDamperMul = 0.80f;  // damper reduced to 80% at full rear slip
        private float driftMzMul = 1.20f;  // Mz gain boosted to 120% at full rear slip
        private float driftTrailMul = 1.15f;  // trail boosted to 115% at full rear slip

        private float rearSlipExtraFromVelo = 0.0f; // optional (0..1), keep 0 for now

        private float rearBumpScale = 0.45f;   // 0..1 how much rear jolts affect steering (start 0.3–0.6)

        // Jolt -> torque
        private float bumpGain = 0.0018f;      // torque per (N/s)
        private float bumpDead = 2500f;        // N/s deadzone
        private float bumpClamp = 2.0f;        // clamp bump torque (torque units)
        private float bumpSmoothing = 0.35f;   // 0.2–0.6

        private float steeringFriction = 1.1f;      // rack friction (0.4–1.5)
        private float frictionDeadVel = 0.01f;      // deg/s deadzone (if using wheel deg), tweak as needed

        // Software damper (analog) using wheel angular velocity
        private float damperStop = 0.80f;           // torque per deg/s when stopped (heavier)
        private float damperRoll = 0.35f;           // torque per deg/s at low speed (lighter)
        private float damperFast = 0.85f;           // torque per deg/s at high speed (heavier again)
        private float damperLowSpeed = 6f;          // m/s where it reaches "roll"
        private float damperHighSpeed = 25f;        // m/s where it reaches "fast"

        // Aligning torque shaping
        private float mzNormalize = 1000f;
        private float mzSoftPower = 1.7f;

        // Output shaping
        private float torqueScale = 0.07f; //SCALE ALL
        private float maxForce = 100f;
        private float torqueSmoothing = 0.08f;
        private float softLimitK = 0.04f;
        private float rateUpPerSec = 450f;
        private float rateDownPerSec = 1200f;
        private bool forceInvert = true;

        // Air handling (prevents spikes / "stuck at 100")
        private float minNormalForceForFFB = 1000f;
        private float airHardReleasePerSec = 8000f;
        private float airFilterReset = 0.35f;
        private float landInTime = 0.35f;


        private float mzGain = 1.4f;        // 1.0–2.5 (more = stronger natural self-steer)
        private float trailMeters = 0.06f;  // 0.03–0.10 meters (more = more self-steer)

        private float filteredTorque = 0f;
        private float lastSentForce = 0f;
        private float contactBlend = 1f;

        // Wheel angle tracking (software damper uses this)
        private bool wheelAngleInited = false;
        private float lastWheelDeg = 0f;
        #endregion
        private static float SoftLimit(float x, float k) => x / (1f + k * Mathf.Abs(x));
        private static float PowSigned(float x, float p)
        {
            float a = Mathf.Abs(x);
            return Mathf.Sign(x) * Mathf.Pow(a, p);
        }

        private float _flPrevFz, _frPrevFz, _rlPrevFz, _rrPrevFz;
        private float _bumpFiltered;
        private int CalculateForces(RegularCarInfo Car, int WheelDInputPositionValue)
        {
            float dt = Time.fixedDeltaTime;

            // ===== 1) Read physical wheel angle (preferred) =====
            float maxWheelAngle = maxSteeringAngle.Value;
            float wheelDeg = WheelDInputPositionValue * (maxWheelAngle / 32767f); // adjust if you use 1080/540 etc.

            if (!wheelAngleInited)
            {
                wheelAngleInited = true;
                lastWheelDeg = wheelDeg;

                // init prev values to avoid first-frame spike
                var fl0 = Car.Axles.frontAxle.leftWheel;
                var fr0 = Car.Axles.frontAxle.rightWheel;
                var rl0 = Car.Axles.rearAxle.leftWheel;
                var rr0 = Car.Axles.rearAxle.rightWheel;

                _flPrevFz = fl0.normalForce;
                _frPrevFz = fr0.normalForce;
                _rlPrevFz = rl0.normalForce;
                _rrPrevFz = rr0.normalForce;
            }

            float wheelVelDegPerSec = (wheelDeg - lastWheelDeg) / dt;
            lastWheelDeg = wheelDeg;

            // ===== 2) Car speed =====
            float speed = Car.Rigidbody.velocity.magnitude;

            // ===== 3) Wheel references & contact detection =====
            var fl = Car.Axles.frontAxle.leftWheel;
            var fr = Car.Axles.frontAxle.rightWheel;
            var rl = Car.Axles.rearAxle.leftWheel;
            var rr = Car.Axles.rearAxle.rightWheel;

            bool flLoaded = fl.onGroundDown && fl.normalForce > minNormalForceForFFB;
            bool frLoaded = fr.onGroundDown && fr.normalForce > minNormalForceForFFB;
            bool frontLoaded = flLoaded && frLoaded;

            bool rlLoaded = rl.onGroundDown && rl.normalForce > minNormalForceForFFB;
            bool rrLoaded = rr.onGroundDown && rr.normalForce > minNormalForceForFFB;
            bool rearLoaded = rlLoaded && rrLoaded;

            // ===== 4) Contact blend for smooth landing =====
            float targetBlend = frontLoaded ? 1f : 0f;
            float inStep = (landInTime <= 0.0001f) ? 1f : (dt / landInTime);
            contactBlend = Mathf.MoveTowards(contactBlend, targetBlend, (targetBlend > contactBlend) ? inStep : 1f);

            // ===== 5) Wheel normalization (-1..1) =====
            float wheelHalfRangeDeg = maxWheelAngle / 2; // for 900°
            float wheelNorm = Mathf.Clamp(wheelDeg / wheelHalfRangeDeg, -1f, 1f);

            // ===== 6) Rear slip factor (0..1) -> modulates parameters (NO extra assist torque) =====
            float rearSlip01 = 0f;
            if (rearLoaded && speed > rearSlipMinSpeed)
            {
                float rearSlipDeg = (Mathf.Abs(rl.slipAngle) + Mathf.Abs(rr.slipAngle)) * 0.5f;

                float slip01 = Mathf.InverseLerp(rearSlipStartDeg, rearSlipFullDeg, rearSlipDeg);
                slip01 = Mathf.Clamp01(slip01);

                float speed01 = Mathf.Clamp01((speed - rearSlipMinSpeed) / 10f);

                rearSlip01 = slip01 * speed01;
            }


            float damperMul = Mathf.Lerp(1f, driftDamperMul, rearSlip01);
            float mzMul = Mathf.Lerp(1f, driftMzMul, rearSlip01);
            float trailMul = Mathf.Lerp(1f, driftTrailMul, rearSlip01);


            // ===== 8) Software damper (analog), speed-shaped =====
            float low01 = Mathf.Clamp01(speed / damperLowSpeed);
            float high01 = Mathf.Clamp01(speed / damperHighSpeed);

            float damperGain = Mathf.Lerp(damperStop, damperRoll, low01);
            damperGain = Mathf.Lerp(damperGain, damperFast, high01);
            damperGain *= damperMul;

            float damperTorque = -wheelVelDegPerSec * damperGain;

            // ===== 9) Friction (Coulomb) =====
            float frictionTorque = 0f;
            if (Mathf.Abs(wheelVelDegPerSec) > frictionDeadVel)
                frictionTorque = -Mathf.Sign(wheelVelDegPerSec) * steeringFriction;

            // ===== 10) Tire aligning torque (Mz + trail from Fy) softened near center =====
            float tireTorque = 0f;
            if (frontLoaded)
            {
                float rawMz = (fl.Mz + fr.Mz) * (mzGain * mzMul);

                // If direction feels wrong, flip sign once:
                // float rawTrail = -(fl.Fy + fr.Fy) * (trailMeters * trailMul);
                float rawTrail = (fl.Fy + fr.Fy) * (trailMeters * trailMul);

                float rawAlign = rawMz + rawTrail;

                float normAlign = Mathf.Clamp(rawAlign / mzNormalize, -1f, 1f);
                float softenedAlign = PowSigned(normAlign, mzSoftPower) * mzNormalize;

                tireTorque = softenedAlign * contactBlend;
            }

            // ===== 11) Bumps / curb kick (dFz/dt) FRONT + REAR, both directions =====
            float bumpTorque = 0f;

            float frontJolt = 0f;
            if (frontLoaded)
            {
                float flJ = (fl.normalForce - _flPrevFz) / dt;
                float frJ = (fr.normalForce - _frPrevFz) / dt;
                frontJolt = (flJ + frJ) * 0.5f;
            }

            float rearJolt = 0f;
            if (rearLoaded)
            {
                float rlJ = (rl.normalForce - _rlPrevFz) / dt;
                float rrJ = (rr.normalForce - _rrPrevFz) / dt;
                rearJolt = (rlJ + rrJ) * 0.5f;
            }

            // update prevs every frame (important!)
            _flPrevFz = fl.normalForce;
            _frPrevFz = fr.normalForce;
            _rlPrevFz = rl.normalForce;
            _rrPrevFz = rr.normalForce;

            float jolt = frontJolt + rearJolt * rearBumpScale; // N/s

            // deadzone for noise (both directions)
            if (Mathf.Abs(jolt) < bumpDead) jolt = 0f;
            else jolt -= Mathf.Sign(jolt) * bumpDead;

            float rawBump = Mathf.Clamp(jolt * bumpGain, -bumpClamp, bumpClamp);

            // fast filter
            _bumpFiltered = Mathf.Lerp(_bumpFiltered, rawBump, bumpSmoothing);

            bumpTorque = _bumpFiltered;

            // Optional: make bumps stronger when steering is turned
            // bumpTorque *= (0.5f + 0.5f * Mathf.Abs(wheelNorm));

            // ===== 12) Combine torque =====
            float totalTorque = damperTorque + frictionTorque + tireTorque + bumpTorque;

            // ===== 13) Filter torque =====
            filteredTorque = Mathf.Lerp(filteredTorque, totalTorque, torqueSmoothing);

            // ===== 14) Map to Logitech force units =====
            float targetForce = filteredTorque * torqueScale;
            targetForce = SoftLimit(targetForce, softLimitK);
            targetForce = Mathf.Clamp(targetForce, -maxForce, maxForce);

            // ===== 15) Airborne hard release (front only) =====
            if (!frontLoaded)
            {
                targetForce = 0f;
                filteredTorque = Mathf.Lerp(filteredTorque, 0f, airFilterReset);
                lastSentForce = Mathf.MoveTowards(lastSentForce, 0f, airHardReleasePerSec * dt);
            }

            // ===== 16) Rate limit (release faster than apply) =====
            float maxUpStep = rateUpPerSec * dt;
            float maxDownStep = rateDownPerSec * dt;

            float step = (Mathf.Abs(targetForce) < Mathf.Abs(lastSentForce)) ? maxDownStep : maxUpStep;
            float limitedForce = Mathf.MoveTowards(lastSentForce, targetForce, step);

            lastSentForce = limitedForce;

            limitedForce = ApplyLut(limitedForce, maxForce);

            int force = Mathf.RoundToInt(Mathf.Clamp(limitedForce, -maxForce, maxForce));
            if (forceInvert) force = -force;

            if (debugIsEnabled)
            {
                ShowInDebugWindow("Speed(m/s)", speed);
                ShowInDebugWindow("RearSlip01", rearSlip01);

                ShowInDebugWindow("TireTorque", tireTorque);
                ShowInDebugWindow("DamperTorque", damperTorque);
                ShowInDebugWindow("TotalTorque", totalTorque);
                ShowInDebugWindow("LimitedForce", limitedForce);
                ShowInDebugWindow("FFB Force", force);
            }

            return force;
        }
        private void ShowInDebugWindow(string Name, float value)
        {
            if (_DEBUGVALS.ContainsKey(Name))
            {
                _DEBUGVALS[Name] = value;
            }
            else
            {
                _DEBUGVALS.Add(Name, value);
            }
        }
        private void Update()
        {
            if (UseDirectInputFFB) return;

            if (keybind.GetKeybindDown())
            {
                SetForcesToZero();
            }
            if (!debugIsEnabled) return;

            if (LogitechGSDK.LogiUpdate() && LogitechGSDK.LogiIsConnected(_CONTROLLERINDEX))
            {
                StringBuilder stringBuilder = new StringBuilder(256);
                LogitechGSDK.LogiGetFriendlyProductName(_CONTROLLERINDEX, stringBuilder, 256);
                propertiesEdit = string.Concat("Current Controller : ", stringBuilder, "\n");
                propertiesEdit += "Current controller properties : \n\n";
                activeForces = "Active values:\n";
                activeForces += $"Force function finish: {forceFuncFinish}\n";
                string text = propertiesEdit;
                logiPropertiesOK = LoadProfilerWheelProperties(_CONTROLLERINDEX, ref logiControllerPropertiesData);
                if (logiPropertiesOK)
                {
                    propertiesEdit = text + "forceEnable = " + logiControllerPropertiesData.forceEnable + "\n";
                    text = propertiesEdit;
                    propertiesEdit = text + "overallGain = " + logiControllerPropertiesData.overallGain + "\n";
                    text = propertiesEdit;
                    propertiesEdit = text + "springGain = " + logiControllerPropertiesData.springGain + "\n";
                    text = propertiesEdit;
                    propertiesEdit = text + "damperGain = " + logiControllerPropertiesData.damperGain + "\n";
                    text = propertiesEdit;
                    propertiesEdit = text + "defaultSpringEnabled = " + logiControllerPropertiesData.defaultSpringEnabled + "\n";
                    text = propertiesEdit;
                    propertiesEdit = text + "combinePedals = " + logiControllerPropertiesData.combinePedals + "\n";
                    text = propertiesEdit;
                    propertiesEdit = text + "wheelRange = " + logiControllerPropertiesData.wheelRange + "\n";
                    text = propertiesEdit;
                    propertiesEdit = text + "gameSettingsEnabled = " + logiControllerPropertiesData.gameSettingsEnabled + "\n";
                    text = propertiesEdit;
                    propertiesEdit = text + "allowControllerProperties = " + logiControllerPropertiesData.allowControllerProperties + "\n";
                    text = propertiesEdit;
                    propertiesEdit = text + "Profiler Operating Range = " + profilerOperatinRange + "\n";


                }
                actualState = "Steering wheel current state : \n\n";
                LogitechGSDK.DIJOYSTATE2ENGINES dIJOYSTATE2ENGINES = LogitechGSDK.LogiGetStateCSharp(_CONTROLLERINDEX);
                text = actualState;
                actualState = text + "Wheel position:" + dIJOYSTATE2ENGINES.lX + "\n";
                text = actualState;
                actualState = text + "Throttle:" + dIJOYSTATE2ENGINES.lY + "\n";
                text = actualState;
                actualState = text + "Brake:" + dIJOYSTATE2ENGINES.lRz + "\n";
                text = actualState;
                actualState = text + "Clutch:" + dIJOYSTATE2ENGINES.rglSlider[0] + "\n";
                text = actualState;
                actualState = text + "z-axis position :" + dIJOYSTATE2ENGINES.lZ + "\n";
                text = actualState;
                actualState = text + "x-axis rotation :" + dIJOYSTATE2ENGINES.lRx + "\n";
                text = actualState;
                actualState = text + "y-axis rotation :" + dIJOYSTATE2ENGINES.lRy + "\n";
                text = actualState;
                actualState = text + "extra axes positions 2 :" + dIJOYSTATE2ENGINES.rglSlider[1] + "\n";
                text = actualState;

                activeForces += $"Controller Index = {_CONTROLLERINDEX}\n";
                activeForces += $"Vanilia force = {currentForce.ToString("0.00")}\n";

                activeForces += $"Car: {currentVeh.Value}\n";
                activeForces += $"Car speed: {currentSpeed.ToString("0.00")}\n";
                activeForces += $"RPM = {currentRPM.ToString("0.00")}\n";
                activeForces += $"RPM Led Start = {rpm_FIRST_LED.ToString("0.00")}\n";
                activeForces += $"RPM Led Max = {shiftPoint.ToString("0.00")}\n";
                activeForces += $"DEBUG VARS:\n";

                foreach (var varible in _DEBUGVALS)
                {
                    activeForces += $"{varible.Key}:{varible.Value.ToString("0.000")}\n";
                }



            }
            else if (!LogitechGSDK.LogiIsConnected(_CONTROLLERINDEX))
            {
                actualState = "PLEASE PLUG IN A STEERING WHEEL OR A FORCE FEEDBACK CONTROLLER";
                activeForces = $"Controller Index = {_CONTROLLERINDEX}\n";
                propertiesEdit = "Controller not connected with this index";
            }
            else
            {
                var message = "THIS WINDOW NEEDS TO BE IN FOREGROUND IN ORDER FOR THE SDK TO WORK PROPERLY";
                actualState = message;
                actualState = message;
                activeForces = message;
                propertiesEdit = message;
            }
        }
        bool logiPropertiesOK = false;
        private bool LoadProfilerWheelProperties(int controllerIndex, ref RPMLeds.LogiControllerPropertiesData outProps)
        {
            if (!LogitechGSDK.LogiUpdate())
                return false;

            if (!LogitechGSDK.LogiIsConnected(controllerIndex))
                return false;

            outProps = new RPMLeds.LogiControllerPropertiesData();
            return RPMLeds.LogiGetCurrentControllerProperties(
                controllerIndex,
                ref outProps);
        }
        private void SetOldWheelProperties()
        {
            profilerOperatinRange = _profilerWheelMaxRange.GetValue();
            logiControllerPropertiesData.wheelRange = profilerOperatinRange;
            logiControllerPropertiesData.forceEnable = _profilerForceEnabled.GetValue() ? 1 : 0;
            logiControllerPropertiesData.overallGain = _profilerOverallGain.GetValue();
            logiControllerPropertiesData.springGain = _profilerSpringllGain.GetValue();
            logiControllerPropertiesData.damperGain = _profilerDamperGain.GetValue();
            logiControllerPropertiesData.combinePedals = _profilerCombinedPedals.GetValue() ? 1 : 0; ;
            logiControllerPropertiesData.defaultSpringEnabled = _profilerDefaultSpringEnabled.GetValue() ? 1 : 0; ;
            logiControllerPropertiesData.defaultSpringGain = _profilerDefaultSpringGain.GetValue();
        }

        [DllImport("LogitechSteeringWheel", CallingConvention = CallingConvention.Cdecl)]
        private static extern bool LogiSetPreferredControllerProperties(int controllerIndex, ref RPMLeds.LogiControllerPropertiesData properties);
        [DllImport("LogitechSteeringWheel", CallingConvention = CallingConvention.Cdecl, CharSet = CharSet.Unicode)]
        public static extern bool LogiGetCurrentControllerProperties(int index, ref LogiControllerPropertiesData properties);
        private bool ApplyProfilerWheelProperties(int controllerIndex, ref RPMLeds.LogiControllerPropertiesData props)
        {
            return RPMLeds.LogiSetPreferredControllerProperties(controllerIndex, ref props);
        }
        private void applyProfiler()
        {
            LogitechGSDK.LogiGetOperatingRange(_CONTROLLERINDEX, ref profilerOperatinRange); // e.g., 900

            profilerOperatinRange = _profilerWheelMaxRange.GetValue();
            // Update wheel properties
            SetOldWheelProperties(); // sets wheelRange = profilerOperatinRange

            // Apply to the wheel
            ApplyProfilerWheelProperties(_CONTROLLERINDEX, ref logiControllerPropertiesData);

            // Optional: enforce driver-level operating range
            LogitechGSDK.LogiSetOperatingRange(_CONTROLLERINDEX, profilerOperatinRange);
        }
        private float ApplyLut(float limitedForce, float maxForce)
        {
            if (!LUTEnabled || !_lut.IsValid) return limitedForce;
            return _lut.ApplyToForce(limitedForce, maxForce);
        }

    }
}

