Test.name("Redux Better AA repeated DLAA cloud solution matrix")

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
    }
}

local conditions = {
    {
        name = "full-bias",
        full_bias = true
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

local function capture(condition, cycle, index)
    local prefix = string.format(
        "%s-cycle-%d-report-%02d",
        condition.name,
        cycle,
        index)
    Test.capture.screenshot(
        prefix,
        { scale = 1, hideUI = true, waitFrames = 1 })
    Test.report.value(prefix .. "CloudRenderer", better_aa.cloud_renderer_snapshot())
end

local function configure(condition)
    better_aa.set_backend("Off")
    better_aa.restore_motion_clear()
    better_aa.restore_depth_override()
    better_aa.set_dlaa_execution_bypass(false)
    better_aa.set_dlaa_transparent_projection_jitter(false)
    better_aa.set_temporal_jitter_suppressed(false)
    better_aa.set_motion_sanitizer(condition.motion_sanitizer == true)
    better_aa.set_depth_disocclusion_mask(condition.depth_mask ~= false)
    better_aa.set_dlaa_no_depth_bias(condition.no_depth_bias == true)
    better_aa.set_dlaa_full_bias(condition.full_bias == true)
end

local function run_cycle(condition, cycle)
    configure(condition)
    Test.game.load_save("local/cloud-time-sweep/cloud-time-08")
    Test.game.wait_for_state("Flight", 45)
    Test.flight.start("Fly Safe-15")
    Test.camera.mode("Flight")
    Test.camera.target_vessel()
    Test.wait.frames(45)
    better_aa.select_camera("FlightCameraPhysics_Main")
    if condition.zero_motion then
        better_aa.clear_motion_at_event("BeforeImageEffects")
    end
    if condition.far_depth then
        better_aa.override_depth_with_far("BeforeImageEffects")
    end
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
    capture(condition, cycle, 13)
    move_between(poses.report_13, poses.report_14, 30)
    capture(condition, cycle, 14)
    move_between(poses.report_14, poses.report_15, 30)
    capture(condition, cycle, 15)
    move_between(poses.report_15, poses.report_16, 30)
    capture(condition, cycle, 16)
end

for _, condition in ipairs(conditions) do
    for cycle = 1, 3 do
        run_cycle(condition, cycle)
    end
end

better_aa.set_backend("Off")
better_aa.set_dlaa_execution_bypass(false)
better_aa.set_dlaa_transparent_projection_jitter(false)
better_aa.set_temporal_jitter_suppressed(false)
better_aa.set_motion_sanitizer(false)
better_aa.set_depth_disocclusion_mask(true)
better_aa.set_dlaa_no_depth_bias(false)
better_aa.set_dlaa_full_bias(false)
better_aa.restore_motion_clear()
better_aa.restore_depth_override()
Test.report.note(
    "Repeated the failing cloud transition three times with the entire frame marked in the DLAA current-color bias mask. Every cycle reloads the same cloudy fixture and creates a fresh DLAA M context.")
