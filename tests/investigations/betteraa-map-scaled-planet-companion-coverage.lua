Test.name("Redux Better AA map scaled-planet companion coverage")

Test.assert.true_(Test.mod.is_loaded("ReduxBetterAA"), "ReduxBetterAA is required")
local better_aa = Test.mod.extension("ReduxBetterAA")
Test.assert.not_equal(better_aa, nil, "ReduxBetterAA TestHarness adapter is required")

Test.game.load_save("local/launchpad-fly-safe-15")
Test.game.wait_for_state("Flight", 45)
Test.flight.start("Fly Safe-15")
better_aa.set_backend("Off")
Test.render.wait_stable(45)
better_aa.open_map_view()
Test.game.wait_for_state("Map3DView", 30)
Test.render.wait_stable(75)
better_aa.select_camera("MapCamera")
better_aa.set_buffer_view("raw")

better_aa.set_scaled_planet_motion_probe(1)
Test.wait.frames(8)
Test.assert.true_(
    better_aa.capture_motion_at_event("BeforeImageEffects"),
    "companion coverage capture should attach")
Test.capture.screenshot(
    "map-scaled-planet-companion-coverage",
    { scale = 1, hideUI = false, waitFrames = 1 })
better_aa.restore_motion_capture()

better_aa.set_scaled_planet_motion_probe(0)
Test.wait.frames(8)
Test.assert.true_(
    better_aa.capture_motion_at_event("BeforeImageEffects"),
    "companion calculated-motion capture should attach")
Test.capture.screenshot(
    "map-scaled-planet-companion-calculated",
    { scale = 1, hideUI = false, waitFrames = 1 })
better_aa.restore_motion_capture()
Test.report.note(
    "Compared a constant companion-pass coverage marker against its calculated map-planet motion")
