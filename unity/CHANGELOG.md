# Changelog

All notable changes to this package are documented here.
Format based on [Keep a Changelog](https://keepachangelog.com/); versioning is [SemVer](https://semver.org/).

## [0.2.0] - 2026-09-04

### Added
- **Spring easing** — Figma springs (bounce) are baked with a tunable `Spring Settle`
  (punchier kick + settle, closer to Figma).
- **Animated fill color** (`FILL_R/G/B/A`) and **SCALE_XY** (as `SCALE_X`/`SCALE_Y`).
- **ROTATION** track (direction fixed) and **HOLD**/step easing.
- **Rings & arcs** — ellipses with a stroke or Figma `arcData` render as crisp rings /
  annular sectors via the SDF shader (spinners, progress rings, gauges).
- **Center-pivot wrapper** — nodes that both move/resize *and* scale/rotate now scale and
  rotate around their center while position/size stay top-left based.
- **Playback controls** on `FmuPlayer`: play mode (Loop/Once/PingPong), speed, autoplay,
  start delay, plus an **edit-mode preview** (scrub the animation in the Scene without Play).
- More samples: Spring + Animated Color, and Tier 1 — Transforms.

## [0.1.0] - 2026-09-03

### Added
- `ScriptedImporter` for `.fmu` files → native UI prefab + `AnimationClip`.
- Baked easing: cubic-bezier segments sampled at the clip frame rate (exact match to Figma).
- `FmuShapeGraphic` + `FMU/UI SDF` shader for resolution-independent rounded-rect / ellipse.
- `FmuPlayer` runtime component (PlayableGraph, manual loop over the timeline duration).
- Custom importer inspector with summary, compatibility report, and "Add to Scene (under Canvas)".
- Supported tracks: `OPACITY`, `HEIGHT`, `WIDTH`, `TRANSLATION_X/Y`; `ROTATION`, `SCALE` (beta).
- Sample: Listening Indicator.
