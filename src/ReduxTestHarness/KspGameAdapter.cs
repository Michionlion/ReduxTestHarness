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
        private CameraRequest _cameraRequest;
        private VesselComponent _cameraTarget;

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
            VesselComponent vessel = RequireActiveVessel();
            FlightCtrlState state = vessel.flightCtrlState;
            state.mainThrottle = Mathf.Clamp01((float)throttle);
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
            if (_cameraTarget == null)
            {
                TargetActiveVessel();
            }
            _cameraRequest = CameraRequest.Orbit(
                (float)distance, (float)yaw, (float)pitch, (float)fov);
        }

        public void SetCamera(Vector3 position, Vector3 rotation, float fov)
        {
            if (_cameraTarget == null)
            {
                TargetActiveVessel();
            }
            _cameraRequest = CameraRequest.Explicit(position, rotation, fov);
        }

        public void ClearCameraOverride()
        {
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
                if (factor < 0.25 || factor > 4.0)
                {
                    throw new ArgumentOutOfRangeException("value", "supersampling must be between 0.25 and 4.0.");
                }
                settings.SetRenderScalePercent((int)Math.Round(factor * 100.0));
                _renderValues[key] = factor;
                return;
            }
            if (string.Equals(key, "taa", StringComparison.OrdinalIgnoreCase))
            {
                bool enabled = Convert.ToBoolean(value, CultureInfo.InvariantCulture);
                settings.SetAntiAliasing(enabled ? 3 : 0);
                _renderValues[key] = enabled;
                return;
            }
            if (string.Equals(key, "clouds", StringComparison.OrdinalIgnoreCase))
            {
                bool enabled = Convert.ToBoolean(value, CultureInfo.InvariantCulture);
                settings.EnableClouds(enabled);
                _renderValues[key] = enabled;
                return;
            }
            if (string.Equals(key, "cloudQuality", StringComparison.OrdinalIgnoreCase))
            {
                int quality = ParseQuality(value);
                settings.SetCloudQuality(quality);
                _renderValues[key] = value;
                return;
            }
            if (string.Equals(key, "vfxQuality", StringComparison.OrdinalIgnoreCase))
            {
                int quality = ParseQuality(value);
                QualitySettings.SetQualityLevel(quality, true);
                _renderValues[key] = value;
                return;
            }
            if (value is bool)
            {
                settings.SetBoolSetting(key, (bool)value);
                _renderValues[key] = value;
                return;
            }
            throw new ArgumentException(
                "Unsupported rendering setting '" + name +
                "'. Supported MVP settings: supersampling, taa, clouds, cloudQuality, vfxQuality, and KSP boolean settings.");
        }

        public object GetRenderSetting(string name)
        {
            object value;
            if (_renderValues.TryGetValue(name, out value))
            {
                return value;
            }

            GraphicsSettings settings = RequireGraphicsSettings();
            if (string.Equals(name, "clouds", StringComparison.OrdinalIgnoreCase))
            {
                return settings.GetEnableClouds();
            }
            if (string.Equals(name, "cloudQuality", StringComparison.OrdinalIgnoreCase))
            {
                return settings.GetCloudQuality();
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
            string basePath = Path.IsPathRooted(fixture)
                ? fixture
                : Path.Combine(root, fixture.Replace('/', Path.DirectorySeparatorChar));
            string[] candidates = { basePath, basePath + ".json", basePath + ".json.gz" };
            for (int index = 0; index < candidates.Length; index++)
            {
                string full = Path.GetFullPath(candidates[index]);
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
