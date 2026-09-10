Test.name("Redux Better AA launchpad object motion-history probe")

Test.assert.true_(
    Test.mod.is_loaded("ReduxBetterAA"),
    "ReduxBetterAA is required")

local better_aa = Test.mod.extension("ReduxBetterAA")
Test.assert.not_equal(
    better_aa,
    nil,
    "ReduxBetterAA TestHarness adapter is required")

better_aa.set_backend("Off")
Test.game.load_save("local/launchpad-fly-safe-15")
Test.game.wait_for_state("Flight", 45)
Test.flight.start("Fly Safe-15")
Test.camera.mode("Flight")
Test.camera.target_vessel()
Test.render.wait_stable(45)
better_aa.select_camera("FlightCameraPhysics_Main")
better_aa.set_buffer_view("raw")
Test.camera.orbit {
    distance = 45,
    yaw = 0,
    pitch = 35,
    fov = 55
}
Test.wait.frames(24)

local modes = {
    { id = 0, name = "native-object-history" },
    { id = 9, name = "current-vertex-current-object-matrix" },
    { id = 10, name = "current-vertex-previous-object-matrix" },
    { id = 11, name = "previous-vertex-current-object-matrix" },
    { id = 12, name = "has-last-position-data-tag" }
}

for _, mode in ipairs(modes) do
    better_aa.set_motion_pass_probe(mode.id)
    Test.wait.frames(6)
    Test.capture.screenshot(
        mode.name,
        { scale = 1, hideUI = false, waitFrames = 1 })
end

better_aa.set_buffer_view("validity")
Test.wait.frames(4)
for mode = 13, 16 do
    better_aa.set_motion_pass_probe(mode)
    Test.wait.frames(4)
    Test.assert.true_(
        better_aa.request_capture(),
        "BetterAA should queue an object-matrix statistics capture")
    Test.wait.frames(30)
end

better_aa.restore_motion_pass_probe()
Test.report.note(
    "Modes 9-12 split Unity's object history into previous-position-stream and previous-object-matrix inputs; modes 13-16 dump previous/current object matrices")
