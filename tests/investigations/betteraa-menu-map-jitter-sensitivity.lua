Test.name("Redux Better AA menu/map jitter sensitivity")

Test.assert.true_(
    Test.mod.is_loaded("ReduxBetterAA"),
    "ReduxBetterAA is required")
local better_aa = Test.mod.extension("ReduxBetterAA")
Test.assert.not_equal(
    better_aa,
    nil,
    "ReduxBetterAA TestHarness adapter is required")

local function capture_sequence(prefix)
    for frame = 1, 16 do
        Test.capture.screenshot(
            string.format("%s-%02d", prefix, frame),
            { scale = 1, hideUI = false, waitFrames = 1 })
    end
end

local function set_dlaa_spread(spread)
    better_aa.set_vendor_jitter_spread("NvidiaDlaa", spread)
    Test.render.wait_stable(45)
end

Test.game.wait_for_state("MainMenu", 45)
better_aa.set_buffer_view("Off")
better_aa.set_backend("NvidiaDlaa")
set_dlaa_spread(0.1)
capture_sequence("menu-dlaa-jitter-010")
set_dlaa_spread(0.75)
capture_sequence("menu-dlaa-jitter-075")

better_aa.set_backend("Off")
Test.game.load_save("local/launchpad-fly-safe-15")
Test.game.wait_for_state("Flight", 45)
Test.flight.start("Fly Safe-15")
Test.render.wait_stable(45)
better_aa.open_map_view()
Test.game.wait_for_state("Map3DView", 30)
Test.render.wait_stable(75)
better_aa.set_backend("NvidiaDlaa")
set_dlaa_spread(0.1)
capture_sequence("map-dlaa-jitter-010")
set_dlaa_spread(0.75)
capture_sequence("map-dlaa-jitter-075")

better_aa.set_vendor_jitter_spread("NvidiaDlaa", 0.75)
Test.report.note(
    "Compared minimum supported projection jitter against the normal DLAA jitter spread in main-menu and map scaled-space rendering")
