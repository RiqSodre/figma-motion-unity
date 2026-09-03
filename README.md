# Figma Motion → Unity (POC)

Proof-of-concept for a pipeline that copies a **Figma Motion** animation into
**Unity** as native, editable UI + an `AnimationClip` — not a rendered video.

It exists because, as of mid-2026, Figma's **Motion Plugin API** (`node.animations`,
beta) finally exposes keyframe tracks programmatically. That was the missing piece:
the animation data can now leave Figma with easing intact.

```
Figma frame ──[figma-plugin]──▶  name.fmu (JSON)  ──[Unity ScriptedImporter]──▶  Prefab + AnimationClip
  Motion timeline                 engine-neutral                                   native UI, plays on ▶
```

## What's here

| Path | Role |
|------|------|
| [`figma-plugin/`](figma-plugin) | Figma plugin that reads Motion data and exports `.fmu` |
| [`unity/`](unity) | `ScriptedImporter` (`.fmu` → prefab + clip) + `FmuPlayer` runtime |
| [`samples/listening-indicator-1.fmu`](samples/listening-indicator-1.fmu) | Real export of the listening-indicator (bars + pulsing ring) |
| [`SCHEMA.md`](SCHEMA.md) | The `.fmu` exchange format spec |

The sample was **generated from the actual Figma file** via the Motion API, so the
numbers are real, not mocked.

## The example

`listening-indicator-1` (64×64): three equalizer bars animating `HEIGHT` +
`TRANSLATION_Y`, and an outer ring pulsing `OPACITY`. 2 s timeline, keyframes to
1.2 s then holds/loops. Chosen because it's pure transform + opacity — the slice
that maps cleanly to native Unity UI.

## Try it

**Figma side**
1. Figma → Plugins → Development → *Import plugin from manifest…* → pick
   `figma-plugin/manifest.json`.
2. Select the animated frame, run the plugin, click **Exportar .fmu**. (Or just use
   the provided sample and skip this.)

**Unity side** (Unity 2021.3+; uGUI)
1. Install **Newtonsoft Json** — Package Manager → *Add package by name* →
   `com.unity.nuget.newtonsoft-json`.
2. Copy `unity/Editor/` and `unity/Runtime/` into your project's `Assets/` (the
   `Editor` folder name matters — it keeps the importer editor-only).
3. Drop a `.fmu` file into `Assets/`. It imports as a prefab with a child clip.
4. Put a **Canvas** in a scene, drag the imported prefab under it, press **Play**.
   The `FmuPlayer` drives the clip (loops by default).

## How fidelity is preserved

- **Coordinate model**: children use a top-left anchor + pivot and `y` is negated,
  mirroring Figma's top-left / y-down space one-to-one. `HEIGHT` grows downward
  because the pivot is at the top — so the bar bounce matches exactly.
- **Easing**: every bezier segment is **baked** — the importer samples the
  cubic-bezier timing function at the clip frame rate and writes linear keyframes.
  No bezier→Hermite approximation, so playback matches Figma. The same mechanism
  will absorb Figma **springs** (which the API hands over as sampled/spring data).

## Honest limits (POC scope)

- Renders rectangles as sharp quads and ellipses via Unity's built-in round sprite;
  the outer ring is approximated as a tinted circle (drop shadow not reproduced).
  The POC proves **motion**, not pixel-perfect vector rendering.
- Only `OPACITY / HEIGHT / WIDTH / TRANSLATION_*` are wired. `ROTATION`, `SCALE`,
  color and gradients are the next tracks. Vector morphs stay out of scope — route
  those through a Lottie lane instead.
- Figma's Motion API is **beta** and may change.

See [`SCHEMA.md`](SCHEMA.md) for the full format and the property→Unity mapping.

## License & ownership

Copyright (c) 2026 Ricardo Sodré. Released under the **MIT License** — see
[`LICENSE`](LICENSE). You may use, modify and redistribute it (including in commercial
products) provided the copyright notice is kept. The author retains copyright and may
also offer the work under separate commercial terms (dual licensing).

Third-party components and platform APIs are listed in
[`THIRD-PARTY-NOTICES.md`](THIRD-PARTY-NOTICES.md). Being open-source does **not** waive
authorship: in Brazil the code is protected by copyright from creation (Lei 9.609/98), and
authorship can additionally be registered at **INPI** (Registro de Programa de Computador)
for proof of anteriority.
