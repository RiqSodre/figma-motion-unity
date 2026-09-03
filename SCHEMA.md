# `.fmu` — Figma Motion → Unity exchange format

A small, **engine-neutral** JSON description of a Figma frame and its Motion
keyframe tracks. The Figma plugin writes it; the Unity importer reads it. Keeping
it neutral means the same file could later feed other engines (Godot, web, etc.).

> File extension is `.fmu` (content is JSON). Unity matches importers by the final
> extension, so `.fmu.json` would hijack every `.json` in the project — hence `.fmu`.

## Design rules

- **Coordinate space stays Figma's**: top-left origin, y grows **down**, pixels.
  The consumer (importer) converts to its own space. The exporter never guesses
  engine conventions.
- **Values are raw Figma values.** No easing approximation happens at export time —
  the cubic-bezier control points travel verbatim and the consumer decides how to
  reproduce them (we bake).
- **One timeline per document** (matches Figma Motion today).

## Shape

```jsonc
{
  "format": "fmu",
  "version": "0.1",
  "source":  { "file": "...", "nodeId": "55:1333", "name": "listening-indicator-1" },
  "coordinateSpace": "figma-top-left-px",
  "timeline": { "duration": 2.0, "loop": true },
  "node": { /* recursive node tree, root first */ }
}
```

### Node

```jsonc
{
  "id": "55:1354",
  "name": "bar-1",
  "type": "RECTANGLE",              // FRAME | RECTANGLE | ELLIPSE | GROUP | ...
  "rect": { "x": 24, "y": 26, "w": 3, "h": 12 },  // relative to parent, Figma space
  "opacity": 1,
  "cornerRadius": 2,
  "fill":   { "color": "#87a782", "opacity": 1 } | null,
  "stroke": { "color": "#87a782", "opacity": 1, "weight": 1.5 } | null,
  "effects": [ { "type": "DROP_SHADOW", "radius": 10 } ],   // optional, informational
  "tracks": [ /* animation tracks */ ],
  "children": [ /* nodes */ ]        // optional
}
```

### Track

```jsonc
{
  "property": "TRANSLATION_Y",   // OPACITY | HEIGHT | WIDTH | TRANSLATION_X | TRANSLATION_Y
  "base": 26,                    // base value. For relative tracks, the pivot deltas add to.
  "relative": true,              // true => key.v is a delta from base (Figma does this for TRANSLATION_*)
  "keys": [
    { "t": 0.0,  "v": 0,  "ease": [0.5, 0, 0.5, 1] },   // ease = cubic-bezier of the OUTGOING segment
    { "t": 0.24, "v": -5, "ease": [0.42, 0, 0.58, 1] }
  ]
}
```

- `t` — seconds on the timeline.
- `v` — absolute value, unless `relative` (then a delta from `base`).
- `ease` — `[x1,y1,x2,y2]` cubic-bezier controlling the interpolation **from this key
  to the next**. `"HOLD"` (string) means a stepped hold. The last key's `ease` is unused.

## Property → intent

| `property`      | Figma meaning                    | Unity mapping (this POC)                        |
|-----------------|----------------------------------|-------------------------------------------------|
| `OPACITY`       | layer opacity 0..1               | `CanvasGroup.m_Alpha`                           |
| `HEIGHT`        | layer height (px, grows down)    | `RectTransform.m_SizeDelta.y` (top pivot)       |
| `WIDTH`         | layer width (px)                 | `RectTransform.m_SizeDelta.x`                   |
| `TRANSLATION_X` | x offset (px, right +)           | `RectTransform.m_AnchoredPosition.x` = base+Δ   |
| `TRANSLATION_Y` | y offset (px, **down +**)        | `RectTransform.m_AnchoredPosition.y` = −(base+Δ)|

Unsupported today (skipped by the importer, same gaps as Figma's Lottie export):
`ROTATION`, `SCALE`, `FILL_COLOR`/gradients, text content, boolean ops, blur/shader
effects, vector path morphs.

## Not covered by this POC (roadmap)

- **Springs** — Figma exposes `CUSTOM_SPRING`; the plan is to bake them the same way
  we bake bezier (sample over time), which the importer already supports structurally.
- **Rotation / scale / color** — straightforward next tracks to add.
- **Vector shapes** — no native Unity equivalent; route those through the Lottie lane.
