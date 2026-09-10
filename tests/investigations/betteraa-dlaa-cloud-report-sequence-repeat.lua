Test.name("Redux Better AA repeated DLAA cloud report sequence")

Test.assert.true_(Test.mod.is_loaded("ReduxBetterAA"), "ReduxBetterAA is required")
local better_aa = Test.mod.extension("ReduxBetterAA")
Test.assert.not_equal(better_aa, nil, "ReduxBetterAA adapter is required")

local poses = {
    far = {
        position = { 74903.81, -128062.648, 22121.957 },
        rotation = { -0.704959631, -0.277077258, 0.100631677, 0.6450841 }
    },
    low = {
        position = { 35856.9844, -17543.2637, -7451.934 },
        rotation = { -0.5214957, -0.369172066, 0.368642479, 0.6751718 }
    },
    approach_a = {
        position = { 44492.39, -20046.5879, -3725.20679 },
        rotation = { -0.5509429, -0.3980007, 0.3656657, 0.635882139 }
    },
    approach_b = {
        position = { 44596.11, -20091.8027, -1679.98645 },
        rotation = { -0.5635268, -0.4065396, 0.358914226, 0.623172343 }
    },
    report_13 = {
        position = { 45389.918, -18301.9844, 329.4944 },
        rotation = { -0.56927824, -0.423424631, 0.367149383, 0.6015274 }
    },
    report_14 = {
        position = { 45273.5352, -18494.7168, 1888.92139 },
        rotation = { -0.5790932, -0.428892255, 0.35972935, 0.592703342 }
    },
    report_15 = {
        position = { 45022.69, -18867.1523, 3507.6355 },
        rotation = { -0.5898472, -0.433491558, 0.350389, 0.5842885 }
    },
    report_16 = {
        position = { 44772.1875, -19104.252, 5078.8335 },
        rotation = { -0.59979856, -0.438324, 0.3420987, 0.575397432 }
    },
    settled_wiggle = {
        position = { 45389.918, -18301.9844, 329.4944 },
        rotation = { -0.5790932, -0.428892255, 0.35972935, 0.592703342 }
    }
}

local function set_pose(pose, frames)
    Test.camera.absolute {
        position = pose.position,
        rotation = pose.rotation,
        fov = 60
    }
    Test.wait.frames(frames)
end

local function move_between(from_pose, to_pose, frames)
    for frame = 1, frames do
        local t = frame / frames
        local position = {}
        local rotation = {}
        local length_squared = 0
        for axis = 1, 3 do
            position[axis] = from_pose.position[axis] +
                (to_pose.position[axis] - from_pose.position[axis]) * t
        end
        for axis = 1, 4 do
            rotation[axis] = from_pose.rotation[axis] +
                (to_pose.rotation[axis] - from_pose.rotation[axis]) * t
            length_squared = length_squared + rotation[axis] * rotation[axis]
        end
        local inverse_length = 1 / math.sqrt(length_squared)
        for axis = 1, 4 do
            rotation[axis] = rotation[axis] * inverse_length
        end
        Test.camera.absolute {
            position = position,
            rotation = rotation,
            fov = 60
        }
        Test.wait.frames(1)
    end
end

local function capture(cycle, index, pose)
    local prefix = string.format("cycle-%d-report-%02d", cycle, index)
    Test.capture.screenshot(
        prefix,
        { scale = 1, hideUI = true, waitFrames = 1 })
    Test.report.value(prefix .. "CloudRenderer", better_aa.cloud_renderer_snapshot())
    Test.report.value(
        prefix .. "DlaaCloudTransition",
        better_aa.dlaa_cloud_transition_snapshot())
    Test.report.attach(better_aa.capture_cloud_texture("_finalSceneColor", "rgb"))
    Test.report.attach(better_aa.capture_cloud_texture("_finalSceneColor", "alpha"))
end

local function run_cycle(cycle)
    better_aa.set_backend("Off")
    better_aa.set_temporal_jitter_suppressed(false)
    Test.game.load_save("local/cloud-time-sweep/cloud-time-08")
    Test.game.wait_for_state("Flight", 45)
    Test.flight.start("Fly Safe-15")
    Test.camera.mode("Flight")
    Test.camera.target_vessel()
    Test.wait.frames(45)
    better_aa.select_camera("FlightCameraPhysics_Main")
    better_aa.set_dlaa_preset("M")
    better_aa.set_vendor_sharpness("NvidiaDlaa", 0.24)
    better_aa.set_vendor_auto_exposure("NvidiaDlaa", true, 1.0, true)
    better_aa.set_backend("NvidiaDlaa")
    Test.wait.frames(90)

    set_pose(poses.far, 60)
    move_between(poses.far, poses.low, 90)
    move_between(poses.low, poses.approach_a, 45)
    move_between(poses.approach_a, poses.approach_b, 30)
    move_between(poses.approach_b, poses.report_13, 30)
    capture(cycle, 13, poses.report_13)
    move_between(poses.report_13, poses.report_14, 30)
    capture(cycle, 14, poses.report_14)
    move_between(poses.report_14, poses.report_15, 30)
    capture(cycle, 15, poses.report_15)
    move_between(poses.report_15, poses.report_16, 30)
    capture(cycle, 16, poses.report_16)
    move_between(poses.report_16, poses.report_13, 60)
    capture(cycle, 17, poses.report_13)
    Test.wait.frames(240)
    capture(cycle, 18, poses.report_13)
    move_between(poses.report_13, poses.settled_wiggle, 30)
    capture(cycle, 19, poses.settled_wiggle)
    move_between(poses.settled_wiggle, poses.report_13, 30)
    capture(cycle, 20, poses.report_13)
end

for cycle = 1, 3 do
    run_cycle(cycle)
end

better_aa.set_backend("Off")
Test.report.note(
    "Replayed the user's far-to-near approach, reports 13-16, the reverse transition, a 240-frame settling period, and a small two-way camera rotation after DLAA resumed in three independently reloaded DLAA M contexts. Each presented frame is paired with the cloud renderer's final RGB and alpha source and the automatic cloud compatibility state.")
