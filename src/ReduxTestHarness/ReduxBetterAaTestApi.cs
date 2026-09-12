using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Text;
using HarmonyLib;
using MoonSharp.Interpreter;
using UnityEngine;
using UnityEngine.Rendering;

namespace ReduxTestHarness
{
    /// <summary>
    /// Test-only semantic adapter for Redux Better AA. The reflection boundary
    /// keeps the harness independent of the feature mod and is exercised only
    /// when a Lua test explicitly requests a Better AA operation.
    /// </summary>
    internal static class ReduxBetterAaTestApi
    {
        private const BindingFlags StaticAny =
            BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic;
        private const BindingFlags InstanceAny =
            BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;
        private static readonly List<Renderer> SuppressedRenderers =
            new List<Renderer>();
        private static readonly List<MotionVectorGenerationMode> SuppressedModes =
            new List<MotionVectorGenerationMode>();
        private static readonly List<Renderer> ScaledPlanetRenderers =
            new List<Renderer>();
        private static readonly List<MotionVectorGenerationMode> ScaledPlanetModes =
            new List<MotionVectorGenerationMode>();
        private static readonly List<Camera> IsolatedCameras =
            new List<Camera>();
        private static readonly List<DepthTextureMode> IsolatedCameraModes =
            new List<DepthTextureMode>();
        private static readonly List<Behaviour> SuppressedPostBehaviours =
            new List<Behaviour>();
        private static readonly List<bool> SuppressedPostBehaviourStates =
            new List<bool>();
        private static readonly List<Camera> SuppressedCameras =
            new List<Camera>();
        private static readonly List<bool> SuppressedCameraStates =
            new List<bool>();
        private static readonly int CameraMotionVectorsTexture =
            Shader.PropertyToID("_CameraMotionVectorsTexture");
        private static readonly int CameraDepthTexture =
            Shader.PropertyToID("_CameraDepthTexture");
        private static readonly int CloneDisplayTemporary =
            Shader.PropertyToID("_ReduxTestHarnessCloneDisplayTemporary");
        private static readonly int ScaledPlanetMotionProbe =
            Shader.PropertyToID("_ReduxBetterAAScaledPlanetMotionProbe");
        private static Camera _earlyMotionCamera;
        private static Material _earlyMotionMaterial;
        private static RenderTexture _earlyMotionTexture;
        private static CommandBuffer _earlyMotionCommandBuffer;
        private static CameraEvent _earlyMotionEvent;
        private static Camera _earlyDepthCamera;
        private static Material _earlyDepthMaterial;
        private static RenderTexture _earlyDepthTexture;
        private static CommandBuffer _earlyDepthCommandBuffer;
        private static CameraEvent _earlyDepthEvent;
        private static Camera _globalDepthOverrideCamera;
        private static RenderTexture _globalDepthOverrideTexture;
        private static CommandBuffer _globalDepthOverrideCommandBuffer;
        private static CameraEvent _globalDepthOverrideEvent;
        private static Camera _motionClearCamera;
        private static CommandBuffer _motionClearCommandBuffer;
        private static CameraEvent _motionClearEvent;
        private static Camera _overriddenCamera;
        private static int _overriddenCullingMask;
        private static bool _overriddenForceIntoRenderTexture;
        private static CameraClearFlags _overriddenClearFlags;
        private static RenderingPath _overriddenRenderingPath;
        private static DepthTextureMode _overriddenDepthTextureMode;
        private static Camera _cloneMotionCamera;
        private static GameObject _cloneMotionGameObject;
        private static RenderTexture _cloneColorTexture;
        private static RenderTexture _cloneMotionTexture;
        private static CommandBuffer _cloneMotionCommandBuffer;
        private static CommandBuffer _cloneDisplayCommandBuffer;
        private static Material _cloneMotionVisualizerMaterial;
        private const int CameraTraceCapacity = 512;
        private static readonly CameraTraceRecord[] CameraTraceRecords =
            new CameraTraceRecord[CameraTraceCapacity];
        private static Camera _tracedCamera;
        private static bool _cameraTraceActive;
        private static int _cameraTraceCount;
        private static int _cameraTraceWriteIndex;
        private static readonly List<object> SuppressedCubemapSystems =
            new List<object>();
        private static readonly List<Behaviour> SuppressedPqsRenderers =
            new List<Behaviour>();
        private static readonly List<bool> SuppressedPqsRendererStates =
            new List<bool>();
        private static readonly List<CameraEvent> SuppressedCommandBufferEvents =
            new List<CameraEvent>();
        private static readonly List<CommandBuffer> SuppressedCommandBuffers =
            new List<CommandBuffer>();
        private static Camera _suppressedCommandBufferCamera;
        private const string RenderSuppressionHarmonyId =
            "ReduxTestHarness.BetterAA.RenderSuppression";
        private const string TemporalJitterHarmonyId =
            "ReduxTestHarness.BetterAA.TemporalJitterSuppression";
        private static readonly Harmony RenderSuppressionHarmony =
            new Harmony(RenderSuppressionHarmonyId);
        private static readonly Harmony TemporalJitterHarmony =
            new Harmony(TemporalJitterHarmonyId);
        private static bool _temporalJitterSuppressed;
        private static ShadowQuality? _originalShadowQuality;
        private static readonly List<MethodBase> SuppressedRenderMethods =
            new List<MethodBase>();
        private static int _suppressedRenderProducerHits;
        private static readonly List<VegetationDrawRecord> VegetationDrawRecords =
            new List<VegetationDrawRecord>();
        private static string _isolatedVegetationItemId;
        private static bool _isolateVegetationFirstMaterial;
        private static AwesomeTechnologies.VegetationSystem
            .VegetationItemModelInfo _materialIsolatedVegetationItem;
        private static Material[] _originalVegetationMaterialsLod0;
        private static Material[] _originalVegetationMaterialsLod1;
        private static Material[] _originalVegetationMaterialsLod2;
        private static Material[] _originalVegetationMaterialsLod3;

        private sealed class VegetationDrawRecord
        {
            public string ItemId;
            public string Name;
            public string Model;
            public string Mesh;
            public string Materials;
            public int LodIndex;
            public int Calls;
            public int DirectGraphicsCalls;
            public int CommandBufferCalls;
            public int ShadowCalls;
            public string Camera;
            public Bounds FirstBounds;
        }

        private struct CameraTraceRecord
        {
            public int Frame;
            public byte Phase;
            public Camera Camera;
            public Vector3 Position;
            public Matrix4x4 CurrentViewProjection;
            public Matrix4x4 PreviousViewProjection;
        }

        public static void Configure(Script script, Table table)
        {
            table.Set("release_status", TestApiRegistry.Callback(
                "ReduxBetterAA.release_status", (context, arguments) => ReleaseStatus(script)));
            table.Set("set_map_enabled", TestApiRegistry.Callback(
                "ReduxBetterAA.set_map_enabled", (context, arguments) =>
                {
                    Type type = RequireType(RequireBetterAaAssembly(),
                        "ReduxBetterAA.Rendering.TemporalCoordinator");
                    RequireMethod(type, "SetMapViewAaEnabled", InstanceAny).Invoke(
                        RequireStaticField(type, "Current"), new object[] {
                            RequiredBoolean(arguments, 0, "set_map_enabled") });
                    return DynValue.Nil;
                }));
            table.Set("request_issue_report", TestApiRegistry.Callback(
                "ReduxBetterAA.request_issue_report", (context, arguments) =>
                {
                    Type type = RequireType(RequireBetterAaAssembly(),
                        "ReduxBetterAA.Diagnostics.Phase1ProbeService");
                    return DynValue.NewBoolean((bool)RequireMethod(type,
                        "RequestIssueReport", InstanceAny).Invoke(RequireStaticField(type, "Current"), null));
                }));
            table.Set(
                "available",
                TestApiRegistry.Callback(
                    "Test.mod.extensions.ReduxBetterAA.available",
                    (context, arguments) => DynValue.NewBoolean(
                        FindBetterAaAssembly() != null)));
            table.Set(
                "set_backend",
                TestApiRegistry.Callback(
                    "Test.mod.extensions.ReduxBetterAA.set_backend",
                    SetBackend));
            table.Set(
                "set_dlaa_preset",
                TestApiRegistry.Callback(
                    "Test.mod.extensions.ReduxBetterAA.set_dlaa_preset",
                    SetDlaaPreset));
            table.Set(
                "set_vendor_jitter_spread",
                TestApiRegistry.Callback(
                    "Test.mod.extensions.ReduxBetterAA.set_vendor_jitter_spread",
                    SetVendorJitterSpread));
            table.Set(
                "set_vendor_sharpness",
                TestApiRegistry.Callback(
                    "Test.mod.extensions.ReduxBetterAA.set_vendor_sharpness",
                    SetVendorSharpness));
            table.Set(
                "set_vendor_auto_exposure",
                TestApiRegistry.Callback(
                    "Test.mod.extensions.ReduxBetterAA.set_vendor_auto_exposure",
                    SetVendorAutoExposure));
            table.Set(
                "set_temporal_jitter_suppressed",
                TestApiRegistry.Callback(
                    "Test.mod.extensions.ReduxBetterAA.set_temporal_jitter_suppressed",
                    SetTemporalJitterSuppressed));
            table.Set(
                "open_map_view",
                TestApiRegistry.Callback(
                    "Test.mod.extensions.ReduxBetterAA.open_map_view",
                    OpenMapView));
            table.Set(
                "set_buffer_view",
                TestApiRegistry.Callback(
                    "Test.mod.extensions.ReduxBetterAA.set_buffer_view",
                    SetBufferView));
            table.Set(
                "select_camera",
                TestApiRegistry.Callback(
                    "Test.mod.extensions.ReduxBetterAA.select_camera",
                    SelectCamera));
            table.Set(
                "request_capture",
                TestApiRegistry.Callback(
                    "Test.mod.extensions.ReduxBetterAA.request_capture",
                    RequestCapture));
            table.Set(
                "current_view",
                TestApiRegistry.Callback(
                    "Test.mod.extensions.ReduxBetterAA.current_view",
                    CurrentView));
            table.Set(
                "selected_camera",
                TestApiRegistry.Callback(
                    "Test.mod.extensions.ReduxBetterAA.selected_camera",
                    SelectedCamera));
            table.Set(
                "matrix_snapshot",
                TestApiRegistry.Callback(
                    "Test.mod.extensions.ReduxBetterAA.matrix_snapshot",
                    (context, arguments) => MatrixSnapshot(script)));
            table.Set(
                "suppress_object_motion",
                TestApiRegistry.Callback(
                    "Test.mod.extensions.ReduxBetterAA.suppress_object_motion",
                    SuppressObjectMotion));
            table.Set(
                "restore_object_motion",
                TestApiRegistry.Callback(
                    "Test.mod.extensions.ReduxBetterAA.restore_object_motion",
                    RestoreObjectMotion));
            table.Set(
                "isolate_motion_camera",
                TestApiRegistry.Callback(
                    "Test.mod.extensions.ReduxBetterAA.isolate_motion_camera",
                    IsolateMotionCamera));
            table.Set(
                "restore_motion_cameras",
                TestApiRegistry.Callback(
                    "Test.mod.extensions.ReduxBetterAA.restore_motion_cameras",
                    RestoreMotionCameras));
            table.Set(
                "capture_motion_at_event",
                TestApiRegistry.Callback(
                    "Test.mod.extensions.ReduxBetterAA.capture_motion_at_event",
                    CaptureMotionAtEvent));
            table.Set(
                "restore_motion_capture",
                TestApiRegistry.Callback(
                    "Test.mod.extensions.ReduxBetterAA.restore_motion_capture",
                    RestoreMotionCapture));
            table.Set(
                "capture_depth_at_event",
                TestApiRegistry.Callback(
                    "Test.mod.extensions.ReduxBetterAA.capture_depth_at_event",
                    CaptureDepthAtEvent));
            table.Set(
                "restore_depth_capture",
                TestApiRegistry.Callback(
                    "Test.mod.extensions.ReduxBetterAA.restore_depth_capture",
                    RestoreDepthCapture));
            table.Set(
                "override_depth_with_far",
                TestApiRegistry.Callback(
                    "Test.mod.extensions.ReduxBetterAA.override_depth_with_far",
                    OverrideDepthWithFar));
            table.Set(
                "restore_depth_override",
                TestApiRegistry.Callback(
                    "Test.mod.extensions.ReduxBetterAA.restore_depth_override",
                    RestoreDepthOverride));
            table.Set(
                "clear_motion_at_event",
                TestApiRegistry.Callback(
                    "Test.mod.extensions.ReduxBetterAA.clear_motion_at_event",
                    ClearMotionAtEvent));
            table.Set(
                "restore_motion_clear",
                TestApiRegistry.Callback(
                    "Test.mod.extensions.ReduxBetterAA.restore_motion_clear",
                    RestoreMotionClear));
            table.Set(
                "suppress_post_processing",
                TestApiRegistry.Callback(
                    "Test.mod.extensions.ReduxBetterAA.suppress_post_processing",
                    SuppressPostProcessing));
            table.Set(
                "restore_post_processing",
                TestApiRegistry.Callback(
                    "Test.mod.extensions.ReduxBetterAA.restore_post_processing",
                    RestorePostProcessing));
            table.Set(
                "suppress_camera_component",
                TestApiRegistry.Callback(
                    "Test.mod.extensions.ReduxBetterAA.suppress_camera_component",
                    SuppressCameraComponent));
            table.Set(
                "restore_camera_components",
                TestApiRegistry.Callback(
                    "Test.mod.extensions.ReduxBetterAA.restore_camera_components",
                    RestorePostProcessing));
            table.Set(
                "override_camera_state",
                TestApiRegistry.Callback(
                    "Test.mod.extensions.ReduxBetterAA.override_camera_state",
                    OverrideCameraState));
            table.Set(
                "restore_camera_state",
                TestApiRegistry.Callback(
                    "Test.mod.extensions.ReduxBetterAA.restore_camera_state",
                    RestoreCameraState));
            table.Set(
                "set_camera_culling_mask",
                TestApiRegistry.Callback(
                    "Test.mod.extensions.ReduxBetterAA.set_camera_culling_mask",
                    SetCameraCullingMask));
            table.Set(
                "suppress_camera",
                TestApiRegistry.Callback(
                    "Test.mod.extensions.ReduxBetterAA.suppress_camera",
                    SuppressCamera));
            table.Set(
                "restore_suppressed_cameras",
                TestApiRegistry.Callback(
                    "Test.mod.extensions.ReduxBetterAA.restore_suppressed_cameras",
                    RestoreSuppressedCameras));
            table.Set(
                "use_clone_motion_camera",
                TestApiRegistry.Callback(
                    "Test.mod.extensions.ReduxBetterAA.use_clone_motion_camera",
                    UseCloneMotionCamera));
            table.Set(
                "restore_clone_motion_camera",
                TestApiRegistry.Callback(
                    "Test.mod.extensions.ReduxBetterAA.restore_clone_motion_camera",
                    RestoreCloneMotionCamera));
            table.Set(
                "clone_matrix_snapshot",
                TestApiRegistry.Callback(
                    "Test.mod.extensions.ReduxBetterAA.clone_matrix_snapshot",
                    (context, arguments) => CloneMatrixSnapshot(script)));
            table.Set(
                "start_camera_render_trace",
                TestApiRegistry.Callback(
                    "Test.mod.extensions.ReduxBetterAA.start_camera_render_trace",
                    StartCameraRenderTrace));
            table.Set(
                "camera_render_trace",
                TestApiRegistry.Callback(
                    "Test.mod.extensions.ReduxBetterAA.camera_render_trace",
                    (context, arguments) => CameraRenderTrace(script)));
            table.Set(
                "stop_camera_render_trace",
                TestApiRegistry.Callback(
                    "Test.mod.extensions.ReduxBetterAA.stop_camera_render_trace",
                    StopCameraRenderTrace));
            table.Set(
                "suppress_observer_cubemap",
                TestApiRegistry.Callback(
                    "Test.mod.extensions.ReduxBetterAA.suppress_observer_cubemap",
                    SuppressObserverCubemap));
            table.Set(
                "restore_observer_cubemap",
                TestApiRegistry.Callback(
                    "Test.mod.extensions.ReduxBetterAA.restore_observer_cubemap",
                    RestoreObserverCubemap));
            table.Set(
                "pqs_snapshot",
                TestApiRegistry.Callback(
                    "Test.mod.extensions.ReduxBetterAA.pqs_snapshot",
                    (context, arguments) => PqsSnapshot(script)));
            table.Set(
                "scaled_planet_snapshot",
                TestApiRegistry.Callback(
                    "Test.mod.extensions.ReduxBetterAA.scaled_planet_snapshot",
                    (context, arguments) => ScaledPlanetSnapshot(script)));
            table.Set(
                "skybox_snapshot",
                TestApiRegistry.Callback(
                    "Test.mod.extensions.ReduxBetterAA.skybox_snapshot",
                    (context, arguments) => SkyboxSnapshot(script)));
            table.Set(
                "set_scaled_planet_motion_mode",
                TestApiRegistry.Callback(
                    "Test.mod.extensions.ReduxBetterAA.set_scaled_planet_motion_mode",
                    SetScaledPlanetMotionMode));
            table.Set(
                "restore_scaled_planet_motion_mode",
                TestApiRegistry.Callback(
                    "Test.mod.extensions.ReduxBetterAA.restore_scaled_planet_motion_mode",
                    RestoreScaledPlanetMotionMode));
            table.Set(
                "set_scaled_planet_motion_probe",
                TestApiRegistry.Callback(
                    "Test.mod.extensions.ReduxBetterAA.set_scaled_planet_motion_probe",
                    SetScaledPlanetMotionProbe));
            table.Set(
                "suppress_pqs_prepass",
                TestApiRegistry.Callback(
                    "Test.mod.extensions.ReduxBetterAA.suppress_pqs_prepass",
                    SuppressPqsPrepass));
            table.Set(
                "restore_pqs_prepass",
                TestApiRegistry.Callback(
                    "Test.mod.extensions.ReduxBetterAA.restore_pqs_prepass",
                    RestorePqsPrepass));
            table.Set(
                "suppress_camera_command_buffers",
                TestApiRegistry.Callback(
                    "Test.mod.extensions.ReduxBetterAA.suppress_camera_command_buffers",
                    SuppressCameraCommandBuffers));
            table.Set(
                "restore_camera_command_buffers",
                TestApiRegistry.Callback(
                    "Test.mod.extensions.ReduxBetterAA.restore_camera_command_buffers",
                    RestoreCameraCommandBuffers));
            table.Set(
                "set_motion_pass_probe",
                TestApiRegistry.Callback(
                    "Test.mod.extensions.ReduxBetterAA.set_motion_pass_probe",
                    SetMotionPassProbe));
            table.Set(
                "restore_motion_pass_probe",
                TestApiRegistry.Callback(
                    "Test.mod.extensions.ReduxBetterAA.restore_motion_pass_probe",
                    RestoreMotionPassProbe));
            table.Set(
                "scene_objects_at_camera",
                TestApiRegistry.Callback(
                    "Test.mod.extensions.ReduxBetterAA.scene_objects_at_camera",
                    (context, arguments) => SceneObjectsAtCamera(script)));
            table.Set(
                "suppress_render_producer",
                TestApiRegistry.Callback(
                    "Test.mod.extensions.ReduxBetterAA.suppress_render_producer",
                    SuppressRenderProducer));
            table.Set(
                "restore_render_producers",
                TestApiRegistry.Callback(
                    "Test.mod.extensions.ReduxBetterAA.restore_render_producers",
                    RestoreRenderProducers));
            table.Set(
                "render_producer_suppression_hits",
                TestApiRegistry.Callback(
                    "Test.mod.extensions.ReduxBetterAA.render_producer_suppression_hits",
                    RenderProducerSuppressionHits));
            table.Set(
                "vegetation_render_snapshot",
                TestApiRegistry.Callback(
                    "Test.mod.extensions.ReduxBetterAA.vegetation_render_snapshot",
                    (context, arguments) => VegetationRenderSnapshot(script)));
            table.Set(
                "isolate_vegetation_item",
                TestApiRegistry.Callback(
                    "Test.mod.extensions.ReduxBetterAA.isolate_vegetation_item",
                    IsolateVegetationItem));
            table.Set(
                "isolate_vegetation_item_first_material",
                TestApiRegistry.Callback(
                    "Test.mod.extensions.ReduxBetterAA.isolate_vegetation_item_first_material",
                    IsolateVegetationItemFirstMaterial));
            table.Set(
                "reroute_vegetation_camera_motion",
                TestApiRegistry.Callback(
                    "Test.mod.extensions.ReduxBetterAA.reroute_vegetation_camera_motion",
                    RerouteVegetationCameraMotion));
            table.Set(
                "set_vegetation_motion_repair",
                TestApiRegistry.Callback(
                    "Test.mod.extensions.ReduxBetterAA.set_vegetation_motion_repair",
                    SetVegetationMotionRepair));
            table.Set(
                "set_motion_sanitizer",
                TestApiRegistry.Callback(
                    "Test.mod.extensions.ReduxBetterAA.set_motion_sanitizer",
                    SetMotionSanitizer));
            table.Set(
                "set_depth_disocclusion_mask",
                TestApiRegistry.Callback(
                    "Test.mod.extensions.ReduxBetterAA.set_depth_disocclusion_mask",
                    SetDepthDisocclusionMask));
            table.Set(
                "set_dlaa_no_depth_bias",
                TestApiRegistry.Callback(
                    "Test.mod.extensions.ReduxBetterAA.set_dlaa_no_depth_bias",
                    SetDlaaNoDepthBias));
            table.Set(
                "set_dlaa_full_bias",
                TestApiRegistry.Callback(
                    "Test.mod.extensions.ReduxBetterAA.set_dlaa_full_bias",
                    SetDlaaFullBias));
            table.Set(
                "set_dlaa_execution_bypass",
                TestApiRegistry.Callback(
                    "Test.mod.extensions.ReduxBetterAA.set_dlaa_execution_bypass",
                    SetDlaaExecutionBypass));
            table.Set(
                "set_dlaa_transparent_projection_jitter",
                TestApiRegistry.Callback(
                    "Test.mod.extensions.ReduxBetterAA.set_dlaa_transparent_projection_jitter",
                    SetDlaaTransparentProjectionJitter));
            table.Set(
                "set_dlaa_flat_color_input",
                TestApiRegistry.Callback(
                    "Test.mod.extensions.ReduxBetterAA.set_dlaa_flat_color_input",
                    SetDlaaFlatColorInput));
            table.Set(
                "request_history_reset",
                TestApiRegistry.Callback(
                    "Test.mod.extensions.ReduxBetterAA.request_history_reset",
                    RequestHistoryReset));
            table.Set(
                "set_quality_shadows",
                TestApiRegistry.Callback(
                    "Test.mod.extensions.ReduxBetterAA.set_quality_shadows",
                    SetQualityShadows));
            table.Set(
                "motion_input_snapshot",
                TestApiRegistry.Callback(
                    "Test.mod.extensions.ReduxBetterAA.motion_input_snapshot",
                    (context, arguments) => MotionInputSnapshot(script)));
            table.Set(
                "cloud_renderer_snapshot",
                TestApiRegistry.Callback(
                    "Test.mod.extensions.ReduxBetterAA.cloud_renderer_snapshot",
                    (context, arguments) => CloudRendererSnapshot(script)));
            table.Set(
                "dlaa_cloud_transition_snapshot",
                TestApiRegistry.Callback(
                    "Test.mod.extensions.ReduxBetterAA.dlaa_cloud_transition_snapshot",
                    (context, arguments) => DlaaCloudTransitionSnapshot(script)));
            table.Set(
                "capture_cloud_texture",
                TestApiRegistry.Callback(
                    "Test.mod.extensions.ReduxBetterAA.capture_cloud_texture",
                    CaptureCloudTexture));
        }

