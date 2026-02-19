using Harmony;
using HutongGames.PlayMaker;
using MSCLoader;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Text;
using UnityEngine;
using static LogitechGSDK;


namespace RPMLeds
{
    public class RPMLeds : Mod
    {
        public override string ID => "RPMLeds"; // Your (unique) mod ID 
        public override string Name => "RPM Leds And Advanced FFB"; // Your mod name
        public override string Author => "Izuko"; // Name of the Author (your name)
        public override string Version => "1.5.1"; // Version
        public override string Description => "Logitech SDK FFB Advanced And RPM Leds for Logitech G923/G29"; // Short description of your mod 
        public override Game SupportedGames => Game.MyWinterCar;
        public static bool Patch = true;
        private HarmonyInstance harmony;
        public class PartInfo
        {
            public string Path;
            public string Name;
            public FsmFloat PartValue;
            public FsmBool Installed;
        }

        #region SettingsVars

        SettingsSlider _ffbSpringMoveBoost;
        SettingsSlider _ffbSpringBoostSpeed;
        SettingsSlider _ffbSpringLockBoost;
        SettingsSlider _ffbSpringLockStart;

        SettingsCheckBox _showDebugMSG;
        SettingsCheckBox _enableAdvancedFFB;
        SettingsDropDownList _maxRPMSource;
        SettingsSliderInt _manualMaxRPM;
        SettingsSlider _startPointPercent;
        SettingsSlider _maxPointPercent;
        SettingsCheckBox _colisionsEnabled;
        SettingsSliderInt _colisionForce;
        SettingsSlider _colisionForceMltpy;
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

        #region SettingsVars_FFBAdvanced
        // Advanced ConstantForce FFB settings (your new system)
        SettingsCheckBox _advFFBEnabled;

        SettingsSlider _ffbTorqueScale;


        SettingsSlider _ffbSteeringSpring;
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
        SettingsSlider _ffbRateDownPerSec;

        SettingsSlider _ffbMinNormalForceForFFB;
        SettingsSlider _ffbAirHardReleasePerSec;
        SettingsSlider _ffbAirFilterReset;
        SettingsSlider _ffbLandInTime;

        SettingsCheckBox _ffbInvertForce;
        #endregion

        #region Vars
        FsmFloat maxSteeringAngle;
        PartInfo revLimit;
        PartInfo raceTachot;
        FsmString currentVeh;
        public static Dictionary<string, RegularCarInfo> CARS = new Dictionary<string, RegularCarInfo>();
        int profilerOperatinRange = 0;
        const float RPM_MAX_DEFAULT = 7000f;
        const float RPM_FIRST_DEFAULT = 5000f;
        static int colisionForceMax = 0;
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
        Dictionary<string,float> _DEBUGVALS = new Dictionary<string,float>();
        LogitechGSDK.LogiControllerPropertiesData logiControllerPropertiesData = new LogitechGSDK.LogiControllerPropertiesData();

        float rpm_MAX = RPM_MAX_DEFAULT;
        float rpm_FIRST_LED = RPM_FIRST_DEFAULT;
        float startPercent = 0;
        float maxPercent = 0;
        float currentRPM = 0;
        float currentForce = 0;
        float currentSpeed = 0;
        float manualRPMMax = RPM_MAX_DEFAULT;
        int settingsRPMSource = 0;
        float targetForce = 0;

        static float colisionForceMultiply = 1;
        static bool inCar = false;
        public static int collisionForceSetted = 0;
        public bool countersteeringEnabled = false;


