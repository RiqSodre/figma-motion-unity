// SPDX-License-Identifier: MIT
// Copyright (c) 2026 Ricardo Sodré. Part of FMU (Figma Motion → Unity).
//
// FMU Exporter — reads Figma Motion keyframe data from the selected frame,
// scans it for Unity compatibility, and emits the ".fmu.json" exchange format.
// Runs in Figma's plugin sandbox. Motion API is Beta (node.animations).
//   https://developers.figma.com/docs/plugins/api/figma-motion/

figma.showUI(__html__, { width: 380, height: 520 });

// --- helpers -------------------------------------------------------------

function hex(c) {
  const f = (x) => ('0' + Math.round(x * 255).toString(16)).slice(-2);
  return '#' + f(c.r) + f(c.g) + f(c.b);
}

// Returns the easing part of a key: { ease:[x1,y1,x2,y2] } | { spring:bounce } | { hold:true }
function easeOf(e) {
  if (!e) return { ease: [0, 0, 1, 1] };
  if (e.type === 'HOLD') return { hold: true };
  if (e.easingFunctionSpring && e.easingFunctionSpring.bounce != null)
    return { spring: e.easingFunctionSpring.bounce };
  if (e.easingFunctionCubicBezier) {
    const b = e.easingFunctionCubicBezier;
    return { ease: [b.x1, b.y1, b.x2, b.y2] };
  }
  if (e.type === 'LINEAR') return { ease: [0, 0, 1, 1] };
  return { ease: [0.42, 0, 0.58, 1] };
}

function keyOf(k, getValue) {
  return Object.assign({ t: k.timelinePosition, v: getValue(k.value) }, easeOf(k.easing));
}

const RELATIVE = { TRANSLATION_X: true, TRANSLATION_Y: true };

// Property support matrix, shared by the scanner and shown in the UI.
const SUPPORT = {
  OPACITY: 'ok', HEIGHT: 'ok', WIDTH: 'ok',
  TRANSLATION_X: 'ok', TRANSLATION_Y: 'ok',
  SCALE_XY: 'ok', SCALE: 'ok', SCALE_X: 'ok', SCALE_Y: 'ok',
  fills: 'ok', ROTATION: 'beta',
};

function isGradient(node) {
  const f = node.fills;
  if (!f || f === figma.mixed) return false;
  return f.some((x) => x.visible !== false && x.type && x.type.indexOf('GRADIENT') === 0);
}

// --- exporter core -------------------------------------------------------

function extractTracks(node) {
  const anims = node.animations || {};
  const tracks = [];
  for (const prop in anims) {
    // Animated fill color: anims.fills is indexed by fill ("0" = first fill),
    // each holding COLOR keyframes. Split into R/G/B/A float tracks.
    if (prop === 'fills') {
      const fa = anims.fills && anims.fills['0'];
      const trk = fa && fa.tracks && fa.tracks[0];
      if (!trk) continue;
      [['FILL_R', 'r'], ['FILL_G', 'g'], ['FILL_B', 'b'], ['FILL_A', 'a']].forEach(function (ci) {
        tracks.push({
          property: ci[0],
          base: fa.baseValue ? fa.baseValue.value[ci[1]] : null,
          relative: false,
          keys: trk.keyframes.map((k) => keyOf(k, (val) => val.value[ci[1]])),
        });
      });
      continue;
    }

    const a = anims[prop];
    const trk = a.tracks && a.tracks[0];
    if (!trk) continue;
    const vtype = a.baseValue ? a.baseValue.type : 'FLOAT';

    // Vector value (e.g. SCALE_XY) -> per-axis float tracks (SCALE_X / SCALE_Y).
    if (vtype === 'VECTOR') {
      const baseName = prop === 'SCALE_XY' ? 'SCALE' : prop;
      ['x', 'y'].forEach((ch) => {
        tracks.push({
          property: baseName + '_' + ch.toUpperCase(),
          base: a.baseValue ? a.baseValue.value[ch] : null,
          relative: false,
          keys: trk.keyframes.map((k) => keyOf(k, (val) => val.value[ch])),
        });
      });
      continue;
    }

    // Scalar FLOAT.
    tracks.push({
      property: prop,
      base: a.baseValue ? a.baseValue.value : null,
      relative: !!RELATIVE[prop],
      keys: trk.keyframes.map((k) => keyOf(k, (val) => val.value)),
    });
  }
  return tracks;
}

function firstSolidFill(node) {
  const fills = node.fills;
  if (!fills || fills === figma.mixed) return null;
  const s = fills.find((f) => f.visible !== false && f.type === 'SOLID');
  return s ? { color: hex(s.color), opacity: s.opacity != null ? s.opacity : 1 } : null;
}

function firstStroke(node) {
  const st = node.strokes;
  if (!st || !st.length) return null;
  const s = st.find((x) => x.type === 'SOLID' && x.visible !== false);
  return s
    ? { color: hex(s.color), opacity: s.opacity != null ? s.opacity : 1, weight: node.strokeWeight }
    : null;
}

function round(n, p) { const m = Math.pow(10, p); return Math.round(n * m) / m; }

