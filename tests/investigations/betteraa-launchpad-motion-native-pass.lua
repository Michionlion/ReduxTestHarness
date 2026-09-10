Test.name("Redux Better AA launchpad native motion-pass probe")

Test.assert.true_(
    Test.mod.is_loaded("ReduxBetterAA"),
    "ReduxBetterAA is required")

local better_aa = Test.mod.extension("ReduxBetterAA")
Test.assert.not_equal(
    better_aa,
    nil,
    "ReduxBetterAA TestHarness adapter is required")

better_aa.set_backend("Off")
Test.game.load_save("local/launchpad-fly-safe-15")
Test.game.wait_for_state("Flight", 45)
Test.flight.start("Fly Safe-15")
Test.camera.mode("Flight")
Test.camera.target_vessel()
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

local modes = {
    { id = 0, name = "unity-algorithm" },
    { id = 1, name = "camera-object-coverage-tags" },
    { id = 2, name = "camera-motion-zero" },
    { id = 3, name = "managed-previous-vp" },
    { id = 4, name = "native-minus-managed-motion" }
}

for _, mode in ipairs(modes) do
    better_aa.set_motion_pass_probe(mode.id)
    Test.wait.frames(6)
    Test.capture.screenshot(
        mode.name,
        { scale = 1, hideUI = false, waitFrames = 1 })
end

better_aa.set_buffer_view("validity")
Test.wait.frames(4)
for mode = 5, 8 do
    better_aa.set_motion_pass_probe(mode)
    Test.wait.frames(4)
    Test.assert.true_(
        better_aa.request_capture(),
        "BetterAA should queue a matrix-dump statistics capture")
    Test.wait.frames(30)
end

better_aa.restore_motion_pass_probe()
Test.report.value("publicMatrices", better_aa.matrix_snapshot())
Test.report.note(
    "Modes 0-4 isolate Unity's camera and object motion passes; modes 5-8 dump the private previous and current VP matrix rows into the RG motion target for statistics readback")
