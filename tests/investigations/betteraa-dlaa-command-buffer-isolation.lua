Test.name("Redux Better AA DLAA command-buffer isolation")

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
better_aa.set_backend("NvidiaDlaa")
Test.wait.frames(90)

local function capture_sequence(prefix, count)
    for index = 1, count do
        Test.capture.screenshot(
            string.format("%s-%02d", prefix, index),
            { scale = 1, hideUI = false, waitFrames = 2 })
    end
end

capture_sequence("00-normal", 6)

local command_buffers = {
    { prefix = "10-without-cloud-color", filter = "CloudCommandBuffer" },
    { prefix = "20-without-cloud-shadow", filter = "cloudsShadowCommandBuffer" },
    { prefix = "30-without-scaled-shadow", filter = "Scaled Space ShadowMap Resolve Pass" },
    { prefix = "40-without-ambient-occlusion", filter = "Ambient Occlusion" },
    { prefix = "50-without-post-processing", filter = "Post-processing" },
    { prefix = "60-without-underwater", filter = "Underwater" }
}

for _, item in ipairs(command_buffers) do
    better_aa.restore_camera_command_buffers()
    local removed = better_aa.suppress_camera_command_buffers(item.filter)
    Test.assert.greater(
        removed,
        0,
        "Expected command buffer matching " .. item.filter)
    better_aa.set_backend("Off")
    better_aa.set_backend("NvidiaDlaa")
    Test.wait.frames(90)
    capture_sequence(item.prefix, 4)
end

better_aa.restore_camera_command_buffers()
better_aa.set_depth_disocclusion_mask(true)
better_aa.set_backend("Off")
Test.report.note(
    "Compared the fixed DLAA K artifact pose while removing each major physics-camera command buffer independently.")
