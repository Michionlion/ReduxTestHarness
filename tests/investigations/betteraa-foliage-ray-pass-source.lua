Test.name("Redux Better AA foliage ray pass source")

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

-- Exact stationary pose from phase1-20260828-204306-001 through 003.
Test.camera.absolute {
    position = { -60.41134, -364.5381, -74.89148 },
    rotation = { 0.632249355, 0.0306785312, 0.163434058, -0.7567093 },
    fov = 60
}

better_aa.set_vegetation_motion_repair(true)
better_aa.set_motion_sanitizer(false)
better_aa.set_buffer_view("raw")

local function capture(name, frames)
    Test.wait.frames(frames or 12)
    Test.capture.screenshot(name, { scale = 1, hideUI = true, waitFrames = 1 })
end

better_aa.set_motion_pass_probe(0)
capture("10-native-all-passes", 60)

-- Positive X is the camera pass and negative X is an object-pass overwrite.
better_aa.set_motion_pass_probe(1)
capture("20-camera-vs-object-tag", 12)

-- The shader discards every object pass and leaves camera reconstruction intact.
better_aa.set_motion_pass_probe(17)
capture("30-camera-pass-only", 12)

better_aa.set_motion_pass_probe(0)
better_aa.isolate_vegetation_item("7821a620-ecf3-4106-909d-0d7ca7cb01a4")
capture("40-base-poplar-only", 30)

better_aa.isolate_vegetation_item_first_material(
    "7821a620-ecf3-4106-909d-0d7ca7cb01a4")
capture("50-base-poplar-leaves-only", 30)

better_aa.restore_render_producers()
better_aa.restore_motion_pass_probe()
better_aa.set_motion_sanitizer(false)
better_aa.set_vegetation_motion_repair(true)
better_aa.set_buffer_view("Off")
Test.report.note(
    "Split the reported stationary foliage ray between Unity's full-screen camera and per-object motion passes, then isolated the known poplar leaf producer.")