// Ellipse arc / ring: from Figma arcData (partial sweep or inner radius) or from a
// stroked-only ellipse (rendered as a ring of thickness = strokeWeight).
function arcOf(node) {
  if (node.type !== 'ELLIPSE') return null;
  const TAU = Math.PI * 2;
  const R = Math.min(node.width, node.height) / 2;
  const ad = node.arcData;
  if (ad && (ad.innerRadius > 0.0001 || ad.endingAngle - ad.startingAngle < TAU - 0.001)) {
    return { inner: round(ad.innerRadius, 4), a0: round(ad.startingAngle, 5), a1: round(ad.endingAngle, 5) };
  }
  const hasFill = node.fills && node.fills !== figma.mixed &&
    node.fills.some((f) => f.visible !== false && f.type === 'SOLID');
  const hasStroke = node.strokes && node.strokes.length;
  if (!hasFill && hasStroke && R > 0) {
    const w = node.strokeWeight || 1;
    return { inner: round(Math.max(0, (R - w) / R), 4), a0: 0, a1: round(TAU, 5) };
  }
  return null;
}

function serialize(node) {
  const out = {
    id: node.id, name: node.name, type: node.type,
    rect: { x: round(node.x, 2), y: round(node.y, 2), w: node.width, h: node.height },
    opacity: round(node.opacity, 3),
    cornerRadius:
      node.cornerRadius !== undefined && node.cornerRadius !== figma.mixed ? node.cornerRadius : 0,
    fill: firstSolidFill(node), stroke: firstStroke(node), arc: arcOf(node),
    tracks: extractTracks(node),
  };
  if (node.effects && node.effects.length) {
    out.effects = node.effects.filter((e) => e.visible !== false).map((e) => ({ type: e.type, radius: e.radius }));
  }
  if ('children' in node && node.children.length) out.children = node.children.map(serialize);
  return out;
}

function buildDoc(root) {
  const tl = (root.timelines && root.timelines[0]) || { duration: 0 };
  return {
    format: 'fmu', version: '0.1', generator: 'figma-motion-unity/exporter 0.1',
    source: { file: figma.root.name, nodeId: root.id, name: root.name },
    coordinateSpace: 'figma-top-left-px',
    timeline: { duration: tl.duration, loop: true },
    node: serialize(root),
  };
}

// --- compatibility scanner ----------------------------------------------

function scan(root) {
  const summary = { animatedLayers: 0, layers: 0, tracks: {}, keyframes: 0, lastKey: 0 };
  const issues = [];

  function walk(node) {
    summary.layers++;
    const anims = node.animations || {};
    const props = Object.keys(anims);
    if (props.length) summary.animatedLayers++;

    for (const p of props) {
      summary.tracks[p] = (summary.tracks[p] || 0) + 1;
      // fills animation lives under an index ("0"); everything else under .tracks
      const trk = p === 'fills'
        ? (anims.fills && anims.fills['0'] && anims.fills['0'].tracks && anims.fills['0'].tracks[0])
        : (anims[p].tracks && anims[p].tracks[0]);
      if (trk) {
        summary.keyframes += trk.keyframes.length;
        for (const k of trk.keyframes) summary.lastKey = Math.max(summary.lastKey, k.timelinePosition);
      }
      // Springs are supported (baked) — no warning. Fill color is supported for
      // SOLID fills; animated gradients are not.
      if (p === 'fills') {
        if (isGradient(node))
          issues.push({ level: 'error', layer: node.name, msg: 'gradiente animado não suportado (só cor sólida)' });
        continue;
      }
      if (!SUPPORT[p]) issues.push({ level: 'error', layer: node.name, msg: "'" + p + "' não suportado (ignorado)" });
      else if (SUPPORT[p] === 'beta') issues.push({ level: 'info', layer: node.name, msg: "'" + p + "' suportado (não testado)" });
    }
    if (node.type === 'TEXT' && props.length)
      issues.push({ level: 'warn', layer: node.name, msg: 'animação de texto não suportada' });

    if ('children' in node) node.children.forEach(walk);
  }
  walk(root);

  // dedupe identical issues
  const seen = {};
  const uniq = issues.filter((i) => {
    const k = i.level + i.layer + i.msg;
    if (seen[k]) return false; seen[k] = 1; return true;
  });
  return { summary, issues: uniq };
}

// --- message plumbing ----------------------------------------------------

function currentTarget() {
  const sel = figma.currentPage.selection;
  if (sel.length !== 1) return null;
  const n = sel[0];
  if (!('children' in n)) return null;
  return n;
}

function pushScan() {
  const t = currentTarget();
  if (!t) {
    figma.ui.postMessage({ type: 'scan', ok: false, hint: 'Selecione um único frame com timeline de Motion.' });
    return;
  }
  const tl = (t.timelines && t.timelines[0]) || { duration: 0 };
  const r = scan(t);
  figma.ui.postMessage({
    type: 'scan', ok: true, name: t.name,
    size: { w: Math.round(t.width), h: Math.round(t.height) },
    duration: tl.duration, summary: r.summary, issues: r.issues,
  });
}

figma.on('selectionchange', pushScan);
pushScan();

figma.ui.onmessage = (msg) => {
  if (msg.type === 'rescan') { pushScan(); return; }
  if (msg.type === 'export') {
    const t = currentTarget();
    if (!t) { figma.notify('Selecione um frame com Motion.'); return; }
    const doc = buildDoc(t);
    figma.ui.postMessage({
      type: 'file',
      filename: t.name.replace(/[^a-z0-9\-_]+/gi, '-') + '.fmu',
      json: JSON.stringify(doc, null, 2),
    });
    figma.notify('Exportado: ' + t.name + '.fmu');
  }
};