        RegularCarInfo CURRENTCAR;
        #endregion

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
                catch
                {
                    ModConsole.Error("LogiSteeringInitialize function failed");
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
        private void SettingsLoaded()
        {
            SettingChanged();
        }
        private void FFBSettingsChanged()
        {
            bool advOn = _enableAdvancedFFB.GetValue(); // <-- keep only this

            _ffbSpringMoveBoost.SetVisibility(advOn);
            _ffbSpringBoostSpeed.SetVisibility(advOn);
            _ffbSpringLockBoost.SetVisibility(advOn);
            _ffbSpringLockStart.SetVisibility(advOn);

            springMoveBoost = _ffbSpringMoveBoost.GetValue();
            springBoostSpeed = _ffbSpringBoostSpeed.GetValue();
            springLockBoost = _ffbSpringLockBoost.GetValue();
            springLockStart = _ffbSpringLockStart.GetValue();

            _ffbTorqueScale.SetVisibility(advOn);
            _ffbMaxForce.SetVisibility(advOn);
            _ffbInvertForce.SetVisibility(advOn);

            _ffbSteeringSpring.SetVisibility(advOn);
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

            // Apply values into runtime variables
            steeringSpring = _ffbSteeringSpring.GetValue();
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
            if (!debugIsEnabled) return;
            propertiesEdit = GUI.TextArea(new Rect(200f, 10f, 200f, 350f), propertiesEdit, 400);
            actualState = GUI.TextArea(new Rect(410f, 10f, 300f, 350f), actualState, 1000);
            activeForces = GUI.TextArea(new Rect(10f, 10f, 180f, 350f), activeForces, 400);
        }
        private void FFBAdvancedSettings()
        {
            Settings.AddHeader("Advanced FFB Settings");

            _ffbTorqueScale = Settings.AddSlider("_ffbTorqueScale", "Torque Scale", 0.001f, 5f, 0.07f, SettingChanged, visibleByDefault: false);
            _ffbMaxForce = Settings.AddSlider("_ffbMaxForce", "Max Force", 0f, 100f, 50f, SettingChanged, visibleByDefault: false);
            _ffbInvertForce = Settings.AddCheckBox("_ffbInvertForce", "Invert Force", false, SettingChanged, visibleByDefault: false);

            _ffbSteeringSpring = Settings.AddSlider("_ffbSteeringSpring", "Steering Spring", 0f, 100f, 4f, SettingChanged, visibleByDefault: false);
            _ffbSteeringFriction = Settings.AddSlider("_ffbSteeringFriction", "Steering Friction", 0f, 2f, 1.1f, SettingChanged, visibleByDefault: false);
            _ffbFrictionDeadVel = Settings.AddSlider("_ffbFrictionDeadVel", "Friction Dead Vel", 0f, 5f, 0.01f, SettingChanged, visibleByDefault: false);

            _ffbSpringMoveBoost = Settings.AddSlider("_ffbSpringMoveBoost","Spring Boost While Moving",0f, 100f, 2.5f,SettingChanged,visibleByDefault: false);

            _ffbSpringBoostSpeed = Settings.AddSlider("_ffbSpringBoostSpeed","Speed For Full Spring Boost (m/s)",0.1f, 30f, 6f,SettingChanged,visibleByDefault: false);

            _ffbSpringLockBoost = Settings.AddSlider("_ffbSpringLockBoost","Spring Boost Near Full Lock", 0f, 5f, 2.0f, SettingChanged,visibleByDefault: false);

            _ffbSpringLockStart = Settings.AddSlider("_ffbSpringLockStart", "Start Lock Boost At (0-1)", 0.2f, 0.95f, 0.6f,SettingChanged, visibleByDefault: false);

            _ffbDamperStop = Settings.AddSlider("_ffbDamperStop", "Damper Stop", 0f, 2f, 0.8f, SettingChanged, visibleByDefault: false);
            _ffbDamperRoll = Settings.AddSlider("_ffbDamperRoll", "Damper Roll", 0f, 2f, 0.35f, SettingChanged, visibleByDefault: false);
            _ffbDamperFast = Settings.AddSlider("_ffbDamperFast", "Damper Fast", 0f, 2f, 0.85f, SettingChanged, visibleByDefault: false);
            _ffbDamperLowSpeed = Settings.AddSlider("_ffbDamperLowSpeed", "Damper Low Speed", 0f, 50f, 6f, SettingChanged, visibleByDefault: false);
            _ffbDamperHighSpeed = Settings.AddSlider("_ffbDamperHighSpeed", "Damper High Speed", 0f, 50f, 25f, SettingChanged, visibleByDefault: false);

            _ffbMzNormalize = Settings.AddSlider("_ffbMzNormalize", "Mz Normalize", 0f, 10000f, 1000f, SettingChanged, visibleByDefault: false);
            _ffbMzSoftPower = Settings.AddSlider("_ffbMzSoftPower", "Mz Soft Power", 0.1f, 8f, 1.7f, SettingChanged, visibleByDefault: false);

            _ffbTorqueSmoothing = Settings.AddSlider("_ffbTorqueSmoothing", "Torque Smoothing", 0f, 1f, 0.08f, SettingChanged, visibleByDefault: false);
            _ffbSoftLimitK = Settings.AddSlider("_ffbSoftLimitK", "Soft Limit K", 0f, 10f, 0.04f, SettingChanged, visibleByDefault: false);
            _ffbRateUpPerSec = Settings.AddSlider("_ffbRateUpPerSec", "Rate Up / sec", 0f, 2000f, 450f, SettingChanged, visibleByDefault: false);
            _ffbRateDownPerSec = Settings.AddSlider("_ffbRateDownPerSec", "Rate Down / sec", 0f, 2000f, 1200f, SettingChanged, visibleByDefault: false);

            _ffbMinNormalForceForFFB = Settings.AddSlider("_ffbMinNormalForceForFFB", "Min Normal Force", 0f, 10000f, 1000f, SettingChanged, visibleByDefault: false);
            _ffbAirHardReleasePerSec = Settings.AddSlider("_ffbAirHardReleasePerSec", "Air Hard Release / sec", 0f, 10000f, 8000f, SettingChanged, visibleByDefault: false);
            _ffbAirFilterReset = Settings.AddSlider("_ffbAirFilterReset", "Air Filter Reset", 0f, 2f, 0.35f, SettingChanged, visibleByDefault: false);
            _ffbLandInTime = Settings.AddSlider("_ffbLandInTime", "Land In Time", 0f, 2f, 0.35f, SettingChanged, visibleByDefault: false);
        }
        private void Mod_Settings()
        {
            Settings.AddHeader("Enable for cars");
            Settings.AddHeader("RPM Leds");
            _rpmLedsEnabled = Settings.AddCheckBox("_rpmLedsEnabled", "LEDs Enabled", true, SettingChanged);

            string[] sourceDDSettings = new string[] { "Auto", "Race Tachometer", "Rev Limiter", "Manual" };
            _maxRPMSource = Settings.AddDropDownList("Max RPM Source", "Max Corris RPM Source", sourceDDSettings, OnSelectionChanged: SettingChanged);

            _startPointPercent = Settings.AddSlider("Percent Appear", "Start Point", 1f, 100F, 70F, SettingChanged);
            _maxPointPercent = Settings.AddSlider("Max Point", "Max Point Shift", 1f, 100F, 90F, SettingChanged);
            _manualMaxRPM = Settings.AddSlider("Manual MaxRPM", "Manual MaxRPM", 650, 10000, 7000, SettingChanged, visibleByDefault: false);

            Settings.AddHeader("Enable Advanced FFB");
            _enableAdvancedFFB = Settings.AddCheckBox("_AdvancedFFB", "Enable", false, SettingChanged);

            
            FFBAdvancedSettings();

            _colisionsEnabled = Settings.AddCheckBox("_colisionsEnabled", "Colision Force", true, SettingChanged);
            _colisionForce = Settings.AddSlider("_colisionForce", "Max. Collision Force", 1, 100, 55, SettingChanged, visibleByDefault: false);
            _colisionForceMltpy = Settings.AddSlider("_colisionForceMltpy", "Collision Force Multiply", 1f, 60f, 15f, SettingChanged, visibleByDefault: false);

            Settings.AddHeader("Properties for Profiler (LGS)");
            Settings.AddText("*BETA MAY CAUSE CRASH* Use if your wheel is set up via Profiler (Logitech Gaming Software) *BETA* Need Testers for LGS");
            _profilerEnabled = Settings.AddCheckBox("_profilerEnabled", "Settings Enabled", false, SettingChanged, visibleByDefault: true);
            _profilerWheelMaxRange = Settings.AddSlider("_profilerWheelMaxRange", "Default Spring Gain", 90, 900, 900, SettingChanged, visibleByDefault: false);
            _profilerForceEnabled = Settings.AddCheckBox("_profilerForceEnabled", "Force Feedback Enabled", true, SettingChanged, visibleByDefault: false);
            _profilerOverallGain = Settings.AddSlider("_profilerOverallGain", "Overall Gain", 1, 100, 80, SettingChanged, visibleByDefault: false);
            _profilerSpringllGain = Settings.AddSlider("_profilerSpringllGain", "Spring Gain", 1, 100, 80, SettingChanged, visibleByDefault: false);
            _profilerDamperGain = Settings.AddSlider("_profilerDamperGain", "Damper Gain", 1, 100, 80, SettingChanged, visibleByDefault: false);
            _profilerAllowGameSettings = Settings.AddCheckBox("_profilerAllowGameSettings", "Allow Game Settings", true, SettingChanged, visibleByDefault: false);
            _profilerCombinedPedals = Settings.AddCheckBox("_profilerCombinedPedals", "Combined Pedals", true, SettingChanged, visibleByDefault: false);
            _profilerDefaultSpringEnabled = Settings.AddCheckBox("_profilerDefaultSpringEnabled", "Default Spring Enabled", true, SettingChanged, visibleByDefault: false);
            _profilerDefaultSpringGain = Settings.AddSlider("_profilerDefaultSpringGain", "Default Spring Gain", 1, 100, 80, SettingChanged, visibleByDefault: false);
            _applyProffilerSettings = Settings.AddButton("Save profiler settings", applyProfiler, visibleByDefault: false);

            Settings.AddHeader("Debug and Controller");
            _showDebugMSG = Settings.AddCheckBox("_showDebugMSG", "Show debug window", false, SettingChanged);
            Settings.AddText("If the controller shown in the debug window is incorrect, try changing the controller index used for detection. After adjusting the index, restart the game and check again.");
            _controllerIndex = Settings.AddSlider("_controllerIndex", "Controller index", 0, 10, 0, SettingChanged, visibleByDefault: true);

            _modEnabled = Settings.AddCheckBox("_modEnabled", "Patch Vanilla FFB (Restart req)", true, SettingChanged);
        }

        private void SettingChanged()
        {
            FFBSettingsChanged();


            _CONTROLLERINDEX = _controllerIndex.GetValue();
            ledsEnabled = _rpmLedsEnabled.GetValue();
            debugIsEnabled = _showDebugMSG.GetValue();
            advancedFFBOn = _enableAdvancedFFB.GetValue();

            _manualMaxRPM.SetVisibility(_maxRPMSource.GetSelectedItemName() == "Manual");

            startPercent = _startPointPercent.GetValue();
            maxPercent = _maxPointPercent.GetValue();
            manualRPMMax = _manualMaxRPM.GetValue();
            settingsRPMSource = _maxRPMSource.GetSelectedItemIndex();

            ffbColisionsEnabled = _colisionsEnabled.GetValue();
            _colisionForce.SetVisibility(ffbColisionsEnabled);
            _colisionForceMltpy.SetVisibility(ffbColisionsEnabled);
            colisionForceMax = _colisionForce.GetValue();
            colisionForceMultiply = _colisionForceMltpy.GetValue();

            var profilerEnablde = _profilerEnabled.GetValue();
            _profilerWheelMaxRange.SetVisibility(profilerEnablde);
            _profilerForceEnabled.SetVisibility(profilerEnablde);
            _profilerOverallGain.SetVisibility(profilerEnablde);
            _profilerSpringllGain.SetVisibility(profilerEnablde);
            _profilerDamperGain.SetVisibility(profilerEnablde);
            _profilerAllowGameSettings.SetVisibility(profilerEnablde);
            _profilerCombinedPedals.SetVisibility(profilerEnablde);
            _profilerDefaultSpringEnabled.SetVisibility(profilerEnablde);
            _profilerDefaultSpringGain.SetVisibility(profilerEnablde);

            if (!debugIsEnabled)
            {
                propertiesEdit = string.Empty;
                actualState = string.Empty;
            }

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

            if (_maxRPMSource.GetSelectedItemName() == "Manual")
            {
                _manualMaxRPM.SetVisibility(true);
            }

            SettingChanged();
            harmony = HarmonyInstance.Create("izuko.rpmledffb");
            harmony.PatchAll();
            ModConsole.Print("RPMLed - Harmony FFB patches applied. Default FFB Disabled");

        }

        private bool logiInit = false;
        bool softStopEnable = false;
        private void Mod_OnMenuLoad()
        {
            SettingChanged();
            if (LogitechManager.Initialize())
            {
                ModConsole.Print("RPMLed - Logitech initialized successfully");
                logiInit = true;
                if(LoadProfilerWheelProperties(_CONTROLLERINDEX, ref logiControllerPropertiesData))
                {
                    SetOldWheelProperties();
                    ApplyProfilerWheelProperties(_CONTROLLERINDEX, ref logiControllerPropertiesData);
                }
            }
            else
            {
                ModConsole.Error("RPMLed - Logitech init failed");
            }
        }
        private void SetForcesToZero()
        {
            if (!LogitechGSDK.LogiIsConnected(_CONTROLLERINDEX)) return;
            LogitechGSDK.LogiStopSoftstopForce(_CONTROLLERINDEX);
            LogitechGSDK.LogiStopConstantForce(_CONTROLLERINDEX);
            LogitechGSDK.LogiStopDamperForce(_CONTROLLERINDEX);
            LogitechGSDK.LogiStopSpringForce(_CONTROLLERINDEX);
            forcesIsZero = true;
            softStopEnable = false;
        }
        private void PlayRPMLeds()
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

            LogitechGSDK.LogiPlayLeds(_CONTROLLERINDEX, currentRPM, rpm_FIRST_LED, shiftPoint);
        }
        private void Mod_FixedUpdate()
        {
            if (string.IsNullOrEmpty(currentVeh.Value))
            {
                if (!forcesIsZero)
                    SetForcesToZero();
                inCar = false;
                return;
            }

            if (!LogitechGSDK.LogiIsConnected(_CONTROLLERINDEX)) return;

            if (!LogitechGSDK.LogiUpdate()) return;

            if (!setSteerAngle)
            {
                LogitechGSDK.LogiSetOperatingRange(_CONTROLLERINDEX, (int)maxSteeringAngle.Value);
                setSteerAngle = true;
            }

            CURRENTCAR = CARS[currentVeh.Value];

            if (CURRENTCAR == null) {
                ModConsole.Error("Current Car not found it cars list");
                return;
            }
            inCar = true;

            currentRPM = CURRENTCAR.Drivetrain.rpm;
            currentSpeed = Mathf.Abs(CURRENTCAR.Drivetrain.differentialSpeed);

            if (currentRPM > 50 && ledsEnabled)
            {       
                PlayRPMLeds();
            }

            if (advancedFFBOn)
            {
                if (!softStopEnable)
                {
                    LogitechGSDK.LogiPlaySoftstopForce(_CONTROLLERINDEX, 90);
                    LogitechGSDK.LogiPlayDamperForce(_CONTROLLERINDEX, 18); // 5–15 good range
                    softStopEnable = true;
                }
                var state = LogitechGSDK.LogiGetStateCSharp(_CONTROLLERINDEX);
                var force = CalculateForces();
                LogitechGSDK.LogiPlayConstantForce(_CONTROLLERINDEX, force);
            }
        }

