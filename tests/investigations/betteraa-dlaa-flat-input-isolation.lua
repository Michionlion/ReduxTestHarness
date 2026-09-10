Test.name("Redux Better AA DLAA flat input isolation")

Test.assert.true_(Test.mod.is_loaded("ReduxBetterAA"), "ReduxBetterAA is required")
local better_aa = Test.mod.extension("ReduxBetterAA")
Test.assert.not_equal(better_aa, nil, "ReduxBetterAA adapter is required")

better_aa.set_backend("Off")
Test.game.load_save("local/launchpad-cloudy-fly-safe-15")
Test.game.wait_for_state("Flight", 45)
Test.flight.start("Fly Safe-15")
Test.camera.mode("Flight")
Test.camera.target_vessel()
Test.wait.frames(30)
better_aa.select_camera("FlightCameraPhysics_Main")

Test.camera.absolute {
    position = { -13.9586172, -184.676163, -71.94289 },
    rotation = { 0.561901, 0.05674237, 0.144563019, -0.8124956 },
    fov = 60
}

local function burst(prefix)
    Test.wait.frames(90)
    for index = 1, 6 do
        Test.capture.screenshot(
            string.format("%s-%02d", prefix, index),
            { scale = 1, hideUI = true, waitFrames = 1 })
    end
end

better_aa.set_vendor_auto_exposure("NvidiaDlaa", false, 1.0, false)
better_aa.set_vendor_sharpness("NvidiaDlaa", 0.0)
better_aa.set_backend("NvidiaDlaa")
burst("10-scene-color")

better_aa.set_dlaa_flat_color_input(true)
burst("20-flat-color-scene-depth-motion")

better_aa.override_depth_with_far("BeforeImageEffects")
burst("30-flat-color-far-depth-scene-motion")

better_aa.clear_motion_at_event("BeforeImageEffects")
burst("40-flat-color-far-depth-zero-motion")

better_aa.restore_motion_clear()
better_aa.restore_depth_override()
better_aa.set_dlaa_flat_color_input(false)
better_aa.set_backend("Off")
Test.report.note(
    "Compared the exact dotted-ray pose with scene color, flat color, flat color plus far depth, and flat color plus far depth and zero motion.")
