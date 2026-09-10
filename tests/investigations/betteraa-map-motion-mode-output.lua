Test.name("Redux Better AA map motion-mode output")

Test.assert.true_(
    Test.mod.is_loaded("ReduxBetterAA"),
    "ReduxBetterAA is required")
local better_aa = Test.mod.extension("ReduxBetterAA")
Test.assert.not_equal(
    better_aa,
    nil,
    "ReduxBetterAA TestHarness adapter is required")

local function capture_mode(mode, prefix)
    better_aa.set_scaled_planet_motion_mode(mode)
    better_aa.set_backend("Off")
    Test.wait.frames(8)
    better_aa.set_backend("NvidiaDlaa")
    Test.render.wait_stable(45)
    for frame = 1, 16 do
        Test.capture.screenshot(
            string.format("%s-%02d", prefix, frame),
            { scale = 1, hideUI = false, waitFrames = 1 })
    end
    better_aa.restore_scaled_planet_motion_mode()
end

Test.game.load_save("local/launchpad-fly-safe-15")
Test.game.wait_for_state("Flight", 45)
Test.flight.start("Fly Safe-15")
better_aa.set_backend("Off")
Test.render.wait_stable(45)
better_aa.open_map_view()
Test.game.wait_for_state("Map3DView", 30)
Test.render.wait_stable(75)

capture_mode("Object", "map-dlaa-motion-object")
capture_mode("ForceNoMotion", "map-dlaa-motion-force-none")
capture_mode("Camera", "map-dlaa-motion-camera")

better_aa.restore_scaled_planet_motion_mode()
Test.report.note(
    "Compared the map scaled-planet hierarchy with object motion, forced zero motion, and camera-only motion generation")