        [SerializeField] private float steeringSpring = 4.0f;        // small centering (1–4)
        [SerializeField] private float steeringFriction = 1.1f;      // rack friction (0.4–1.5)
        [SerializeField] private float frictionDeadVel = 0.01f;      // deg/s deadzone (if using wheel deg), tweak as needed

        // Software damper (analog) using wheel angular velocity
        [SerializeField] private float damperStop = 0.80f;           // torque per deg/s when stopped (heavier)
        [SerializeField] private float damperRoll = 0.35f;           // torque per deg/s at low speed (lighter)
        [SerializeField] private float damperFast = 0.85f;           // torque per deg/s at high speed (heavier again)
        [SerializeField] private float damperLowSpeed = 6f;          // m/s where it reaches "roll"
        [SerializeField] private float damperHighSpeed = 25f;        // m/s where it reaches "fast"

        // Aligning torque shaping
        [SerializeField] private float mzNormalize = 1000f;
        [SerializeField] private float mzSoftPower = 1.7f;

        // Output shaping
        [SerializeField] private float torqueScale = 0.07f; //SCALE ALL
        [SerializeField] private float maxForce = 100f;
        [SerializeField] private float torqueSmoothing = 0.08f;
        [SerializeField] private float softLimitK = 0.04f;
        [SerializeField] private float rateUpPerSec = 450f;
        [SerializeField] private float rateDownPerSec = 1200f;
        [SerializeField] private bool forceInvert = true;

