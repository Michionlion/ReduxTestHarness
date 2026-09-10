Test.name("Redux Better AA launchpad force-zero object motion")

Test.assert.true_(
    Test.mod.is_loaded("ReduxBetterAA"),
    "ReduxBetterAA is required")

local better_aa = Test.mod.extension("ReduxBetterAA")
Test.assert.not_equal(
    better_aa,
    nil,
    "ReduxBetterAA TestHarness adapter is required")

Test.game.load_save("local/launchpad-fly-safe-15")
Test.game.wait_for_state("Flight", 45)
Test.flight.start("Fly Safe-15")
Test.camera.mode("Flight")
Test.camera.target_vessel()
better_aa.set_backend("Off")
Test.render.wait_stable(45)
better_aa.select_camera("FlightCameraPhysics_Main")
better_aa.set_buffer_view("raw")
Test.camera.orbit {
    distance = 45,
    yaw = 0,
    pitch = 35,
    fov = 55
}
Test.wait.frames(24)

Test.capture.screenshot(
    "baseline-camera-and-object-motion",
    { scale = 1, hideUI = false, waitFrames = 1 })

local changed = better_aa.suppress_object_motion("ForceNoMotion")
Test.report.value("forceNoMotionRendererCount", changed)
Test.wait.frames(8)
Test.capture.screenshot(
    "all-renderers-force-no-motion",
    { scale = 1, hideUI = false, waitFrames = 1 })

Test.report.value(
    "restoredRendererCount",
    better_aa.restore_object_motion())
Test.wait.frames(8)
Test.capture.screenshot(
    "restored-motion-modes",
    { scale = 1, hideUI = false, waitFrames = 1 })

Test.report.note(
    "Forced every active renderer to write explicit zero motion so camera reconstruction can be separated from per-renderer overrides")
