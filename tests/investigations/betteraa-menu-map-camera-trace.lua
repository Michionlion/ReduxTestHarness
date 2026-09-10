Test.name("Redux Better AA menu/map camera render trace")

Test.assert.true_(
    Test.mod.is_loaded("ReduxBetterAA"),
    "ReduxBetterAA is required")
local better_aa = Test.mod.extension("ReduxBetterAA")
Test.assert.not_equal(
    better_aa,
    nil,
    "ReduxBetterAA TestHarness adapter is required")

local function trace_camera(camera_name, report_name)
    better_aa.select_camera(camera_name)
    Test.report.value(report_name .. "Selected", better_aa.start_camera_render_trace())
    Test.wait.frames(12)
    Test.report.value(report_name, better_aa.camera_render_trace())
    Test.assert.true_(
        better_aa.stop_camera_render_trace(),
        "camera render trace should have been active")
end

Test.game.wait_for_state("MainMenu", 45)
better_aa.set_backend("NvidiaDlaa")
Test.render.wait_stable(60)
trace_camera("Skybox", "menuSkyboxTrace")
trace_camera("Camera.Scaled", "menuScaledTrace")

better_aa.set_backend("Off")
Test.game.load_save("local/launchpad-fly-safe-15")
Test.game.wait_for_state("Flight", 45)
Test.flight.start("Fly Safe-15")
Test.render.wait_stable(45)
better_aa.open_map_view()
Test.game.wait_for_state("Map3DView", 30)
Test.render.wait_stable(75)
better_aa.set_backend("NvidiaDlaa")
Test.render.wait_stable(45)
trace_camera("MapCamera", "mapTrace")

Test.report.note(
    "Compared selected-camera VP state at Unity pre-cull, pre-render, and post-render callbacks under DLAA")
