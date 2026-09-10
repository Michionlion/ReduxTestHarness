Test.name("Redux Better AA launchpad render-producer isolation")

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
better_aa.set_motion_pass_probe(0)
Test.wait.frames(24)

Test.capture.screenshot(
    "baseline",
    { scale = 1, hideUI = false, waitFrames = 1 })

local producers = {
    { id = "atmosphere", capture = "without-atmosphere-post-effect" },
    { id = "clouds", capture = "without-volume-cloud-render" },
    { id = "pqs-quads", capture = "without-pqs-quad-render" },
    { id = "pqs-ocean", capture = "without-pqs-ocean-render" },
    { id = "light-culling", capture = "without-light-culling" },
    {
        id = "vegetation-billboards",
        capture = "without-vegetation-billboards"
    },
    { id = "vegetation-lod", capture = "without-vegetation-lod" }
}

for _, producer in ipairs(producers) do
    local patched = better_aa.suppress_render_producer(producer.id)
    Test.report.value(producer.id .. "PatchedMethods", patched)
    Test.wait.frames(12)
    Test.capture.screenshot(
        producer.capture,
        { scale = 1, hideUI = false, waitFrames = 1 })
    Test.report.value(
        producer.id .. "SuppressedCalls",
        better_aa.render_producer_suppression_hits())
    if producer.id == "vegetation-lod" then
        Test.report.value(
            "vegetationDraws",
            better_aa.vegetation_render_snapshot())
    end
    better_aa.restore_render_producers()
    Test.wait.frames(12)
end

better_aa.restore_motion_pass_probe()
Test.report.note(
    "Each capture suppresses one managed KSP rendering producer while preserving Unity's native motion-vector algorithm; this is diagnostic-only and changes no BetterAA backend or sanitizer behavior")
