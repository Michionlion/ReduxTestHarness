Test.name("Redux Better AA DLAA vendor-input isolation")

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

local function capture_sequence(prefix, count)
    for index = 1, count do
        Test.capture.screenshot(
            string.format("%s-%02d", prefix, index),
            { scale = 1, hideUI = false, waitFrames = 2 })
    end
end

capture_sequence("00-off", 3)

better_aa.set_depth_disocclusion_mask(true)
better_aa.set_backend("NvidiaDlaa")
Test.wait.frames(90)
capture_sequence("10-mask-on", 8)

better_aa.set_depth_disocclusion_mask(false)
Test.wait.frames(90)
capture_sequence("20-mask-off", 8)

better_aa.clear_motion_at_event("BeforeImageEffects")
Test.wait.frames(90)
capture_sequence("30-mask-off-zero-motion", 8)

better_aa.set_depth_disocclusion_mask(true)
Test.wait.frames(90)
capture_sequence("40-mask-on-zero-motion", 8)

better_aa.restore_motion_clear()
Test.wait.frames(90)
capture_sequence("50-mask-on-restored-motion", 4)

better_aa.set_backend("Off")
better_aa.set_depth_disocclusion_mask(true)
better_aa.restore_motion_clear()
Test.report.note(
    "Compared the fixed DLAA K artifact pose with the custom bias mask on/off and raw motion present/cleared before image effects.")
