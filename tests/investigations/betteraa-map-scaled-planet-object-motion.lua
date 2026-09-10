Test.name("Redux Better AA map scaled-planet object-motion experiment")

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
Test.render.wait_stable(60)
better_aa.select_camera("MapCamera")

Test.report.value(
    "scaledPlanetBeforeA",
    better_aa.scaled_planet_snapshot())
Test.wait.frames(30)
Test.report.value(
    "scaledPlanetBeforeB",
    better_aa.scaled_planet_snapshot())

better_aa.set_buffer_view("raw")
Test.assert.true_(
    better_aa.capture_motion_at_event("BeforeImageEffects"),
    "baseline MapCamera motion capture should attach")
Test.wait.frames(8)
Test.capture.screenshot(
    "map-motion-default-mode",
    { scale = 1, hideUI = false, waitFrames = 1 })
better_aa.restore_motion_capture()

better_aa.set_motion_pass_probe(1)
Test.wait.frames(8)
Test.assert.true_(
    better_aa.capture_motion_at_event("BeforeImageEffects"),
    "motion-pass mask capture should attach")
Test.capture.screenshot(
    "map-motion-object-pass-mask",
    { scale = 1, hideUI = false, waitFrames = 1 })
better_aa.restore_motion_capture()

better_aa.set_motion_pass_probe(0)
Test.wait.frames(30)
Test.report.value(
    "scaledPlanetCustomMotionShader",
    better_aa.scaled_planet_snapshot())

Test.assert.true_(
    better_aa.capture_motion_at_event("BeforeImageEffects"),
    "custom-shader MapCamera motion capture should attach")
Test.wait.frames(8)
Test.capture.screenshot(
    "map-motion-custom-shader",
    { scale = 1, hideUI = false, waitFrames = 1 })
better_aa.restore_motion_capture()

better_aa.set_buffer_view("Off")
better_aa.set_backend("NvidiaDlaa")
Test.render.wait_stable(45)
for frame = 1, 8 do
    Test.capture.screenshot(
        string.format("map-dlaa-custom-motion-%02d", frame),
        { scale = 1, hideUI = false, waitFrames = 1 })
end

Test.assert.true_(
    better_aa.restore_motion_pass_probe(),
    "the original Unity motion shader should restore")
Test.report.note(
    "Compared the map planet's native renderer state, transform movement, raw motion, and DLAA output before and after installing the Unity-derived motion shader")
