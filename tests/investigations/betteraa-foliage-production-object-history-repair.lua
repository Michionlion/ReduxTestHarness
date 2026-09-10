Test.name("Redux Better AA production foliage object-history repair")

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

Test.camera.absolute {
    position = { -60.41134, -364.5381, -74.89148 },
    rotation = { 0.632249355, 0.0306785312, 0.163434058, -0.7567093 },
    fov = 60
}
Test.wait.frames(60)
better_aa.set_buffer_view("magnitude")
Test.capture.screenshot(
    "10-production-exact-pose-magnitude",
    { scale = 1, hideUI = true, waitFrames = 0 })
better_aa.set_buffer_view("validity")
Test.capture.screenshot(
    "11-production-exact-pose-validity",
    { scale = 1, hideUI = true, waitFrames = 0 })
better_aa.request_capture("production foliage exact object-history repair")

better_aa.set_buffer_view("raw")
Test.camera.orbit { distance = 45, yaw = 0, pitch = 35, fov = 55 }
Test.wait.frames(30)
for index = 1, 8 do
    Test.camera.orbit {
        distance = 45,
        yaw = index * 0.75,
        pitch = 35,
        fov = 55
    }
    Test.capture.screenshot(
        string.format("20-production-raw-pan-%02d", index),
        { scale = 1, hideUI = true, waitFrames = 0 })
end

better_aa.set_buffer_view("Off")
better_aa.set_dlaa_preset("K")
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
        string.format("30-production-dlaa-k-pan-%02d", index),
        { scale = 1, hideUI = true, waitFrames = 0 })
end

better_aa.set_backend("Off")
better_aa.set_motion_sanitizer(false)
better_aa.set_vegetation_motion_repair(true)
Test.report.note(
    "Validated the packaged 0.5.28 production object-history repair at the exact dotted-ray pose and during raw-motion and DLAA-K camera pans, with sanitizer E disabled and no diagnostic motion-pass override.")
