Test.name("Redux Better AA 0.5.26 DLAA flight artifact isolation")

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
Test.camera.orbit {
    distance = 2200,
    yaw = 303,
    pitch = 35,
    fov = 55
}
Test.render.wait_stable(60)
better_aa.select_camera("FlightCameraPhysics_Main")

local function capture_sequence(prefix, count, hide_ui)
    for index = 1, count do
        Test.capture.screenshot(
            string.format("%s-%02d", prefix, index),
            { scale = 1, hideUI = hide_ui, waitFrames = 2 })
    end
end

capture_sequence("00-off", 4, false)

better_aa.set_backend("NvidiaDlaa")
Test.wait.frames(90)
capture_sequence("10-dlaa-k-normal", 12, false)
capture_sequence("11-dlaa-k-hidden-ui", 4, true)

better_aa.set_temporal_jitter_suppressed(true)
Test.wait.frames(90)
capture_sequence("20-dlaa-k-zero-jitter", 12, false)
better_aa.set_temporal_jitter_suppressed(false)

local producers = {
    "clouds",
    "vegetation-lod",
    "vegetation-billboards",
    "pqs-quads",
    "atmosphere"
}

for index, producer in ipairs(producers) do
    better_aa.set_backend("Off")
    better_aa.restore_render_producers()
    local patched = better_aa.suppress_render_producer(producer)
    Test.assert.greater(
        patched,
        0,
        "The requested producer should have a suppressible render method")
    better_aa.set_backend("NvidiaDlaa")
    Test.wait.frames(90)
    capture_sequence(
        string.format("3%d-without-%s", index, producer),
        4,
        false)
end

better_aa.set_backend("Off")
better_aa.restore_render_producers()
better_aa.set_temporal_jitter_suppressed(false)
Test.report.note(
    "Compared fixed-pose DLAA against AA-off, zero projection jitter, hidden UI, and isolated cloud, foliage, PQS, and atmosphere producers.")
