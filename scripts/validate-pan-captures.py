"""Check complete, aligned camera-pan image sequences without image dependencies."""
import argparse
import json
import math
from pathlib import Path


def check(root, preview=False):
    paths = sorted(Path(root).rglob('capture.json'))
    names = {'off'} if preview else {'off', 'taa', 'dlaa-k', 'fsr31'}
    if len(paths) != len(names) or {p.parent.name for p in paths} != names:
        raise ValueError('Expected one sequence per requested AA mode')
    reference = None
    results = []
    for path in paths:
        data = json.loads(path.read_text(encoding='utf-8'))
        count = 3 if preview else 480
        expected = dict(fps=60, frames=count, width=2560, height=1440,
                        pitch=35, fov=60, uiIncluded=True, offlineCapture=True)
        if any(data.get(k) != v for k, v in expected.items()):
            raise ValueError(f'{path}: unexpected capture settings')
        if data['endYaw'] != data['startYaw'] - 80:
            raise ValueError('Expected an 80-degree right-to-left pan')
        if len(data['poses']) != count:
            raise ValueError('Missing camera poses')
        if {p.name for p in path.parent.glob('*.png')} != {f'{i:06d}.png' for i in range(count)}:
            raise ValueError('Missing or unexpected PNG frames')
        max_angle, max_position = 0.0, 0.0
        for i, pose in enumerate(data['poses']):
            if pose['frame'] != i or pose['unityFrame'] != data['poses'][0]['unityFrame'] + i:
                raise ValueError('Capture skipped a rendered frame')
            if abs(pose['yaw'] - (data['startYaw'] - 80 * i / (count - 1))) > 1e-8:
                raise ValueError('Camera path differs from the requested sweep')
            if abs(pose['deltaTime'] - 1 / 60) > 1e-6:
                raise ValueError('Capture did not use the fixed timestep')
            if i and abs(pose['time'] - data['poses'][i - 1]['time'] - 1 / 60) > 1e-6:
                raise ValueError('Capture simulation time is discontinuous')
            if reference is not None:
                other = reference['poses'][i]
                for key in ('yaw', 'pitch', 'distance', 'fov'):
                    if pose[key] != other[key]:
                        raise ValueError('Camera parameters differ across modes')
                a, b = pose['rotation'], other['rotation']
                dot = abs(sum(x * y for x, y in zip(a, b)))
                dot /= math.sqrt(sum(x*x for x in a) * sum(x*x for x in b))
                angle = math.degrees(2 * math.acos(min(1, dot)))
                distance = math.dist(pose['position'], other['position'])
                max_angle, max_position = max(max_angle, angle), max(max_position, distance)
        if max_angle > 0.01 or max_position > 0.01:
            raise ValueError(f'{path.parent.name}: camera alignment differs: {max_angle} degrees, {max_position} m')
        results.append(dict(mode=path.parent.name, frames=count,
                            maxAngleDifferenceDegrees=max_angle,
                            maxPositionDifferenceMetres=max_position))
        reference = reference or data
    return dict(passed=True, sequences=results)


if __name__ == '__main__':
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('root', type=Path)
    parser.add_argument('--preview', action='store_true')
    parser.add_argument('--output', type=Path, required=True)
    args = parser.parse_args()
    result = check(args.root, args.preview)
    args.output.write_text(json.dumps(result, indent=2) + '\n', encoding='utf-8')
    print(json.dumps(result))
