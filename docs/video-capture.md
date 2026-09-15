# Better AA comparison videos

Use a disposable Redux game installation. The script installs the supplied
complete Better AA ZIP and this harness there. The previous mod is retained in
the output directory. It temporarily isolates the normal player profile and
restores it when the game exits, including on capture failure. It also restores
Redux's online-service settings. Captures and the temporary profile are retained.

```powershell
./scripts/record-betteraa-videos.ps1 `
    -GameRoot 'D:\Captures\game' `
    -BetterAaRoot 'D:\Source\ReduxBetterAA' `
    -ReleaseZip 'D:\Releases\ReduxBetterAA-0.6.2-Redux-0.2.9.0.zip' `
    -Fixture 'D:\Fixtures\launchpad.json' `
    -Output 'D:\Captures\take-01' `
    -UnityRoot 'D:\Unity\6000.5.8f1'
```

Requires PowerShell 7.4+, Python, FFmpeg/FFprobe, a matching Unity editor, and
previously accepted game agreements in the normal profile. Close KSP2 first.
Use a new output directory for each take. Add `-Preview` for three framing
stills at the start, midpoint and end, using Off only.

The four clips are `off.mp4`, `taa.mp4`, `dlaa-k.mp4` and `fsr31.mp4`.
Each contains 480 frames at 2560×1440 and 60 fps, with the regular flight UI
visible and F10 closed. The camera sweeps 80 degrees at a constant rate, at a
35-degree downward pitch and 60-degree field of view. `-StartYaw` sets its
starting direction; `-Distance` sets its distance from the vessel.

Each mode reloads the same save, waits six seconds for notifications and temporal
history to settle, and records unpaused.
The routine uses Unity's fixed capture timestep and saves every rendered frame
after UI composition. No frames are duplicated or interpolated. Camera speed
is independent of readback/PNG encoding speed; these are image comparisons,
not real-time performance measurements. Procedural animation can differ between
reloads even with matching camera poses.
The Better AA build must expose its FSR frame interval; the routine verifies
that FSR receives 16.667 ms rather than the wall time spent saving each frame.

The lossless PNG frames remain beside `capture.json`, which records every camera
pose and Unity frame number. `videos.json` contains video hashes, codec checks,
the fixture hash and the installed release manifest. Production AA diagnostics
are also retained. `alignment.json` checks camera poses across all four modes.
The script fails if the expected backend is unavailable, the DLAA preset differs,
capture skips a frame, camera alignment changes, or encoding loses frames.

For an eight-second edit with three wipes, keep all clips aligned at frame zero:
use Off frames 0–119, TAA 120–239, DLAA K 240–359 and FSR 3.1 360–479.
The MP4s use H.264 at CRF 10; use the PNG sequences for lossless editing.

## Harness API

`Test.capture.pan(name, options)` yields until capture completes and returns the
PNG directory. `name` allows letters, digits, underscores and hyphens. Required
options are `startYaw`, `endYaw`, `pitch` and `distance`; optional options are
`fps` (60), `frames` (480), `warmup` (120), `width` (2560), `height` (1440) and
`fov` (60). The game must already be in flight with the desired mode and pause
state. The routine restores resolution and capture timing after completion,
failure or cancellation. Camera control restores at test teardown.