        private static DynValue RequestHistoryReset(
            ScriptExecutionContext context,
            CallbackArguments arguments)
        {
            Assembly assembly = RequireBetterAaAssembly();
            Type coordinatorType = RequireType(
                assembly,
                "ReduxBetterAA.Rendering.TemporalCoordinator");
            object coordinator = RequireStaticField(coordinatorType, "Current");
            RequireMethod(coordinatorType, "RequestHistoryReset", InstanceAny)
                .Invoke(coordinator, null);
            return DynValue.Nil;
        }

        public static void RestoreAll()
        {
            RestoreObjectMotionInternal();
            RestoreScaledPlanetMotionModeInternal();
            Shader.SetGlobalFloat(ScaledPlanetMotionProbe, 0.0f);
            RestoreMotionCamerasInternal();
            RestoreMotionCaptureInternal();
            RestoreDepthCaptureInternal();
            RestoreDepthOverrideInternal();
            RestoreMotionClearInternal();
            RestorePostProcessingInternal();
            RestoreCameraStateInternal();
            RestoreSuppressedCamerasInternal();
            RestoreCloneMotionCameraInternal();
            StopCameraRenderTraceInternal();
            RestoreObserverCubemapInternal();
            RestorePqsPrepassInternal();
            RestoreCameraCommandBuffersInternal();
            RestoreMotionPassProbeInternal();
            RestoreRenderProducersInternal();
            RestoreTemporalJitterSuppression();
            RestoreProductionMotionDefaults();
        }

        private static DynValue SuppressRenderProducer(
            ScriptExecutionContext context,
            CallbackArguments arguments)
        {
            string producer = RequiredString(
                arguments,
                0,
                "suppress_render_producer");
            Type type;
            string methodName;
            if (string.Equals(
                producer,
                "atmosphere",
                StringComparison.OrdinalIgnoreCase))
            {
                type = typeof(KSP.Rendering.Planets.AtmosphereScatterManager);
                methodName = "UpdateAndRenderPostEffect";
            }
            else if (string.Equals(
                producer,
                "clouds",
                StringComparison.OrdinalIgnoreCase))
            {
                type = typeof(KSP.VolumeCloud.VolumeCloudRenderer);
                methodName = "RenderClouds";
            }
            else if (string.Equals(
                producer,
                "pqs-quads",
                StringComparison.OrdinalIgnoreCase))
            {
                type = typeof(KSP.Rendering.Planets.PQSRenderer);
                methodName = "DrawPQSQuads";
            }
            else if (string.Equals(
                producer,
                "pqs-ocean",
                StringComparison.OrdinalIgnoreCase))
            {
                type = typeof(KSP.Rendering.Planets.PQSRenderer);
                methodName = "OnDrawOcean";
            }
            else if (string.Equals(
                producer,
                "light-culling",
                StringComparison.OrdinalIgnoreCase))
            {
                type = typeof(LightCullingManager);
                methodName = "LateUpdate";
            }
            else if (string.Equals(
                producer,
                "vegetation-billboards",
                StringComparison.OrdinalIgnoreCase))
            {
                type = typeof(
                    AwesomeTechnologies.VegetationSystem.VegetationSystemPro);
                methodName = "RenderBillboardCellsInstancedIndirect";
            }
            else if (string.Equals(
                producer,
                "vegetation-lod",
                StringComparison.OrdinalIgnoreCase))
            {
                type = typeof(
                    AwesomeTechnologies.VegetationSystem.VegetationSystemPro);
                methodName = "RenderVegetationItemLODIndirect";
            }
            else
            {
                throw new ScriptRuntimeException(
                    "Unknown render producer '" + producer + "'.");
            }

            string prefixName = string.Equals(
                    producer,
                    "vegetation-lod",
                    StringComparison.OrdinalIgnoreCase)
                ? "CaptureAndSkipVegetationLod"
                : "SkipManagedRenderProducer";
            MethodInfo prefix = typeof(ReduxBetterAaTestApi).GetMethod(
                prefixName,
                StaticAny);
            MethodInfo[] methods = type.GetMethods(InstanceAny);
            int count = 0;
            _suppressedRenderProducerHits = 0;
            VegetationDrawRecords.Clear();
            for (int index = 0; index < methods.Length; index++)
            {
                MethodInfo method = methods[index];
                if (!string.Equals(
                    method.Name,
                    methodName,
                    StringComparison.Ordinal))
                {
                    continue;
                }
                RenderSuppressionHarmony.Patch(
                    method,
                    new HarmonyMethod(prefix));
                SuppressedRenderMethods.Add(method);
                count++;
            }
            if (count == 0)
            {
                throw new MissingMethodException(type.FullName, methodName);
            }
            return DynValue.NewNumber(count);
        }

        private static DynValue RestoreRenderProducers(
            ScriptExecutionContext context,
            CallbackArguments arguments)
        {
            int count = SuppressedRenderMethods.Count;
            RestoreRenderProducersInternal();
            return DynValue.NewNumber(count);
        }

        private static DynValue RenderProducerSuppressionHits(
            ScriptExecutionContext context,
            CallbackArguments arguments)
        {
            return DynValue.NewNumber(_suppressedRenderProducerHits);
        }

        private static DynValue RerouteVegetationCameraMotion(
            ScriptExecutionContext context,
            CallbackArguments arguments)
        {
            RestoreRenderProducersInternal();
            Type type = typeof(
                AwesomeTechnologies.VegetationSystem.VegetationSystemPro);
            MethodInfo prefix = typeof(ReduxBetterAaTestApi).GetMethod(
                "RenderVegetationLodWithCameraMotion",
                StaticAny);
            MethodInfo[] methods = type.GetMethods(InstanceAny);
            int count = 0;
            for (int index = 0; index < methods.Length; index++)
            {
                MethodInfo method = methods[index];
                if (!string.Equals(
                    method.Name,
                    "RenderVegetationItemLODIndirect",
                    StringComparison.Ordinal))
                {
                    continue;
                }
                RenderSuppressionHarmony.Patch(
                    method,
                    new HarmonyMethod(prefix));
                SuppressedRenderMethods.Add(method);
                count++;
            }
            if (count == 0)
            {
                throw new MissingMethodException(
                    type.FullName,
                    "RenderVegetationItemLODIndirect");
            }
            _suppressedRenderProducerHits = 0;
            return DynValue.NewNumber(count);
        }

        private static DynValue SetVegetationMotionRepair(
            ScriptExecutionContext context,
            CallbackArguments arguments)
        {
            bool enabled = RequiredBoolean(
                arguments,
                0,
                "set_vegetation_motion_repair");
            Assembly assembly = RequireBetterAaAssembly();
            Type type = RequireType(
                assembly,
                "ReduxBetterAA.Rendering.VegetationMotionCompatibility");
            object service = RequireStaticField(type, "Current");
            RequireMethod(type, "SetEnabled", InstanceAny)
                .Invoke(service, new object[] { enabled });
            return DynValue.Nil;
        }

        private static DynValue SetMotionSanitizer(
            ScriptExecutionContext context,
            CallbackArguments arguments)
        {
            bool enabled = RequiredBoolean(
                arguments,
                0,
                "set_motion_sanitizer");
            Assembly assembly = RequireBetterAaAssembly();
            Type type = RequireType(
                assembly,
                "ReduxBetterAA.Rendering.TemporalCoordinator");
            object coordinator = RequireStaticField(type, "Current");
            RequireMethod(type, "SetMotionVectorSanitizerEnabled", InstanceAny)
                .Invoke(coordinator, new object[] { enabled });
            return DynValue.Nil;
        }

        private static DynValue SetDepthDisocclusionMask(
            ScriptExecutionContext context,
            CallbackArguments arguments)
        {
            bool enabled = RequiredBoolean(
                arguments,
                0,
                "set_depth_disocclusion_mask");
            Assembly assembly = RequireBetterAaAssembly();
            Type type = RequireType(
                assembly,
                "ReduxBetterAA.Rendering.TemporalCoordinator");
            object coordinator = RequireStaticField(type, "Current");
            RequireMethod(type, "SetDepthDisocclusionMaskEnabled", InstanceAny)
                .Invoke(coordinator, new object[] { enabled });
            return DynValue.Nil;
        }

        private static DynValue SetDlaaNoDepthBias(
            ScriptExecutionContext context,
            CallbackArguments arguments)
        {
            bool enabled = RequiredBoolean(
                arguments,
                0,
                "set_dlaa_no_depth_bias");
            Assembly assembly = RequireBetterAaAssembly();
            Type type = RequireType(
                assembly,
                "ReduxBetterAA.Rendering.TemporalCoordinator");
            object coordinator = RequireStaticField(type, "Current");
            RequireMethod(type, "SetDlaaNoDepthBiasEnabled", InstanceAny)
                .Invoke(coordinator, new object[] { enabled });
            return DynValue.Nil;
        }

        private static DynValue SetDlaaFullBias(
            ScriptExecutionContext context,
            CallbackArguments arguments)
        {
            bool enabled = RequiredBoolean(
                arguments,
                0,
                "set_dlaa_full_bias");
            Assembly assembly = RequireBetterAaAssembly();
            Type type = RequireType(
                assembly,
                "ReduxBetterAA.Rendering.TemporalCoordinator");
            object coordinator = RequireStaticField(type, "Current");
            RequireMethod(type, "SetDlaaFullBiasEnabled", InstanceAny)
                .Invoke(coordinator, new object[] { enabled });
            return DynValue.Nil;
        }

        private static DynValue SetDlaaExecutionBypass(
            ScriptExecutionContext context,
            CallbackArguments arguments)
        {
            bool enabled = RequiredBoolean(
                arguments,
                0,
                "set_dlaa_execution_bypass");
            Assembly assembly = RequireBetterAaAssembly();
            Type type = RequireType(
                assembly,
                "ReduxBetterAA.Rendering.TemporalCoordinator");
            object coordinator = RequireStaticField(type, "Current");
            RequireMethod(type, "SetDlaaExecutionBypass", InstanceAny)
                .Invoke(coordinator, new object[] { enabled });
            return DynValue.Nil;
        }

        private static DynValue SetDlaaTransparentProjectionJitter(
            ScriptExecutionContext context,
            CallbackArguments arguments)
        {
            bool enabled = RequiredBoolean(
                arguments,
                0,
                "set_dlaa_transparent_projection_jitter");
            Assembly assembly = RequireBetterAaAssembly();
            Type type = RequireType(
                assembly,
                "ReduxBetterAA.Rendering.TemporalCoordinator");
            object coordinator = RequireStaticField(type, "Current");
            RequireMethod(
                    type,
                    "SetDlaaTransparentProjectionJitter",
                    InstanceAny)
                .Invoke(coordinator, new object[] { enabled });
            return DynValue.Nil;
        }

        private static DynValue SetDlaaFlatColorInput(
            ScriptExecutionContext context,
            CallbackArguments arguments)
        {
            bool enabled = RequiredBoolean(
                arguments,
                0,
                "set_dlaa_flat_color_input");
            Assembly assembly = RequireBetterAaAssembly();
            Type type = RequireType(
                assembly,
                "ReduxBetterAA.Rendering.TemporalCoordinator");
            object coordinator = RequireStaticField(type, "Current");
            RequireMethod(type, "SetDlaaFlatColorInput", InstanceAny)
                .Invoke(coordinator, new object[] { enabled });
            return DynValue.Nil;
        }

        private static DynValue SetQualityShadows(
            ScriptExecutionContext context,
            CallbackArguments arguments)
        {
            bool enabled = RequiredBoolean(
                arguments,
                0,
                "set_quality_shadows");
            if (!_originalShadowQuality.HasValue)
            {
                _originalShadowQuality = QualitySettings.shadows;
            }
            if (enabled)
            {
                QualitySettings.shadows = _originalShadowQuality.Value;
                _originalShadowQuality = null;
            }
            else
            {
                QualitySettings.shadows = ShadowQuality.Disable;
            }
            return DynValue.Nil;
        }

        private static DynValue MotionInputSnapshot(Script script)
        {
            Assembly assembly = RequireBetterAaAssembly();
            Type vegetationType = RequireType(
                assembly,
                "ReduxBetterAA.Rendering.VegetationMotionCompatibility");
            object vegetation = RequireStaticField(vegetationType, "Current");
            Type coordinatorType = RequireType(
                assembly,
                "ReduxBetterAA.Rendering.TemporalCoordinator");
            object coordinator = RequireStaticField(coordinatorType, "Current");

            var result = new Table(script);
            result.Set(
                "vegetationRepairEnabled",
                DynValue.NewBoolean((bool)RequireProperty(
                    vegetationType,
                    "Enabled").GetValue(vegetation, null)));
            result.Set(
                "vegetationRepairAvailable",
                DynValue.NewBoolean((bool)RequireProperty(
                    vegetationType,
                    "Available").GetValue(vegetation, null)));
            result.Set(
                "vegetationRepairStatus",
                DynValue.NewString((string)RequireProperty(
                    vegetationType,
                    "Status").GetValue(vegetation, null)));
            result.Set(
                "vegetationReroutedCalls",
                DynValue.NewNumber(Convert.ToDouble(RequireProperty(
                    vegetationType,
                    "ReroutedCalls").GetValue(vegetation, null))));
            result.Set(
                "sanitizerEnabled",
                DynValue.NewBoolean((bool)RequireProperty(
                    coordinatorType,
                    "MotionVectorSanitizerEnabled").GetValue(
                        coordinator,
                        null)));
            result.Set(
                "sanitizerStatus",
                DynValue.NewString((string)RequireProperty(
                    coordinatorType,
                    "MotionVectorSanitizerStatus").GetValue(
                        coordinator,
                        null)));
            result.Set(
                "depthDisocclusionMaskEnabled",
                DynValue.NewBoolean((bool)RequireProperty(
                    coordinatorType,
                    "DepthDisocclusionMaskEnabled").GetValue(
                        coordinator,
                        null)));
            result.Set(
                "depthDisocclusionMaskStatus",
                DynValue.NewString((string)RequireProperty(
                    coordinatorType,
                    "DepthDisocclusionMaskStatus").GetValue(
                        coordinator,
                        null)));
            result.Set(
                "dlaaExecutionBypass",
                DynValue.NewBoolean((bool)RequireProperty(
                    coordinatorType,
                    "DlaaExecutionBypass").GetValue(
                        coordinator,
                        null)));
            result.Set(
                "dlaaFlatColorInputEnabled",
                DynValue.NewBoolean((bool)RequireProperty(
                    coordinatorType,
                    "DlaaFlatColorInputEnabled").GetValue(
                        coordinator,
                        null)));
            return DynValue.NewTable(result);
        }

        private static DynValue CloudRendererSnapshot(Script script)
        {
            object visualizer = RequireVisualizer(RequireBetterAaAssembly());
            Camera camera = (Camera)RequireMethod(
                    visualizer.GetType(),
                    "GetSelectedCamera",
                    InstanceAny)
                .Invoke(visualizer, null);
            if (camera == null)
            {
                throw new InvalidOperationException(
                    "Redux Better AA has no selected diagnostic camera.");
            }

            KSP.VolumeCloud.VolumeCloudRenderer renderer =
                camera.GetComponent<KSP.VolumeCloud.VolumeCloudRenderer>();
            if (renderer == null)
            {
                throw new InvalidOperationException(
                    "The selected camera has no VolumeCloudRenderer.");
            }

            var result = new Table(script);
            result.Set("camera", DynValue.NewString(camera.name));
            result.Set("enabled", DynValue.NewBoolean(renderer.enabled));
            FieldInfo[] fields = renderer.GetType().GetFields(InstanceAny);
            for (int index = 0; index < fields.Length; index++)
            {
                FieldInfo field = fields[index];
                object value;
                try
                {
                    value = field.GetValue(renderer);
                }
                catch (Exception exception)
                {
                    result.Set(
                        field.Name,
                        DynValue.NewString("<" + exception.GetType().Name + ">"));
                    continue;
                }
                result.Set(field.Name, DescribeRuntimeValue(script, value));
            }
            return DynValue.NewTable(result);
        }

