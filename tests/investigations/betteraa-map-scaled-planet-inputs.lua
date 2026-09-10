Test.name("Redux Better AA map-view scaled-planet input ownership")

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
better_aa.set_buffer_view("raw")
Test.report.value(
    "mapMotionCapture",
    better_aa.capture_motion_at_event("BeforeImageEffects"))
Test.wait.frames(8)
Test.capture.screenshot(
    "map-motion-before-image-effects",
    { scale = 1, hideUI = false, waitFrames = 1 })
Test.assert.true_(
    better_aa.restore_motion_capture(),
    "the MapCamera motion capture should be restored")

better_aa.set_buffer_view("LinearDepth")
Test.report.value(
    "mapDepthCapture",
    better_aa.capture_depth_at_event("BeforeImageEffects"))
Test.wait.frames(8)
Test.capture.screenshot(
    "map-depth-before-image-effects",
    { scale = 1, hideUI = false, waitFrames = 1 })
Test.assert.true_(
    better_aa.restore_depth_capture(),
    "the MapCamera depth capture should be restored")

better_aa.set_buffer_view("Off")
better_aa.set_backend("NvidiaDlaa")
Test.render.wait_stable(45)
for frame = 1, 8 do
    Test.capture.screenshot(
        string.format("map-dlaa-frame-%02d", frame),
        { scale = 1, hideUI = false, waitFrames = 1 })
end

better_aa.set_backend("AmdFsr2")
Test.render.wait_stable(45)
for frame = 1, 8 do
    Test.capture.screenshot(
        string.format("map-fsr2-frame-%02d", frame),
        { scale = 1, hideUI = false, waitFrames = 1 })
end

Test.assert.true_(
    better_aa.request_capture(),
    "the Better AA capability capture should be queued")
Test.wait.frames(8)
Test.report.note(
    "Entered map view through KSP2's public UI route, captured immutable MapCamera motion/depth before image effects, then eight-frame DLAA and FSR2 sequences")
