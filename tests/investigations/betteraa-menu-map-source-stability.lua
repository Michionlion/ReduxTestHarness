Test.name("Redux Better AA menu/map source stability")

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

Test.game.wait_for_state("MainMenu", 45)
better_aa.set_buffer_view("Off")
better_aa.set_backend("Off")
Test.render.wait_stable(60)
better_aa.select_camera("Camera.Scaled")
Test.report.value("menuScaledPlanet", better_aa.scaled_planet_snapshot())
capture_sequence("menu-off-source", 16)

better_aa.set_backend("NvidiaDlaa")
Test.render.wait_stable(60)
capture_sequence("menu-dlaa-output", 16)

better_aa.set_backend("Off")
Test.game.load_save("local/launchpad-fly-safe-15")
Test.game.wait_for_state("Flight", 45)
Test.flight.start("Fly Safe-15")
Test.render.wait_stable(45)
better_aa.open_map_view()
Test.game.wait_for_state("Map3DView", 30)
Test.render.wait_stable(75)
better_aa.select_camera("MapCamera")
Test.report.value("mapScaledPlanet", better_aa.scaled_planet_snapshot())
capture_sequence("map-off-source", 16)

better_aa.set_backend("NvidiaDlaa")
Test.render.wait_stable(60)
capture_sequence("map-dlaa-output", 16)

Test.report.note(
    "Compared consecutive unfiltered source frames against DLAA output for the deterministic main-menu and map views")