        private static DynValue DlaaCloudTransitionSnapshot(Script script)
        {
            Assembly assembly = RequireBetterAaAssembly();
            Type coordinatorType = RequireType(
                assembly,
                "ReduxBetterAA.Rendering.TemporalCoordinator");
            object coordinator = RequireStaticField(coordinatorType, "Current");

            var result = new Table(script);
            result.Set(
                "resizeCount",
                DynValue.NewNumber(Convert.ToDouble(RequireProperty(
                    coordinatorType,
                    "DlaaCloudResizeCount").GetValue(coordinator, null))));
            result.Set(
                "compatibilityBypassActive",
                DynValue.NewBoolean((bool)RequireProperty(
                    coordinatorType,
                    "DlaaCloudCompatibilityBypassActive").GetValue(
                        coordinator,
                        null)));
            result.Set(
                "settleFramesRemaining",
                DynValue.NewNumber(Convert.ToDouble(RequireProperty(
                    coordinatorType,
                    "DlaaCloudSettleFramesRemaining").GetValue(
                        coordinator,
                        null))));
            result.Set(
                "renderWidth",
                DynValue.NewNumber(Convert.ToDouble(RequireProperty(
                    coordinatorType,
                    "DlaaCloudRenderWidth").GetValue(coordinator, null))));
            result.Set(
                "renderHeight",
                DynValue.NewNumber(Convert.ToDouble(RequireProperty(
                    coordinatorType,
                    "DlaaCloudRenderHeight").GetValue(coordinator, null))));
            return DynValue.NewTable(result);
        }

        private static DynValue CaptureCloudTexture(
            ScriptExecutionContext context,
            CallbackArguments arguments)
        {
            string fieldName = RequiredString(
                arguments,
                0,
                "capture_cloud_texture");
            string channel = RequiredString(
                arguments,
                1,
                "capture_cloud_texture").ToLowerInvariant();
            if (channel != "rgb" && channel != "alpha" && channel != "r" &&
                channel != "g" && channel != "b")
            {
                throw new ArgumentException(
                    "Cloud texture channel must be rgb, alpha, r, g, or b.");
            }

            object visualizer = RequireVisualizer(RequireBetterAaAssembly());
            Camera camera = (Camera)RequireMethod(
                    visualizer.GetType(),
                    "GetSelectedCamera",
                    InstanceAny)
                .Invoke(visualizer, null);
            if (camera == null)
            {
                throw new InvalidOperationException(
                    "Redux Better AA has no selected diagnostic camera.");
            }

            KSP.VolumeCloud.VolumeCloudRenderer renderer =
                camera.GetComponent<KSP.VolumeCloud.VolumeCloudRenderer>();
            if (renderer == null)
            {
                throw new InvalidOperationException(
                    "The selected camera has no VolumeCloudRenderer.");
            }

            FieldInfo field = renderer.GetType().GetField(fieldName, InstanceAny);
            if (field == null)
            {
                throw new MissingFieldException(renderer.GetType().FullName, fieldName);
            }
            RenderTexture source = field.GetValue(renderer) as RenderTexture;
            if (source == null || !source.IsCreated())
            {
                throw new InvalidOperationException(
                    "Cloud field " + fieldName + " is not a created RenderTexture.");
            }

            RenderTexture previous = RenderTexture.active;
            Texture2D readable = null;
            try
            {
                RenderTexture.active = source;
                readable = new Texture2D(
                    source.width,
                    source.height,
                    TextureFormat.RGBA32,
                    false,
                    true);
                readable.ReadPixels(
                    new Rect(0.0f, 0.0f, source.width, source.height),
                    0,
                    0,
                    false);
                readable.Apply(false, false);

                Color32[] pixels = readable.GetPixels32();
                for (int index = 0; index < pixels.Length; index++)
                {
                    Color32 pixel = pixels[index];
                    byte value;
                    switch (channel)
                    {
                        case "alpha": value = pixel.a; break;
                        case "r": value = pixel.r; break;
                        case "g": value = pixel.g; break;
                        case "b": value = pixel.b; break;
                        default:
                            pixel.a = 255;
                            pixels[index] = pixel;
                            continue;
                    }
                    pixels[index] = new Color32(value, value, value, 255);
                }
                readable.SetPixels32(pixels);
                readable.Apply(false, false);

                string path = Path.Combine(
                    Path.GetTempPath(),
                    "redux-cloud-" + fieldName.TrimStart('_') + "-" +
                    channel + "-" + Guid.NewGuid().ToString("N") + ".png");
                File.WriteAllBytes(path, readable.EncodeToPNG());
                return DynValue.NewString(path);
            }
            finally
            {
                RenderTexture.active = previous;
                if (readable != null)
                {
                    UnityEngine.Object.Destroy(readable);
                }
            }
        }

        private static DynValue DescribeRuntimeValue(Script script, object value)
        {
            if (value == null)
            {
                return DynValue.Nil;
            }
            if (value is bool boolean)
            {
                return DynValue.NewBoolean(boolean);
            }
            if (value is string text)
            {
                return DynValue.NewString(text);
            }
            Type type = value.GetType();
            if (type.IsEnum)
            {
                return DynValue.NewString(value.ToString());
            }
            if (value is byte || value is sbyte || value is short ||
                value is ushort || value is int || value is uint ||
                value is long || value is ulong || value is float ||
                value is double || value is decimal)
            {
                return DynValue.NewNumber(Convert.ToDouble(value));
            }
            if (value is Texture texture)
            {
                var textureResult = new Table(script);
                textureResult.Set("type", DynValue.NewString(type.FullName));
                textureResult.Set("name", DynValue.NewString(texture.name));
                textureResult.Set("width", DynValue.NewNumber(texture.width));
                textureResult.Set("height", DynValue.NewNumber(texture.height));
                if (texture is RenderTexture renderTexture)
                {
                    textureResult.Set(
                        "format",
                        DynValue.NewString(renderTexture.format.ToString()));
                    textureResult.Set(
                        "graphicsFormat",
                        DynValue.NewString(
                            renderTexture.graphicsFormat.ToString()));
                    textureResult.Set(
                        "created",
                        DynValue.NewBoolean(renderTexture.IsCreated()));
                }
                return DynValue.NewTable(textureResult);
            }
            if (value is Material material)
            {
                var materialResult = new Table(script);
                materialResult.Set("type", DynValue.NewString(type.FullName));
                materialResult.Set("name", DynValue.NewString(material.name));
                materialResult.Set(
                    "shader",
                    DynValue.NewString(
                        material.shader == null
                            ? string.Empty
                            : material.shader.name));
                return DynValue.NewTable(materialResult);
            }
            if (value is CommandBuffer commandBuffer)
            {
                return DynValue.NewString(
                    "CommandBuffer:" + commandBuffer.name);
            }
            if (value is UnityEngine.Object unityObject)
            {
                return DynValue.NewString(
                    type.FullName + ":" + unityObject.name);
            }
            if (value is System.Collections.ICollection collection)
            {
                return DynValue.NewString(
                    type.FullName + " Count=" + collection.Count);
            }
            return DynValue.NewString(type.FullName + ":" + value);
        }

        private static DynValue VegetationRenderSnapshot(Script script)
        {
            var result = new Table(script);
            for (int index = 0; index < VegetationDrawRecords.Count; index++)
            {
                VegetationDrawRecord record = VegetationDrawRecords[index];
                var item = new Table(script);
                item.Set("itemId", DynValue.NewString(record.ItemId));
                item.Set("name", DynValue.NewString(record.Name));
                item.Set("model", DynValue.NewString(record.Model));
                item.Set("mesh", DynValue.NewString(record.Mesh));
                item.Set("materials", DynValue.NewString(record.Materials));
                item.Set("lodIndex", DynValue.NewNumber(record.LodIndex));
                item.Set("calls", DynValue.NewNumber(record.Calls));
                item.Set(
                    "directGraphicsCalls",
                    DynValue.NewNumber(record.DirectGraphicsCalls));
                item.Set(
                    "commandBufferCalls",
                    DynValue.NewNumber(record.CommandBufferCalls));
                item.Set("shadowCalls", DynValue.NewNumber(record.ShadowCalls));
                item.Set("camera", DynValue.NewString(record.Camera));
                item.Set(
                    "firstBounds",
                    DynValue.NewString(record.FirstBounds.ToString("F3")));
                result.Set(index + 1, DynValue.NewTable(item));
            }
            result.Set("count", DynValue.NewNumber(VegetationDrawRecords.Count));
            return DynValue.NewTable(result);
        }

        private static DynValue IsolateVegetationItem(
            ScriptExecutionContext context,
            CallbackArguments arguments)
        {
            string itemId = RequiredString(
                arguments,
                0,
                "isolate_vegetation_item");
            return IsolateVegetationItemInternal(itemId, false);
        }

        private static DynValue IsolateVegetationItemFirstMaterial(
            ScriptExecutionContext context,
            CallbackArguments arguments)
        {
            string itemId = RequiredString(
                arguments,
                0,
                "isolate_vegetation_item_first_material");
            return IsolateVegetationItemInternal(itemId, true);
        }

        private static DynValue IsolateVegetationItemInternal(
            string itemId,
            bool firstMaterialOnly)
        {
            RestoreRenderProducersInternal();
            _isolatedVegetationItemId = itemId;
            _isolateVegetationFirstMaterial = firstMaterialOnly;
            _suppressedRenderProducerHits = 0;
            MethodInfo prefix = typeof(ReduxBetterAaTestApi).GetMethod(
                "RenderOnlySelectedVegetationItem",
                StaticAny);
            MethodInfo[] methods = typeof(
                    AwesomeTechnologies.VegetationSystem.VegetationSystemPro)
                .GetMethods(InstanceAny);
            int count = 0;
            for (int index = 0; index < methods.Length; index++)
            {
                MethodInfo method = methods[index];
                if (!string.Equals(
                    method.Name,
                    "RenderVegetationItemLODIndirect",
                    StringComparison.Ordinal))
                {
                    continue;
                }
                RenderSuppressionHarmony.Patch(
                    method,
                    new HarmonyMethod(prefix));
                SuppressedRenderMethods.Add(method);
                count++;
            }
            if (count == 0)
            {
                throw new MissingMethodException(
                    typeof(AwesomeTechnologies.VegetationSystem
                        .VegetationSystemPro).FullName,
                    "RenderVegetationItemLODIndirect");
            }
            return DynValue.NewNumber(count);
        }

        private static void RestoreRenderProducersInternal()
        {
            RestoreVegetationMaterialsInternal();
            for (int index = 0; index < SuppressedRenderMethods.Count; index++)
            {
                RenderSuppressionHarmony.Unpatch(
                    SuppressedRenderMethods[index],
                    HarmonyPatchType.Prefix,
                    RenderSuppressionHarmonyId);
            }
            SuppressedRenderMethods.Clear();
            _suppressedRenderProducerHits = 0;
            _isolatedVegetationItemId = null;
            _isolateVegetationFirstMaterial = false;
        }

        private static bool SkipManagedRenderProducer()
        {
            _suppressedRenderProducerHits++;
            return false;
        }

        private static bool CaptureAndSkipVegetationLod(
            AwesomeTechnologies.VegetationSystem.VegetationItemModelInfo
                vegetationItemModelInfo,
            Bounds cellBounds,
            int lodIndex,
            Camera selectedCamera,
            bool shadows,
            CommandBuffer commandBuffer)
        {
            _suppressedRenderProducerHits++;
            if (vegetationItemModelInfo == null)
            {
                return false;
            }

            AwesomeTechnologies.VegetationSystem.VegetationItemInfoPro info =
                vegetationItemModelInfo.VegetationItemInfo;
            string itemId = info != null ? info.VegetationItemID : string.Empty;
            string itemName = info != null ? info.Name : string.Empty;
            for (int index = 0; index < VegetationDrawRecords.Count; index++)
            {
                VegetationDrawRecord existing = VegetationDrawRecords[index];
                if (existing.LodIndex == lodIndex &&
                    string.Equals(existing.ItemId, itemId, StringComparison.Ordinal))
                {
                    existing.Calls++;
                    if (commandBuffer == null)
                    {
                        existing.DirectGraphicsCalls++;
                    }
                    else
                    {
                        existing.CommandBufferCalls++;
                    }
                    if (shadows)
                    {
                        existing.ShadowCalls++;
                    }
                    return false;
                }
            }

            Mesh mesh = vegetationItemModelInfo.GetLODMesh(lodIndex);
            Material[] materials =
                vegetationItemModelInfo.GetLODMaterials(lodIndex);
            VegetationDrawRecords.Add(new VegetationDrawRecord
            {
                ItemId = itemId ?? string.Empty,
                Name = itemName ?? string.Empty,
                Model = vegetationItemModelInfo.VegetationModel != null
                    ? vegetationItemModelInfo.VegetationModel.name
                    : string.Empty,
                Mesh = mesh != null ? mesh.name : string.Empty,
                Materials = DescribeMaterials(materials),
                LodIndex = lodIndex,
                Calls = 1,
                DirectGraphicsCalls = commandBuffer == null ? 1 : 0,
                CommandBufferCalls = commandBuffer != null ? 1 : 0,
                ShadowCalls = shadows ? 1 : 0,
                Camera = selectedCamera != null ? selectedCamera.name : string.Empty,
                FirstBounds = cellBounds
            });
            return false;
        }

        private static bool RenderVegetationLodWithCameraMotion(
            AwesomeTechnologies.VegetationSystem.VegetationItemModelInfo
                vegetationItemModelInfo,
            Bounds cellBounds,
            int cameraIndex,
            int lodIndex,
            Camera selectedCamera,
            ShadowCastingMode shadowCastingMode,
            int layer,
            bool shadows,
            CommandBuffer commandBuffer,
            int ____visibleShaderDataBufferID,
            int ____indirectShaderDataBufferID)
        {
            // The command-buffer branch selects explicit material passes and is
            // outside this prototype. Runtime evidence at the failing flight
            // camera shows the affected draws use the direct Graphics branch.
            if (commandBuffer != null || vegetationItemModelInfo == null)
            {
                return true;
            }

            _suppressedRenderProducerHits++;
            MaterialPropertyBlock properties =
                vegetationItemModelInfo.GetLODMaterialPropertyBlock(lodIndex);
            properties.Clear();
            GraphicsBuffer visibleBuffer =
                vegetationItemModelInfo.GetLODVisibleBuffer(
                    lodIndex,
                    cameraIndex,
                    shadows);
            Mesh mesh = vegetationItemModelInfo.GetLODMesh(lodIndex);
            Material[] materials =
                vegetationItemModelInfo.GetLODMaterials(lodIndex);

            if (vegetationItemModelInfo.ShaderControler != null &&
                vegetationItemModelInfo.ShaderControler.Settings.SampleWind)
            {
                MeshRenderer windSampler =
                    vegetationItemModelInfo.WindSamplerMeshRendererList[
                        cameraIndex];
                if (windSampler != null)
                {
                    windSampler.GetPropertyBlock(properties);
                }
            }

            properties.SetBuffer(
                ____visibleShaderDataBufferID,
                visibleBuffer);
            properties.SetBuffer(
                ____indirectShaderDataBufferID,
                visibleBuffer);
            List<GraphicsBuffer> argumentBuffers =
                vegetationItemModelInfo.GetLODArgsBufferList(
                    lodIndex,
                    cameraIndex,
                    shadows);
            int drawCount = Mathf.Min(mesh.subMeshCount, materials.Length);
            for (int materialIndex = 0;
                materialIndex < drawCount;
                materialIndex++)
            {
                var renderParams = new RenderParams(materials[materialIndex])
                {
                    camera = selectedCamera,
                    layer = layer,
                    lightProbeUsage = LightProbeUsage.Off,
                    matProps = properties,
                    motionVectorMode = MotionVectorGenerationMode.Camera,
                    receiveShadows = true,
                    shadowCastingMode = shadowCastingMode,
                    worldBounds = cellBounds
                };
                Graphics.RenderMeshIndirect(
                    renderParams,
                    mesh,
                    argumentBuffers[materialIndex],
                    1,
                    0);
            }
            return false;
        }

        [HarmonyPriority(Priority.First)]
        private static bool RenderOnlySelectedVegetationItem(
            AwesomeTechnologies.VegetationSystem.VegetationItemModelInfo
                vegetationItemModelInfo)
        {
            _suppressedRenderProducerHits++;
            if (vegetationItemModelInfo == null ||
                vegetationItemModelInfo.VegetationItemInfo == null)
            {
                return false;
            }
            bool selected = string.Equals(
                vegetationItemModelInfo.VegetationItemInfo.VegetationItemID,
                _isolatedVegetationItemId,
                StringComparison.Ordinal);
            if (selected && _isolateVegetationFirstMaterial)
            {
                IsolateFirstVegetationMaterial(vegetationItemModelInfo);
            }
            return selected;
        }

        private static void IsolateFirstVegetationMaterial(
            AwesomeTechnologies.VegetationSystem.VegetationItemModelInfo item)
        {
            if (_materialIsolatedVegetationItem == item)
            {
                return;
            }
            RestoreVegetationMaterialsInternal();
            _materialIsolatedVegetationItem = item;
            _originalVegetationMaterialsLod0 = item.VegetationMaterialsLOD0;
            _originalVegetationMaterialsLod1 = item.VegetationMaterialsLOD1;
            _originalVegetationMaterialsLod2 = item.VegetationMaterialsLOD2;
            _originalVegetationMaterialsLod3 = item.VegetationMaterialsLOD3;
            item.VegetationMaterialsLOD0 = FirstMaterialOnly(
                _originalVegetationMaterialsLod0);
            item.VegetationMaterialsLOD1 = FirstMaterialOnly(
                _originalVegetationMaterialsLod1);
            item.VegetationMaterialsLOD2 = FirstMaterialOnly(
                _originalVegetationMaterialsLod2);
            item.VegetationMaterialsLOD3 = FirstMaterialOnly(
                _originalVegetationMaterialsLod3);
        }

        private static Material[] FirstMaterialOnly(Material[] materials)
        {
            if (materials == null || materials.Length <= 1)
            {
                return materials;
            }
            return new[] { materials[0] };
        }

        private static void RestoreVegetationMaterialsInternal()
        {
            if (_materialIsolatedVegetationItem != null)
            {
                _materialIsolatedVegetationItem.VegetationMaterialsLOD0 =
                    _originalVegetationMaterialsLod0;
                _materialIsolatedVegetationItem.VegetationMaterialsLOD1 =
                    _originalVegetationMaterialsLod1;
                _materialIsolatedVegetationItem.VegetationMaterialsLOD2 =
                    _originalVegetationMaterialsLod2;
                _materialIsolatedVegetationItem.VegetationMaterialsLOD3 =
                    _originalVegetationMaterialsLod3;
            }
            _materialIsolatedVegetationItem = null;
            _originalVegetationMaterialsLod0 = null;
            _originalVegetationMaterialsLod1 = null;
            _originalVegetationMaterialsLod2 = null;
            _originalVegetationMaterialsLod3 = null;
        }

        private static string DescribeMaterials(Material[] materials)
        {
            if (materials == null || materials.Length == 0)
            {
                return string.Empty;
            }
            var builder = new StringBuilder();
            for (int index = 0; index < materials.Length; index++)
            {
                if (index > 0)
                {
                    builder.Append(" | ");
                }
                Material material = materials[index];
                if (material == null)
                {
                    builder.Append("<null>");
                    continue;
                }
                builder.Append(material.name);
                builder.Append(" [");
                builder.Append(material.shader != null
                    ? material.shader.name
                    : "no shader");
                builder.Append("; queue=");
                builder.Append(material.renderQueue);
                builder.Append("; instancing=");
                builder.Append(material.enableInstancing ? "on" : "off");
                builder.Append("; renderType=");
                builder.Append(material.GetTag("RenderType", false, string.Empty));
                if (material.shader != null)
                {
                    builder.Append("; passes=");
                    for (int passIndex = 0;
                        passIndex < material.passCount;
                        passIndex++)
                    {
                        if (passIndex > 0)
                        {
                            builder.Append(',');
                        }
                        string passName = material.GetPassName(passIndex);
                        builder.Append(passName);
                        builder.Append(material.GetShaderPassEnabled(passName)
                            ? "+"
                            : "-");
                    }
                }
                builder.Append(']');
            }
            return builder.ToString();
        }

