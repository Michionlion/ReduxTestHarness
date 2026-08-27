Test.name("Orbit rendering smoke test")

Test.game.load_save("rendering/kerbin-orbit")
Test.game.wait_for_state("Flight", 30)

local vessel = Test.flight.start("Render Test Vehicle")
Test.assert.equal(vessel.body, "Kerbin", "reference vessel should orbit Kerbin")
Test.assert.greater(vessel.altitude, 70000, "reference vessel should be above the atmosphere")

Test.camera.mode("Flight")
Test.camera.target_vessel()
Test.camera.orbit {
    distance = 18,
    yaw = 135,
    pitch = 12,
    fov = 55
}

Test.render.set("supersampling", 1.0)
Test.render.wait_stable(30)
Test.capture.screenshot("orbit-render", {
    scale = 1,
    hideUI = true,
    waitFrames = 0
})

Test.report.value("vessel", vessel.name)
Test.report.metric("altitude", vessel.altitude)
Test.report.note("Captured a deterministic vessel-relative Kerbin orbit view")

