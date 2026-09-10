Test.name("Redux Better AA KSC visual artifact pose sweep")

Test.assert.true_(
    Test.mod.is_loaded("ReduxBetterAA"),
    "ReduxBetterAA is required")
local better_aa = Test.mod.extension("ReduxBetterAA")
Test.assert.not_equal(better_aa, nil, "ReduxBetterAA adapter is required")

better_aa.set_backend("Off")
Test.game.load_save("local/launchpad-cloudy-fly-safe-15")
Test.game.wait_for_state("Flight", 45)
Test.flight.start("Fly Safe-15")
Test.camera.mode("Flight")
Test.camera.target_vessel()
Test.wait.frames(30)
better_aa.select_camera("FlightCameraPhysics_Main")

local poses = {
    { name = "200m-p10", distance = 200, pitch = 10 },
    { name = "200m-p25", distance = 200, pitch = 25 },
    { name = "500m-p15", distance = 500, pitch = 15 },
    { name = "500m-p30", distance = 500, pitch = 30 },
    { name = "2200m-p20", distance = 2200, pitch = 20 },
    { name = "2200m-p35", distance = 2200, pitch = 35 }
}

for _, pose in ipairs(poses) do
    Test.camera.direct_orbit {
        distance = pose.distance,
        yaw = 303,
        pitch = pose.pitch,
        fov = 60
    }
    Test.wait.frames(45)
    Test.capture.screenshot(
        "off-" .. pose.name,
        { scale = 1, hideUI = true, waitFrames = 2 })
end

better_aa.set_backend("Off")
Test.report.note(
    "Off-only pose sweep used to select reproducible low-horizon and terrain views before backend isolation.")
