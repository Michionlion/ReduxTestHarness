Test.name("Redux Better AA map scaled-planet production motion")

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
better_aa.set_backend("Off")
Test.render.wait_stable(45)

better_aa.open_map_view()
Test.game.wait_for_state("Map3DView", 30)
Test.render.wait_stable(75)
better_aa.select_camera("MapCamera")
Test.report.value(
    "scaledPlanetWithProductionRepair",
    better_aa.scaled_planet_snapshot())

better_aa.set_buffer_view("raw")
Test.assert.true_(
    better_aa.capture_motion_at_event("BeforeImageEffects"),
    "production MapCamera motion capture should attach")
Test.wait.frames(8)
Test.capture.screenshot(
    "map-motion-production-companion",
    { scale = 1, hideUI = false, waitFrames = 1 })
better_aa.restore_motion_capture()

better_aa.set_buffer_view("Off")
better_aa.set_backend("NvidiaDlaa")
Test.render.wait_stable(60)
for frame = 1, 12 do
    Test.capture.screenshot(
        string.format("map-dlaa-production-motion-%02d", frame),
        { scale = 1, hideUI = false, waitFrames = 1 })
end

better_aa.set_backend("AmdFsr2")
Test.render.wait_stable(60)
for frame = 1, 12 do
    Test.capture.screenshot(
        string.format("map-fsr2-production-motion-%02d", frame),
        { scale = 1, hideUI = false, waitFrames = 1 })
end

Test.assert.true_(
    better_aa.request_capture(),
    "the Better AA capability capture should be queued")
Test.wait.frames(8)
Test.report.note(
    "Verified the production scaled-planet motion companion in raw MapCamera motion and twelve-frame DLAA/FSR2 sequences")
