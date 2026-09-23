export function hashToColor(value: string, palette: readonly string[]): string {
  let hash = 0;
  for (let i = 0; i < value.length; i++) {
    hash = (hash * 31 + value.charCodeAt(i)) >>> 0;
  }
  return palette[hash % palette.length];
}

export function polarToCartesian(cx: number, cy: number, r: number, angleDeg: number) {
  const angleRad = ((angleDeg - 90) * Math.PI) / 180;
  return { x: cx + r * Math.cos(angleRad), y: cy + r * Math.sin(angleRad) };
}

export function buildPieSlices<T extends { percent: number }>(
  breakdown: T[],
  radius: number,
): (T & { path: string })[] {
  let sliceStart = 0;
  return breakdown.map((slice) => {
    const sweep = (slice.percent / 100) * 360;
    const path = describePieSlice(radius, radius, radius, sliceStart, sliceStart + sweep);
    sliceStart += sweep;
    return { ...slice, path };
  });
}

export function describePieSlice(
  cx: number,
  cy: number,
  r: number,
  startAngle: number,
  endAngle: number,
): string {
  if (endAngle - startAngle >= 359.99) {
    // A single 100% slice has no distinct start/end point for an arc - draw it as two half-circles.
    const mid = startAngle + 180;
    return [
      describePieSlice(cx, cy, r, startAngle, mid),
      describePieSlice(cx, cy, r, mid, endAngle),
    ].join(' ');
  }
  const start = polarToCartesian(cx, cy, r, endAngle);
  const end = polarToCartesian(cx, cy, r, startAngle);
  const largeArcFlag = endAngle - startAngle <= 180 ? '0' : '1';
  return [
    'M',
    cx,
    cy,
    'L',
    start.x,
    start.y,
    'A',
    r,
    r,
    0,
    largeArcFlag,
    0,
    end.x,
    end.y,
    'Z',
  ].join(' ');
}