        // Air handling (prevents spikes / "stuck at 100")
        [SerializeField] private float minNormalForceForFFB = 1000f;
        [SerializeField] private float airHardReleasePerSec = 8000f;
        [SerializeField] private float airFilterReset = 0.35f;
        [SerializeField] private float landInTime = 0.35f;

        [SerializeField] private float springMoveBoost = 2.5f;     // extra spring when moving
        [SerializeField] private float springBoostSpeed = 6f;      // m/s where boost reaches max
        [SerializeField] private float springLockBoost = 2.0f;     // extra boost near full lock
        [SerializeField] private float springLockStart = 0.6f;

        private float filteredTorque = 0f;
        private float lastSentForce = 0f;
        private float contactBlend = 1f;

        // Wheel angle tracking (software damper uses this)
        private bool wheelAngleInited = false;
        private float lastWheelDeg = 0f;

        private static float SoftLimit(float x, float k) => x / (1f + k * Mathf.Abs(x));
        private static float PowSigned(float x, float p)
        {
            float a = Mathf.Abs(x);
            return Mathf.Sign(x) * Mathf.Pow(a, p);
        }
        private int CalculateForces()
        {
            float dt = Time.fixedDeltaTime;

            // ===== 1) Read physical wheel angle (preferred) =====
            // IMPORTANT: mapping depends on your wrapper.
            // Common Unity wrappers: st.lX is -32768..32767 for wheel axis.
            var st = LogitechGSDK.LogiGetStateCSharp(_CONTROLLERINDEX);
            float wheelDeg = st.lX * (900f / 32767f); // if your wheel is 900°. Adjust if you use 1080/540 etc.

            if (!wheelAngleInited)
            {
                wheelAngleInited = true;
                lastWheelDeg = wheelDeg;
            }

            float wheelVelDegPerSec = (wheelDeg - lastWheelDeg) / dt;
            lastWheelDeg = wheelDeg;



            // ===== 2) Car speed =====
            float speed = CURRENTCAR.Rigidbody.velocity.magnitude;

            // ===== 3) Front wheel contact detection =====
            var fl = CURRENTCAR.Axles.frontAxle.leftWheel;
            var fr = CURRENTCAR.Axles.frontAxle.rightWheel;

            bool flLoaded = fl.onGroundDown && fl.normalForce > minNormalForceForFFB;
            bool frLoaded = fr.onGroundDown && fr.normalForce > minNormalForceForFFB;
            bool frontLoaded = flLoaded && frLoaded;

            // Contact blend for smooth landing
            float targetBlend = frontLoaded ? 1f : 0f;
            float inStep = (landInTime <= 0.0001f) ? 1f : (dt / landInTime);
            contactBlend = Mathf.MoveTowards(contactBlend, targetBlend, (targetBlend > contactBlend) ? inStep : 1f);

            // ===== 4) Spring (use wheel angle, not input) =====
            // Convert wheelDeg -> normalized -1..1 for spring (based on half-range)
            float wheelHalfRangeDeg = 450f; // for 900° wheel. If your wheel range differs, change this.
            float wheelNorm = Mathf.Clamp(wheelDeg / wheelHalfRangeDeg, -1f, 1f);

            float baseSpring = steeringSpring;

            // speed boost: 0..1
            float speed01 = Mathf.Clamp01(speed / springBoostSpeed);

            // lock boost: 0..1 once past springLockStart
            float lock01 = Mathf.InverseLerp(springLockStart, 1f, Mathf.Abs(wheelNorm));

            // effective spring multiplier
            float springMult = 1f + speed01 * springMoveBoost + lock01 * springLockBoost;

            // apply only when front loaded (same as you already do)
            float springTorque = frontLoaded ? (-wheelNorm * baseSpring * springMult) : 0f;
            // ===== 5) Software damper (analog), speed-shaped =====
            float low01 = Mathf.Clamp01(speed / damperLowSpeed);
            float high01 = Mathf.Clamp01(speed / damperHighSpeed);

            float damperGain = Mathf.Lerp(damperStop, damperRoll, low01);
            damperGain = Mathf.Lerp(damperGain, damperFast, high01);

            float damperTorque = -wheelVelDegPerSec * damperGain;

            // ===== 6) Friction (Coulomb) =====
            float frictionTorque = 0f;
            if (Mathf.Abs(wheelVelDegPerSec) > frictionDeadVel)
                frictionTorque = -Mathf.Sign(wheelVelDegPerSec) * steeringFriction;

            // ===== 7) Tire aligning torque (Mz) softened near center =====
            float tireTorque = 0f;
            if (frontLoaded)
            {
                float rawMz = fl.Mz + fr.Mz;

                float normMz = Mathf.Clamp(rawMz / mzNormalize, -1f, 1f);
                float softenedMz = PowSigned(normMz, mzSoftPower) * mzNormalize;

                tireTorque = softenedMz * contactBlend;
            }

            // ===== 8) Combine torque =====
            float totalTorque = springTorque + damperTorque + frictionTorque + tireTorque;

            // ===== 9) Filter torque =====
            filteredTorque = Mathf.Lerp(filteredTorque, totalTorque, torqueSmoothing);

            // ===== 10) Map to Logitech force units =====
            float targetForce = filteredTorque * torqueScale;
            ShowInDebugWindow("Target Force", targetForce);
            targetForce = SoftLimit(targetForce, softLimitK);
            targetForce = Mathf.Clamp(targetForce, -maxForce, maxForce);

            // ===== 11) Airborne hard release =====
            if (!frontLoaded)
            {
                targetForce = 0f;
                filteredTorque = Mathf.Lerp(filteredTorque, 0f, airFilterReset);
                lastSentForce = Mathf.MoveTowards(lastSentForce, 0f, airHardReleasePerSec * dt);
            }

            // ===== 12) Rate limit (release faster than apply) =====
            float maxUpStep = rateUpPerSec * dt;
            float maxDownStep = rateDownPerSec * dt;

            float step = (Mathf.Abs(targetForce) < Mathf.Abs(lastSentForce)) ? maxDownStep : maxUpStep;
            float limitedForce = Mathf.MoveTowards(lastSentForce, targetForce, step);
            lastSentForce = limitedForce;

            int force = Mathf.RoundToInt(Mathf.Clamp(limitedForce, -maxForce, maxForce));
            if (forceInvert) force = -force;

            ShowInDebugWindow("FFB_force", force);
            ShowInDebugWindow("wheelDeg", (int)wheelDeg);
            ShowInDebugWindow("wheelVel", (int)wheelVelDegPerSec);
            ShowInDebugWindow("frontLoaded", frontLoaded ? 1 : 0);

            return force;
        }
        private void ShowInDebugWindow(string Name,float value)
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
                    propertiesEdit = text + "allowGameSettings = " + logiControllerPropertiesData.allowGameSettings + "\n";
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
                
