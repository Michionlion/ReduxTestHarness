using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using KSP.Game;
using Newtonsoft.Json;
using UnityEngine;

namespace ReduxTestHarness
{
    // Offline capture: one normal rendered frame per output frame, including UI.
    // Encoding cost changes wall time, not camera speed or simulation timestep.
    internal sealed class PanCapture : IDisposable
    {
        private readonly MonoBehaviour _owner;
        private readonly KspGameAdapter _game;
        private UnityEngine.Coroutine _routine;
        private readonly float _oldCaptureDelta;
        private readonly int _oldWidth, _oldHeight;
        private readonly FullScreenMode _oldScreenMode;
        private bool _restored;
        public bool Complete { get; private set; }
        public Exception Error { get; private set; }
        public string DirectoryPath { get; private set; }

        public PanCapture(MonoBehaviour owner, KspGameAdapter game, string directory)
        {
            _owner = owner;
            _game = game;
            DirectoryPath = directory;
            if (Directory.Exists(directory)) throw new IOException("Capture directory already exists.");
            Directory.CreateDirectory(directory);
            _oldCaptureDelta = Time.captureDeltaTime;
            _oldWidth = Screen.width;
            _oldHeight = Screen.height;
            _oldScreenMode = Screen.fullScreenMode;
        }

        public void Start(int fps, int frames, int warmup, int width, int height,
            double startYaw, double endYaw, double pitch, double distance, double fov)
        {
            _routine = _owner.StartCoroutine(Record(fps, frames, warmup, width, height,
                startYaw, endYaw, pitch, distance, fov));
        }

        private IEnumerator Record(int fps, int frames, int warmup, int width, int height,
            double startYaw, double endYaw, double pitch, double distance, double fov)
        {
            var poses = new List<object>(frames);
            try
            {
                Screen.SetResolution(width, height, FullScreenMode.Windowed);
                Time.captureDeltaTime = 1f / fps;
                for (int i = 0; i < 4; ++i) yield return new WaitForEndOfFrame();
                if (Screen.width != width || Screen.height != height)
                {
                    Error = new InvalidOperationException("Player did not adopt requested capture resolution.");
                    yield break;
                }
                for (int frame = -warmup; frame < frames; ++frame)
                {
                    double yaw = startYaw + (endYaw - startYaw) * Math.Max(0, frame) / (frames - 1.0);
                    try { _game.SetOrbitCamera(distance, yaw, pitch, fov); }
                    catch (Exception error) { Error = error; yield break; }
                    yield return new WaitForEndOfFrame();
                    if (frame < 0) continue;
                    Texture2D screenshot = null;
                    try
                    {
                        Camera camera = GameManager.Instance.Game.GraphicsManager.GetCurrentUnityCamera();
                        Vector3 position = camera.transform.position;
                        Quaternion rotation = camera.transform.rotation;
                        screenshot = ScreenCapture.CaptureScreenshotAsTexture();
                        if (screenshot.width != width || screenshot.height != height)
                            throw new InvalidOperationException("Captured frame dimensions changed.");
                        File.WriteAllBytes(Path.Combine(DirectoryPath, frame.ToString("D6") + ".png"),
                            screenshot.EncodeToPNG());
                        poses.Add(new { frame, yaw, pitch, distance, fov, unityFrame = Time.frameCount,
                            time = Time.timeAsDouble, deltaTime = Time.deltaTime,
                            unscaledDeltaTime = Time.unscaledDeltaTime,
                            position = new[] { position.x, position.y, position.z },
                            rotation = new[] { rotation.x, rotation.y, rotation.z, rotation.w } });
                    }
                    catch (Exception error) { Error = error; yield break; }
                    finally { if (screenshot != null) UnityEngine.Object.Destroy(screenshot); }
                }
                try
                {
                    File.WriteAllText(Path.Combine(DirectoryPath, "capture.json"),
                        JsonConvert.SerializeObject(new { fps, frames, warmup, width, height,
                            startYaw, endYaw, pitch, distance, fov, uiIncluded = true,
                            offlineCapture = true, poses }, Formatting.Indented));
                }
                catch (Exception error) { Error = error; }
            }
            finally { Restore(); Complete = true; }
        }

        private void Restore()
        {
            if (_restored) return;
            _restored = true;
            Time.captureDeltaTime = _oldCaptureDelta;
            Screen.SetResolution(_oldWidth, _oldHeight, _oldScreenMode);
        }

        public void Dispose()
        {
            if (!Complete && Error == null) Error = new OperationCanceledException("Pan capture cancelled.");
            if (_routine != null) { _owner.StopCoroutine(_routine); _routine = null; }
            Restore();
            Complete = true;
        }
    }
}
