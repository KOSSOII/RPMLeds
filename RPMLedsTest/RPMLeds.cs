using HutongGames.PlayMaker;
using MSCLoader;
using System.Linq;
using System.Runtime.InteropServices;
using UnityEngine;
using Harmony;


namespace RPMLeds
{
    public class RPMLeds : Mod
    {
        public override string ID => "RPMLeds"; // Your (unique) mod ID 
        public override string Name => "RPM Leds And Advanced FFB"; // Your mod name
        public override string Author => "Izuko"; // Name of the Author (your name)
        public override string Version => "1.2.2"; // Version
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

        SettingsCheckBox _sorbetEnabled;
        SettingsCheckBox _taxiEnabled;
        SettingsCheckBox _kekmetEnabled;
        SettingsCheckBox _gifuEnabled;
        SettingsCheckBox _corrisEnabled;
        SettingsCheckBox _modEnabled;
        SettingsHeader _headerFFBA;
        SettingsCheckBox _rpmLedsEnabled;
        #endregion SettingsVars

        #region Vars
        ForceFeedback FFBComp;

        FsmFloat carRPM;
        FsmFloat maxSteeringAngle;
        FsmFloat carSpeed;
        PartInfo revLimit;
        PartInfo raceTachot;
        FsmString currentVeh;

        RegularCarInfo Kekmet;
        RegularCarInfo Taxi;
        RegularCarInfo Sorbet;
        RegularCarInfo Gifu;

        const float RPM_MAX_DEFAULT = 7000f;
        const float RPM_FIRST_DEFAULT = 5000f;

