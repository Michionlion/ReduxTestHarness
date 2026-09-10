Test.name("Redux Better AA launchpad motion projection lifecycle")

Test.assert.true_(
    Test.mod.is_loaded("ReduxBetterAA"),
    "ReduxBetterAA is required")

local better_aa = Test.mod.extension("ReduxBetterAA")
Test.assert.not_equal(
    better_aa,
    nil,
    "ReduxBetterAA TestHarness adapter is required")

Test.game.load_save("local/launchpad-fly-safe-15")
Test.game.wait_for_state("Flight", 45)
Test.flight.start("Fly Safe-15")
Test.camera.mode("Flight")
Test.camera.target_vessel()
better_aa.set_backend("Off")
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

local function capture(label)
    Test.report.value(label .. "Matrices", better_aa.matrix_snapshot())
    Test.capture.screenshot(
        label,
        { scale = 1, hideUI = false, waitFrames = 1 })
end

capture("baseline")

local ppv1 = better_aa.suppress_post_processing("ppv1")
Test.report.value("ppv1Suppressed", ppv1)
Test.wait.frames(12)
capture("ppv1-disabled")
Test.report.value("ppv1Restored", better_aa.restore_post_processing())
Test.wait.frames(12)

local ppv2 = better_aa.suppress_post_processing("ppv2")
Test.report.value("ppv2Suppressed", ppv2)
Test.wait.frames(12)
capture("ppv2-disabled")
Test.report.value("ppv2Restored", better_aa.restore_post_processing())
Test.wait.frames(12)

local all = better_aa.suppress_post_processing("all")
Test.report.value("allSuppressed", all)
Test.wait.frames(12)
capture("all-post-disabled")
Test.report.value("allRestored", better_aa.restore_post_processing())

Test.report.note(
    "Compared the actively regenerated radial field while selectively disabling the selected physics camera's PPv1 and PPv2 lifecycle components; no production AA or sanitizer state was changed")
