Test.name("Redux Better AA launchpad motion depth correlation")

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
Test.camera.orbit {
    distance = 45,
    yaw = 0,
    pitch = 35,
    fov = 55
}
Test.wait.frames(24)

local views = {
    { name = "FinalColor", capture = "final-color" },
    { name = "LinearDepth", capture = "linear-depth" },
    { name = "ContributionMask", capture = "depth-coverage" },
    { name = "MotionVectorsRaw", capture = "raw-motion" },
    { name = "MotionVectorsValidity", capture = "motion-validity" },
    {
        name = "MotionVectorsManagedPreviousVP",
        capture = "managed-same-frame-motion"
    }
}

for _, view in ipairs(views) do
    better_aa.set_buffer_view(view.name)
    Test.wait.frames(3)
    Test.capture.screenshot(
        view.capture,
        { scale = 1, hideUI = false, waitFrames = 1 })
end

Test.report.value("matrices", better_aa.matrix_snapshot())
Test.report.note(
    "Captured scene color, depth, depth coverage, raw motion, validity, and public-camera reconstruction at one settled corrupt pose for pixel-aligned producer identification")
