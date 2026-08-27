using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Reflection;
using KSP.Game;
using KSP.Rendering;
using KSP.Sim;
using KSP.Sim.impl;
using KSP.Sim.State;
using MoonSharp.Interpreter;
using UnityEngine;

namespace ReduxTestHarness
{
    internal sealed class KspGameAdapter
    {
        private readonly Dictionary<string, object> _renderValues =
            new Dictionary<string, object>(StringComparer.OrdinalIgnoreCase);
        private readonly Dictionary<string, object> _renderRestoreValues =
            new Dictionary<string, object>(StringComparer.OrdinalIgnoreCase);
        private CameraRequest _cameraRequest;
        private VesselComponent _cameraTarget;
        private Camera _overriddenCamera;
        private Vector3 _originalCameraPosition;
        private Quaternion _originalCameraRotation;
        private float _originalCameraFov;
        private bool _testSessionActive;
        private bool _restorePauseState;
        private bool _initialPauseState;
        private bool _restoreCameraMode;
        private CameraMode _initialCameraMode;

        private GameInstance Game
        {
            get
            {
                GameManager manager = GameManager.Instance;
                return manager == null ? null : manager.Game;
            }
        }

        public bool IsReady
        {
            get
            {
                GameInstance game = Game;
                if (game == null || !game.IsInitialized)
                {
                    return false;
                }
                string state = State;
                return state != "Loading" && state != "WarmUpLoading" && state != "Invalid";
            }
        }

        public string State
        {
            get
            {
                GameInstance game = Game;
                if (game == null || game.GlobalGameState == null)
                {
                    return "Unavailable";
                }
                GameState state = game.GlobalGameState.GetGameState().GameState;
                return state == GameState.FlightView ? "Flight" : state.ToString();
            }
        }

        public bool IsPaused
        {
            get
            {
                GameInstance game = Game;
                return game != null && game.UniverseModel != null && game.UniverseModel.IsTimePaused;
            }
        }

        public void BeginTestSession()
        {
            if (_testSessionActive)
            {
                throw new InvalidOperationException("A KSP2 test session is already active.");
            }
            _testSessionActive = true;
            _renderValues.Clear();
            _renderRestoreValues.Clear();
            ClearCameraOverride();

            GameInstance game = Game;
            _restorePauseState = game != null && game.UniverseModel != null;
            _initialPauseState = _restorePauseState && game.UniverseModel.IsTimePaused;
            _restoreCameraMode = false;
        }

        public List<string> EndTestSession()
        {
            var warnings = new List<string>();
            ClearCameraOverride();
            if (!_testSessionActive)
            {
                return warnings;
            }
            _testSessionActive = false;

            GraphicsSettings settings = null;
            try
            {
                settings = RequireGraphicsSettings();
            }
            catch (Exception error)
            {
                if (_renderRestoreValues.Count > 0)
                {
                    warnings.Add("Could not restore graphics settings: " + error.Message);
                }
            }

            if (settings != null)
            {
                RestoreRenderSettings(settings, warnings);
            }
            if (_restorePauseState)
            {
                try
                {
                    GameInstance game = Game;
                    if (game != null && game.UniverseModel != null)
                    {
                        game.UniverseModel.SetTimePaused(_initialPauseState, true);
                    }
                }
                catch (Exception error)
                {
                    warnings.Add("Could not restore the initial pause state: " + error.Message);
                }
            }

            if (_restoreCameraMode)
            {
                try
                {
                    GameInstance game = Game;
                    if (game != null && game.CameraManager != null)
                    {
                        game.CameraManager.SelectFlightCameraMode(_initialCameraMode);
                    }
                }
                catch (Exception error)
                {
                    warnings.Add("Could not restore the initial flight camera mode: " + error.Message);
                }
            }

            _restorePauseState = false;
            _restoreCameraMode = false;
            _renderRestoreValues.Clear();
            _renderValues.Clear();
            return warnings;
        }

        public void SetPaused(bool paused)
        {
            RequireGame().UniverseModel.SetTimePaused(paused, true);
        }

