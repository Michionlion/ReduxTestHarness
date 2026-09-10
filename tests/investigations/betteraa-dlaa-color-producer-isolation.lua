Test.name("Redux Better AA DLAA color producer isolation")

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

local function restart_and_burst(prefix)
    better_aa.set_backend("Off")
    better_aa.set_backend("NvidiaDlaa")
    Test.wait.frames(90)
    for index = 1, 6 do
        Test.capture.screenshot(
            string.format("%s-%02d", prefix, index),
            { scale = 1, hideUI = true, waitFrames = 1 })
    end
end

better_aa.set_vendor_auto_exposure("NvidiaDlaa", false, 1.0, false)
better_aa.set_vendor_sharpness("NvidiaDlaa", 0.0)
restart_and_burst("10-normal")

better_aa.override_camera_state("noCulling")
restart_and_burst("20-no-physics-camera-culling")

local removed = better_aa.suppress_camera_command_buffers("")
Test.assert.greater(removed, 0, "Expected physics-camera command buffers")
restart_and_burst("30-no-culling-no-command-buffers")

better_aa.restore_camera_command_buffers()
better_aa.restore_camera_state()
better_aa.set_backend("Off")
Test.report.note(
    "Compared the exact dotted-ray pose with normal scene production, no physics-camera culling, and no physics-camera culling or command buffers.")
