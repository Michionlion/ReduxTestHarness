Test.name("Redux Better AA DLAA sharpening and depth isolation")

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

better_aa.set_depth_disocclusion_mask(false)
better_aa.set_vendor_sharpness("NvidiaDlaa", 0.15)
better_aa.set_backend("NvidiaDlaa")
Test.wait.frames(90)
capture_sequence("10-sharpness-015", 8)

better_aa.set_vendor_sharpness("NvidiaDlaa", 0.0)
Test.wait.frames(90)
capture_sequence("20-sharpness-000", 8)

better_aa.override_depth_with_far("BeforeImageEffects")
Test.wait.frames(90)
capture_sequence("30-sharpness-000-far-depth", 8)

better_aa.set_vendor_sharpness("NvidiaDlaa", 0.15)
Test.wait.frames(90)
capture_sequence("40-sharpness-015-far-depth", 8)

better_aa.restore_depth_override()
better_aa.set_vendor_sharpness("NvidiaDlaa", 0.15)
better_aa.set_depth_disocclusion_mask(true)
better_aa.set_backend("Off")
Test.report.note(
    "Compared DLAA K at 0.15/0.0 sharpness, then repeated both with a constant far-depth texture and the custom bias mask disabled.")
