Test.name("Redux Better AA launchpad clean clone-camera control")

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
    "original-physics-camera-motion",
    { scale = 1, hideUI = false, waitFrames = 1 })

Test.report.value(
    "cloneMotionTarget",
    better_aa.use_clone_motion_camera())
Test.wait.frames(30)
Test.capture.screenshot(
    "clean-clone-camera-motion",
    { scale = 1, hideUI = false, waitFrames = 1 })

Test.assert.true_(
    better_aa.restore_clone_motion_camera(),
    "the clone camera and its render targets should be restored")
Test.wait.frames(12)
Test.capture.screenshot(
    "original-motion-restored",
    { scale = 1, hideUI = false, waitFrames = 1 })

Test.report.note(
    "Compared the original KSP physics camera's built-in motion texture with a component-free camera clone using the same transform, projection, culling mask, and scene on an isolated render target")