        private static DynValue SetMotionPassProbe(
            ScriptExecutionContext context,
            CallbackArguments arguments)
        {
            if (arguments.Count < 1 || arguments[0].Type != DataType.Number)
            {
                throw new ScriptRuntimeException(
                    "ReduxBetterAA.set_motion_pass_probe requires a numeric mode.");
            }
            int mode = (int)arguments[0].Number;
            object visualizer = RequireVisualizer(RequireBetterAaAssembly());
            object[] callArguments = { mode, null };
            bool success = (bool)RequireMethod(
                    visualizer.GetType(),
                    "TrySetMotionVectorPassProbe",
                    InstanceAny)
                .Invoke(visualizer, callArguments);
            if (!success)
            {
                throw new InvalidOperationException(
                    "Motion-vector pass probe unavailable: " +
                    Convert.ToString(callArguments[1]));
            }
            return DynValue.NewNumber(mode);
        }

        private static DynValue RestoreMotionPassProbe(
            ScriptExecutionContext context,
            CallbackArguments arguments)
        {
            RestoreMotionPassProbeInternal();
            return DynValue.True;
        }

        private static void RestoreMotionPassProbeInternal()
        {
            Assembly assembly = FindBetterAaAssembly();
            if (assembly == null)
            {
                return;
            }
            try
            {
                object visualizer = RequireVisualizer(assembly);
                RequireMethod(
                        visualizer.GetType(),
                        "RestoreMotionVectorPassProbe",
                        InstanceAny)
                    .Invoke(visualizer, null);
            }
            catch (InvalidOperationException)
            {
                // The feature mod may already be shutting down.
            }
        }

        private static DynValue SceneObjectsAtCamera(Script script)
        {
            object visualizer = RequireVisualizer(RequireBetterAaAssembly());
            Camera camera = (Camera)RequireMethod(
                    visualizer.GetType(),
                    "GetSelectedCamera",
                    InstanceAny)
                .Invoke(visualizer, null);
            if (camera == null)
            {
                throw new InvalidOperationException(
                    "Redux Better AA has no selected diagnostic camera.");
            }

            var result = new Table(script);
            Transform[] transforms = Resources.FindObjectsOfTypeAll<Transform>();
            int outputIndex = 1;
            for (int index = 0; index < transforms.Length; index++)
            {
                Transform transform = transforms[index];
                if (transform == null || !transform.gameObject.activeInHierarchy ||
                    Vector3.Distance(
                        transform.position,
                        camera.transform.position) > 0.1f)
                {
                    continue;
                }

                var item = new Table(script);
                item.Set("path", DynValue.NewString(HierarchyPath(transform)));
                item.Set("layer", DynValue.NewNumber(transform.gameObject.layer));
                item.Set("position", DynValue.NewString(
                    transform.position.ToString("F6")));
                item.Set("components", DynValue.NewString(
                    DescribeComponents(transform.gameObject)));
                Renderer renderer = transform.GetComponent<Renderer>();
                if (renderer != null)
                {
                    item.Set("rendererType", DynValue.NewString(
                        renderer.GetType().FullName));
                    item.Set("motionMode", DynValue.NewString(
                        renderer.motionVectorGenerationMode.ToString()));
                    item.Set("bounds", DynValue.NewString(
                        renderer.bounds.ToString("F4")));
                }
                MeshFilter filter = transform.GetComponent<MeshFilter>();
                if (filter != null && filter.sharedMesh != null)
                {
                    item.Set("mesh", DynValue.NewString(filter.sharedMesh.name));
                    item.Set("meshVertices", DynValue.NewNumber(
                        filter.sharedMesh.vertexCount));
                }
                result.Set(outputIndex++, DynValue.NewTable(item));
            }
            result.Set("count", DynValue.NewNumber(outputIndex - 1));
            result.Set("camera", DynValue.NewString(camera.name));
            result.Set("cameraPosition", DynValue.NewString(
                camera.transform.position.ToString("F6")));
            return DynValue.NewTable(result);
        }

        private static string HierarchyPath(Transform transform)
        {
            var builder = new StringBuilder(transform.name);
            Transform parent = transform.parent;
            while (parent != null)
            {
                builder.Insert(0, '/');
                builder.Insert(0, parent.name);
                parent = parent.parent;
            }
            return builder.ToString();
        }

        private static string DescribeComponents(GameObject gameObject)
        {
            Component[] components = gameObject.GetComponents<Component>();
            var builder = new StringBuilder(256);
            for (int index = 0; index < components.Length; index++)
            {
                if (index > 0)
                {
                    builder.Append(" | ");
                }
                Component component = components[index];
                builder.Append(component == null
                    ? "<missing>"
                    : component.GetType().FullName);
            }
            return builder.ToString();
        }

        private static DynValue SuppressCameraCommandBuffers(
            ScriptExecutionContext context,
            CallbackArguments arguments)
        {
            RestoreCameraCommandBuffersInternal();
            object visualizer = RequireVisualizer(RequireBetterAaAssembly());
            Camera camera = (Camera)RequireMethod(
                    visualizer.GetType(),
                    "GetSelectedCamera",
                    InstanceAny)
                .Invoke(visualizer, null);
            if (camera == null)
            {
                throw new InvalidOperationException(
                    "Redux Better AA has no selected diagnostic camera.");
            }

            string filter = arguments.Count > 0 &&
                arguments[0].Type == DataType.String
                    ? arguments[0].String
                    : string.Empty;
            _suppressedCommandBufferCamera = camera;
            Array events = Enum.GetValues(typeof(CameraEvent));
            for (int eventIndex = 0; eventIndex < events.Length; eventIndex++)
            {
                CameraEvent cameraEvent = (CameraEvent)events.GetValue(eventIndex);
                CommandBuffer[] buffers = camera.GetCommandBuffers(cameraEvent);
                for (int bufferIndex = 0; bufferIndex < buffers.Length; bufferIndex++)
                {
                    CommandBuffer buffer = buffers[bufferIndex];
                    if (buffer == null || string.Equals(
                        buffer.name,
                        "Redux Better AA Phase 1 Debug View",
                        StringComparison.Ordinal))
                    {
                        continue;
                    }
                    if (!string.IsNullOrEmpty(filter) &&
                        (buffer.name == null || buffer.name.IndexOf(
                            filter,
                            StringComparison.OrdinalIgnoreCase) < 0))
                    {
                        continue;
                    }
                    camera.RemoveCommandBuffer(cameraEvent, buffer);
                    SuppressedCommandBufferEvents.Add(cameraEvent);
                    SuppressedCommandBuffers.Add(buffer);
                }
            }
            return DynValue.NewNumber(SuppressedCommandBuffers.Count);
        }

        private static DynValue RestoreCameraCommandBuffers(
            ScriptExecutionContext context,
            CallbackArguments arguments)
        {
            int count = SuppressedCommandBuffers.Count;
            RestoreCameraCommandBuffersInternal();
            return DynValue.NewNumber(count);
        }

        private static void RestoreCameraCommandBuffersInternal()
        {
            if (_suppressedCommandBufferCamera != null)
            {
                for (int index = 0; index < SuppressedCommandBuffers.Count; index++)
                {
                    CommandBuffer buffer = SuppressedCommandBuffers[index];
                    if (buffer != null)
                    {
                        _suppressedCommandBufferCamera.AddCommandBuffer(
                            SuppressedCommandBufferEvents[index],
                            buffer);
                    }
                }
            }
            SuppressedCommandBufferEvents.Clear();
            SuppressedCommandBuffers.Clear();
            _suppressedCommandBufferCamera = null;
        }

        private static DynValue PqsSnapshot(Script script)
        {
            Type type = typeof(KSP.Rendering.Planets.PQSRenderer);
            FieldInfo sourceCameraField = type.GetField(
                "SourceCamera",
                InstanceAny);
            UnityEngine.Object[] renderers = Resources.FindObjectsOfTypeAll(type);
            var table = new Table(script);
            int outputIndex = 1;
            for (int index = 0; index < renderers.Length; index++)
            {
                Behaviour renderer = renderers[index] as Behaviour;
                if (renderer == null || !renderer.gameObject.activeInHierarchy)
                {
                    continue;
                }
                Camera source = sourceCameraField == null
                    ? null
                    : sourceCameraField.GetValue(renderer) as Camera;
                var item = new Table(script);
                item.Set("name", DynValue.NewString(renderer.name));
                item.Set("enabled", DynValue.NewBoolean(renderer.enabled));
                item.Set(
                    "sourceCamera",
                    DynValue.NewString(source == null ? string.Empty : source.name));
                item.Set(
                    "sourceFov",
                    DynValue.NewNumber(source == null ? 0.0 : source.fieldOfView));
                item.Set(
                    "sourcePosition",
                    DynValue.NewString(source == null
                        ? string.Empty
                        : source.transform.position.ToString("F6")));
                item.Set(
                    "sourceProjection",
                    DynValue.NewString(source == null
                        ? string.Empty
                        : source.projectionMatrix.ToString("F6")));
                table.Set(outputIndex++, DynValue.NewTable(item));
            }
            table.Set("count", DynValue.NewNumber(outputIndex - 1));
            return DynValue.NewTable(table);
        }

        private static DynValue ScaledPlanetSnapshot(Script script)
        {
            GameObject root = FindMapScaledPlanet();
            Camera camera = GetSelectedDiagnosticCamera();
            var table = new Table(script);
            table.Set("source", DynValue.NewString(
                root == null ? "selected-camera-layers" : "Map3DView"));
            table.Set("camera", DynValue.NewString(
                camera == null ? string.Empty : camera.name));

            if (root != null)
            {
                table.Set("root", DynValue.NewString(HierarchyPath(root.transform)));
                table.Set("rootLayer", DynValue.NewNumber(root.layer));
                table.Set("rootPosition", DynValue.NewString(
                    root.transform.position.ToString("F6")));
                table.Set("rootRotation", DynValue.NewString(
                    root.transform.rotation.eulerAngles.ToString("F6")));
                table.Set("rootScale", DynValue.NewString(
                    root.transform.lossyScale.ToString("F6")));
                table.Set("rootMatrix", DynValue.NewString(
                    root.transform.localToWorldMatrix.ToString("F6")));
            }

            Renderer[] renderers = root == null
                ? Resources.FindObjectsOfTypeAll<Renderer>()
                : root.GetComponentsInChildren<Renderer>(true);
            var rendererTable = new Table(script);
            int outputIndex = 1;
            for (int index = 0; index < renderers.Length; index++)
            {
                Renderer renderer = renderers[index];
                if (renderer == null || !renderer.gameObject.activeInHierarchy)
                {
                    continue;
                }
                if (root == null && (camera == null ||
                    (camera.cullingMask & (1 << renderer.gameObject.layer)) == 0))
                {
                    continue;
                }
                if (outputIndex > 128)
                {
                    break;
                }

                var item = new Table(script);
                item.Set("path", DynValue.NewString(
                    HierarchyPath(renderer.transform)));
                item.Set("type", DynValue.NewString(
                    renderer.GetType().FullName ?? renderer.GetType().Name));
                item.Set("enabled", DynValue.NewBoolean(renderer.enabled));
                item.Set("layer", DynValue.NewNumber(renderer.gameObject.layer));
                item.Set("motionMode", DynValue.NewString(
                    renderer.motionVectorGenerationMode.ToString()));
                item.Set("position", DynValue.NewString(
                    renderer.transform.position.ToString("F6")));
                item.Set("rotation", DynValue.NewString(
                    renderer.transform.rotation.eulerAngles.ToString("F6")));
                item.Set("scale", DynValue.NewString(
                    renderer.transform.lossyScale.ToString("F6")));
                item.Set("matrix", DynValue.NewString(
                    renderer.transform.localToWorldMatrix.ToString("F6")));
                item.Set("bounds", DynValue.NewString(
                    renderer.bounds.ToString("F4")));
                item.Set("materials", DynValue.NewString(
                    DescribeRendererMaterials(renderer)));
                MeshFilter filter = renderer.GetComponent<MeshFilter>();
                if (filter != null && filter.sharedMesh != null)
                {
                    item.Set("mesh", DynValue.NewString(filter.sharedMesh.name));
                    item.Set("meshVertices", DynValue.NewNumber(
                        filter.sharedMesh.vertexCount));
                }
                rendererTable.Set(outputIndex++, DynValue.NewTable(item));
            }
            rendererTable.Set("count", DynValue.NewNumber(outputIndex - 1));
            table.Set("renderers", DynValue.NewTable(rendererTable));
            return DynValue.NewTable(table);
        }

        private static DynValue SkyboxSnapshot(Script script)
        {
            Camera camera = GetSelectedDiagnosticCamera();
            if (camera == null)
            {
                throw new InvalidOperationException(
                    "Redux Better AA has no selected diagnostic camera.");
            }

            var table = new Table(script);
            table.Set("camera", DynValue.NewString(camera.name));
            UnityEngine.Skybox skybox =
                camera.GetComponent<UnityEngine.Skybox>();
            Material material = skybox == null ? null : skybox.material;
            table.Set("componentPresent", DynValue.NewBoolean(skybox != null));
            table.Set("componentEnabled", DynValue.NewBoolean(
                skybox != null && skybox.enabled));
            table.Set("material", DynValue.NewString(
                material == null ? string.Empty : material.name));
            table.Set("shader", DynValue.NewString(
                material == null || material.shader == null
                    ? string.Empty
                    : material.shader.name));

            var properties = new Table(script);
            int outputIndex = 1;
            Shader shader = material == null ? null : material.shader;
            if (shader != null)
            {
                int propertyCount = shader.GetPropertyCount();
                for (int index = 0; index < propertyCount; index++)
                {
                    string name = shader.GetPropertyName(index);
                    ShaderPropertyType propertyType = shader.GetPropertyType(index);
                    var item = new Table(script);
                    item.Set("name", DynValue.NewString(name));
                    item.Set("type", DynValue.NewString(propertyType.ToString()));
                    if (propertyType == ShaderPropertyType.Texture)
                    {
                        Texture texture = material.GetTexture(name);
                        item.Set("value", DynValue.NewString(
                            DescribeTexture(texture)));
                    }
                    else if (propertyType == ShaderPropertyType.Float ||
                        propertyType == ShaderPropertyType.Range)
                    {
                        item.Set("value", DynValue.NewString(
                            material.GetFloat(name).ToString("R")));
                    }
                    else if (propertyType == ShaderPropertyType.Vector ||
                        propertyType == ShaderPropertyType.Color)
                    {
                        item.Set("value", DynValue.NewString(
                            material.GetVector(name).ToString("F6")));
                    }
                    properties.Set(outputIndex++, DynValue.NewTable(item));
                }
            }
            properties.Set("count", DynValue.NewNumber(outputIndex - 1));
            table.Set("properties", DynValue.NewTable(properties));
            return DynValue.NewTable(table);
        }

        private static string DescribeTexture(Texture texture)
        {
            if (texture == null)
            {
                return "<null>";
            }
            return texture.name + " type=" + texture.GetType().FullName +
                " size=" + texture.width + "x" + texture.height +
                " dimension=" + texture.dimension;
        }

        private static DynValue SetScaledPlanetMotionMode(
            ScriptExecutionContext context,
            CallbackArguments arguments)
        {
            string requested = RequiredString(
                arguments,
                0,
                "set_scaled_planet_motion_mode");
            MotionVectorGenerationMode mode;
            if (!Enum.TryParse(requested, true, out mode))
            {
                throw new ArgumentException(
                    "Unknown motion-vector generation mode '" + requested + "'.");
            }

            RestoreScaledPlanetMotionModeInternal();
            GameObject root = FindMapScaledPlanet();
            if (root == null)
            {
                throw new InvalidOperationException(
                    "No active Map3DView scaled-planet instance was found.");
            }
            Renderer[] renderers = root.GetComponentsInChildren<Renderer>(true);
            for (int index = 0; index < renderers.Length; index++)
            {
                Renderer renderer = renderers[index];
                if (renderer == null)
                {
                    continue;
                }
                ScaledPlanetRenderers.Add(renderer);
                ScaledPlanetModes.Add(renderer.motionVectorGenerationMode);
                renderer.motionVectorGenerationMode = mode;
            }
            return DynValue.NewNumber(ScaledPlanetRenderers.Count);
        }

        private static DynValue RestoreScaledPlanetMotionMode(
            ScriptExecutionContext context,
            CallbackArguments arguments)
        {
            int count = ScaledPlanetRenderers.Count;
            RestoreScaledPlanetMotionModeInternal();
            return DynValue.NewNumber(count);
        }

        private static DynValue SetScaledPlanetMotionProbe(
            ScriptExecutionContext context,
            CallbackArguments arguments)
        {
            if (arguments.Count < 1 || arguments[0].Type != DataType.Number)
            {
                throw new ScriptRuntimeException(
                    "ReduxBetterAA.set_scaled_planet_motion_probe requires a numeric mode.");
            }
            float value = (float)arguments[0].Number;
            Shader.SetGlobalFloat(ScaledPlanetMotionProbe, value);
            return DynValue.NewNumber(value);
        }

        private static void RestoreScaledPlanetMotionModeInternal()
        {
            int count = Math.Min(
                ScaledPlanetRenderers.Count,
                ScaledPlanetModes.Count);
            for (int index = 0; index < count; index++)
            {
                Renderer renderer = ScaledPlanetRenderers[index];
                if (renderer != null)
                {
                    renderer.motionVectorGenerationMode = ScaledPlanetModes[index];
                }
            }
            ScaledPlanetRenderers.Clear();
            ScaledPlanetModes.Clear();
        }

        private static GameObject FindMapScaledPlanet()
        {
            Type type = typeof(KSP.Map.Map3DView);
            FieldInfo field = type.GetField(
                "scaledSpaceCelestialBodyInstance",
                InstanceAny);
            if (field == null)
            {
                return null;
            }
            UnityEngine.Object[] views = Resources.FindObjectsOfTypeAll(type);
            for (int index = 0; index < views.Length; index++)
            {
                Behaviour view = views[index] as Behaviour;
                if (view == null || !view.gameObject.activeInHierarchy)
                {
                    continue;
                }
                GameObject root = field.GetValue(view) as GameObject;
                if (root != null && root.activeInHierarchy)
                {
                    return root;
                }
            }

            // KSP2 0.2.3 leaves the Map3DView field unset after moving the
            // instantiated body under its focus item. Identify the concrete
            // surface renderer the same way the frame does instead.
            Renderer[] renderers = Resources.FindObjectsOfTypeAll<Renderer>();
            for (int index = 0; index < renderers.Length; index++)
            {
                Renderer renderer = renderers[index];
                if (renderer == null || !renderer.gameObject.activeInHierarchy ||
                    renderer.gameObject.layer != 27)
                {
                    continue;
                }
                Material[] materials = renderer.sharedMaterials;
                for (int materialIndex = 0;
                    materialIndex < materials.Length;
                    materialIndex++)
                {
                    Material material = materials[materialIndex];
                    if (material != null && material.shader != null &&
                        string.Equals(
                            material.shader.name,
                            "KSP2/Environment/CelestialBody/CelestialBody_Scaled",
                            StringComparison.Ordinal))
                    {
                        return renderer.gameObject;
                    }
                }
            }
            return null;
        }

        private static Camera GetSelectedDiagnosticCamera()
        {
            Assembly assembly = FindBetterAaAssembly();
            if (assembly == null)
            {
                return null;
            }
            object visualizer = RequireVisualizer(assembly);
            return (Camera)RequireMethod(
                    visualizer.GetType(),
                    "GetSelectedCamera",
                    InstanceAny)
                .Invoke(visualizer, null);
        }

        private static string DescribeRendererMaterials(Renderer renderer)
        {
            Material[] materials = renderer.sharedMaterials;
            var builder = new StringBuilder(256);
            for (int index = 0; index < materials.Length; index++)
            {
                if (index > 0)
                {
                    builder.Append(" | ");
                }
                Material material = materials[index];
                if (material == null)
                {
                    builder.Append("<missing>");
                    continue;
                }
                builder.Append(material.name);
                builder.Append(" shader=");
                builder.Append(material.shader == null
                    ? "<missing>"
                    : material.shader.name);
                builder.Append(" motionPass=");
                builder.Append(material.FindPass("MotionVectors"));
            }
            return builder.ToString();
        }

