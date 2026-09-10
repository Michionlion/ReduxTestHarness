Test.name("Redux Better AA launchpad motion camera-state isolation")

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
    "baseline",
    { scale = 1, hideUI = false, waitFrames = 1 })

local overrides = {
    { name = "noCulling", label = "no-scene-culling" },
    {
        name = "forceIntoRenderTextureOff",
        label = "force-into-rt-off"
    },
    { name = "clearColor", label = "clear-color" },
    { name = "forward", label = "forward-rendering" },
    { name = "motionOnly", label = "motion-only-depth-mode" }
}

for _, override in ipairs(overrides) do
    Test.report.value(
        override.label .. "Override",
        better_aa.override_camera_state(override.name))
    Test.wait.frames(12)
    Test.report.value(
        override.label .. "Matrices",
        better_aa.matrix_snapshot())
    Test.capture.screenshot(
        override.label,
        { scale = 1, hideUI = false, waitFrames = 1 })
    Test.assert.true_(
        better_aa.restore_camera_state(),
        override.label .. " should restore the selected camera")
    Test.wait.frames(12)
end

Test.report.note(
    "Separated the full-screen camera-motion contribution from procedural scene draws, then tested the selected physics camera's intermediate-target, clear, rendering-path, and depth-mode state")
