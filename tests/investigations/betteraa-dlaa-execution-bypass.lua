Test.name("Redux Better AA DLAA execution bypass isolation")

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
better_aa.set_vendor_sharpness("NvidiaDlaa", 0.15)
better_aa.set_dlaa_execution_bypass(false)
better_aa.set_backend("NvidiaDlaa")

local function capture_sequence(prefix, count)
    for index = 1, count do
        Test.capture.screenshot(
            string.format("%s-%02d", prefix, index),
            { scale = 1, hideUI = false, waitFrames = 2 })
    end
end

Test.wait.frames(90)
capture_sequence("10-ngx-evaluation", 8)

better_aa.set_dlaa_execution_bypass(true)
Test.wait.frames(90)
capture_sequence("20-backend-state-with-ngx-bypassed", 8)

better_aa.set_dlaa_execution_bypass(false)
Test.wait.frames(90)
capture_sequence("30-ngx-evaluation-restored", 8)

better_aa.set_backend("Off")
better_aa.set_dlaa_execution_bypass(false)
better_aa.set_depth_disocclusion_mask(true)
Test.report.note(
    "Held the complete DLAA camera/backend state constant and skipped only the NGX execution call for the middle capture group.")