        private static DynValue SuppressPqsPrepass(
            ScriptExecutionContext context,
            CallbackArguments arguments)
        {
            RestorePqsPrepassInternal();
            UnityEngine.Object[] renderers = Resources.FindObjectsOfTypeAll(
                typeof(KSP.Rendering.Planets.PQSRenderer));
            for (int index = 0; index < renderers.Length; index++)
            {
                Behaviour renderer = renderers[index] as Behaviour;
                if (renderer == null || !renderer.gameObject.activeInHierarchy)
                {
                    continue;
                }
                SuppressedPqsRenderers.Add(renderer);
                SuppressedPqsRendererStates.Add(renderer.enabled);
                renderer.enabled = false;
            }
            return DynValue.NewNumber(SuppressedPqsRenderers.Count);
        }

        private static DynValue RestorePqsPrepass(
            ScriptExecutionContext context,
            CallbackArguments arguments)
        {
            int count = SuppressedPqsRenderers.Count;
            RestorePqsPrepassInternal();
            return DynValue.NewNumber(count);
        }

        private static void RestorePqsPrepassInternal()
        {
            for (int index = 0; index < SuppressedPqsRenderers.Count; index++)
            {
                Behaviour renderer = SuppressedPqsRenderers[index];
                if (renderer != null)
                {
                    renderer.enabled = SuppressedPqsRendererStates[index];
                }
            }
            SuppressedPqsRenderers.Clear();
            SuppressedPqsRendererStates.Clear();
        }

        private static DynValue SuppressObserverCubemap(
            ScriptExecutionContext context,
            CallbackArguments arguments)
        {
            RestoreObserverCubemapInternal();
            Type type = typeof(KSP.Rendering.CubemapReflectionSystem);
            MethodInfo disable = RequireMethod(type, "DisableRendering", InstanceAny);
            UnityEngine.Object[] systems = Resources.FindObjectsOfTypeAll(type);
            for (int index = 0; index < systems.Length; index++)
            {
                UnityEngine.Object system = systems[index];
                if (system == null)
                {
                    continue;
                }
                disable.Invoke(system, null);
                SuppressedCubemapSystems.Add(system);
            }
            return DynValue.NewNumber(SuppressedCubemapSystems.Count);
        }

        private static DynValue RestoreObserverCubemap(
            ScriptExecutionContext context,
            CallbackArguments arguments)
        {
            int count = SuppressedCubemapSystems.Count;
            RestoreObserverCubemapInternal();
            return DynValue.NewNumber(count);
        }

        private static void RestoreObserverCubemapInternal()
        {
            for (int index = 0; index < SuppressedCubemapSystems.Count; index++)
            {
                object system = SuppressedCubemapSystems[index];
                if (system == null)
                {
                    continue;
                }
                MethodInfo enable = system.GetType().GetMethod(
                    "EnableRendering",
                    InstanceAny);
                if (enable != null)
                {
                    enable.Invoke(system, null);
                }
            }
            SuppressedCubemapSystems.Clear();
        }

        private static DynValue StartCameraRenderTrace(
            ScriptExecutionContext context,
            CallbackArguments arguments)
        {
            StopCameraRenderTraceInternal();
            object visualizer = RequireVisualizer(RequireBetterAaAssembly());
            _tracedCamera = (Camera)RequireMethod(
                    visualizer.GetType(),
                    "GetSelectedCamera",
                    InstanceAny)
                .Invoke(visualizer, null);
            if (_tracedCamera == null)
            {
                throw new InvalidOperationException(
                    "Redux Better AA has no selected diagnostic camera.");
            }

            _cameraTraceCount = 0;
            _cameraTraceWriteIndex = 0;
            _cameraTraceActive = true;
            Camera.onPreCull += OnTracedCameraPreCull;
            Camera.onPreRender += OnTracedCameraPreRender;
            Camera.onPostRender += OnTracedCameraPostRender;
            return DynValue.NewString(_tracedCamera.name);
        }

        private static DynValue CameraRenderTrace(Script script)
        {
            var table = new Table(script);
            table.Set("active", DynValue.NewBoolean(_cameraTraceActive));
            table.Set(
                "selectedCamera",
                DynValue.NewString(_tracedCamera == null
                    ? string.Empty
                    : _tracedCamera.name));
            table.Set("recordCount", DynValue.NewNumber(_cameraTraceCount));

            int available = Math.Min(_cameraTraceCount, CameraTraceCapacity);
            int start = _cameraTraceCount < CameraTraceCapacity
                ? 0
                : _cameraTraceWriteIndex;
            int selectedPreCullCount = 0;
            int selectedPreRenderCount = 0;
            int selectedPostRenderCount = 0;
            int selectedFrame = -1;
            int selectedRendersInLastFrame = 0;
            var builder = new StringBuilder(4096);
            for (int index = 0; index < available; index++)
            {
                CameraTraceRecord record = CameraTraceRecords[
                    (start + index) % CameraTraceCapacity];
                if (record.Camera != _tracedCamera)
                {
                    continue;
                }

                if (record.Phase == 0)
                {
                    selectedPreCullCount++;
                    if (record.Frame != selectedFrame)
                    {
                        selectedFrame = record.Frame;
                        selectedRendersInLastFrame = 0;
                    }
                    selectedRendersInLastFrame++;
                }
                else if (record.Phase == 1)
                {
                    selectedPreRenderCount++;
                }
                else
                {
                    selectedPostRenderCount++;
                }

                if (builder.Length > 0)
                {
                    builder.Append(" | ");
                }
                builder.Append(record.Frame);
                builder.Append(':');
                builder.Append(record.Phase == 0
                    ? "preCull"
                    : record.Phase == 1 ? "preRender" : "postRender");
                builder.Append(" pos=");
                builder.Append(record.Position.ToString("F6"));
                builder.Append(" currentPrevMax=");
                builder.Append(
                    MatrixDifferenceMaxAbs(
                        record.CurrentViewProjection,
                        record.PreviousViewProjection)
                        .ToString("R"));
            }
            table.Set("selectedPreCullCount", DynValue.NewNumber(selectedPreCullCount));
            table.Set("selectedPreRenderCount", DynValue.NewNumber(selectedPreRenderCount));
            table.Set("selectedPostRenderCount", DynValue.NewNumber(selectedPostRenderCount));
            table.Set(
                "selectedRendersInLastFrame",
                DynValue.NewNumber(selectedRendersInLastFrame));
            table.Set("selectedEvents", DynValue.NewString(builder.ToString()));
            return DynValue.NewTable(table);
        }

        private static DynValue StopCameraRenderTrace(
            ScriptExecutionContext context,
            CallbackArguments arguments)
        {
            bool active = _cameraTraceActive;
            StopCameraRenderTraceInternal();
            return DynValue.NewBoolean(active);
        }

        private static void StopCameraRenderTraceInternal()
        {
            if (_cameraTraceActive)
            {
                Camera.onPreCull -= OnTracedCameraPreCull;
                Camera.onPreRender -= OnTracedCameraPreRender;
                Camera.onPostRender -= OnTracedCameraPostRender;
            }
            _cameraTraceActive = false;
            _tracedCamera = null;
        }

        private static void OnTracedCameraPreCull(Camera camera)
        {
            RecordCameraTrace(camera, 0);
        }

        private static void OnTracedCameraPreRender(Camera camera)
        {
            RecordCameraTrace(camera, 1);
        }

        private static void OnTracedCameraPostRender(Camera camera)
        {
            RecordCameraTrace(camera, 2);
        }

        private static void RecordCameraTrace(Camera camera, byte phase)
        {
            if (!_cameraTraceActive || camera != _tracedCamera)
            {
                return;
            }
            int index = _cameraTraceWriteIndex;
            CameraTraceRecord record = CameraTraceRecords[index];
            record.Frame = Time.frameCount;
            record.Phase = phase;
            record.Camera = camera;
            record.Position = camera.transform.position;
            record.CurrentViewProjection = CurrentGpuViewProjection(camera);
            record.PreviousViewProjection = camera.previousViewProjectionMatrix;
            CameraTraceRecords[index] = record;
            _cameraTraceWriteIndex = (index + 1) % CameraTraceCapacity;
            _cameraTraceCount++;
        }

        private static DynValue SetBackend(
            ScriptExecutionContext context,
            CallbackArguments arguments)
        {
            string name = RequiredString(arguments, 0, "set_backend");
            Assembly assembly = RequireBetterAaAssembly();
            Type coordinatorType = RequireType(
                assembly,
                "ReduxBetterAA.Rendering.TemporalCoordinator");
            object coordinator = RequireStaticField(coordinatorType, "Current");
            Type selectionType = RequireType(
                assembly,
                "ReduxBetterAA.Configuration.BackendSelection");
            object selection = Enum.Parse(selectionType, name, true);
            RequireMethod(coordinatorType, "SetRequestedBackend", InstanceAny)
                .Invoke(coordinator, new[] { selection });
            return DynValue.Nil;
        }

        private static DynValue ReleaseStatus(Script script)
        {
            Assembly assembly = RequireBetterAaAssembly();
            Type modType = RequireType(assembly, "ReduxBetterAA.ReduxBetterAAMod");
            object mod = UnityEngine.Object.FindObjectOfType(modType);
            if (mod == null) throw new InvalidOperationException("Better AA is not initialized.");
            Type coordinatorType = RequireType(assembly, "ReduxBetterAA.Rendering.TemporalCoordinator");
            object coordinator = RequireStaticField(coordinatorType, "Current");
            Type probeType = RequireType(assembly, "ReduxBetterAA.Diagnostics.Phase1ProbeService");
            object probe = RequireStaticField(probeType, "Current");
            object visualizer = RequireVisualizer(assembly);
            var result = new Table(script);
            result.Set("dlaa", DynValue.NewBoolean((bool)modType.GetField("_dlaaSelectable", InstanceAny).GetValue(mod)));
            result.Set("fsr2", DynValue.NewBoolean((bool)modType.GetField("_fsr2Selectable", InstanceAny).GetValue(mod)));
            result.Set("mapEnabled", DynValue.FromObject(script, RequireProperty(coordinatorType, "MapViewAaEnabled").GetValue(coordinator, null)));
            result.Set("captureBusy", DynValue.FromObject(script, RequireProperty(visualizer.GetType(), "CaptureBusy").GetValue(visualizer, null)));
            result.Set("issueBusy", DynValue.FromObject(script, RequireProperty(probeType, "IssueReportBusy").GetValue(probe, null)));
            result.Set("issueZip", DynValue.FromObject(script, RequireProperty(probeType, "LastIssueReport").GetValue(probe, null)));
            return DynValue.NewTable(result);
        }

        private static DynValue SetVendorJitterSpread(
            ScriptExecutionContext context,
            CallbackArguments arguments)
        {
            string backend = RequiredString(
                arguments,
                0,
                "set_vendor_jitter_spread");
            float spread = (float)RequiredNumber(
                arguments,
                1,
                "set_vendor_jitter_spread");
            Assembly assembly = RequireBetterAaAssembly();
            Type coordinatorType = RequireType(
                assembly,
                "ReduxBetterAA.Rendering.TemporalCoordinator");
            object coordinator = RequireStaticField(coordinatorType, "Current");
            if (string.Equals(
                    backend,
                    "NvidiaDlaa",
                    StringComparison.OrdinalIgnoreCase))
            {
                Type configType = RequireType(
                    assembly,
                    "ReduxBetterAA.Configuration.DlaaConfig");
                object current = coordinatorType.GetProperty(
                        "DlaaConfig",
                        InstanceAny)
                    .GetValue(coordinator, null);
                object updated = Activator.CreateInstance(
                    configType,
                    new[]
                    {
                        (object)spread,
                        configType.GetField("SequenceLength").GetValue(current),
                        configType.GetField("Sharpness").GetValue(current),
                        configType.GetField("PreExposure").GetValue(current),
                        configType.GetField("AutoExposure").GetValue(current),
                        configType.GetField("InvertMotionX").GetValue(current),
                        configType.GetField("InvertMotionY").GetValue(current),
                        configType.GetField("Preset").GetValue(current),
                        configType.GetField("AllowSupersampling").GetValue(current),
                        configType.GetField("PreferPpv2Exposure").GetValue(current)
                    });
                RequireMethod(coordinatorType, "SetDlaaConfig", InstanceAny)
                    .Invoke(coordinator, new[] { updated });
                return DynValue.Nil;
            }
            if (string.Equals(
                    backend,
                    "AmdFsr2",
                    StringComparison.OrdinalIgnoreCase))
            {
                Type configType = RequireType(
                    assembly,
                    "ReduxBetterAA.Configuration.Fsr2Config");
                object current = coordinatorType.GetProperty(
                        "Fsr2Config",
                        InstanceAny)
                    .GetValue(coordinator, null);
                object updated = Activator.CreateInstance(
                    configType,
                    new[]
                    {
                        (object)spread,
                        configType.GetField("SequenceLength").GetValue(current),
                        configType.GetField("Sharpness").GetValue(current),
                        configType.GetField("PreExposure").GetValue(current),
                        configType.GetField("AutoExposure").GetValue(current),
                        configType.GetField("InvertMotionX").GetValue(current),
                        configType.GetField("InvertMotionY").GetValue(current),
                        configType.GetField("PreferPpv2Exposure").GetValue(current)
                    });
                RequireMethod(coordinatorType, "SetFsr2Config", InstanceAny)
                    .Invoke(coordinator, new[] { updated });
                return DynValue.Nil;
            }
            throw new ArgumentException(
                "set_vendor_jitter_spread supports NvidiaDlaa or AmdFsr2.");
        }

        private static DynValue SetDlaaPreset(
            ScriptExecutionContext context,
            CallbackArguments arguments)
        {
            string presetName = RequiredString(arguments, 0, "set_dlaa_preset");
            Assembly assembly = RequireBetterAaAssembly();
            Type coordinatorType = RequireType(
                assembly,
                "ReduxBetterAA.Rendering.TemporalCoordinator");
            object coordinator = RequireStaticField(coordinatorType, "Current");
            Type configType = RequireType(
                assembly,
                "ReduxBetterAA.Configuration.DlaaConfig");
            Type presetType = RequireType(
                assembly,
                "ReduxBetterAA.Configuration.DlaaPreset");
            object preset = Enum.Parse(presetType, presetName, true);
            object current = coordinatorType.GetProperty(
                    "DlaaConfig",
                    InstanceAny)
                .GetValue(coordinator, null);
            object updated = Activator.CreateInstance(
                configType,
                new[]
                {
                    configType.GetField("JitterSpread").GetValue(current),
                    configType.GetField("SequenceLength").GetValue(current),
                    configType.GetField("Sharpness").GetValue(current),
                    configType.GetField("PreExposure").GetValue(current),
                    configType.GetField("AutoExposure").GetValue(current),
                    configType.GetField("InvertMotionX").GetValue(current),
                    configType.GetField("InvertMotionY").GetValue(current),
                    preset,
                    configType.GetField("AllowSupersampling").GetValue(current),
                    configType.GetField("PreferPpv2Exposure").GetValue(current)
                });
            RequireMethod(coordinatorType, "SetDlaaConfig", InstanceAny)
                .Invoke(coordinator, new[] { updated });
            return DynValue.Nil;
        }

        private static DynValue SetVendorSharpness(
            ScriptExecutionContext context,
            CallbackArguments arguments)
        {
            string backend = RequiredString(
                arguments,
                0,
                "set_vendor_sharpness");
            float sharpness = (float)RequiredNumber(
                arguments,
                1,
                "set_vendor_sharpness");
            Assembly assembly = RequireBetterAaAssembly();
            Type coordinatorType = RequireType(
                assembly,
                "ReduxBetterAA.Rendering.TemporalCoordinator");
            object coordinator = RequireStaticField(coordinatorType, "Current");
            if (string.Equals(
                    backend,
                    "NvidiaDlaa",
                    StringComparison.OrdinalIgnoreCase))
            {
                Type configType = RequireType(
                    assembly,
                    "ReduxBetterAA.Configuration.DlaaConfig");
                object current = coordinatorType.GetProperty(
                        "DlaaConfig",
                        InstanceAny)
                    .GetValue(coordinator, null);
                object updated = Activator.CreateInstance(
                    configType,
                    new[]
                    {
                        configType.GetField("JitterSpread").GetValue(current),
                        configType.GetField("SequenceLength").GetValue(current),
                        (object)sharpness,
                        configType.GetField("PreExposure").GetValue(current),
                        configType.GetField("AutoExposure").GetValue(current),
                        configType.GetField("InvertMotionX").GetValue(current),
                        configType.GetField("InvertMotionY").GetValue(current),
                        configType.GetField("Preset").GetValue(current),
                        configType.GetField("AllowSupersampling").GetValue(current),
                        configType.GetField("PreferPpv2Exposure").GetValue(current)
                    });
                RequireMethod(coordinatorType, "SetDlaaConfig", InstanceAny)
                    .Invoke(coordinator, new[] { updated });
                return DynValue.Nil;
            }
            if (string.Equals(
                    backend,
                    "AmdFsr2",
                    StringComparison.OrdinalIgnoreCase))
            {
                Type configType = RequireType(
                    assembly,
                    "ReduxBetterAA.Configuration.Fsr2Config");
                object current = coordinatorType.GetProperty(
                        "Fsr2Config",
                        InstanceAny)
                    .GetValue(coordinator, null);
                object updated = Activator.CreateInstance(
                    configType,
                    new[]
                    {
                        configType.GetField("JitterSpread").GetValue(current),
                        configType.GetField("SequenceLength").GetValue(current),
                        (object)sharpness,
                        configType.GetField("PreExposure").GetValue(current),
                        configType.GetField("AutoExposure").GetValue(current),
                        configType.GetField("InvertMotionX").GetValue(current),
                        configType.GetField("InvertMotionY").GetValue(current),
                        configType.GetField("PreferPpv2Exposure").GetValue(current)
                    });
                RequireMethod(coordinatorType, "SetFsr2Config", InstanceAny)
                    .Invoke(coordinator, new[] { updated });
                return DynValue.Nil;
            }
            throw new ArgumentException(
                "set_vendor_sharpness supports NvidiaDlaa or AmdFsr2.");
        }

        private static DynValue SetVendorAutoExposure(
            ScriptExecutionContext context,
            CallbackArguments arguments)
        {
            string backend = RequiredString(
                arguments,
                0,
                "set_vendor_auto_exposure");
            bool enabled = RequiredBoolean(
                arguments,
                1,
                "set_vendor_auto_exposure");
            float preExposure = (float)RequiredNumber(
                arguments,
                2,
                "set_vendor_auto_exposure");
            bool? preferPpv2Exposure = arguments.Count > 3
                ? (bool?)RequiredBoolean(
                    arguments,
                    3,
                    "set_vendor_auto_exposure")
                : null;
            Assembly assembly = RequireBetterAaAssembly();
            Type coordinatorType = RequireType(
                assembly,
                "ReduxBetterAA.Rendering.TemporalCoordinator");
            object coordinator = RequireStaticField(coordinatorType, "Current");
            if (string.Equals(
                    backend,
                    "NvidiaDlaa",
                    StringComparison.OrdinalIgnoreCase))
            {
                Type configType = RequireType(
                    assembly,
                    "ReduxBetterAA.Configuration.DlaaConfig");
                object current = coordinatorType.GetProperty(
                        "DlaaConfig",
                        InstanceAny)
                    .GetValue(coordinator, null);
                object updated = Activator.CreateInstance(
                    configType,
                    new[]
                    {
                        configType.GetField("JitterSpread").GetValue(current),
                        configType.GetField("SequenceLength").GetValue(current),
                        configType.GetField("Sharpness").GetValue(current),
                        (object)preExposure,
                        (object)enabled,
                        configType.GetField("InvertMotionX").GetValue(current),
                        configType.GetField("InvertMotionY").GetValue(current),
                        configType.GetField("Preset").GetValue(current),
                        configType.GetField("AllowSupersampling").GetValue(current),
                        preferPpv2Exposure.HasValue
                            ? (object)preferPpv2Exposure.Value
                            : configType.GetField("PreferPpv2Exposure").GetValue(current)
                    });
                RequireMethod(coordinatorType, "SetDlaaConfig", InstanceAny)
                    .Invoke(coordinator, new[] { updated });
                return DynValue.Nil;
            }
            if (string.Equals(
                    backend,
                    "AmdFsr2",
                    StringComparison.OrdinalIgnoreCase))
            {
                Type configType = RequireType(
                    assembly,
                    "ReduxBetterAA.Configuration.Fsr2Config");
                object current = coordinatorType.GetProperty(
                        "Fsr2Config",
                        InstanceAny)
                    .GetValue(coordinator, null);
                object updated = Activator.CreateInstance(
                    configType,
                    new[]
                    {
                        configType.GetField("JitterSpread").GetValue(current),
                        configType.GetField("SequenceLength").GetValue(current),
                        configType.GetField("Sharpness").GetValue(current),
                        (object)preExposure,
                        (object)enabled,
                        configType.GetField("InvertMotionX").GetValue(current),
                        configType.GetField("InvertMotionY").GetValue(current),
                        preferPpv2Exposure.HasValue
                            ? (object)preferPpv2Exposure.Value
                            : configType.GetField("PreferPpv2Exposure").GetValue(current)
                    });
                RequireMethod(coordinatorType, "SetFsr2Config", InstanceAny)
                    .Invoke(coordinator, new[] { updated });
                return DynValue.Nil;
            }
            throw new ArgumentException(
                "set_vendor_auto_exposure supports NvidiaDlaa or AmdFsr2.");
        }

