import { TestBed } from '@angular/core/testing';
import * as d3 from 'd3';

import { ChartColorService } from './chart-color.service';

describe('ChartColorService', () => {
  let service: ChartColorService;
  const category = 'test-category';
  const paletteSize = d3.schemeTableau10.length;

  beforeEach(() => {
    localStorage.clear();
    TestBed.configureTestingModule({});
    service = TestBed.inject(ChartColorService);
  });

  afterEach(() => {
    localStorage.clear();
  });

  it('should be created', () => {
    expect(service).toBeTruthy();
  });

  it('assigns a color to every requested label', () => {
    const map = service.getColorMap(category, ['a', 'b', 'c']);

    expect(Object.keys(map)).toEqual(['a', 'b', 'c']);
    expect(map['a']).toBeTruthy();
    expect(map['b']).toBeTruthy();
    expect(map['c']).toBeTruthy();
  });

  it('returns the same color for a label across calls', () => {
    const first = service.getColorMap(category, ['a', 'b']);
    const second = service.getColorMap(category, ['b', 'a']);

    expect(second['a']).toBe(first['a']);
    expect(second['b']).toBe(first['b']);
  });

  it('gives distinct labels distinct colors within the palette', () => {
    const labels = Array.from({ length: paletteSize }, (_, i) => `label-${i}`);
    const map = service.getColorMap(category, labels);

    const colors = labels.map(l => map[l]);
    expect(new Set(colors).size).toBe(paletteSize);
  });

  it('assigns palette colors in first-seen order', () => {
    const map = service.getColorMap(category, ['x', 'y']);

    expect(map['x']).toBe(d3.schemeTableau10[0]);
    expect(map['y']).toBe(d3.schemeTableau10[1]);
  });

  it('wraps around to the start of the palette past its size', () => {
    const labels = Array.from({ length: paletteSize + 1 }, (_, i) => `label-${i}`);
    const map = service.getColorMap(category, labels);

    expect(map[`label-${paletteSize}`]).toBe(map['label-0']);
  });

  it('keeps assignments isolated per category', () => {
    const a = service.getColorMap('cat-a', ['shared']);
    // A second category whose first label differs would otherwise collide on index 0.
    const b = service.getColorMap('cat-b', ['other', 'shared']);

    expect(a['shared']).toBe(d3.schemeTableau10[0]);
    expect(b['shared']).toBe(d3.schemeTableau10[1]);
  });

  it('persists assignments across a fresh service instance', () => {
    const first = service.getColorMap(category, ['a', 'b']);

    const fresh = new ChartColorService();
    const reloaded = fresh.getColorMap(category, ['b', 'a']);

    expect(reloaded['a']).toBe(first['a']);
    expect(reloaded['b']).toBe(first['b']);
  });

  it('clear() forgets a category so colors are reassigned', () => {
    const before = service.getColorMap(category, ['a', 'b']);
    service.clear(category);
    // Reassign in a different order; 'b' should now take the first palette slot.
    const after = service.getColorMap(category, ['b', 'a']);

    expect(after['b']).toBe(d3.schemeTableau10[0]);
    expect(after['a']).toBe(d3.schemeTableau10[1]);
    expect(after['b']).not.toBe(before['b']);
  });

  it('still returns colors when localStorage writes throw', () => {
    spyOn(window.localStorage, 'setItem').and.throwError('quota exceeded');

    let map: Record<string, string> | undefined;
    expect(() => { map = service.getColorMap(category, ['a', 'b']); }).not.toThrow();
    expect(map!['a']).toBe(d3.schemeTableau10[0]);
    expect(map!['b']).toBe(d3.schemeTableau10[1]);
  });

  it('falls back to fresh assignment when localStorage reads throw', () => {
    spyOn(window.localStorage, 'getItem').and.throwError('read failure');

    let map: Record<string, string> | undefined;
    expect(() => { map = service.getColorMap(category, ['a']); }).not.toThrow();
    expect(map!['a']).toBe(d3.schemeTableau10[0]);
  });
});
