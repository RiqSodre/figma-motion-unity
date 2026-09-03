# FMU — Figma Motion to Unity

Import **Figma Motion** animations as native Unity UI (real hierarchy + `AnimationClip`) —
not a rendered video. Part of the [FMU project](https://github.com/) (Figma export plugin
+ this Unity package).

## Install

**Package Manager → Add package from git URL:**

```
https://github.com/SEU-USUARIO/figma-motion-unity.git?path=/unity
```

Or **Add package from disk…** and pick this folder's `package.json` (for local development).

The dependency `com.unity.nuget.newtonsoft-json` is resolved automatically.

## Use

1. Drop a `.fmu` (exported by the Figma plugin) into your project.
2. It imports as a prefab with a child `AnimationClip`.
3. Select it and click **Add to Scene (under Canvas)** in the inspector, or drag it under a
   Canvas yourself. Press **Play** — `FmuPlayer` loops the clip.

Import the **Listening Indicator** sample from the package page to see a working example.

## What's supported

Tracks: `OPACITY`, `HEIGHT`, `WIDTH`, `TRANSLATION_X/Y`; `ROTATION`, `SCALE` (beta).
Shapes: rounded rectangle and ellipse, drawn crisp via an SDF shader.

Not yet: animated color/gradient, springs, vector path morphs, effects, animated text.

See the repo's `SCHEMA.md` for the exchange format and `HANDOFF.md` for the full guide.

## License

MIT © 2026 Ricardo Sodré. Third-party notices in the repo's `THIRD-PARTY-NOTICES.md`.
