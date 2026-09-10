Test.name("Redux Better AA DLAA remaining camera-effect isolation")

Test.assert.true_(
    Test.mod.is_loaded("ReduxBetterAA"),
    "ReduxBetterAA is required")
local better_aa = Test.mod.extension("ReduxBetterAA")
Test.assert.not_equal(
    better_aa,
    nil,
    "ReduxBetterAA TestHarness adapter is required")

better_aa.set_backend("Off")
Test.game.load_save("local/launchpad-fly-safe-15")
Test.game.wait_for_state("Flight", 45)
Test.flight.start("Fly Safe-15")
Test.camera.mode("Flight")
Test.camera.target_vessel()
Test.camera.orbit {
    distance = 2200,
    yaw = 303,
    pitch = 35,
    fov = 55
}
Test.render.wait_stable(60)
better_aa.select_camera("FlightCameraPhysics_Main")
better_aa.set_depth_disocclusion_mask(false)

local function capture(prefix)
    Test.capture.screenshot(
        prefix .. "-01",
        { scale = 1, hideUI = false, waitFrames = 2 })
    Test.capture.screenshot(
        prefix .. "-02",
        { scale = 1, hideUI = false, waitFrames = 2 })
end

local function restart_dlaa()
    better_aa.set_backend("Off")
    better_aa.set_backend("NvidiaDlaa")
    Test.wait.frames(75)
end

restart_dlaa()
capture("00-normal")

better_aa.restore_camera_command_buffers()
local all_buffers = better_aa.suppress_camera_command_buffers("")
Test.assert.greater(all_buffers, 0, "Expected physics-camera command buffers")
restart_dlaa()
capture("05-without-all-command-buffers")

local command_buffers = {
    { prefix = "10-without-star-visibility", filter = "Star Visibility" },
    { prefix = "11-without-clear-shadow", filter = "ClearShadowCommandBuffer" },
    { prefix = "12-without-draw-ocean", filter = "DrawOceanCommand" },
    { prefix = "13-without-water-depth", filter = "GenerateWaterDepthCommand" },
    { prefix = "14-without-terrain-blend-copy", filter = "Copy TerrainBlend Buffers" },
    { prefix = "15-without-opaque-post", filter = "Opaque Only Post-processing" },
    { prefix = "16-without-decalicious", filter = "Decalicious" }
}

for _, item in ipairs(command_buffers) do
    better_aa.restore_camera_command_buffers()
    local removed = better_aa.suppress_camera_command_buffers(item.filter)
    Test.assert.greater(
        removed,
        0,
        "Expected command buffer matching " .. item.filter)
    restart_dlaa()
    capture(item.prefix)
end
better_aa.restore_camera_command_buffers()

local components = {
    { prefix = "20-without-camera-effects", type = "KSP.Rendering.CameraEffectsSystem" },
    { prefix = "21-without-flare-layer", type = "UnityEngine.FlareLayer" },
    { prefix = "22-without-volume-cloud-component", type = "KSP.VolumeCloud.VolumeCloudRenderer" },
    { prefix = "23-without-underwater-component", type = "UnderWaterEffect" },
    { prefix = "24-without-ocean-data", type = "SetOceanData" },
    { prefix = "25-without-outline", type = "KSP.OutlineEffect" },
    { prefix = "26-without-terrain-blend", type = "CreateTerrainBlendBuffers" }
}

for _, item in ipairs(components) do
    better_aa.restore_camera_components()
    local disabled = better_aa.suppress_camera_component(item.type)
    Test.assert.greater(
        disabled,
        0,
        "Expected camera component " .. item.type)
    restart_dlaa()
    capture(item.prefix)
end

better_aa.restore_camera_components()
better_aa.restore_camera_command_buffers()
better_aa.set_depth_disocclusion_mask(true)
better_aa.set_backend("Off")
Test.report.note(
    "Compared the fixed DLAA K artifact pose with all camera command buffers removed, then each remaining likely command buffer and camera effect removed independently.")