        float maxChangePerSec = 80f;
        float spring = 0f;
        float damper = 0f;
        bool ledsEnabled = true;
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
                    ModConsole.Print("LogiSteeringInitialize function failed");
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
                Debug.Log("Default FFB Start() Disabled");
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
            SetupFunction(Setup.ModSettings, Mod_Settings);
            SetupFunction(Setup.OnMenuLoad, Mod_OnMenuLoad);
        }
        private void Mod_Settings()
        {
            Settings.AddHeader("Enable for cars");
            _corrisEnabled = Settings.AddCheckBox("_corrisEnabled", "Corris", true);
            _sorbetEnabled = Settings.AddCheckBox("_sorbetEnabled", "Sorbet", true);
            _taxiEnabled = Settings.AddCheckBox("_taxiEnabled", "Taxi", true);
            _gifuEnabled = Settings.AddCheckBox("_gifuEnabled", "Gifu", true);
            _kekmetEnabled = Settings.AddCheckBox("_kekmetEnabled", "Kekmet", false);
            Settings.AddHeader("RPM Leds");
            _rpmLedsEnabled = Settings.AddCheckBox("_rpmLedsEnabled", "LEDs Enabled", true, ChangeLedEnable);
            string[] sourceDDSettings = new string[] { "Auto", "Race Tachometer", "Rev Limiter", "Manual" };
            _maxRPMSource = Settings.AddDropDownList("Max RPM Source", "Max Corris RPM Source", sourceDDSettings, OnSelectionChanged: UpdateSource);
            _startPointPercent = Settings.AddSlider("Percent Appear", "Start Point", 1f, 100F, 70F);
            _maxPointPercent = Settings.AddSlider("Max Point", "Max Point Shift", 1f, 100F, 90F);
            _manualMaxRPM = Settings.AddSlider("Manual MaxRPM", "Manual MaxRPM", 650, 10000, 7000, visibleByDefault: false);
            _showDebugMSG = Settings.AddCheckBox("_showDebugMSG", "Show debug messages", false);
            Settings.AddHeader("Enable Advanced FFB");
            _enableAdvancedFFB = Settings.AddCheckBox("_AdvancedFFB", "Enable", false, onValueChanged:TogleAdvancedFFB);
            _headerFFBA = Settings.AddHeader("Damper settings",visibleByDefault:false);
            _damperTopSpeed = Settings.AddSlider("Damper TopSpeed", "Top speed (At Speed Minmal Damper Force Reach)", 1F, 250, 80, visibleByDefault: false);
            _damperForceAtLowSpeed = Settings.AddSlider("DamperFLS", "Force at low speed", 1F, 100, 45, visibleByDefault: false);
            _damperForceAtHighSpeed = Settings.AddSlider("DamperFHS", "Force at high speed", 1F, 100, 30, visibleByDefault: false);
            _damperForceMultiplyAtMaxWheelAngle = Settings.AddSlider("DamperAngle", "Damper Force Multiply At Max Wheel Angle", 1F, 5, 1.4f, visibleByDefault: false);
            _springForce = Settings.AddSlider("SpringForce", "Spring Force", 1, 100, 80, visibleByDefault: false);
            _modEnabled = Settings.AddCheckBox("_modEnabled", "Patch Vanilla FFB (Restart req)", true);
        }
        private void ChangeLedEnable()
        {
            ledsEnabled = _rpmLedsEnabled.GetValue();
        }
        private void TogleAdvancedFFB()
        {
            if(_enableAdvancedFFB.GetValue())
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
        class RegularCarInfo
        {
            public FsmFloat RPM;
            public FsmFloat Speed;
            public ForceFeedback FFBComp;
            public Drivetrain DTComp;
            public static RegularCarInfo initCar(string carName)
            {
                var car = GameObject.Find(carName);
                var ffbComp = car.GetComponent<ForceFeedback>();
                var drivetrainComp = car.GetComponent<Drivetrain>();
                return new RegularCarInfo
                {
                    FFBComp = ffbComp,
                    DTComp = drivetrainComp
                };
            }
        }
        private void Mod_OnLoad()
        {
            Patch = _modEnabled.GetValue();
            //What drive now
            currentVeh = FsmVariables.GlobalVariables.GetFsmString("PlayerCurrentVehicle");

            maxSteeringAngle = GameObject.Find("Systems/OptionsDB").GetComponents<PlayMakerFSM>().Where(x => x.FsmName == "Controls").First().GetVariable<FsmFloat>("SteeringRotationFull");

            //Corris
            carRPM = FsmVariables.GlobalVariables.FindFsmFloat("RPM");
            carSpeed = FsmVariables.GlobalVariables.FindFsmFloat("SpeedKMH");
            raceTachot = InitPartValue("Tacho", "VINP_Tachometer", "SettingRPM");
            revLimit = InitPartValue("RevLimiter", "VINP_Revlimiter", "SettingRPM");
            FFBComp = GameObject.Find("CORRIS").GetComponent<ForceFeedback>();


            //Kekmet
            Kekmet = new RegularCarInfo
            {
                RPM = FsmVariables.GlobalVariables.FindFsmFloat("RPMvalmet"),
                Speed = FsmVariables.GlobalVariables.FindFsmFloat("SpeedValmet"),
                FFBComp = GameObject.Find("KEKMET(350-400psi)").GetComponent<ForceFeedback>()

            };

            Sorbet = RegularCarInfo.initCar("SORBET(190-200psi)");
            Gifu = RegularCarInfo.initCar("JOBS/TAXIJOB/MACHTWAGEN");
            Taxi = RegularCarInfo.initCar("GIFU(750/450psi)");

            if (_maxRPMSource.GetSelectedItemName() == "Manual")
            {
                _manualMaxRPM.SetVisibility(true);
            }
            TogleAdvancedFFB();

            harmony = HarmonyInstance.Create("izuko.rpmledffb");
            harmony.PatchAll();
            ModConsole.Print("Harmony FFB patches applied. Default FFB Disabled");

            ledsEnabled = _rpmLedsEnabled.GetValue();

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
            TogleAdvancedFFB();

        }
        public bool forcesIsZero = true;
        private void SetForcesToZero()
        {
            if (!LogitechGSDK.LogiIsConnected(0)) return;
            LogitechGSDK.LogiStopConstantForce(0);
            LogitechGSDK.LogiStopDamperForce(0);
            LogitechGSDK.LogiStopSpringForce(0);
            forcesIsZero = true;
        }
        private void Mod_FixedUpdate()
        {

            if (string.IsNullOrEmpty(currentVeh.Value))
            {
                if (!forcesIsZero)
                    SetForcesToZero();
                return;
            }

            if (!LogitechGSDK.LogiIsConnected(0)) return;

            if (!LogitechGSDK.LogiUpdate()) return;

            float currentRPM = 0;
            float currentForce = 0;
            float currentSpeed = 0;
            bool isCorris = false;
            switch(currentVeh.Value)
            {
                case "Corris":
                    currentRPM = carRPM.Value;
                    currentForce = FFBComp.force;
                    currentSpeed = carSpeed.Value;
                    isCorris = true;
                    if (!_corrisEnabled.GetValue())
                    {
                        SetForcesToZero();
                        return;
                    }
                    break;
                case "Sorbet":
                    currentRPM = Sorbet.DTComp.rpm;
                    currentForce = Sorbet.FFBComp.force;
                    currentSpeed = Mathf.Abs(Sorbet.DTComp.differentialSpeed);
                    if (!_sorbetEnabled.GetValue())
                    {
                        SetForcesToZero();
                        return;
                    }
                    break;
                case "Kekmet":
                    currentRPM = Kekmet.RPM.Value;
                    currentForce = Kekmet.FFBComp.force;
                    currentSpeed = Kekmet.Speed.Value;
                    if (!_kekmetEnabled.GetValue())
                    {
                        SetForcesToZero();
                        return;
                    }
                    break;
                case "Taxi":
                    currentRPM = Taxi.DTComp.rpm;
                    currentForce = Taxi.FFBComp.force;
                    currentSpeed = Mathf.Abs(Taxi.DTComp.differentialSpeed);
                    if (!_taxiEnabled.GetValue())
                    {
                        SetForcesToZero();
                        return;
                    }
                    break;
                case "Gifu":
                    currentRPM = Gifu.DTComp.rpm;
                    currentForce = Gifu.FFBComp.force;
                    currentSpeed = Mathf.Abs(Gifu.DTComp.differentialSpeed);
                    if (!_gifuEnabled.GetValue())
                    {
                        SetForcesToZero();
                        return;
                    }
                    break;
            }

            var springForce = _springForce.GetValue();
            var damperLow = _damperForceAtLowSpeed.GetValue();
            var damperHigh = _damperForceAtHighSpeed.GetValue();
            var damperMultyplyMaxAngle = _damperForceMultiplyAtMaxWheelAngle.GetValue();
            var maxanglerot = maxSteeringAngle.Value;

            if (currentRPM > 50 && ledsEnabled)
            {
                // --- Determine max RPM
                float rpm_MAX = RPM_MAX_DEFAULT;
                float rpm_FIRST_LED = RPM_FIRST_DEFAULT;
                float startPercent = _startPointPercent.GetValue();
                float maxPercent = _maxPointPercent.GetValue();
                
                if(isCorris)
                    switch (_maxRPMSource.GetSelectedItemIndex())
                    {
                        case 0: // Auto
                            if (raceTachot.Installed.Value && revLimit.Installed.Value)
                                rpm_MAX = Mathf.Min(raceTachot.PartValue.Value, revLimit.PartValue.Value);
                            else if (raceTachot.Installed.Value)
                                rpm_MAX = raceTachot.PartValue.Value;
                            else if (revLimit.Installed.Value)
                                rpm_MAX = revLimit.PartValue.Value;
                            break;
                        case 1: // Tacho
                            if (raceTachot.Installed.Value)
                                rpm_MAX = raceTachot.PartValue.Value;
                            break;
                        case 2: // Rev Limiter
                            if (revLimit.Installed.Value)
                                rpm_MAX = revLimit.PartValue.Value;
                            break;
                        case 3: // Manual
                            rpm_MAX = _manualMaxRPM.GetValue();
                            break;
                    }

                rpm_FIRST_LED = rpm_MAX * (startPercent / 100f);
                float shiftPoint = rpm_MAX * (maxPercent / 100f);
                LogitechGSDK.LogiPlayLeds(0, currentRPM, rpm_FIRST_LED, shiftPoint);
            }

            // --- Smooth constant force
            float targetForce = currentForce / 100f;
            float delta = maxChangePerSec * Time.deltaTime;
            currentForce = Mathf.MoveTowards(currentForce, targetForce, delta);
            currentForce = Mathf.Clamp(currentForce, -100f, 100f); // clamp to valid range
            if (_showDebugMSG.GetValue())
                ModConsole.Log($"Force to wheel {(int)targetForce} Speed {currentSpeed.ToString("0.0")} RPM {currentRPM.ToString("0.0")}");
            if (_enableAdvancedFFB.GetValue())
            {
                // --- Steering angle
                var state = LogitechGSDK.LogiGetStateCSharp(0);
                float currentAngle = (state.lX / 32768f) * maxanglerot;
                float angleFactor = Mathf.Clamp01(Mathf.Abs(currentAngle) / maxanglerot);

                // --- Speed
                float speed = currentSpeed;
                float speed01 = Mathf.Clamp01(speed / _damperTopSpeed.GetValue()); // normalize 0–1

                // --- Damper (heavy at low speed → lighter at high speed)
                float targetDamper = Mathf.Lerp(damperLow, damperHigh, speed01);
                targetDamper *= Mathf.Lerp(1f, damperMultyplyMaxAngle, angleFactor); // heavier at large lock
                damper = Mathf.Lerp(damper, targetDamper, Time.fixedDeltaTime * 3f);

                // --- Apply forces
                if (speed > 0.1f)
                {
                    if (speed < 10)
                        LogitechGSDK.LogiStopDamperForce(0);
                    else
                        LogitechGSDK.LogiPlayDamperForce(0, (int)damper);

                    LogitechGSDK.LogiPlaySpringForce(0, 0, springForce, 0);
                }
                else
                {
                    LogitechGSDK.LogiStopSpringForce(0);
                    LogitechGSDK.LogiPlayDamperForce(0, (int)damper);
                }
            }
            LogitechGSDK.LogiPlayConstantForce(0, (int)targetForce);

            forcesIsZero = false;
        }
    }
}
