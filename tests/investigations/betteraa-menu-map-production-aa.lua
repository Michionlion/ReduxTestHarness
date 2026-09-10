Test.name("Redux Better AA 0.5.25 menu/map production AA")

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

local function capture_backend(backend, prefix, count)
    better_aa.set_backend("Off")
    Test.wait.frames(8)
    better_aa.set_backend(backend)
    Test.render.wait_stable(60)
    capture_sequence(prefix, count)
end

Test.game.wait_for_state("MainMenu", 45)
better_aa.select_camera("Camera.Scaled")
capture_backend("CustomTaa", "main-menu-custom", 24)
capture_backend("NvidiaDlaa", "main-menu-dlaa", 24)
capture_backend("AmdFsr2", "main-menu-fsr2", 24)
better_aa.request_capture()
Test.wait.frames(4)

better_aa.set_backend("Off")
Test.game.load_save("local/launchpad-fly-safe-15")
Test.game.wait_for_state("Flight", 45)
Test.flight.start("Fly Safe-15")
Test.render.wait_stable(45)
better_aa.open_map_view()
Test.game.wait_for_state("Map3DView", 30)
Test.render.wait_stable(75)
better_aa.select_camera("MapCamera")
capture_backend("CustomTaa", "map-custom", 16)
capture_backend("NvidiaDlaa", "map-dlaa", 16)
capture_backend("AmdFsr2", "map-fsr2", 16)
better_aa.request_capture()
Test.wait.frames(4)

Test.report.note(
    "Captured the installed 0.5.25 zero-jitter scene policy with Custom TAA, DLAA, and FSR2 in the main menu and map view")
