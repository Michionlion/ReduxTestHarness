Test.name("Redux Better AA launchpad on-screen clone-camera control")

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
    "original-on-screen-camera",
    { scale = 1, hideUI = false, waitFrames = 1 })

Test.report.value(
    "screenCloneMotionTarget",
    better_aa.use_clone_motion_camera("screen"))
Test.wait.frames(30)
Test.report.value("cloneMatrices", better_aa.clone_matrix_snapshot())
Test.capture.screenshot(
    "fresh-on-screen-clone-camera",
    { scale = 1, hideUI = false, waitFrames = 1 })

Test.assert.true_(
    better_aa.restore_clone_motion_camera(),
    "the on-screen clone camera should be restored")

Test.report.note(
    "Rendered a fresh component-free clone through the same screen/intermediate-target path and displayed its own captured built-in motion after its render, removing the explicit-target confound from the prior clone control")