        public void LoadSave(string fixture, string fixturesRoot, Action<bool, string> complete)
        {
            string path;
            try
            {
                path = ResolveFixture(fixture, fixturesRoot);
            }
            catch (Exception error)
            {
                complete(false, error.Message);
                return;
            }

            GameInstance game = RequireGame();
            SaveLoadManager manager = game.SaveLoadManager;
            if (manager == null)
            {
                complete(false, "KSP2 SaveLoadManager is not initialized.");
                return;
            }

            // KSP2's CampaignLoadMenu, SaveLoadDialog, and quickload paths all
            // tear down the current universe before asking SaveLoadManager to
            // deserialize another file. Calling LoadGameFromFile directly from
            // Flight leaves the celestial-body catalog populated; the next
            // load then fails on duplicate bodies and produces cascading null
            // references in science, ambience, telemetry, and VFX systems.
            ClearCameraOverride();
            try
            {
                game.ResetUniverse(() =>
                {
                    try
                    {
                        GameInstance currentGame = RequireGame();
                        SaveLoadManager currentManager = currentGame.SaveLoadManager;
                        if (currentManager == null)
                        {
                            complete(false, "KSP2 SaveLoadManager was unavailable after resetting the universe.");
                            return;
                        }

                        bool started = currentManager.LoadGameFromFile(
                            path,
                            (ticket, success) =>
                            {
                                string error = success
                                    ? null
                                    : "KSP2 load failed: " + ticket.LoadOrSaveCampaignFailureCode;
                                complete(success, error);
                            });
                        if (!started)
                        {
                            complete(false, "KSP2 rejected the save-load operation because it is busy or the fixture is invalid.");
                        }
                    }
                    catch (Exception error)
                    {
                        complete(false, "KSP2 could not begin loading after resetting the universe: " + error.Message);
                    }
                });
            }
            catch (Exception error)
            {
                complete(false, "KSP2 could not reset the current universe: " + error.Message);
            }
        }

        public VesselComponent FindVessel(string name)
        {
            GameInstance game = RequireGame();
            if (game.UniverseModel == null)
            {
                return null;
            }

            List<VesselComponent> vessels = game.UniverseModel.GetAllVessels();
            for (int index = 0; index < vessels.Count; index++)
            {
                VesselComponent vessel = vessels[index];
                if (vessel == null)
                {
                    continue;
                }
                string displayName = vessel.RevealDisplayName();
                string internalName = vessel.RevealName();
                if (string.Equals(displayName, name, StringComparison.OrdinalIgnoreCase) ||
                    string.Equals(internalName, name, StringComparison.OrdinalIgnoreCase))
                {
                    return vessel;
                }
            }
            return null;
        }

        public VesselComponent ActiveVessel()
        {
            GameInstance game = Game;
            return game == null || game.ViewController == null
                ? null
                : game.ViewController.GetActiveSimVessel(true);
        }

        public bool ActivateVessel(VesselComponent vessel)
        {
            if (vessel == null)
            {
                return false;
            }
            return RequireGame().ViewController.SetActiveVehicle(vessel, true, true);
        }

        public bool IsActiveAndUsable(VesselComponent vessel)
        {
            return State == "Flight" && vessel != null && vessel.IsControllable &&
                ReferenceEquals(ActiveVessel(), vessel);
        }

        public void SetThrottle(double throttle)
        {
            RequireFinite(throttle, "throttle");
            if (throttle < 0.0 || throttle > 1.0)
            {
                throw new ArgumentOutOfRangeException(
                    "throttle",
                    "Throttle must be between 0.0 and 1.0.");
            }
            VesselComponent vessel = RequireActiveVessel();
            FlightCtrlState state = vessel.flightCtrlState;
            state.mainThrottle = (float)throttle;
            vessel.SetFlightControlState(state, false);
        }

        public void Stage()
        {
            RequireActiveVessel().ActivateNextStage();
        }

        public void SetSas(bool enabled)
        {
            RequireActiveVessel().SetActionGroup(KSPActionGroup.SAS, enabled);
        }

