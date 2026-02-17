using Harmony;
using HutongGames.PlayMaker;
using MSCLoader;
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
        public static int slipForce = 0;
        public class PartInfo
        {
            public string Path;
            public string Name;
            public FsmFloat PartValue;
            public FsmBool Installed;
        }

        #region SettingsVars
        SettingsSliderInt _csmtlp;
        SettingsCheckBox _csEnabled;
        SettingsCheckBox _csInvert;

        SettingsCheckBox _showDebugMSG;
        SettingsCheckBox _enableAdvancedFFB;
        SettingsDropDownList _maxRPMSource;
        SettingsSliderInt _manualMaxRPM;
        SettingsSlider _startPointPercent;
        SettingsSlider _maxPointPercent;
        SettingsSlider _damperTopSpeed;
        SettingsSlider _damperForceAtLowSpeed;
        SettingsSlider _damperForceAtHighSpeed;
        SettingsSlider _damperForceMultiplyAtMaxWheelAngle;
        SettingsSliderInt _springForce;
        SettingsCheckBox _colisionsEnabled;
        SettingsSliderInt _colisionForce;
        SettingsSlider _colisionForceMltpy;
        SettingsSliderInt _controllerIndex;

        SettingsSliderInt _disableVanilaForceAtSpeed;
        SettingsCheckBox _disableVanilaForceAtSpeedEnabled;
        SettingsCheckBox _disableVanillaForce;

        SettingsCheckBox _modEnabled;
        SettingsHeader _headerFFBA;
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
        SettingsButton _applyProffilerSettings;
        SettingsSliderInt testspringOffset;
        SettingsSliderInt testspringSaturation;
        SettingsSliderInt testSpringCofi;
        #endregion SettingsVars

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
        float damper = 0f;
        bool ledsEnabled = true;
        bool debugIsEnabled = false;
        static bool advancedFFBOn = true;
        float shiftPoint = 0;
        bool forceFuncFinish = false;
        private string propertiesEdit;
        private string actualState;
        private string activeForces;
        public static int _CONTROLLERINDEX = 0;
        LogitechGSDK.LogiControllerPropertiesData logiControllerPropertiesData = new LogitechGSDK.LogiControllerPropertiesData();
        int springForce = 0;
        float damperLow = 0;
        float damperHigh = 0;
        float damperMultyplyMaxAngle = 0;
        float maxanglerot = 0;
        float rpm_MAX = RPM_MAX_DEFAULT;
        float rpm_FIRST_LED = RPM_FIRST_DEFAULT;
        float startPercent = 0;
        float maxPercent = 0;
        float currentRPM = 0;
        float currentForce = 0;
        float currentSpeed = 0;
        float manualRPMMax = RPM_MAX_DEFAULT;
        float damperTopSpeed = 0;
        int settingsRPMSource = 0;
        float targetForce = 0;
        bool vaniliaForceApplied = false;
        bool vanillaForceDisable = false;
        bool vanillaForceDisableAtSpeed = false;
        int speedVanilaForceDisable = 0;
        static float colisionForceMultiply = 1;
        static bool inCar = false;
        public static int collisionForceSetted = 0;
        public bool countersteeringEnabled = false;
        int countersteeringmtlpy = 1;
        bool inverCS = false;

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

        #region Colider
        public class FFBColision : MonoBehaviour
        {
            private float collisionForce = 0;
            private bool playEffect = false;
            void OnCollisionEnter(Collision collision)
            {
                if(ffbColisionsEnabled && advancedFFBOn && inCar)
                {
                    if (collision.gameObject.layer == 0 /*Default*/ || collision.gameObject.layer == 18 /*Cars?*/)
                    {

                        collisionForce = collision.relativeVelocity.magnitude;
                        playEffect = true;
                    }
                }
            }
            void FixedUpdate()
            {
                if (!inCar)
                {
                    playEffect = false;
                    return;
                }              

                if (!playEffect)
                    return;
    
                PlayLogitechEffect(collisionForce);
                playEffect = false;
            }
            void PlayLogitechEffect(float force)
            {
                var clampedForce = Mathf.Clamp(force * colisionForceMultiply, 0, colisionForceMax);
                int strength = Mathf.RoundToInt(clampedForce);
                collisionForceSetted = strength;
                LogitechGSDK.LogiPlayFrontalCollisionForce(_CONTROLLERINDEX, strength);
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
        private void OnGui()
        {
            if (!debugIsEnabled) return;
            propertiesEdit = GUI.TextArea(new Rect(200f, 10f, 200f, 350f), propertiesEdit, 400);
            actualState = GUI.TextArea(new Rect(410f, 10f, 300f, 350f), actualState, 1000);
            activeForces = GUI.TextArea(new Rect(10f, 10f, 180f, 350f), activeForces, 400);
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
            _headerFFBA = Settings.AddHeader("Damper settings",visibleByDefault:false);
            _damperTopSpeed = Settings.AddSlider("Damper TopSpeed", "Top speed (At Speed Minmal Damper Force Reach)", 1F, 250, 80, SettingChanged, visibleByDefault: false);
            _damperForceAtLowSpeed = Settings.AddSlider("DamperFLS", "Force at low speed", 1F, 100, 45, SettingChanged, visibleByDefault: false);
            _damperForceAtHighSpeed = Settings.AddSlider("DamperFHS", "Force at high speed", 1F, 100, 30, SettingChanged, visibleByDefault: false);
            _damperForceMultiplyAtMaxWheelAngle = Settings.AddSlider("DamperAngle", "Damper Force Multiply At Max Wheel Angle", 1F, 5, 1.4f, SettingChanged, visibleByDefault: false);
            _springForce = Settings.AddSlider("SpringForce", "Spring Force", 1, 100, 80, SettingChanged, visibleByDefault: false);
            _colisionsEnabled = Settings.AddCheckBox("_colisionsEnabled", "Colision Force", true, SettingChanged);
            _colisionForce = Settings.AddSlider("_colisionForce", "Max. Collision Force", 1, 100, 55, SettingChanged, visibleByDefault: false);
            _colisionForceMltpy = Settings.AddSlider("_colisionForceMltpy", "Collision Force Multiply", 1f, 60f, 15f, SettingChanged, visibleByDefault: false);

            _csEnabled = Settings.AddCheckBox("_csEnabled", "Enable Countersteering Force", false, SettingChanged, visibleByDefault: false);
            _csmtlp = Settings.AddSlider("_csmtlp", "Countersteering Force Multiply", 1, 200, 50, SettingChanged, visibleByDefault: false);
            _csInvert = Settings.AddCheckBox("_csInvert", "Invert Force", false, SettingChanged, visibleByDefault: false);

            _disableVanillaForce = Settings.AddCheckBox("_disableVanillaForce", "Disable Vanilla Force", false, SettingChanged);
            Settings.AddText("Prevent wheel wooble at speed but disable vanila forces");
            _disableVanilaForceAtSpeedEnabled = Settings.AddCheckBox("_disableVanilaForceAtSpeedEnabled", "Disable Vanilla Force At Speed", false, SettingChanged);
            _disableVanilaForceAtSpeed = Settings.AddSlider("_disableVanilaForceAtSpeed", "Vanilla Force Speed Disable", 1, 255, 20, SettingChanged, visibleByDefault: false);


            Settings.AddHeader("Properties for Profiler (LGS)");
            Settings.AddText("*BETA MAY CAUSE CRASH* Use if your wheel is set up via Profiler (Logitech Gaming Software) *BETA* Need Testers for LGS");
            _profilerEnabled = Settings.AddCheckBox("_profilerEnabled", "Settings Enabled", false, SettingChanged,visibleByDefault:true);
            _profilerWheelMaxRange = Settings.AddSlider("_profilerWheelMaxRange", "Default Spring Gain", 90, 900, 900, SettingChanged, visibleByDefault: false);
            _profilerForceEnabled = Settings.AddCheckBox("_profilerForceEnabled", "Force Feedback Enabled", true, SettingChanged, visibleByDefault: false);
            _profilerOverallGain = Settings.AddSlider("_profilerOverallGain", "Overall Gain", 1, 100, 80, SettingChanged, visibleByDefault: false);
            _profilerSpringllGain = Settings.AddSlider("_profilerSpringllGain", "Spring Gain", 1, 100, 80, SettingChanged, visibleByDefault: false);
            _profilerDamperGain = Settings.AddSlider("_profilerDamperGain", "Damper Gain", 1, 100, 80, SettingChanged, visibleByDefault: false);
            _profilerAllowGameSettings = Settings.AddCheckBox("_profilerAllowGameSettings", "Allow Game Settings", true, SettingChanged, visibleByDefault: false);
            _profilerCombinedPedals = Settings.AddCheckBox("_profilerCombinedPedals", "Combined Pedals", true, SettingChanged, visibleByDefault: false);
            _profilerDefaultSpringEnabled = Settings.AddCheckBox("_profilerDefaultSpringEnabled", "Default Spring Enabled", true, SettingChanged, visibleByDefault: false);
            _profilerDefaultSpringGain = Settings.AddSlider("_profilerDefaultSpringGain", "Default Spring Gain", 1, 100, 80, SettingChanged, visibleByDefault: false);
            _applyProffilerSettings = Settings.AddButton("Save profiler settings",applyProfiler,visibleByDefault:false);
            Settings.AddHeader("Debug and Controller");
            _showDebugMSG = Settings.AddCheckBox("_showDebugMSG", "Show debug window", false, SettingChanged);
            Settings.AddText("If the controller shown in the debug window is incorrect, try changing the controller index used for detection. After adjusting the index, restart the game and check again.");
            _controllerIndex = Settings.AddSlider("_controllerIndex", "Controller index", 0, 10, 0, SettingChanged, visibleByDefault: true);
            _modEnabled = Settings.AddCheckBox("_modEnabled", "Patch Vanilla FFB (Restart req)", true, SettingChanged);
        }
        private void SettingChanged()
        {

            vanillaForceDisable = _disableVanillaForce.GetValue();
            vanillaForceDisableAtSpeed = _disableVanilaForceAtSpeedEnabled.GetValue();
            speedVanilaForceDisable = _disableVanilaForceAtSpeed.GetValue();

            _CONTROLLERINDEX = _controllerIndex.GetValue();
            ledsEnabled = _rpmLedsEnabled.GetValue();
            debugIsEnabled = _showDebugMSG.GetValue();
            advancedFFBOn = _enableAdvancedFFB.GetValue();
            if (_maxRPMSource.GetSelectedItemName() == "Manual")
            {
                _manualMaxRPM.SetVisibility(true);
            }
            else
            {
                _manualMaxRPM.SetVisibility(false);
            }
            springForce = _springForce.GetValue();
            damperLow = _damperForceAtLowSpeed.GetValue();
            damperHigh = _damperForceAtHighSpeed.GetValue();
            damperMultyplyMaxAngle = _damperForceMultiplyAtMaxWheelAngle.GetValue();
            startPercent = _startPointPercent.GetValue();
            maxPercent = _maxPointPercent.GetValue();
            manualRPMMax = _manualMaxRPM.GetValue();
            damperTopSpeed = _damperTopSpeed.GetValue();
            settingsRPMSource = _maxRPMSource.GetSelectedItemIndex();
            TogleAdvancedFFB();

            countersteeringEnabled = _csEnabled.GetValue();
            _csEnabled.SetVisibility(_enableAdvancedFFB.GetValue());
            _csInvert.SetVisibility(countersteeringEnabled);
            _csmtlp.SetVisibility(countersteeringEnabled);

            countersteeringmtlpy = _csmtlp.GetValue();
            inverCS = _csInvert.GetValue();

            if (_colisionsEnabled.GetValue())
            {
                ffbColisionsEnabled = true;
                _colisionForce.SetVisibility(true);
                _colisionForceMltpy.SetVisibility(true);
                colisionForceMax = _colisionForce.GetValue();
                colisionForceMultiply = _colisionForceMltpy.GetValue();
            }
            else
            {
                ffbColisionsEnabled = false;
                _colisionForce.SetVisibility(true);
                _colisionForceMltpy.SetVisibility(false);

            }

            if (_profilerEnabled.GetValue())
            {
                _profilerWheelMaxRange.SetVisibility(true);
                _profilerForceEnabled.SetVisibility(true);
                _profilerOverallGain.SetVisibility(true);
                _profilerSpringllGain.SetVisibility(true);
                _profilerDamperGain.SetVisibility(true);
                _profilerAllowGameSettings.SetVisibility(true);
                _profilerCombinedPedals.SetVisibility(true);
                _profilerDefaultSpringEnabled.SetVisibility(true);
                _profilerDefaultSpringGain.SetVisibility(true);
                _applyProffilerSettings.SetVisibility(true);
            }
            else
            {
                _profilerWheelMaxRange.SetVisibility(false);
                _profilerForceEnabled.SetVisibility(false);
                _profilerOverallGain.SetVisibility(false);
                _profilerSpringllGain.SetVisibility(false);
                _profilerDamperGain.SetVisibility(false);
                _profilerAllowGameSettings.SetVisibility(false);
                _profilerCombinedPedals.SetVisibility(false);
                _profilerDefaultSpringEnabled.SetVisibility(false);
                _profilerDefaultSpringGain.SetVisibility(false);
                _applyProffilerSettings.SetVisibility(false);
            }

            if (!debugIsEnabled)
            {
                propertiesEdit = string.Empty;
                actualState = string.Empty;
            }
            if (_disableVanilaForceAtSpeedEnabled.GetValue())
            {
                _disableVanilaForceAtSpeed.SetVisibility(true);
            }
            else
            {
                _disableVanilaForceAtSpeed.SetVisibility(false);
            }

            if(logiInit)
            {
                SetOldWheelProperties();
            }
        }
        private void TogleAdvancedFFB()
        {
            if (_enableAdvancedFFB.GetValue())
            {
                _headerFFBA.SetVisibility(true);
                _damperTopSpeed.SetVisibility(true);
                _damperForceAtLowSpeed.SetVisibility(true);
                _damperForceAtHighSpeed.SetVisibility(true);
                _damperForceMultiplyAtMaxWheelAngle.SetVisibility(true);
                _springForce.SetVisibility(true);
            }
            else
            {
                _headerFFBA.SetVisibility(false);
                _damperTopSpeed.SetVisibility(false);
                _damperForceAtLowSpeed.SetVisibility(false);
                _damperForceAtHighSpeed.SetVisibility(false);
                _damperForceMultiplyAtMaxWheelAngle.SetVisibility(false);
                _springForce.SetVisibility(false);
            }

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
            public Drivetrain DTComp;
            public GameObject CarObject;
            public Rigidbody CarRigidbody;
            public bool IsCorris;
            public static RegularCarInfo initCar(string carName)
            {
                var car = GameObject.Find(carName);
                car.AddComponent<FFBColision>();
                var ffbComp = car.GetComponent<ForceFeedback>();
                var drivetrainComp = car.GetComponent<Drivetrain>();
                return new RegularCarInfo
                {
                    FFBComp = ffbComp,
                    DTComp = drivetrainComp,
                    CarObject = car,
                    CarRigidbody = car.GetComponent<Rigidbody>(),
                    IsCorris = carName == "CORRIS"
                };
            }
        }
        bool setSteerAngle = false;
        float smoothForce = 0f;
        int offsetPercentage = 0;
        int springCenter = 0;
        GameObject corrisGameObject;
        Rigidbody corrisRB;
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

            TogleAdvancedFFB();
            SettingChanged();
            harmony = HarmonyInstance.Create("izuko.rpmledffb");
            harmony.PatchAll();
            ModConsole.Print("RPMLed - Harmony FFB patches applied. Default FFB Disabled");

        }
        private float lateralVelocityFiltered = 0f;
        private float yawRateFiltered = 0f;
        private bool logiInit = false;
        private void Mod_OnMenuLoad()
        {
            TogleAdvancedFFB();
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
        public bool forcesIsZero = true;
        private void SetForcesToZero()
        {
            if (!LogitechGSDK.LogiIsConnected(_CONTROLLERINDEX)) return;
            LogitechGSDK.LogiStopSoftstopForce(_CONTROLLERINDEX);
            LogitechGSDK.LogiStopConstantForce(_CONTROLLERINDEX);
            LogitechGSDK.LogiStopDamperForce(_CONTROLLERINDEX);
            LogitechGSDK.LogiStopSpringForce(_CONTROLLERINDEX);
            forcesIsZero = true;
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

            

            bool isCorris = CURRENTCAR.IsCorris;

            currentRPM = CURRENTCAR.DTComp.rpm;
            currentSpeed = Mathf.Abs(CURRENTCAR.DTComp.differentialSpeed);
            maxanglerot = maxSteeringAngle.Value;

            if (currentRPM > 50 && ledsEnabled)
            {
                rpm_MAX = RPM_MAX_DEFAULT;
                rpm_FIRST_LED = RPM_FIRST_DEFAULT;

                if (isCorris)
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

            targetForce = currentForce / 100f;

            if (advancedFFBOn)
            {
                var state = LogitechGSDK.LogiGetStateCSharp(_CONTROLLERINDEX);
                float currentAngle = (state.lX / 32768f) * maxanglerot;
                float angleFactor = Mathf.Clamp01(Mathf.Abs(currentAngle) / maxanglerot);

                float speed01 = Mathf.Clamp01(currentSpeed / damperTopSpeed); 

                float targetDamper = Mathf.Lerp(damperLow, damperHigh, speed01);
                targetDamper *= Mathf.Lerp(1f, damperMultyplyMaxAngle, angleFactor); 
                damper = Mathf.Lerp(damper, targetDamper, Time.fixedDeltaTime * 3f);

                if (currentSpeed > 0.05f)
                {

                    if(countersteeringEnabled)
                    {
                        Vector3 localVel = CURRENTCAR.CarObject.transform.InverseTransformDirection(CURRENTCAR.CarRigidbody.velocity);

                        float forwardVelocity = localVel.z;   // +forward / -reverse (m/s)
                        float lateralVelocity = localVel.x;   // +right / -left (m/s)
                        float verticalVelocity = localVel.y;  // bumps / jumps

                        Vector3 localAngVel = CURRENTCAR.CarObject.transform.InverseTransformDirection(CURRENTCAR.CarRigidbody.angularVelocity);

                        float yawRate = localAngVel.y;

                        Vector3 forwardDir = CURRENTCAR.CarObject.transform.forward;
                        Vector3 velocityDir = CURRENTCAR.CarRigidbody.velocity.normalized;
                        float speed = CURRENTCAR.CarRigidbody.velocity.magnitude;        // m/s
                        float speedKmh = speed * 3.6f;

                        float slipAngle = Mathf.Atan2(lateralVelocity, Mathf.Abs(forwardVelocity));


                        float rearAxleDistance = 1.410f; // meters (tune per car)
                        float rearSlipVelocity =
                            lateralVelocity + yawRate * rearAxleDistance;


                        bool rearSliding = Mathf.Abs(rearSlipVelocity) > 0.02f && Mathf.Abs(yawRate) > 0.05f && speed > 0.5f;

                        float counterSteerDir = -Mathf.Sign(rearSlipVelocity);

                        lateralVelocityFiltered += (lateralVelocity - lateralVelocityFiltered) * 0.1f;
                        yawRateFiltered += (yawRate - yawRateFiltered) * 0.1f;

                        float rearSlip = lateralVelocityFiltered + yawRateFiltered * rearAxleDistance;

                        float slipStrength = Mathf.Clamp(Mathf.Abs(rearSlip) * 0.8f, 0f, 1f);

                        slipStrength = Mathf.Pow(slipStrength, 0.7f);

                        if (rearSliding)
                        {
                            offsetPercentage = Mathf.RoundToInt(
                                counterSteerDir * slipStrength * countersteeringmtlpy
                            );
                        }
                        else
                            offsetPercentage = 0;

                        offsetPercentage = Mathf.Clamp(offsetPercentage, -95, 95);
                    }
                    LogitechGSDK.LogiStopDamperForce(_CONTROLLERINDEX);
                    LogitechGSDK.LogiPlaySoftstopForce(_CONTROLLERINDEX, 90);
                    LogitechGSDK.LogiPlaySpringForce(_CONTROLLERINDEX, offsetPercentage, springForce, springCenter);
                }
                else
                {

                    LogitechGSDK.LogiStopSpringForce(_CONTROLLERINDEX);
                    LogitechGSDK.LogiPlayDamperForce(_CONTROLLERINDEX, (int)damper);
                }
            }

            if(vanillaForceDisable || (vanillaForceDisableAtSpeed && currentSpeed > speedVanilaForceDisable))
            {
                LogitechGSDK.LogiStopConstantForce(_CONTROLLERINDEX);
                vaniliaForceApplied = false;
            }
            else
            {
                const float SMOOTHING = 0.1f;
                smoothForce += (targetForce - smoothForce) * SMOOTHING;
                LogitechGSDK.LogiPlayConstantForce(_CONTROLLERINDEX, (int)smoothForce);
                vaniliaForceApplied = true;
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
                activeForces += $"Has Logi Prop: {logiPropertiesOK}\n";
                activeForces += $"Vanilia force = {currentForce.ToString("0.00")}\n";
                activeForces += $"Logi force = {targetForce.ToString("0.00")}\n";
                activeForces += $"Colision force = {collisionForceSetted.ToString("0.00")}\n"; 
                activeForces += $"Smooth Logi force = {smoothForce.ToString("0.00")}\n";
                activeForces += $"Car: {currentVeh.Value}\n";
                activeForces += $"Car speed: {currentSpeed.ToString("0.00")}\n";
                activeForces += $"RPM = {currentRPM.ToString("0.00")}\n";
                activeForces += $"RPM Led Start = {rpm_FIRST_LED.ToString("0.00")}\n";
                activeForces += $"RPM Led Max = {shiftPoint.ToString("0.00")}\n";
                activeForces += $"Spring force = {springForce}\n";
                activeForces += $"Damper force = {damper}\n";
                activeForces += $"Vanilia Force Aplied:{vaniliaForceApplied}\n";
                activeForces += $"Forces is zero: {forcesIsZero}\n";
                //activeForces += $"Slip force = {slipForce}\n";
                activeForces += $"Wheel slip offset = {offsetPercentage}\n";
                
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

