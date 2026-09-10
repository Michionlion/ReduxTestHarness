Test.name("Redux Better AA foliage force-no-motion pan")

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
better_aa.set_vegetation_motion_repair(true)
better_aa.set_motion_sanitizer(false)
better_aa.set_buffer_view("raw")

Test.camera.orbit { distance = 45, yaw = 0, pitch = 35, fov = 55 }
Test.wait.frames(30)
Test.capture.screenshot("10-stationary", { scale = 1, hideUI = true, waitFrames = 0 })

for index = 1, 8 do
    Test.camera.orbit {
        distance = 45,
        yaw = index * 0.75,
        pitch = 35,
        fov = 55
    }
    Test.capture.screenshot(
        string.format("20-pan-%02d", index),
        { scale = 1, hideUI = true, waitFrames = 0 })
end

better_aa.set_buffer_view("Off")
better_aa.set_backend("NvidiaDlaa")
Test.wait.frames(60)
for index = 9, 16 do
    Test.camera.orbit {
        distance = 45,
        yaw = index * 0.75,
        pitch = 35,
        fov = 55
    }
    Test.capture.screenshot(
        string.format("30-dlaa-pan-%02d", index),
        { scale = 1, hideUI = true, waitFrames = 0 })
end

better_aa.set_backend("Off")
better_aa.set_motion_sanitizer(false)
better_aa.set_vegetation_motion_repair(true)
Test.report.note(
    "Captured consecutive camera steps with the experimental ForceNoMotion foliage submission to expose zero-vector overwrites during camera motion.")