                foreach(var varible in _DEBUGVALS)
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
        private bool LoadProfilerWheelProperties(int controllerIndex, ref LogitechGSDK.LogiControllerPropertiesData outProps)
        {
            if (!LogitechGSDK.LogiUpdate())
                return false;

            if (!LogitechGSDK.LogiIsConnected(controllerIndex))
                return false;

            outProps = new LogitechGSDK.LogiControllerPropertiesData();
            return LogitechGSDK.LogiGetCurrentControllerProperties(
                controllerIndex,
                ref outProps);
        }
        private void SetOldWheelProperties()
        {
            profilerOperatinRange = _profilerWheelMaxRange.GetValue();
            logiControllerPropertiesData.wheelRange = profilerOperatinRange;
            logiControllerPropertiesData.forceEnable = _profilerForceEnabled.GetValue();
            logiControllerPropertiesData.overallGain = _profilerOverallGain.GetValue();
            logiControllerPropertiesData.springGain = _profilerSpringllGain.GetValue();
            logiControllerPropertiesData.damperGain = _profilerDamperGain.GetValue();
            logiControllerPropertiesData.combinePedals = _profilerCombinedPedals.GetValue();
            logiControllerPropertiesData.defaultSpringEnabled = _profilerDefaultSpringEnabled.GetValue();
            logiControllerPropertiesData.defaultSpringGain = _profilerDefaultSpringGain.GetValue();
        }

        [DllImport("LogitechSteeringWheel",CallingConvention = CallingConvention.Cdecl)] 
        private static extern bool LogiSetPreferredControllerPropertiesEx(int controllerIndex,ref LogitechGSDK.LogiControllerPropertiesData properties);
        private bool ApplyProfilerWheelProperties(int controllerIndex, ref LogitechGSDK.LogiControllerPropertiesData props)
        {
            return RPMLeds.LogiSetPreferredControllerPropertiesEx(controllerIndex, ref props);
        }
        private void applyProfiler()
        {
            profilerOperatinRange = _profilerWheelMaxRange.GetValue(); // e.g., 900

            // Update wheel properties
            SetOldWheelProperties(); // sets wheelRange = profilerOperatinRange

            // Disable game override to allow full range
            logiControllerPropertiesData.allowGameSettings = false;

            // Apply to the wheel
            ApplyProfilerWheelProperties(_CONTROLLERINDEX, ref logiControllerPropertiesData);

            // Optional: enforce driver-level operating range
            LogitechGSDK.LogiSetOperatingRange(_CONTROLLERINDEX, profilerOperatinRange);
        }
    }
}

