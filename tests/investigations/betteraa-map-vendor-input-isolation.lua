Test.name("Redux Better AA map vendor input isolation")

Test.assert.true_(
    Test.mod.is_loaded("ReduxBetterAA"),
    "ReduxBetterAA is required")
local better_aa = Test.mod.extension("ReduxBetterAA")
Test.assert.not_equal(
    better_aa,
    nil,
    "ReduxBetterAA TestHarness adapter is required")

local function capture_sequence(prefix, count)
    for frame = 1, count do
        Test.capture.screenshot(
            string.format("%s-%02d", prefix, frame),
            { scale = 1, hideUI = false, waitFrames = 1 })
    end
end

local function restart_dlaa()
    better_aa.set_backend("Off")
    Test.wait.frames(8)
    better_aa.set_backend("NvidiaDlaa")
    Test.render.wait_stable(45)
end

Test.game.load_save("local/launchpad-fly-safe-15")
Test.game.wait_for_state("Flight", 45)
Test.flight.start("Fly Safe-15")
better_aa.set_backend("Off")
Test.render.wait_stable(45)
better_aa.open_map_view()
Test.game.wait_for_state("Map3DView", 30)
Test.render.wait_stable(75)
better_aa.select_camera("MapCamera")

better_aa.set_vendor_auto_exposure("NvidiaDlaa", true, 1.0)
restart_dlaa()
capture_sequence("map-dlaa-auto-exposure", 16)

better_aa.set_vendor_auto_exposure("NvidiaDlaa", false, 1.0)
restart_dlaa()
capture_sequence("map-dlaa-manual-exposure", 16)

better_aa.set_vendor_auto_exposure("NvidiaDlaa", true, 1.0)
Test.assert.true_(
    better_aa.set_temporal_jitter_suppressed(true),
    "test-only temporal jitter suppression should enable")
restart_dlaa()
capture_sequence("map-dlaa-zero-jitter", 16)

better_aa.set_backend("Off")
Test.assert.false_(
    better_aa.set_temporal_jitter_suppressed(false),
    "test-only temporal jitter suppression should disable")
Test.report.note(
    "Compared normal vendor auto exposure, fixed manual exposure, and a zero-jitter vendor dispatch while retaining the same map color/depth/motion sources")
