Test.name("Launchpad vessel launch smoke test")

Test.game.load_save("local/launchpad-fly-safe-15")
Test.game.wait_for_state("Flight", 45)

local vessel = Test.flight.start("Fly Safe-15")
Test.assert.equal(vessel.body, "Kerbin", "launch vessel should be on Kerbin")
Test.assert.equal(vessel.situation, "PreLaunch", "fixture should begin on the launchpad")

Test.camera.mode("Flight")
Test.camera.target_vessel()
Test.camera.orbit {
    distance = 45,
    yaw = 45,
    pitch = 5,
    fov = 55
}

Test.render.wait_stable(30)
Test.capture.screenshot("launchpad-before-launch", {
    scale = 1,
    hideUI = false,
    waitFrames = 0
})

Test.flight.set_sas(true)
Test.flight.set_throttle(1.0)
Test.wait.frames(5)
Test.flight.stage()

Test.wait["until"](function()
    local active = Test.flight.active_vessel()
    return active ~= nil and
        active.situation ~= "PreLaunch" and
        active.altitude > 5
end, 45)

Test.render.wait_stable(30)
local launched = Test.flight.active_vessel()
Test.capture.screenshot("launchpad-after-launch", {
    scale = 1,
    hideUI = false,
    waitFrames = 0
})

Test.assert.equal(launched.name, "Fly Safe-15", "active vessel should remain the launch vessel")
Test.assert.not_equal(launched.situation, "PreLaunch", "vessel should leave prelaunch state")
Test.assert.greater(launched.altitude, 5, "vessel should rise above the launchpad")
Test.report.value("launchSituation", launched.situation)
Test.report.metric("launchAltitude", launched.altitude)
Test.report.note("Loaded a launchpad fixture, staged the vessel, and verified liftoff")