        private static DynValue SetTemporalJitterSuppressed(
            ScriptExecutionContext context,
            CallbackArguments arguments)
        {
            bool suppressed = RequiredBoolean(
                arguments,
                0,
                "set_temporal_jitter_suppressed");
            if (suppressed == _temporalJitterSuppressed)
            {
                return DynValue.NewBoolean(_temporalJitterSuppressed);
            }

            RestoreTemporalJitterSuppression();
            if (suppressed)
            {
                Assembly assembly = RequireBetterAaAssembly();
                Type sequenceType = RequireType(
                    assembly,
                    "ReduxBetterAA.Rendering.SharedJitterSequence");
                MethodInfo original = RequireMethod(
                    sequenceType,
                    "GetCustomOffset",
                    StaticAny);
                MethodInfo prefix = typeof(ReduxBetterAaTestApi).GetMethod(
                    nameof(SuppressTemporalJitterPrefix),
                    StaticAny);
                TemporalJitterHarmony.Patch(
                    original,
                    prefix: new HarmonyMethod(prefix));
                _temporalJitterSuppressed = true;
            }
            return DynValue.NewBoolean(_temporalJitterSuppressed);
        }

        private static bool SuppressTemporalJitterPrefix(ref Vector2 __result)
        {
            __result = Vector2.zero;
            return false;
        }

        private static void RestoreTemporalJitterSuppression()
        {
            if (!_temporalJitterSuppressed)
            {
                return;
            }
            TemporalJitterHarmony.UnpatchAll(TemporalJitterHarmonyId);
            _temporalJitterSuppressed = false;
        }

        private static DynValue OpenMapView(
            ScriptExecutionContext context,
            CallbackArguments arguments)
        {
            KSP.Game.GameManager manager = KSP.Game.GameManager.Instance;
            if (manager == null || manager.Game == null ||
                manager.Game.UI == null)
            {
                throw new InvalidOperationException(
                    "KSP2's UI manager is not available to open map view.");
            }
            manager.Game.UI.LoadMap();
            return DynValue.Nil;
        }

        private static DynValue SetBufferView(
            ScriptExecutionContext context,
            CallbackArguments arguments)
        {
            string name = NormalizeViewName(
                RequiredString(arguments, 0, "set_buffer_view"));
            Assembly assembly = RequireBetterAaAssembly();
            object visualizer = RequireVisualizer(assembly);
            Type viewType = RequireType(
                assembly,
                "ReduxBetterAA.Diagnostics.BufferDebugView");
            object view = Enum.Parse(viewType, name, true);
            RequireMethod(visualizer.GetType(), "SetView", InstanceAny)
                .Invoke(visualizer, new[] { view });
            return DynValue.Nil;
        }

        private static DynValue SelectCamera(
            ScriptExecutionContext context,
            CallbackArguments arguments)
        {
            string requestedName = RequiredString(arguments, 0, "select_camera");
            Assembly assembly = RequireBetterAaAssembly();
            object visualizer = RequireVisualizer(assembly);
            Type discoveryType = RequireType(
                assembly,
                "ReduxBetterAA.Rendering.CameraDiscovery");
            Camera[] cameras = (Camera[])RequireMethod(
                    discoveryType,
                    "CaptureDebugCandidates",
                    StaticAny)
                .Invoke(null, null);
            RequireMethod(visualizer.GetType(), "SetCandidates", InstanceAny)
                .Invoke(visualizer, new object[] { cameras });

            int selectedIndex = -1;
            for (int index = 0; index < cameras.Length; index++)
            {
                Camera camera = cameras[index];
                if (camera != null && camera.gameObject.activeInHierarchy &&
                    string.Equals(
                        camera.name,
                        requestedName,
                        StringComparison.OrdinalIgnoreCase))
                {
                    selectedIndex = index;
                }
            }
            if (selectedIndex < 0)
            {
                throw new InvalidOperationException(
                    "No active Better AA diagnostic camera named '" +
                    requestedName + "' was found.");
            }

            RequireMethod(visualizer.GetType(), "SelectCamera", InstanceAny)
                .Invoke(visualizer, new object[] { selectedIndex });
            return DynValue.NewString(cameras[selectedIndex].name);
        }

        private static DynValue RequestCapture(
            ScriptExecutionContext context,
            CallbackArguments arguments)
        {
            object visualizer = RequireVisualizer(RequireBetterAaAssembly());
            bool queued = (bool)RequireMethod(
                    visualizer.GetType(),
                    "RequestScreenshot",
                    InstanceAny)
                .Invoke(visualizer, null);
            return DynValue.NewBoolean(queued);
        }

        private static DynValue CurrentView(
            ScriptExecutionContext context,
            CallbackArguments arguments)
        {
            object visualizer = RequireVisualizer(RequireBetterAaAssembly());
            object value = RequireProperty(
                    visualizer.GetType(),
                    "CurrentViewName")
                .GetValue(visualizer, null);
            return DynValue.NewString(value == null ? string.Empty : value.ToString());
        }

        private static DynValue SelectedCamera(
            ScriptExecutionContext context,
            CallbackArguments arguments)
        {
            object visualizer = RequireVisualizer(RequireBetterAaAssembly());
            object value = RequireProperty(
                    visualizer.GetType(),
                    "SelectedCameraName")
                .GetValue(visualizer, null);
            return DynValue.NewString(value == null ? string.Empty : value.ToString());
        }

        private static DynValue MatrixSnapshot(Script script)
        {
            Assembly assembly = RequireBetterAaAssembly();
            object visualizer = RequireVisualizer(assembly);
            Camera selected = (Camera)RequireMethod(
                    visualizer.GetType(),
                    "GetSelectedCamera",
                    InstanceAny)
                .Invoke(visualizer, null);
            if (selected == null)
            {
                throw new InvalidOperationException(
                    "Redux Better AA has no selected diagnostic camera.");
            }

            Matrix4x4 currentGpu = CurrentGpuViewProjection(selected);
            Matrix4x4 currentGpuJittered = GL.GetGPUProjectionMatrix(
                    selected.projectionMatrix,
                    selected.targetTexture != null ||
                    selected.forceIntoRenderTexture) *
                selected.worldToCameraMatrix;
            Matrix4x4 currentGpuTexture = GL.GetGPUProjectionMatrix(
                    selected.nonJitteredProjectionMatrix,
                    true) *
                selected.worldToCameraMatrix;
            Matrix4x4 currentCpu = selected.nonJitteredProjectionMatrix *
                selected.worldToCameraMatrix;
            Matrix4x4 previous = selected.previousViewProjectionMatrix;

            Type discoveryType = RequireType(
                assembly,
                "ReduxBetterAA.Rendering.CameraDiscovery");
            Camera[] cameras = (Camera[])RequireMethod(
                    discoveryType,
                    "CaptureDebugCandidates",
                    StaticAny)
                .Invoke(null, null);
            string closestName = string.Empty;
            float closestDifference = float.PositiveInfinity;
            for (int index = 0; index < cameras.Length; index++)
            {
                Camera candidate = cameras[index];
                if (candidate == null || !candidate.gameObject.activeInHierarchy)
                {
                    continue;
                }
                float difference = MatrixDifferenceMaxAbs(
                    previous,
                    CurrentGpuViewProjection(candidate));
                if (difference < closestDifference)
                {
                    closestDifference = difference;
                    closestName = candidate.name;
                }
            }

            var result = new Table(script);
            result.Set("camera", DynValue.NewString(selected.name));
            result.Set(
                "currentPreviousGpuMaxAbs",
                DynValue.NewNumber(MatrixDifferenceMaxAbs(currentGpu, previous)));
            result.Set(
                "jitteredNonJitteredGpuMaxAbs",
                DynValue.NewNumber(MatrixDifferenceMaxAbs(
                    currentGpuJittered,
                    currentGpu)));
            result.Set(
                "jitteredPreviousGpuMaxAbs",
                DynValue.NewNumber(MatrixDifferenceMaxAbs(
                    currentGpuJittered,
                    previous)));
            result.Set(
                "currentPreviousCpuMaxAbs",
                DynValue.NewNumber(MatrixDifferenceMaxAbs(currentCpu, previous)));
            result.Set(
                "currentTexturePreviousMaxAbs",
                DynValue.NewNumber(MatrixDifferenceMaxAbs(
                    currentGpuTexture,
                    previous)));
            result.Set(
                "previousIdentityMaxAbs",
                DynValue.NewNumber(MatrixDifferenceMaxAbs(
                    previous,
                    Matrix4x4.identity)));
            result.Set(
                "currentGpuMatrix",
                DynValue.NewString(currentGpu.ToString("F6")));
            result.Set(
                "currentGpuJitteredMatrix",
                DynValue.NewString(currentGpuJittered.ToString("F6")));
            result.Set(
                "previousMatrix",
                DynValue.NewString(previous.ToString("F6")));
            result.Set(
                "forceIntoRenderTexture",
                DynValue.NewBoolean(selected.forceIntoRenderTexture));
            result.Set(
                "projectionJitterMaxAbs",
                DynValue.NewNumber(MatrixDifferenceMaxAbs(
                    selected.projectionMatrix,
                    selected.nonJitteredProjectionMatrix)));
            result.Set(
                "viewTransformInverseMaxAbs",
                DynValue.NewNumber(MatrixDifferenceMaxAbs(
                    selected.worldToCameraMatrix,
                    selected.transform.localToWorldMatrix.inverse)));
            result.Set(
                "projectionMatrix",
                DynValue.NewString(selected.projectionMatrix.ToString("F6")));
            result.Set(
                "nonJitteredProjectionMatrix",
                DynValue.NewString(
                    selected.nonJitteredProjectionMatrix.ToString("F6")));
            result.Set(
                "worldToCameraMatrix",
                DynValue.NewString(selected.worldToCameraMatrix.ToString("F6")));
            result.Set("enabled", DynValue.NewBoolean(selected.enabled));
            result.Set(
                "depthTextureMode",
                DynValue.NewString(selected.depthTextureMode.ToString()));
            result.Set(
                "renderingPath",
                DynValue.NewString(selected.renderingPath.ToString()));
            result.Set(
                "actualRenderingPath",
                DynValue.NewString(selected.actualRenderingPath.ToString()));
            result.Set(
                "clearFlags",
                DynValue.NewString(selected.clearFlags.ToString()));
            result.Set("depth", DynValue.NewNumber(selected.depth));
            result.Set("fieldOfView", DynValue.NewNumber(selected.fieldOfView));
            result.Set("nearClipPlane", DynValue.NewNumber(selected.nearClipPlane));
            result.Set("farClipPlane", DynValue.NewNumber(selected.farClipPlane));
            result.Set("rect", DynValue.NewString(selected.rect.ToString("F6")));
            result.Set(
                "pixelRect",
                DynValue.NewString(selected.pixelRect.ToString("F6")));
            result.Set("pixelWidth", DynValue.NewNumber(selected.pixelWidth));
            result.Set("pixelHeight", DynValue.NewNumber(selected.pixelHeight));
            result.Set(
                "scaledPixelWidth",
                DynValue.NewNumber(selected.scaledPixelWidth));
            result.Set(
                "scaledPixelHeight",
                DynValue.NewNumber(selected.scaledPixelHeight));
            result.Set(
                "targetTexture",
                DynValue.NewString(
                    selected.targetTexture == null
                        ? string.Empty
                        : selected.targetTexture.name));
            result.Set(
                "components",
                DynValue.NewString(DescribeComponents(selected)));
            result.Set(
                "commandBuffers",
                DynValue.NewString(DescribeCommandBuffers(selected)));
            result.Set("closestCurrentCamera", DynValue.NewString(closestName));
            result.Set(
                "closestCurrentCameraMaxAbs",
                DynValue.NewNumber(closestDifference));
            result.Set(
                "position",
                DynValue.NewString(selected.transform.position.ToString("F6")));
            result.Set(
                "rotation",
                DynValue.NewString(selected.transform.rotation.eulerAngles.ToString("F6")));
            result.Set("frame", DynValue.NewNumber(Time.frameCount));
            return DynValue.NewTable(result);
        }

        private static DynValue SuppressObjectMotion(
            ScriptExecutionContext context,
            CallbackArguments arguments)
        {
            MotionVectorGenerationMode replacement =
                MotionVectorGenerationMode.Camera;
            if (arguments != null && arguments.Count > 0 &&
                arguments[0].Type != DataType.Nil &&
                arguments[0].Type != DataType.Void)
            {
                string requested = RequiredString(
                    arguments,
                    0,
                    "suppress_object_motion");
                if (!Enum.TryParse(requested, true, out replacement))
                {
                    throw new ArgumentException(
                        "Unknown MotionVectorGenerationMode '" +
                        requested + "'.");
                }
            }

            RestoreObjectMotionInternal();
            Renderer[] renderers = Resources.FindObjectsOfTypeAll<Renderer>();
            for (int index = 0; index < renderers.Length; index++)
            {
                Renderer renderer = renderers[index];
                if (renderer == null || !renderer.gameObject.scene.IsValid() ||
                    !renderer.gameObject.activeInHierarchy || !renderer.enabled)
                {
                    continue;
                }
                MotionVectorGenerationMode mode =
                    renderer.motionVectorGenerationMode;
                if (mode == replacement)
                {
                    continue;
                }
                SuppressedRenderers.Add(renderer);
                SuppressedModes.Add(mode);
                renderer.motionVectorGenerationMode = replacement;
            }
            return DynValue.NewNumber(SuppressedRenderers.Count);
        }

        private static DynValue RestoreObjectMotion(
            ScriptExecutionContext context,
            CallbackArguments arguments)
        {
            int count = SuppressedRenderers.Count;
            RestoreObjectMotionInternal();
            return DynValue.NewNumber(count);
        }

        private static void RestoreObjectMotionInternal()
        {
            int count = Math.Min(
                SuppressedRenderers.Count,
                SuppressedModes.Count);
            for (int index = 0; index < count; index++)
            {
                Renderer renderer = SuppressedRenderers[index];
                if (renderer != null)
                {
                    renderer.motionVectorGenerationMode =
                        SuppressedModes[index];
                }
            }
            SuppressedRenderers.Clear();
            SuppressedModes.Clear();
        }

        private static DynValue IsolateMotionCamera(
            ScriptExecutionContext context,
            CallbackArguments arguments)
        {
            RestoreMotionCamerasInternal();
            object visualizer = RequireVisualizer(RequireBetterAaAssembly());
            Camera selected = (Camera)RequireMethod(
                    visualizer.GetType(),
                    "GetSelectedCamera",
                    InstanceAny)
                .Invoke(visualizer, null);
            if (selected == null)
            {
                throw new InvalidOperationException(
                    "Redux Better AA has no selected diagnostic camera.");
            }

            Camera[] cameras = Camera.allCameras;
            for (int index = 0; index < cameras.Length; index++)
            {
                Camera camera = cameras[index];
                if (camera == null || camera == selected ||
                    (camera.depthTextureMode & DepthTextureMode.MotionVectors) == 0)
                {
                    continue;
                }
                IsolatedCameras.Add(camera);
                IsolatedCameraModes.Add(camera.depthTextureMode);
                camera.depthTextureMode &= ~DepthTextureMode.MotionVectors;
            }
            return DynValue.NewNumber(IsolatedCameras.Count);
        }

        private static DynValue RestoreMotionCameras(
            ScriptExecutionContext context,
            CallbackArguments arguments)
        {
            int count = IsolatedCameras.Count;
            RestoreMotionCamerasInternal();
            return DynValue.NewNumber(count);
        }

        private static void RestoreMotionCamerasInternal()
        {
            int count = Math.Min(
                IsolatedCameras.Count,
                IsolatedCameraModes.Count);
            for (int index = 0; index < count; index++)
            {
                Camera camera = IsolatedCameras[index];
                if (camera != null)
                {
                    camera.depthTextureMode = IsolatedCameraModes[index];
                }
            }
            IsolatedCameras.Clear();
            IsolatedCameraModes.Clear();
        }

        private static DynValue CaptureMotionAtEvent(
            ScriptExecutionContext context,
            CallbackArguments arguments)
        {
            string eventName = RequiredString(
                arguments,
                0,
                "capture_motion_at_event");
            CameraEvent cameraEvent;
            if (!Enum.TryParse(eventName, true, out cameraEvent))
            {
                throw new ArgumentException(
                    "Unknown Unity CameraEvent '" + eventName + "'.");
            }

            RestoreMotionCaptureInternal();
            object visualizer = RequireVisualizer(RequireBetterAaAssembly());
            Camera camera = (Camera)RequireMethod(
                    visualizer.GetType(),
                    "GetSelectedCamera",
                    InstanceAny)
                .Invoke(visualizer, null);
            FieldInfo materialField = visualizer.GetType().GetField(
                "_material",
                InstanceAny);
            Material material = materialField == null
                ? null
                : materialField.GetValue(visualizer) as Material;
            if (camera == null || material == null)
            {
                throw new InvalidOperationException(
                    "Redux Better AA's selected camera/material is not attached.");
            }

            int width = Math.Max(1, camera.scaledPixelWidth);
            int height = Math.Max(1, camera.scaledPixelHeight);
            var texture = new RenderTexture(
                width,
                height,
                0,
                RenderTextureFormat.RGHalf,
                RenderTextureReadWrite.Linear)
            {
                name = "Redux TestHarness Early Motion Capture",
                filterMode = FilterMode.Point,
                wrapMode = TextureWrapMode.Clamp,
                hideFlags = HideFlags.HideAndDontSave,
                useMipMap = false,
                autoGenerateMips = false
            };
            texture.Create();
            if (!texture.IsCreated())
            {
                UnityEngine.Object.Destroy(texture);
                throw new InvalidOperationException(
                    "The early motion-vector capture texture could not be created.");
            }

            var commandBuffer = new CommandBuffer
            {
                name = "Redux TestHarness Early Motion Capture"
            };
            commandBuffer.Blit(
                BuiltinRenderTextureType.MotionVectors,
                texture);
            camera.AddCommandBuffer(cameraEvent, commandBuffer);
            material.SetTexture(CameraMotionVectorsTexture, texture);

            _earlyMotionCamera = camera;
            _earlyMotionMaterial = material;
            _earlyMotionTexture = texture;
            _earlyMotionCommandBuffer = commandBuffer;
            _earlyMotionEvent = cameraEvent;
            return DynValue.NewString(
                cameraEvent + " " + width + "x" + height);
        }

        private static DynValue RestoreMotionCapture(
            ScriptExecutionContext context,
            CallbackArguments arguments)
        {
            bool active = _earlyMotionTexture != null;
            RestoreMotionCaptureInternal();
            return DynValue.NewBoolean(active);
        }

