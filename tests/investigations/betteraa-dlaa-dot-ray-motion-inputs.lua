Test.name("Redux Better AA DLAA dot ray motion inputs")

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

local function capture_view(prefix, view)
    better_aa.set_buffer_view(view)
    Test.wait.frames(60)
    for index = 1, 3 do
        Test.capture.screenshot(
            string.format("%s-%02d", prefix, index),
            { scale = 1, hideUI = true, waitFrames = 1 })
    end
end

better_aa.set_vegetation_motion_repair(true)
capture_view("10-repair-on-raw", "MotionVectorsRaw")
capture_view("11-repair-on-normalized", "MotionVectorsNormalized")
capture_view("12-repair-on-sanitized", "SanitizedVendorMotion")
capture_view("13-repair-on-decision", "MotionSanitizerDecision")

better_aa.set_vegetation_motion_repair(false)
capture_view("20-repair-off-raw", "MotionVectorsRaw")
capture_view("21-repair-off-normalized", "MotionVectorsNormalized")
capture_view("22-repair-off-sanitized", "SanitizedVendorMotion")
capture_view("23-repair-off-decision", "MotionSanitizerDecision")

better_aa.set_buffer_view("Off")
better_aa.set_vegetation_motion_repair(true)
better_aa.set_backend("Off")
Test.report.note(
    "Captured raw, normalized, sanitized, and sanitizer-decision motion at the exact dotted-ray pose with foliage repair on and off.")
