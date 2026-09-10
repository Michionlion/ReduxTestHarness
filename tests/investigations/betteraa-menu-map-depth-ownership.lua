Test.name("Redux Better AA menu/map depth ownership")

Test.assert.true_(
    Test.mod.is_loaded("ReduxBetterAA"),
    "ReduxBetterAA is required")
local better_aa = Test.mod.extension("ReduxBetterAA")
Test.assert.not_equal(
    better_aa,
    nil,
    "ReduxBetterAA TestHarness adapter is required")

local function capture_depth(prefix)
    better_aa.set_buffer_view("LinearDepth")
    Test.wait.frames(8)
    Test.capture.screenshot(
        prefix .. "-global-after-everything",
        { scale = 1, hideUI = false, waitFrames = 1 })

    Test.assert.true_(
        better_aa.capture_depth_at_event("BeforeImageEffects"),
        "camera-local depth capture should attach")
    Test.wait.frames(8)
    Test.capture.screenshot(
        prefix .. "-owned-before-image-effects",
        { scale = 1, hideUI = false, waitFrames = 1 })
    better_aa.restore_depth_capture()
    better_aa.set_buffer_view("Off")
end

Test.game.wait_for_state("MainMenu", 45)
better_aa.set_backend("Off")
Test.render.wait_stable(60)
better_aa.select_camera("Camera.Scaled")
capture_depth("menu-scaled-depth")

Test.game.load_save("local/launchpad-fly-safe-15")
Test.game.wait_for_state("Flight", 45)
Test.flight.start("Fly Safe-15")
Test.render.wait_stable(45)
better_aa.open_map_view()
Test.game.wait_for_state("Map3DView", 30)
Test.render.wait_stable(75)
better_aa.select_camera("MapCamera")
capture_depth("map-depth")

Test.report.note(
    "Compared the late global depth binding against an owned copy of the selected camera depth made before image effects")
