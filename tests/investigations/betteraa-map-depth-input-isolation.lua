Test.name("Redux Better AA map depth input isolation")

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

Test.game.load_save("local/launchpad-fly-safe-15")
Test.game.wait_for_state("Flight", 45)
Test.flight.start("Fly Safe-15")
better_aa.set_backend("Off")
Test.render.wait_stable(45)
better_aa.open_map_view()
Test.game.wait_for_state("Map3DView", 30)
Test.render.wait_stable(75)
better_aa.select_camera("MapCamera")

better_aa.set_backend("NvidiaDlaa")
Test.render.wait_stable(45)
capture_sequence("map-dlaa-normal-depth", 16)

better_aa.set_backend("Off")
Test.wait.frames(8)
Test.report.value(
    "farDepthOverride",
    better_aa.override_depth_with_far("BeforeImageEffects"))
better_aa.set_backend("NvidiaDlaa")
Test.render.wait_stable(45)
capture_sequence("map-dlaa-all-far-depth", 16)

better_aa.set_backend("Off")
better_aa.restore_depth_override()
Test.report.note(
    "Compared DLAA with MapCamera's normal depth against a deterministic all-far depth input while leaving color and motion unchanged")
