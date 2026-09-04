// SPDX-License-Identifier: MIT
// Copyright (c) 2026 Ricardo Sodré. Part of FMU (Figma Motion → Unity).
//
// Serializable DTOs for the .fmu exchange format (JSON).
// Parsed with Newtonsoft.Json (package: com.unity.nuget.newtonsoft-json).
using System.Collections.Generic;

namespace Fmu
{
    public class FmuDoc
    {
        public string format;
        public string version;
        public FmuSource source;
        public string coordinateSpace;
        public FmuTimeline timeline;
        public FmuNode node;
    }

    public class FmuSource
    {
        public string file;
        public string nodeId;
        public string name;
    }

    public class FmuTimeline
    {
        public float duration;
        public bool loop;
    }

    public class FmuNode
    {
        public string id;
        public string name;
        public string type;      // FRAME | RECTANGLE | ELLIPSE | ...
        public FmuRect rect;
        public float opacity = 1f;
        public float cornerRadius;
        public FmuPaint fill;    // null => no fill
        public FmuStroke stroke; // null => no stroke
        public FmuArc arc;       // null => solid ellipse/rect
        public List<FmuTrack> tracks;
        public List<FmuNode> children;
    }

    public class FmuRect { public float x, y, w, h; }

    // Ellipse arc / ring (from Figma arcData or a stroked ellipse). Angles in radians,
    // Figma convention (0 = +x, clockwise). inner is the inner radius as a fraction of R.
    public class FmuArc { public float inner; public float a0; public float a1; }

    public class FmuPaint { public string color; public float opacity = 1f; }

    public class FmuStroke { public string color; public float opacity = 1f; public float weight = 1f; }

    public class FmuTrack
    {
        public string property;  // OPACITY | HEIGHT | WIDTH | TRANSLATION_X | TRANSLATION_Y
        public float @base;      // base value (absolute); for relative tracks this is the pivot the deltas add to
        public bool relative;    // true => key.v is a delta from base
        public List<FmuKey> keys;
    }

    public class FmuKey
    {
        public float t;          // seconds on the timeline
        public float v;          // value (absolute, or delta if track.relative)
        // Exactly one of the following describes the OUTGOING segment easing:
        public float[] ease;     // cubic-bezier [x1,y1,x2,y2]
        public float? spring;    // Figma spring "bounce" (0..1); overrides ease
        public bool hold;        // stepped hold (constant until next key)
    }
}
