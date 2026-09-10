Test.name("Redux Better AA DLAA exact cloud-distance isolation")

Test.assert.true_(Test.mod.is_loaded("ReduxBetterAA"), "ReduxBetterAA is required")
local better_aa = Test.mod.extension("ReduxBetterAA")
Test.assert.not_equal(better_aa, nil, "ReduxBetterAA adapter is required")

better_aa.set_backend("Off")
better_aa.set_temporal_jitter_suppressed(false)
Test.game.load_save("local/launchpad-cloudy-fly-safe-15")
Test.game.wait_for_state("Flight", 45)
Test.flight.start("Fly Safe-15")
Test.camera.mode("Flight")
Test.camera.target_vessel()
Test.wait.frames(30)
better_aa.select_camera("FlightCameraPhysics_Main")

local poses = {
    {
        name = "near",
        position = { 11711.9854, -21593.69, -13177.0762 },
        rotation = { -0.475774676, -0.193106592, 0.09540464, 0.8527874 }
    },
    {
        name = "intermediate",
        position = { 15496.4785, -28571.2539, -17434.9824 },
        rotation = { -0.475774676, -0.193106592, 0.09540464, 0.8527874 }
    },
    {
        name = "far",
        position = { 17410.1641, -35362.8672, -20554.3281 },
        rotation = { -0.483956337, -0.186005175, 0.0773279145, 0.851591945 }
    }
}

local function set_pose(pose)
    Test.camera.absolute {
        position = pose.position,
        rotation = pose.rotation,
        fov = 60
    }
    Test.wait.frames(90)
end

local function capture(prefix, pose)
    Test.capture.screenshot(
        string.format("%s-%s", prefix, pose.name),
        { scale = 1, hideUI = true, waitFrames = 2 })
    Test.report.value(
        string.format("%sCloudRenderer-%s", prefix, pose.name),
        better_aa.cloud_renderer_snapshot())
end

-- First capture the stock producer at every exact report position.
better_aa.set_backend("Off")
for _, pose in ipairs(poses) do
    set_pose(pose)
    capture("10-off", pose)
end

-- Repeat the same positions under K with normal jitter.
better_aa.set_dlaa_preset("K")
better_aa.set_vendor_sharpness("NvidiaDlaa", 0.0)
better_aa.set_vendor_auto_exposure("NvidiaDlaa", false, 1.0, false)
better_aa.set_backend("NvidiaDlaa")
for _, pose in ipairs(poses) do
    set_pose(pose)
    capture("20-k", pose)
end

-- A fresh K context with no projection jitter isolates cloud culling/projection.
better_aa.set_backend("Off")
better_aa.set_temporal_jitter_suppressed(true)
better_aa.set_backend("NvidiaDlaa")
for _, pose in ipairs(poses) do
    set_pose(pose)
    capture("30-k-zero-jitter", pose)
end

-- M is included because the latest user capture suggests preset sensitivity.
better_aa.set_backend("Off")
better_aa.set_temporal_jitter_suppressed(false)
better_aa.set_dlaa_preset("M")
better_aa.set_backend("NvidiaDlaa")
for _, pose in ipairs(poses) do
    set_pose(pose)
    capture("40-m", pose)
end

better_aa.set_backend("Off")
Test.report.note(
    "Compared Off, K, fresh zero-jitter K, and M at the exact near/intermediate/far camera poses from the latest reports; sharpening and exposure were held at zero and manual 1.")