        private static void RestoreMotionCaptureInternal()
        {
            if (_earlyMotionMaterial != null)
            {
                _earlyMotionMaterial.SetTexture(
                    CameraMotionVectorsTexture,
                    null);
            }
            if (_earlyMotionCamera != null &&
                _earlyMotionCommandBuffer != null)
            {
                _earlyMotionCamera.RemoveCommandBuffer(
                    _earlyMotionEvent,
                    _earlyMotionCommandBuffer);
            }
            if (_earlyMotionCommandBuffer != null)
            {
                _earlyMotionCommandBuffer.Release();
            }
            if (_earlyMotionTexture != null)
            {
                if (_earlyMotionTexture.IsCreated())
                {
                    _earlyMotionTexture.Release();
                }
                UnityEngine.Object.Destroy(_earlyMotionTexture);
            }
            _earlyMotionCamera = null;
            _earlyMotionMaterial = null;
            _earlyMotionTexture = null;
            _earlyMotionCommandBuffer = null;
            _earlyMotionEvent = default(CameraEvent);
        }

        private static DynValue CaptureDepthAtEvent(
            ScriptExecutionContext context,
            CallbackArguments arguments)
        {
            string eventName = RequiredString(
                arguments,
                0,
                "capture_depth_at_event");
            CameraEvent cameraEvent;
            if (!Enum.TryParse(eventName, true, out cameraEvent))
            {
                throw new ArgumentException(
                    "Unknown Unity CameraEvent '" + eventName + "'.");
            }

            RestoreDepthCaptureInternal();
            object visualizer = RequireVisualizer(RequireBetterAaAssembly());
            Camera camera = (Camera)RequireMethod(
                    visualizer.GetType(),
                    "GetSelectedCamera",
                    InstanceAny)
                .Invoke(visualizer, null);
            FieldInfo materialField = visualizer.GetType().GetField(
                "_material",
                InstanceAny);
            Material material = materialField == null
                ? null
                : materialField.GetValue(visualizer) as Material;
            if (camera == null || material == null)
            {
                throw new InvalidOperationException(
                    "Redux Better AA's selected camera/material is not attached.");
            }

            int width = Math.Max(1, camera.scaledPixelWidth);
            int height = Math.Max(1, camera.scaledPixelHeight);
            var texture = new RenderTexture(
                width,
                height,
                0,
                RenderTextureFormat.RFloat,
                RenderTextureReadWrite.Linear)
            {
                name = "Redux TestHarness Early Depth Capture",
                filterMode = FilterMode.Point,
                wrapMode = TextureWrapMode.Clamp,
                hideFlags = HideFlags.HideAndDontSave,
                useMipMap = false,
                autoGenerateMips = false
            };
            texture.Create();
            if (!texture.IsCreated())
            {
                UnityEngine.Object.Destroy(texture);
                throw new InvalidOperationException(
                    "The early depth capture texture could not be created.");
            }

            var commandBuffer = new CommandBuffer
            {
                name = "Redux TestHarness Early Depth Capture"
            };
            commandBuffer.Blit(BuiltinRenderTextureType.Depth, texture);
            camera.AddCommandBuffer(cameraEvent, commandBuffer);
            material.SetTexture(CameraDepthTexture, texture);

            _earlyDepthCamera = camera;
            _earlyDepthMaterial = material;
            _earlyDepthTexture = texture;
            _earlyDepthCommandBuffer = commandBuffer;
            _earlyDepthEvent = cameraEvent;
            return DynValue.NewString(
                cameraEvent + " " + width + "x" + height);
        }

        private static DynValue RestoreDepthCapture(
            ScriptExecutionContext context,
            CallbackArguments arguments)
        {
            bool active = _earlyDepthTexture != null;
            RestoreDepthCaptureInternal();
            return DynValue.NewBoolean(active);
        }

        private static void RestoreDepthCaptureInternal()
        {
            if (_earlyDepthMaterial != null)
            {
                _earlyDepthMaterial.SetTexture(CameraDepthTexture, null);
            }
            if (_earlyDepthCamera != null &&
                _earlyDepthCommandBuffer != null)
            {
                _earlyDepthCamera.RemoveCommandBuffer(
                    _earlyDepthEvent,
                    _earlyDepthCommandBuffer);
            }
            if (_earlyDepthCommandBuffer != null)
            {
                _earlyDepthCommandBuffer.Release();
            }
            if (_earlyDepthTexture != null)
            {
                if (_earlyDepthTexture.IsCreated())
                {
                    _earlyDepthTexture.Release();
                }
                UnityEngine.Object.Destroy(_earlyDepthTexture);
            }
            _earlyDepthCamera = null;
            _earlyDepthMaterial = null;
            _earlyDepthTexture = null;
            _earlyDepthCommandBuffer = null;
            _earlyDepthEvent = default(CameraEvent);
        }

        private static DynValue OverrideDepthWithFar(
            ScriptExecutionContext context,
            CallbackArguments arguments)
        {
            string eventName = RequiredString(
                arguments,
                0,
                "override_depth_with_far");
            CameraEvent cameraEvent;
            if (!Enum.TryParse(eventName, true, out cameraEvent))
            {
                throw new ArgumentException(
                    "Unknown Unity CameraEvent '" + eventName + "'.");
            }

            RestoreDepthOverrideInternal();
            object visualizer = RequireVisualizer(RequireBetterAaAssembly());
            Camera camera = (Camera)RequireMethod(
                    visualizer.GetType(),
                    "GetSelectedCamera",
                    InstanceAny)
                .Invoke(visualizer, null);
            if (camera == null)
            {
                throw new InvalidOperationException(
                    "Redux Better AA has no selected diagnostic camera.");
            }

            int width = Math.Max(1, camera.scaledPixelWidth);
            int height = Math.Max(1, camera.scaledPixelHeight);
            var texture = new RenderTexture(
                width,
                height,
                0,
                RenderTextureFormat.RFloat,
                RenderTextureReadWrite.Linear)
            {
                name = "Redux TestHarness Far Depth Override",
                filterMode = FilterMode.Point,
                wrapMode = TextureWrapMode.Clamp,
                hideFlags = HideFlags.HideAndDontSave,
                useMipMap = false,
                autoGenerateMips = false
            };
            texture.Create();
            Graphics.Blit(
                SystemInfo.usesReversedZBuffer
                    ? Texture2D.blackTexture
                    : Texture2D.whiteTexture,
                texture);

            var commandBuffer = new CommandBuffer
            {
                name = "Redux TestHarness Far Depth Override"
            };
            commandBuffer.SetGlobalTexture(CameraDepthTexture, texture);
            camera.AddCommandBuffer(cameraEvent, commandBuffer);

            _globalDepthOverrideCamera = camera;
            _globalDepthOverrideTexture = texture;
            _globalDepthOverrideCommandBuffer = commandBuffer;
            _globalDepthOverrideEvent = cameraEvent;
            return DynValue.NewString(
                cameraEvent + " " + width + "x" + height);
        }

        private static DynValue RestoreDepthOverride(
            ScriptExecutionContext context,
            CallbackArguments arguments)
        {
            bool active = _globalDepthOverrideTexture != null;
            RestoreDepthOverrideInternal();
            return DynValue.NewBoolean(active);
        }

        private static void RestoreDepthOverrideInternal()
        {
            if (_globalDepthOverrideCamera != null &&
                _globalDepthOverrideCommandBuffer != null)
            {
                _globalDepthOverrideCamera.RemoveCommandBuffer(
                    _globalDepthOverrideEvent,
                    _globalDepthOverrideCommandBuffer);
            }
            if (_globalDepthOverrideCommandBuffer != null)
            {
                _globalDepthOverrideCommandBuffer.Release();
            }
            if (_globalDepthOverrideTexture != null)
            {
                if (_globalDepthOverrideTexture.IsCreated())
                {
                    _globalDepthOverrideTexture.Release();
                }
                UnityEngine.Object.Destroy(_globalDepthOverrideTexture);
            }
            _globalDepthOverrideCamera = null;
            _globalDepthOverrideTexture = null;
            _globalDepthOverrideCommandBuffer = null;
            _globalDepthOverrideEvent = default(CameraEvent);
        }

        private static DynValue ClearMotionAtEvent(
            ScriptExecutionContext context,
            CallbackArguments arguments)
        {
            string eventName = RequiredString(
                arguments,
                0,
                "clear_motion_at_event");
            CameraEvent cameraEvent;
            if (!Enum.TryParse(eventName, true, out cameraEvent))
            {
                throw new ArgumentException(
                    "Unknown Unity CameraEvent '" + eventName + "'.");
            }

            RestoreMotionClearInternal();
            object visualizer = RequireVisualizer(RequireBetterAaAssembly());
            Camera camera = (Camera)RequireMethod(
                    visualizer.GetType(),
                    "GetSelectedCamera",
                    InstanceAny)
                .Invoke(visualizer, null);
            if (camera == null)
            {
                throw new InvalidOperationException(
                    "Redux Better AA has no selected diagnostic camera.");
            }

            var commandBuffer = new CommandBuffer
            {
                name = "Redux TestHarness Motion Clear"
            };
            commandBuffer.SetRenderTarget(
                BuiltinRenderTextureType.MotionVectors);
            commandBuffer.ClearRenderTarget(false, true, Color.clear);
            commandBuffer.SetRenderTarget(
                BuiltinRenderTextureType.CameraTarget);
            camera.AddCommandBuffer(cameraEvent, commandBuffer);

            _motionClearCamera = camera;
            _motionClearCommandBuffer = commandBuffer;
            _motionClearEvent = cameraEvent;
            return DynValue.NewString(cameraEvent.ToString());
        }

        private static DynValue RestoreMotionClear(
            ScriptExecutionContext context,
            CallbackArguments arguments)
        {
            bool active = _motionClearCommandBuffer != null;
            RestoreMotionClearInternal();
            return DynValue.NewBoolean(active);
        }

        private static void RestoreMotionClearInternal()
        {
            if (_motionClearCamera != null &&
                _motionClearCommandBuffer != null)
            {
                _motionClearCamera.RemoveCommandBuffer(
                    _motionClearEvent,
                    _motionClearCommandBuffer);
            }
            if (_motionClearCommandBuffer != null)
            {
                _motionClearCommandBuffer.Release();
            }
            _motionClearCamera = null;
            _motionClearCommandBuffer = null;
            _motionClearEvent = default(CameraEvent);
        }

        private static DynValue SuppressPostProcessing(
            ScriptExecutionContext context,
            CallbackArguments arguments)
        {
            string requested = "all";
            if (arguments != null && arguments.Count > 0 &&
                arguments[0].Type != DataType.Nil &&
                arguments[0].Type != DataType.Void)
            {
                requested = RequiredString(
                    arguments,
                    0,
                    "suppress_post_processing");
            }

            RestorePostProcessingInternal();
            object visualizer = RequireVisualizer(RequireBetterAaAssembly());
            Camera camera = (Camera)RequireMethod(
                    visualizer.GetType(),
                    "GetSelectedCamera",
                    InstanceAny)
                .Invoke(visualizer, null);
            if (camera == null)
            {
                throw new InvalidOperationException(
                    "Redux Better AA has no selected diagnostic camera.");
            }

            Component[] components = camera.GetComponents<Component>();
            for (int index = 0; index < components.Length; index++)
            {
                Behaviour behaviour = components[index] as Behaviour;
                if (behaviour == null ||
                    !MatchesPostProcessing(behaviour.GetType(), requested))
                {
                    continue;
                }

                SuppressedPostBehaviours.Add(behaviour);
                SuppressedPostBehaviourStates.Add(behaviour.enabled);
                behaviour.enabled = false;
            }
            return DynValue.NewNumber(SuppressedPostBehaviours.Count);
        }

        private static DynValue RestorePostProcessing(
            ScriptExecutionContext context,
            CallbackArguments arguments)
        {
            int count = SuppressedPostBehaviours.Count;
            RestorePostProcessingInternal();
            return DynValue.NewNumber(count);
        }

        private static DynValue SuppressCameraComponent(
            ScriptExecutionContext context,
            CallbackArguments arguments)
        {
            string requested = RequiredString(
                arguments,
                0,
                "suppress_camera_component");
            RestorePostProcessingInternal();
            object visualizer = RequireVisualizer(RequireBetterAaAssembly());
            Camera camera = (Camera)RequireMethod(
                    visualizer.GetType(),
                    "GetSelectedCamera",
                    InstanceAny)
                .Invoke(visualizer, null);
            if (camera == null)
            {
                throw new InvalidOperationException(
                    "Redux Better AA has no selected diagnostic camera.");
            }

            Component[] components = camera.GetComponents<Component>();
            for (int index = 0; index < components.Length; index++)
            {
                Behaviour behaviour = components[index] as Behaviour;
                if (behaviour == null || behaviour is Camera)
                {
                    continue;
                }
                bool wildcard = string.Equals(
                    requested,
                    "*",
                    StringComparison.Ordinal);
                if (!wildcard && !string.Equals(
                    behaviour.GetType().FullName,
                    requested,
                    StringComparison.Ordinal))
                {
                    continue;
                }
                SuppressedPostBehaviours.Add(behaviour);
                SuppressedPostBehaviourStates.Add(behaviour.enabled);
                behaviour.enabled = false;
            }
            return DynValue.NewNumber(SuppressedPostBehaviours.Count);
        }

        private static void RestorePostProcessingInternal()
        {
            int count = Math.Min(
                SuppressedPostBehaviours.Count,
                SuppressedPostBehaviourStates.Count);
            for (int index = 0; index < count; index++)
            {
                Behaviour behaviour = SuppressedPostBehaviours[index];
                if (behaviour != null)
                {
                    behaviour.enabled = SuppressedPostBehaviourStates[index];
                }
            }
            SuppressedPostBehaviours.Clear();
            SuppressedPostBehaviourStates.Clear();
        }

        private static bool MatchesPostProcessing(Type type, string requested)
        {
            string fullName = type.FullName ?? type.Name;
            bool postProcessingV1 = string.Equals(
                fullName,
                "UnityEngine.PostProcessing.PostProcessingBehaviour",
                StringComparison.Ordinal);
            bool postProcessingV2 = string.Equals(
                fullName,
                "UnityEngine.Rendering.PostProcessing.PostProcessLayer",
                StringComparison.Ordinal);
            if (string.Equals(requested, "all", StringComparison.OrdinalIgnoreCase))
            {
                return postProcessingV1 || postProcessingV2;
            }
            if (string.Equals(requested, "ppv1", StringComparison.OrdinalIgnoreCase))
            {
                return postProcessingV1;
            }
            if (string.Equals(requested, "ppv2", StringComparison.OrdinalIgnoreCase))
            {
                return postProcessingV2;
            }
            throw new ArgumentException(
                "Unknown post-processing group '" + requested + "'.");
        }

        private static string DescribeComponents(Camera camera)
        {
            Component[] components = camera.GetComponents<Component>();
            var builder = new StringBuilder(256);
            for (int index = 0; index < components.Length; index++)
            {
                Component component = components[index];
                if (component == null)
                {
                    continue;
                }
                if (builder.Length > 0)
                {
                    builder.Append(" | ");
                }
                builder.Append(component.GetType().FullName);
                Behaviour behaviour = component as Behaviour;
                if (behaviour != null)
                {
                    builder.Append(" enabled=");
                    builder.Append(behaviour.enabled ? "true" : "false");
                }
            }
            return builder.ToString();
        }

        private static string DescribeCommandBuffers(Camera camera)
        {
            var builder = new StringBuilder(512);
            Array events = Enum.GetValues(typeof(CameraEvent));
            for (int eventIndex = 0; eventIndex < events.Length; eventIndex++)
            {
                CameraEvent cameraEvent = (CameraEvent)events.GetValue(eventIndex);
                CommandBuffer[] buffers = camera.GetCommandBuffers(cameraEvent);
                for (int bufferIndex = 0; bufferIndex < buffers.Length; bufferIndex++)
                {
                    CommandBuffer buffer = buffers[bufferIndex];
                    if (builder.Length > 0)
                    {
                        builder.Append(" | ");
                    }
                    builder.Append(cameraEvent);
                    builder.Append(':');
                    builder.Append(buffer == null ? "<null>" : buffer.name);
                }
            }
            return builder.ToString();
        }

        private static DynValue OverrideCameraState(
            ScriptExecutionContext context,
            CallbackArguments arguments)
        {
            string requested = RequiredString(
                arguments,
                0,
                "override_camera_state");
            RestoreCameraStateInternal();
            object visualizer = RequireVisualizer(RequireBetterAaAssembly());
            Camera camera = (Camera)RequireMethod(
                    visualizer.GetType(),
                    "GetSelectedCamera",
                    InstanceAny)
                .Invoke(visualizer, null);
            if (camera == null)
            {
                throw new InvalidOperationException(
                    "Redux Better AA has no selected diagnostic camera.");
            }

            _overriddenCamera = camera;
            _overriddenCullingMask = camera.cullingMask;
            _overriddenForceIntoRenderTexture = camera.forceIntoRenderTexture;
            _overriddenClearFlags = camera.clearFlags;
            _overriddenRenderingPath = camera.renderingPath;
            _overriddenDepthTextureMode = camera.depthTextureMode;

            if (string.Equals(requested, "noCulling", StringComparison.OrdinalIgnoreCase))
            {
                camera.cullingMask = 0;
            }
            else if (string.Equals(
                requested,
                "forceIntoRenderTextureOff",
                StringComparison.OrdinalIgnoreCase))
            {
                camera.forceIntoRenderTexture = false;
            }
            else if (string.Equals(
                requested,
                "clearColor",
                StringComparison.OrdinalIgnoreCase))
            {
                camera.clearFlags = CameraClearFlags.Color;
            }
            else if (string.Equals(
                requested,
                "forward",
                StringComparison.OrdinalIgnoreCase))
            {
                camera.renderingPath = RenderingPath.Forward;
            }
            else if (string.Equals(
                requested,
                "motionOnly",
                StringComparison.OrdinalIgnoreCase))
            {
                camera.depthTextureMode = DepthTextureMode.MotionVectors;
            }
            else if (string.Equals(
                requested,
                "resetProjection",
                StringComparison.OrdinalIgnoreCase))
            {
                camera.ResetProjectionMatrix();
            }
            else if (string.Equals(
                requested,
                "resetWorldToCamera",
                StringComparison.OrdinalIgnoreCase))
            {
                camera.ResetWorldToCameraMatrix();
            }
            else if (string.Equals(
                requested,
                "resetBothMatrices",
                StringComparison.OrdinalIgnoreCase))
            {
                camera.ResetProjectionMatrix();
                camera.ResetWorldToCameraMatrix();
            }
            else
            {
                RestoreCameraStateInternal();
                throw new ArgumentException(
                    "Unknown camera-state override '" + requested + "'.");
            }
            return DynValue.NewString(requested);
        }

        private static DynValue RestoreCameraState(
            ScriptExecutionContext context,
            CallbackArguments arguments)
        {
            bool active = _overriddenCamera != null;
            RestoreCameraStateInternal();
            return DynValue.NewBoolean(active);
        }

        private static DynValue SetCameraCullingMask(
            ScriptExecutionContext context,
            CallbackArguments arguments)
        {
            int mask = (int)RequiredNumber(
                arguments,
                0,
                "set_camera_culling_mask");
            Camera camera = GetSelectedDiagnosticCamera();
            if (camera == null)
            {
                throw new InvalidOperationException(
                    "Redux Better AA has no selected diagnostic camera.");
            }
            camera.cullingMask = mask;
            return DynValue.NewNumber(mask);
        }

        private static void RestoreCameraStateInternal()
        {
            if (_overriddenCamera != null)
            {
                _overriddenCamera.cullingMask = _overriddenCullingMask;
                _overriddenCamera.forceIntoRenderTexture =
                    _overriddenForceIntoRenderTexture;
                _overriddenCamera.clearFlags = _overriddenClearFlags;
                _overriddenCamera.renderingPath = _overriddenRenderingPath;
                _overriddenCamera.depthTextureMode =
                    _overriddenDepthTextureMode;
            }
            _overriddenCamera = null;
        }

