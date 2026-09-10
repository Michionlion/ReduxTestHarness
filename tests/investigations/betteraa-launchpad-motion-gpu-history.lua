Test.name("Redux Better AA launchpad GPU motion-history correlation")

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

local views = {
    { name = "MotionVectorsRaw", capture = "raw" },
    {
        name = "MotionVectorsBuiltinPreviousVP",
        capture = "unity-matrix-previous-vp"
    },
    {
        name = "MotionVectorsPerPassPreviousVP",
        capture = "per-pass-prev-view-proj"
    },
    {
        name = "MotionVectorsManagedPreviousVP",
        capture = "managed-camera-previous-vp"
    }
}

for _, yaw in ipairs({ 0, 180 }) do
    Test.camera.orbit {
        distance = 45,
        yaw = yaw,
        pitch = 35,
        fov = 55
    }
    Test.wait.frames(24)

    Test.report.value(
        string.format("yaw%03dMatrices", yaw),
        better_aa.matrix_snapshot())

    for _, view in ipairs(views) do
        better_aa.set_buffer_view(view.name)
        Test.wait.frames(3)
        Test.report.value(
            string.format("yaw%03d-%s-view", yaw, view.capture),
            better_aa.current_view())
        Test.capture.screenshot(
            string.format("yaw%03d-%s", yaw, view.capture),
            { scale = 1, hideUI = false, waitFrames = 1 })
    end
end

Test.report.note(
    "Captured Unity raw motion beside depth-reprojected motion from unity_MatrixPreviousVP and _PrevViewProjMatrix at a corrupt and neutral launchpad heading")
