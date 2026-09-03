# Changelog

All notable changes to this package are documented here.
Format based on [Keep a Changelog](https://keepachangelog.com/); versioning is [SemVer](https://semver.org/).

## [0.1.0] - 2026-09-03

### Added
- `ScriptedImporter` for `.fmu` files → native UI prefab + `AnimationClip`.
- Baked easing: cubic-bezier segments sampled at the clip frame rate (exact match to Figma).
- `FmuShapeGraphic` + `FMU/UI SDF` shader for resolution-independent rounded-rect / ellipse.
- `FmuPlayer` runtime component (PlayableGraph, manual loop over the timeline duration).
- Custom importer inspector with summary, compatibility report, and "Add to Scene (under Canvas)".
- Supported tracks: `OPACITY`, `HEIGHT`, `WIDTH`, `TRANSLATION_X/Y`; `ROTATION`, `SCALE` (beta).
- Sample: Listening Indicator.