        private static DynValue SuppressCamera(
            ScriptExecutionContext context,
            CallbackArguments arguments)
        {
            string requested = RequiredString(
                arguments,
                0,
                "suppress_camera");
            RestoreSuppressedCamerasInternal();
            Camera[] cameras = Resources.FindObjectsOfTypeAll<Camera>();
            for (int index = 0; index < cameras.Length; index++)
            {
                Camera camera = cameras[index];
                if (camera == null ||
                    !string.Equals(
                        camera.name,
                        requested,
                        StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }
                SuppressedCameras.Add(camera);
                SuppressedCameraStates.Add(camera.enabled);
                camera.enabled = false;
            }
            return DynValue.NewNumber(SuppressedCameras.Count);
        }

        private static DynValue RestoreSuppressedCameras(
            ScriptExecutionContext context,
            CallbackArguments arguments)
        {
            int count = SuppressedCameras.Count;
            RestoreSuppressedCamerasInternal();
            return DynValue.NewNumber(count);
        }

        private static void RestoreSuppressedCamerasInternal()
        {
            int count = Math.Min(
                SuppressedCameras.Count,
                SuppressedCameraStates.Count);
            for (int index = 0; index < count; index++)
            {
                Camera camera = SuppressedCameras[index];
                if (camera != null)
                {
                    camera.enabled = SuppressedCameraStates[index];
                }
            }
            SuppressedCameras.Clear();
            SuppressedCameraStates.Clear();
        }

        private static DynValue UseCloneMotionCamera(
            ScriptExecutionContext context,
            CallbackArguments arguments)
        {
            bool renderToScreen = arguments != null && arguments.Count > 0 &&
                arguments[0].Type != DataType.Nil &&
                arguments[0].Type != DataType.Void &&
                string.Equals(
                    RequiredString(arguments, 0, "use_clone_motion_camera"),
                    "screen",
                    StringComparison.OrdinalIgnoreCase);
            RestoreCloneMotionCameraInternal();
            object visualizer = RequireVisualizer(RequireBetterAaAssembly());
            Camera source = (Camera)RequireMethod(
                    visualizer.GetType(),
                    "GetSelectedCamera",
                    InstanceAny)
                .Invoke(visualizer, null);
            FieldInfo materialField = visualizer.GetType().GetField(
                "_material",
                InstanceAny);
            Material visualizerMaterial = materialField == null
                ? null
                : materialField.GetValue(visualizer) as Material;
            if (source == null || visualizerMaterial == null)
            {
                throw new InvalidOperationException(
                    "Redux Better AA's selected camera/material is not attached.");
            }

            int width = Math.Max(1, source.scaledPixelWidth);
            int height = Math.Max(1, source.scaledPixelHeight);
            RenderTexture colorTexture = null;
            if (!renderToScreen)
            {
                colorTexture = new RenderTexture(
                    width,
                    height,
                    24,
                    RenderTextureFormat.ARGB32,
                    RenderTextureReadWrite.Linear)
                {
                    name = "Redux TestHarness Clone Camera Color",
                    hideFlags = HideFlags.HideAndDontSave,
                    useMipMap = false,
                    autoGenerateMips = false
                };
                colorTexture.Create();
            }
            var motionTexture = new RenderTexture(
                width,
                height,
                0,
                RenderTextureFormat.RGHalf,
                RenderTextureReadWrite.Linear)
            {
                name = "Redux TestHarness Clone Camera Motion",
                filterMode = FilterMode.Point,
                wrapMode = TextureWrapMode.Clamp,
                hideFlags = HideFlags.HideAndDontSave,
                useMipMap = false,
                autoGenerateMips = false
            };
            motionTexture.Create();
            if ((!renderToScreen && !colorTexture.IsCreated()) ||
                !motionTexture.IsCreated())
            {
                if (colorTexture != null && colorTexture.IsCreated())
                {
                    colorTexture.Release();
                }
                if (motionTexture.IsCreated())
                {
                    motionTexture.Release();
                }
                if (colorTexture != null)
                {
                    UnityEngine.Object.Destroy(colorTexture);
                }
                UnityEngine.Object.Destroy(motionTexture);
                throw new InvalidOperationException(
                    "The clone-camera render textures could not be created.");
            }

            var gameObject = new GameObject(
                "Redux TestHarness Clone Motion Camera");
            gameObject.hideFlags = HideFlags.HideAndDontSave;
            Camera camera = gameObject.AddComponent<Camera>();
            camera.CopyFrom(source);
            camera.name = "ReduxTestHarness_CloneMotionCamera";
            camera.transform.SetPositionAndRotation(
                source.transform.position,
                source.transform.rotation);
            camera.targetTexture = renderToScreen ? null : colorTexture;
            camera.clearFlags = renderToScreen
                ? source.clearFlags
                : CameraClearFlags.Color;
            camera.backgroundColor = Color.black;
            camera.forceIntoRenderTexture = renderToScreen
                ? source.forceIntoRenderTexture
                : false;
            if (renderToScreen)
            {
                camera.depth = source.depth + 0.25f;
            }
            camera.depthTextureMode |=
                DepthTextureMode.Depth | DepthTextureMode.MotionVectors;

            var commandBuffer = new CommandBuffer
            {
                name = "Redux TestHarness Clone Motion Capture"
            };
            commandBuffer.Blit(
                BuiltinRenderTextureType.MotionVectors,
                motionTexture);
            camera.AddCommandBuffer(
                CameraEvent.BeforeImageEffects,
                commandBuffer);
            CommandBuffer displayCommandBuffer = null;
            if (renderToScreen)
            {
                displayCommandBuffer = new CommandBuffer
                {
                    name = "Redux TestHarness Clone Motion Display"
                };
                displayCommandBuffer.GetTemporaryRT(
                    CloneDisplayTemporary,
                    -1,
                    -1,
                    0,
                    FilterMode.Point,
                    RenderTextureFormat.Default);
                displayCommandBuffer.Blit(
                    BuiltinRenderTextureType.CurrentActive,
                    CloneDisplayTemporary);
                displayCommandBuffer.Blit(
                    CloneDisplayTemporary,
                    BuiltinRenderTextureType.CameraTarget,
                    visualizerMaterial,
                    2);
                displayCommandBuffer.ReleaseTemporaryRT(CloneDisplayTemporary);
                camera.AddCommandBuffer(
                    CameraEvent.AfterEverything,
                    displayCommandBuffer);
            }
            visualizerMaterial.SetTexture(
                CameraMotionVectorsTexture,
                motionTexture);
            camera.enabled = true;

            _cloneMotionCamera = camera;
            _cloneMotionGameObject = gameObject;
            _cloneColorTexture = colorTexture;
            _cloneMotionTexture = motionTexture;
            _cloneMotionCommandBuffer = commandBuffer;
            _cloneDisplayCommandBuffer = displayCommandBuffer;
            _cloneMotionVisualizerMaterial = visualizerMaterial;
            return DynValue.NewString(width + "x" + height);
        }

        private static DynValue RestoreCloneMotionCamera(
            ScriptExecutionContext context,
            CallbackArguments arguments)
        {
            bool active = _cloneMotionCamera != null;
            RestoreCloneMotionCameraInternal();
            return DynValue.NewBoolean(active);
        }

        private static DynValue CloneMatrixSnapshot(Script script)
        {
            if (_cloneMotionCamera == null)
            {
                throw new InvalidOperationException(
                    "No clone motion camera is active.");
            }
            object visualizer = RequireVisualizer(RequireBetterAaAssembly());
            Camera source = (Camera)RequireMethod(
                    visualizer.GetType(),
                    "GetSelectedCamera",
                    InstanceAny)
                .Invoke(visualizer, null);
            if (source == null)
            {
                throw new InvalidOperationException(
                    "Redux Better AA has no selected diagnostic camera.");
            }

            Matrix4x4 sourceGpu = CurrentGpuViewProjection(source);
            Matrix4x4 cloneGpu = CurrentGpuViewProjection(_cloneMotionCamera);
            var result = new Table(script);
            result.Set(
                "sourceCloneProjectionMaxAbs",
                DynValue.NewNumber(MatrixDifferenceMaxAbs(
                    source.projectionMatrix,
                    _cloneMotionCamera.projectionMatrix)));
            result.Set(
                "sourceCloneNonJitteredProjectionMaxAbs",
                DynValue.NewNumber(MatrixDifferenceMaxAbs(
                    source.nonJitteredProjectionMatrix,
                    _cloneMotionCamera.nonJitteredProjectionMatrix)));
            result.Set(
                "sourceCloneViewMaxAbs",
                DynValue.NewNumber(MatrixDifferenceMaxAbs(
                    source.worldToCameraMatrix,
                    _cloneMotionCamera.worldToCameraMatrix)));
            result.Set(
                "sourceCloneGpuVpMaxAbs",
                DynValue.NewNumber(MatrixDifferenceMaxAbs(
                    sourceGpu,
                    cloneGpu)));
            result.Set(
                "cloneCurrentPreviousGpuMaxAbs",
                DynValue.NewNumber(MatrixDifferenceMaxAbs(
                    cloneGpu,
                    _cloneMotionCamera.previousViewProjectionMatrix)));
            result.Set(
                "sourceCurrentPreviousGpuMaxAbs",
                DynValue.NewNumber(MatrixDifferenceMaxAbs(
                    sourceGpu,
                    source.previousViewProjectionMatrix)));
            result.Set(
                "cloneCurrentGpuMatrix",
                DynValue.NewString(cloneGpu.ToString("F6")));
            result.Set(
                "clonePreviousMatrix",
                DynValue.NewString(
                    _cloneMotionCamera.previousViewProjectionMatrix.ToString(
                        "F6")));
            result.Set(
                "clonePosition",
                DynValue.NewString(
                    _cloneMotionCamera.transform.position.ToString("F6")));
            result.Set(
                "cloneRotation",
                DynValue.NewString(
                    _cloneMotionCamera.transform.rotation.eulerAngles.ToString(
                        "F6")));
            result.Set("frame", DynValue.NewNumber(Time.frameCount));
            return DynValue.NewTable(result);
        }

        private static void RestoreCloneMotionCameraInternal()
        {
            if (_cloneMotionVisualizerMaterial != null)
            {
                _cloneMotionVisualizerMaterial.SetTexture(
                    CameraMotionVectorsTexture,
                    null);
            }
            if (_cloneMotionCamera != null &&
                _cloneMotionCommandBuffer != null)
            {
                _cloneMotionCamera.RemoveCommandBuffer(
                    CameraEvent.BeforeImageEffects,
                    _cloneMotionCommandBuffer);
            }
            if (_cloneMotionCamera != null &&
                _cloneDisplayCommandBuffer != null)
            {
                _cloneMotionCamera.RemoveCommandBuffer(
                    CameraEvent.AfterEverything,
                    _cloneDisplayCommandBuffer);
            }
            if (_cloneMotionCommandBuffer != null)
            {
                _cloneMotionCommandBuffer.Release();
            }
            if (_cloneDisplayCommandBuffer != null)
            {
                _cloneDisplayCommandBuffer.Release();
            }
            if (_cloneColorTexture != null)
            {
                if (_cloneColorTexture.IsCreated())
                {
                    _cloneColorTexture.Release();
                }
                UnityEngine.Object.Destroy(_cloneColorTexture);
            }
            if (_cloneMotionTexture != null)
            {
                if (_cloneMotionTexture.IsCreated())
                {
                    _cloneMotionTexture.Release();
                }
                UnityEngine.Object.Destroy(_cloneMotionTexture);
            }
            if (_cloneMotionGameObject != null)
            {
                UnityEngine.Object.Destroy(_cloneMotionGameObject);
            }
            _cloneMotionCamera = null;
            _cloneMotionGameObject = null;
            _cloneColorTexture = null;
            _cloneMotionTexture = null;
            _cloneMotionCommandBuffer = null;
            _cloneDisplayCommandBuffer = null;
            _cloneMotionVisualizerMaterial = null;
        }

        private static Matrix4x4 CurrentGpuViewProjection(Camera camera)
        {
            return GL.GetGPUProjectionMatrix(
                    camera.nonJitteredProjectionMatrix,
                    camera.targetTexture != null ||
                    camera.forceIntoRenderTexture) *
                camera.worldToCameraMatrix;
        }

        private static float MatrixDifferenceMaxAbs(
            Matrix4x4 left,
            Matrix4x4 right)
        {
            float maximum = 0.0f;
            for (int index = 0; index < 16; index++)
            {
                maximum = Mathf.Max(maximum, Mathf.Abs(left[index] - right[index]));
            }
            return maximum;
        }

        private static object RequireVisualizer(Assembly assembly)
        {
            Type probeType = RequireType(
                assembly,
                "ReduxBetterAA.Diagnostics.Phase1ProbeService");
            object probe = RequireStaticField(probeType, "Current");
            FieldInfo field = probeType.GetField("_visualizer", InstanceAny);
            if (field == null)
            {
                throw new MissingFieldException(probeType.FullName, "_visualizer");
            }
            object visualizer = field.GetValue(probe);
            if (visualizer == null)
            {
                throw new InvalidOperationException(
                    "Redux Better AA's buffer visualizer is not initialized.");
            }
            return visualizer;
        }

        private static Assembly FindBetterAaAssembly()
        {
            Assembly[] assemblies = AppDomain.CurrentDomain.GetAssemblies();
            for (int index = 0; index < assemblies.Length; index++)
            {
                Assembly assembly = assemblies[index];
                if (string.Equals(
                        assembly.GetName().Name,
                        "ReduxBetterAA",
                        StringComparison.OrdinalIgnoreCase))
                {
                    return assembly;
                }
            }
            return null;
        }

        private static void RestoreProductionMotionDefaults()
        {
            Assembly assembly = FindBetterAaAssembly();
            if (assembly == null)
            {
                return;
            }
            try
            {
                Type vegetationType = assembly.GetType(
                    "ReduxBetterAA.Rendering.VegetationMotionCompatibility",
                    false);
                FieldInfo vegetationCurrent = vegetationType == null
                    ? null
                    : vegetationType.GetField("Current", StaticAny);
                object vegetation = vegetationCurrent == null
                    ? null
                    : vegetationCurrent.GetValue(null);
                MethodInfo setVegetation = vegetationType == null
                    ? null
                    : vegetationType.GetMethod("SetEnabled", InstanceAny);
                if (vegetation != null && setVegetation != null)
                {
                    setVegetation.Invoke(vegetation, new object[] { true });
                }

                Type coordinatorType = assembly.GetType(
                    "ReduxBetterAA.Rendering.TemporalCoordinator",
                    false);
                FieldInfo coordinatorCurrent = coordinatorType == null
                    ? null
                    : coordinatorType.GetField("Current", StaticAny);
                object coordinator = coordinatorCurrent == null
                    ? null
                    : coordinatorCurrent.GetValue(null);
                MethodInfo setSanitizer = coordinatorType == null
                    ? null
                    : coordinatorType.GetMethod(
                        "SetMotionVectorSanitizerEnabled",
                        InstanceAny);
                if (coordinator != null && setSanitizer != null)
                {
                    setSanitizer.Invoke(coordinator, new object[] { false });
                }

                MethodInfo setDepthMask = coordinatorType == null
                    ? null
                    : coordinatorType.GetMethod(
                        "SetDepthDisocclusionMaskEnabled",
                        InstanceAny);
                if (coordinator != null && setDepthMask != null)
                {
                    setDepthMask.Invoke(coordinator, new object[] { true });
                }

                MethodInfo setNoDepthBias = coordinatorType == null
                    ? null
                    : coordinatorType.GetMethod(
                        "SetDlaaNoDepthBiasEnabled",
                        InstanceAny);
                if (coordinator != null && setNoDepthBias != null)
                {
                    setNoDepthBias.Invoke(coordinator, new object[] { false });
                }

                MethodInfo setFullBias = coordinatorType == null
                    ? null
                    : coordinatorType.GetMethod(
                        "SetDlaaFullBiasEnabled",
                        InstanceAny);
                if (coordinator != null && setFullBias != null)
                {
                    setFullBias.Invoke(coordinator, new object[] { false });
                }

                MethodInfo setDlaaBypass = coordinatorType == null
                    ? null
                    : coordinatorType.GetMethod(
                        "SetDlaaExecutionBypass",
                        InstanceAny);
                if (coordinator != null && setDlaaBypass != null)
                {
                    setDlaaBypass.Invoke(coordinator, new object[] { false });
                }

                MethodInfo setTransparentJitter = coordinatorType == null
                    ? null
                    : coordinatorType.GetMethod(
                        "SetDlaaTransparentProjectionJitter",
                        InstanceAny);
                if (coordinator != null && setTransparentJitter != null)
                {
                    setTransparentJitter.Invoke(
                        coordinator,
                        new object[] { false });
                }
            }
            catch (Exception exception)
            {
                Debug.LogWarning(
                    "[ReduxTestHarness/BetterAA] Failed to restore production " +
                    "motion defaults: " + exception.GetType().Name + ": " +
                    exception.Message);
            }
        }

        private static Assembly RequireBetterAaAssembly()
        {
            Assembly assembly = FindBetterAaAssembly();
            if (assembly == null)
            {
                throw new InvalidOperationException(
                    "Redux Better AA is not loaded in this player.");
            }
            return assembly;
        }

        private static Type RequireType(Assembly assembly, string name)
        {
            Type type = assembly.GetType(name, false);
            if (type == null)
            {
                throw new TypeLoadException(
                    "Redux Better AA type '" + name + "' was not found.");
            }
            return type;
        }

        private static object RequireStaticField(Type type, string name)
        {
            FieldInfo field = type.GetField(name, StaticAny);
            if (field == null)
            {
                throw new MissingFieldException(type.FullName, name);
            }
            object value = field.GetValue(null);
            if (value == null)
            {
                throw new InvalidOperationException(
                    type.FullName + "." + name + " is not initialized.");
            }
            return value;
        }

        private static MethodInfo RequireMethod(
            Type type,
            string name,
            BindingFlags flags)
        {
            MethodInfo method = type.GetMethod(name, flags);
            if (method == null)
            {
                throw new MissingMethodException(type.FullName, name);
            }
            return method;
        }

        private static PropertyInfo RequireProperty(Type type, string name)
        {
            PropertyInfo property = type.GetProperty(name, InstanceAny);
            if (property == null)
            {
                throw new MissingMemberException(type.FullName, name);
            }
            return property;
        }

        private static string RequiredString(
            CallbackArguments arguments,
            int index,
            string operation)
        {
            if (arguments == null || arguments.Count <= index ||
                arguments[index].Type != DataType.String ||
                string.IsNullOrWhiteSpace(arguments[index].String))
            {
                throw new ScriptRuntimeException(
                    "ReduxBetterAA." + operation + " requires a non-empty string.");
            }
            return arguments[index].String.Trim();
        }

        private static bool RequiredBoolean(
            CallbackArguments arguments,
            int index,
            string operation)
        {
            if (arguments == null || arguments.Count <= index ||
                arguments[index].Type != DataType.Boolean)
            {
                throw new ScriptRuntimeException(
                    "ReduxBetterAA." + operation + " requires a boolean.");
            }
            return arguments[index].Boolean;
        }

        private static double RequiredNumber(
            CallbackArguments arguments,
            int index,
            string operation)
        {
            if (index >= arguments.Count ||
                arguments[index].Type != DataType.Number)
            {
                throw new ScriptRuntimeException(
                    "ReduxBetterAA." + operation + " requires a number.");
            }
            return arguments[index].Number;
        }

        private static string NormalizeViewName(string name)
        {
            string compact = name.Replace(" ", string.Empty)
                .Replace("_", string.Empty)
                .Replace("-", string.Empty);
            if (string.Equals(compact, "raw", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(compact, "rawmotion", StringComparison.OrdinalIgnoreCase))
            {
                return "MotionVectorsRaw";
            }
            if (string.Equals(compact, "normalized", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(compact, "normalizedmotion", StringComparison.OrdinalIgnoreCase))
            {
                return "MotionVectorsNormalized";
            }
            if (string.Equals(compact, "magnitude", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(compact, "magnitudeangle", StringComparison.OrdinalIgnoreCase))
            {
                return "MotionVectorsMagnitudeAngle";
            }
            if (string.Equals(compact, "validity", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(compact, "motionvalidity", StringComparison.OrdinalIgnoreCase))
            {
                return "MotionVectorsValidity";
            }
            return name;
        }
    }
}