        public Table VesselSnapshot(Script script, VesselComponent vessel)
        {
            if (vessel == null)
            {
                return null;
            }

            var table = new Table(script);
            Set(table, "id", GetString(vessel, "GlobalIdGuidString", "GlobalId", "Guid"));
            Set(table, "name", SafeVesselName(vessel));
            Set(table, "body", vessel.mainBody == null ? null : vessel.mainBody.bodyName);
            Set(table, "situation", vessel.Situation.ToString());
            Set(table, "altitude", vessel.AltitudeFromSeaLevel);
            Set(table, "apoapsis", vessel.Orbit == null ? 0.0 : vessel.Orbit.ApoapsisArl);
            Set(table, "periapsis", vessel.Orbit == null ? 0.0 : vessel.Orbit.PeriapsisArl);
            Set(table, "partCount", GetPartCount(vessel));
            Set(table, "mass", vessel.totalMass);
            return table;
        }

        public void SetCameraMode(string mode)
        {
            UniverseCameraManager manager = RequireGame().CameraManager;
            if (_testSessionActive && !_restoreCameraMode)
            {
                _initialCameraMode = manager.GetFlightCameraMode();
                _restoreCameraMode = true;
            }
            CameraMode selected;
            if (string.Equals(mode, "Flight", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(mode, "Auto", StringComparison.OrdinalIgnoreCase))
            {
                selected = CameraMode.Auto;
            }
            else if (!Enum.TryParse(mode, true, out selected))
            {
                throw new ArgumentException("Unsupported camera mode '" + mode + "'.");
            }
            manager.SelectFlightCameraMode(selected);
        }

        public void TargetActiveVessel()
        {
            _cameraTarget = RequireActiveVessel();
        }

        public void SetOrbitCamera(double distance, double yaw, double pitch, double fov)
        {
            RequireFinite(distance, "camera distance");
            RequireFinite(yaw, "camera yaw");
            RequireFinite(pitch, "camera pitch");
            RequireFinite(fov, "camera fov");
            if (distance <= 0.0)
            {
                throw new ArgumentOutOfRangeException("distance", "Camera distance must be positive.");
            }
            ValidateFov(fov);
            float floatDistance = (float)distance;
            float floatYaw = (float)yaw;
            float floatPitch = (float)pitch;
            RequireFinite(floatDistance, "camera distance");
            RequireFinite(floatYaw, "camera yaw");
            RequireFinite(floatPitch, "camera pitch");
            if (_cameraTarget == null)
            {
                TargetActiveVessel();
            }
            _cameraRequest = CameraRequest.Orbit(
                floatDistance, floatYaw, floatPitch, (float)fov);
        }

        public void SetCamera(Vector3 position, Vector3 rotation, float fov)
        {
            RequireFinite(position.x, "camera position.x");
            RequireFinite(position.y, "camera position.y");
            RequireFinite(position.z, "camera position.z");
            RequireFinite(rotation.x, "camera rotation.x");
            RequireFinite(rotation.y, "camera rotation.y");
            RequireFinite(rotation.z, "camera rotation.z");
            RequireFinite(fov, "camera fov");
            ValidateFov(fov);
            if (_cameraTarget == null)
            {
                TargetActiveVessel();
            }
            _cameraRequest = CameraRequest.Explicit(position, rotation, fov);
        }

        public void ClearCameraOverride()
        {
            RestoreOverriddenCamera();
            _cameraRequest = null;
            _cameraTarget = null;
        }

        public void ApplyCameraOverride()
        {
            if (_cameraRequest == null || _cameraTarget == null)
            {
                return;
            }

            GameInstance game = Game;
            if (game == null || game.ViewController == null || game.GraphicsManager == null)
            {
                return;
            }
            VesselBehavior behavior = game.ViewController.GetBehaviorIfLoaded(_cameraTarget);
            Camera camera = game.GraphicsManager.GetCurrentUnityCamera();
            if (behavior == null || camera == null)
            {
                return;
            }

            if (_overriddenCamera != camera)
            {
                RestoreOverriddenCamera();
                _overriddenCamera = camera;
                _originalCameraPosition = camera.transform.position;
                _originalCameraRotation = camera.transform.rotation;
                _originalCameraFov = camera.fieldOfView;
            }

            Vector3 target = behavior.transform.position;
            if (_cameraRequest.IsOrbit)
            {
                Quaternion localOrbit = Quaternion.Euler(
                    _cameraRequest.Rotation.x,
                    _cameraRequest.Rotation.y,
                    0.0f);
                Vector3 offset = behavior.transform.TransformDirection(
                    localOrbit * (Vector3.back * _cameraRequest.Distance));
                camera.transform.position = target + offset;
                camera.transform.rotation = Quaternion.LookRotation(
                    target - camera.transform.position,
                    behavior.transform.up);
            }
            else
            {
                camera.transform.position = target + _cameraRequest.Position;
                camera.transform.rotation = Quaternion.Euler(_cameraRequest.Rotation);
            }
            camera.fieldOfView = _cameraRequest.Fov;
        }

        private void RestoreOverriddenCamera()
        {
            if (_overriddenCamera != null)
            {
                try
                {
                    _overriddenCamera.transform.position = _originalCameraPosition;
                    _overriddenCamera.transform.rotation = _originalCameraRotation;
                    _overriddenCamera.fieldOfView = _originalCameraFov;
                }
                catch
                {
                    // The camera may have been destroyed during a scene transition.
                }
            }
            _overriddenCamera = null;
        }

        public void SetRenderSetting(string name, object value)
        {
            if (string.IsNullOrWhiteSpace(name))
            {
                throw new ArgumentException("Rendering setting name is required.");
            }

            GraphicsSettings settings = RequireGraphicsSettings();
            string key = name.Trim();
            if (string.Equals(key, "supersampling", StringComparison.OrdinalIgnoreCase))
            {
                double factor = Convert.ToDouble(value, CultureInfo.InvariantCulture);
                RequireFinite(factor, "supersampling");
                if (factor < 1.0 || factor > 2.0)
                {
                    throw new ArgumentOutOfRangeException(
                        "value",
                        "KSP2 Redux supports supersampling factors from 1.0 through 2.0.");
                }
                RememberRenderValue(
                    "renderScalePercent",
                    PersistentProfileManager.RenderScalePercent);
                settings.SetRenderScalePercent((int)Math.Round(factor * 100.0));
                _renderValues[key] = factor;
                return;
            }
            if (string.Equals(key, "taa", StringComparison.OrdinalIgnoreCase))
            {
                bool enabled = Convert.ToBoolean(value, CultureInfo.InvariantCulture);
                RememberRenderValue(
                    "antiAliasingLevel",
                    PersistentProfileManager.AntiAliasing);
                settings.SetAntiAliasing(enabled ? 3 : 0);
                _renderValues[key] = enabled;
                return;
            }
            if (string.Equals(key, "clouds", StringComparison.OrdinalIgnoreCase))
            {
                bool enabled = Convert.ToBoolean(value, CultureInfo.InvariantCulture);
                RememberRenderValue("clouds", settings.GetEnableClouds());
                settings.EnableClouds(enabled);
                _renderValues[key] = enabled;
                return;
            }
            if (string.Equals(key, "cloudQuality", StringComparison.OrdinalIgnoreCase))
            {
                int quality = ParseQuality(value);
                if (quality < 0 || quality > 3)
                {
                    throw new ArgumentOutOfRangeException(
                        "value",
                        "cloudQuality must be Low, Medium, High, Ultra, or 0 through 3.");
                }
                RememberRenderValue("cloudQuality", settings.GetCloudQuality());
                settings.SetCloudQuality(quality);
                _renderValues[key] = value;
                return;
            }
            if (string.Equals(key, "vfxQuality", StringComparison.OrdinalIgnoreCase))
            {
                int quality = ParseQuality(value);
                if (quality < 0 || quality >= QualitySettings.names.Length)
                {
                    throw new ArgumentOutOfRangeException(
                        "value",
                        "vfxQuality must select an available Unity quality level.");
                }
                RememberRenderValue("vfxQuality", QualitySettings.GetQualityLevel());
                QualitySettings.SetQualityLevel(quality, true);
                _renderValues[key] = value;
                return;
            }
            if (value is bool)
            {
                bool original = false;
                if (!settings.GetBoolSetting(key, ref original))
                {
                    throw new ArgumentException(
                        "KSP2 does not expose a boolean graphics setting named '" + key + "'.");
                }
                RememberRenderValue("bool:" + key, original);
                settings.SetBoolSetting(key, (bool)value);
                _renderValues[key] = value;
                return;
            }
            throw new ArgumentException(
                "Unsupported rendering setting '" + name +
                "'. Supported MVP settings: supersampling, taa, clouds, cloudQuality, vfxQuality, and KSP boolean settings.");
        }

        private void RememberRenderValue(string key, object value)
        {
            if (_testSessionActive && !_renderRestoreValues.ContainsKey(key))
            {
                _renderRestoreValues.Add(key, value);
            }
        }

        private void RestoreRenderSettings(
            GraphicsSettings settings,
            List<string> warnings)
        {
            foreach (KeyValuePair<string, object> pair in _renderRestoreValues)
            {
                try
                {
                    switch (pair.Key)
                    {
                        case "renderScalePercent":
                            settings.SetRenderScalePercent((int)pair.Value);
                            break;
                        case "antiAliasingLevel":
                            settings.SetAntiAliasing((int)pair.Value);
                            break;
                        case "clouds":
                            settings.EnableClouds((bool)pair.Value);
                            break;
                        case "cloudQuality":
                            settings.SetCloudQuality((int)pair.Value);
                            break;
                        case "vfxQuality":
                            QualitySettings.SetQualityLevel((int)pair.Value, true);
                            break;
                        default:
                            if (pair.Key.StartsWith("bool:", StringComparison.Ordinal))
                            {
                                settings.SetBoolSetting(pair.Key.Substring(5), (bool)pair.Value);
                            }
                            break;
                    }
                }
                catch (Exception error)
                {
                    warnings.Add(
                        "Could not restore render setting '" + pair.Key + "': " +
                        error.Message);
                }
            }
        }

        public object GetRenderSetting(string name)
        {
            object value;
            if (_renderValues.TryGetValue(name, out value))
            {
                return value;
            }

            GraphicsSettings settings = RequireGraphicsSettings();
            if (string.Equals(name, "supersampling", StringComparison.OrdinalIgnoreCase))
            {
                return PersistentProfileManager.RenderScalePercent / 100.0;
            }
            if (string.Equals(name, "taa", StringComparison.OrdinalIgnoreCase))
            {
                return PersistentProfileManager.AntiAliasing == 3;
            }
            if (string.Equals(name, "clouds", StringComparison.OrdinalIgnoreCase))
            {
                return settings.GetEnableClouds();
            }
            if (string.Equals(name, "cloudQuality", StringComparison.OrdinalIgnoreCase))
            {
                return settings.GetCloudQuality();
            }
            if (string.Equals(name, "vfxQuality", StringComparison.OrdinalIgnoreCase))
            {
                return QualitySettings.GetQualityLevel();
            }
            bool boolean = false;
            if (settings.GetBoolSetting(name, ref boolean))
            {
                return boolean;
            }
            return null;
        }

        private GraphicsSettings RequireGraphicsSettings()
        {
            GameInstance game = RequireGame();
            if (game.GraphicsManager == null || game.GraphicsManager.GraphicsSettings == null)
            {
                throw new InvalidOperationException("KSP2 graphics settings are not initialized.");
            }
            return game.GraphicsManager.GraphicsSettings;
        }

        private GameInstance RequireGame()
        {
            GameInstance game = Game;
            if (game == null || !game.IsInitialized)
            {
                throw new InvalidOperationException("KSP2 game services are not initialized.");
            }
            return game;
        }

        private VesselComponent RequireActiveVessel()
        {
            VesselComponent vessel = ActiveVessel();
            if (vessel == null)
            {
                throw new InvalidOperationException("There is no active flight vessel.");
            }
            return vessel;
        }

        private static string ResolveFixture(string fixture, string root)
        {
            if (string.IsNullOrWhiteSpace(fixture))
            {
                throw new ArgumentException("Fixture name is required.");
            }
            if (Path.IsPathRooted(fixture))
            {
                throw new ArgumentException(
                    "Fixture names must be relative to the configured fixtures directory.");
            }
            string rootPath = Path.GetFullPath(root);
            string rootPrefix = rootPath.TrimEnd(
                Path.DirectorySeparatorChar,
                Path.AltDirectorySeparatorChar) + Path.DirectorySeparatorChar;
            string basePath = Path.Combine(
                rootPath,
                fixture.Replace('/', Path.DirectorySeparatorChar));
            string[] candidates = { basePath, basePath + ".json", basePath + ".json.gz" };
            for (int index = 0; index < candidates.Length; index++)
            {
                string full = Path.GetFullPath(candidates[index]);
                if (!full.StartsWith(rootPrefix, StringComparison.OrdinalIgnoreCase))
                {
                    throw new UnauthorizedAccessException(
                        "Fixture path escapes the configured fixtures directory: " + fixture);
                }
                if (File.Exists(full))
                {
                    return full;
                }
            }
            throw new FileNotFoundException(
                "Save fixture was not found. Checked: " + string.Join(", ", candidates));
        }

        private static string SafeVesselName(VesselComponent vessel)
        {
            string name = vessel.RevealDisplayName();
            return string.IsNullOrWhiteSpace(name) ? vessel.RevealName() : name;
        }

        private static string GetString(object value, params string[] names)
        {
            object member = GetMember(value, names);
            return member == null ? null : member.ToString();
        }

        private static int GetPartCount(VesselComponent vessel)
        {
            object member = GetMember(vessel, "Parts", "parts", "PartComponents");
            ICollection collection = member as ICollection;
            if (collection != null)
            {
                return collection.Count;
            }
            object count = GetMember(member, "Count");
            return count == null ? 0 : Convert.ToInt32(count, CultureInfo.InvariantCulture);
        }

        private static object GetMember(object value, params string[] names)
        {
            if (value == null)
            {
                return null;
            }
            Type type = value.GetType();
            const BindingFlags flags = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance;
            for (int index = 0; index < names.Length; index++)
            {
                PropertyInfo property = type.GetProperty(names[index], flags);
                if (property != null && property.GetIndexParameters().Length == 0)
                {
                    return property.GetValue(value, null);
                }
                FieldInfo field = type.GetField(names[index], flags);
                if (field != null)
                {
                    return field.GetValue(value);
                }
            }
            return null;
        }

        private static void Set(Table table, string key, object value)
        {
            table.Set(key, value == null ? DynValue.Nil : DynValue.FromObject(table.OwnerScript, value));
        }

        private static int ParseQuality(object value)
        {
            if (value is string)
            {
                switch (((string)value).Trim().ToLowerInvariant())
                {
                    case "low": return 0;
                    case "medium": return 1;
                    case "high": return 2;
                    case "ultra": return 3;
                    default: throw new ArgumentException("Quality must be Low, Medium, High, Ultra, or a numeric level.");
                }
            }
            return Convert.ToInt32(value, CultureInfo.InvariantCulture);
        }

        private static void ValidateFov(double fov)
        {
            if (fov < 1.0 || fov > 179.0)
            {
                throw new ArgumentOutOfRangeException(
                    "fov",
                    "Camera field of view must be between 1 and 179 degrees.");
            }
        }

        private static void RequireFinite(double value, string name)
        {
            if (double.IsNaN(value) || double.IsInfinity(value))
            {
                throw new ArgumentException(name + " must be a finite number.", name);
            }
        }

        private sealed class CameraRequest
        {
            public bool IsOrbit;
            public float Distance;
            public Vector3 Position;
            public Vector3 Rotation;
            public float Fov;

            public static CameraRequest Orbit(float distance, float yaw, float pitch, float fov)
            {
                return new CameraRequest
                {
                    IsOrbit = true,
                    Distance = distance,
                    Rotation = new Vector3(pitch, yaw, 0.0f),
                    Fov = fov
                };
            }

            public static CameraRequest Explicit(Vector3 position, Vector3 rotation, float fov)
            {
                return new CameraRequest
                {
                    IsOrbit = false,
                    Position = position,
                    Rotation = rotation,
                    Fov = fov
                };
            }
        }
    }
}
