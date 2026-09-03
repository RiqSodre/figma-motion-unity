# Third-party notices

This project (FMU — Figma Motion → Unity) is licensed under MIT (see [LICENSE](LICENSE)).
It uses or references the following third-party works. Verify each before commercial
redistribution.

## Newtonsoft.Json (Json.NET)
- Used by the Unity importer to parse `.fmu` files, via the Unity package
  `com.unity.nuget.newtonsoft-json`.
- License: MIT. © James Newton-King.
- Not bundled in this repository — pulled in as a Unity Package Manager dependency.
  If you redistribute a build that embeds it, include its MIT license text.
- https://github.com/JamesNK/Newtonsoft.Json

## Signed-distance rounded-box function
- `sdRoundBox` in `unity/Runtime/FmuSDF.shader` is the well-known rounded-box SDF
  popularized by Inigo Quilez.
- Inigo Quilez's distance-function articles/snippets are generally published under the
  MIT License. Attribution retained in the shader source. Confirm the current license
  terms before commercial redistribution.
- https://iquilezles.org/articles/distfunctions2d/

## Platform APIs (not redistributed)
- **Figma Plugin API**, including the beta `figma.motion` / `node.animations` surface —
  used by the Figma plugin. Subject to Figma's Developer Terms. The `figma.motion` API is
  in beta and may change.
- **Unity Engine / uGUI / Playables** — used by the importer and runtime. Subject to
  Unity's terms. Distributing on the Unity Asset Store is governed by the Asset Store
  Provider/EULA terms.

None of these platform SDKs are redistributed by this project; it only calls their APIs.
