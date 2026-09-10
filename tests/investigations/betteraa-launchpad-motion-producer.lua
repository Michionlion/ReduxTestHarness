Test.name("Redux Better AA launchpad motion producer correlation")

Test.assert.true_(
    Test.mod.is_loaded("ReduxBetterAA"),
    "ReduxBetterAA is required")

local better_aa = Test.mod.extension("ReduxBetterAA")
Test.assert.not_equal(
    better_aa,
    nil,
    "ReduxBetterAA TestHarness adapter is required")
Test.assert.true_(better_aa.available(), "ReduxBetterAA assembly should be loaded")

Test.game.load_save("local/launchpad-fly-safe-15")
Test.game.wait_for_state("Flight", 45)

local vessel = Test.flight.start("Fly Safe-15")
Test.assert.equal(vessel.situation, "PreLaunch", "fixture should begin on the launchpad")

Test.camera.mode("Flight")
Test.camera.target_vessel()
better_aa.set_backend("Off")
Test.render.wait_stable(45)
better_aa.select_camera("FlightCameraPhysics_Main")

local function capture_view(yaw, view, file_name)
    Test.camera.orbit {
        distance = 45,
        yaw = yaw,
        pitch = 35,
        fov = 55
    }
    better_aa.set_buffer_view(view)
    Test.wait.frames(16)
    Test.capture.screenshot(
        file_name,
        { scale = 1, hideUI = false, waitFrames = 1 })
end

local views = {
    { name = "FinalColor", suffix = "color" },
    { name = "LinearDepth", suffix = "depth" },
    { name = "ContributionMask", suffix = "coverage" },
    { name = "MotionVectorsRaw", suffix = "raw" },
    { name = "MotionVectorsNormalized", suffix = "normalized" },
    { name = "MotionVectorsValidity", suffix = "validity" }
}

for _, yaw in ipairs({ 0, 180, 315 }) do
    for _, view in ipairs(views) do
        capture_view(
            yaw,
            view.name,
            string.format("yaw%03d-%s", yaw, view.suffix))
    end
end

Test.report.value("bufferCamera", better_aa.selected_camera())
Test.report.value("vessel", vessel.name)
Test.report.note(
    "Correlated final color, depth, coverage, raw motion, normalized motion, and validity at two corrupt directions and one clean control")
