import { Injectable } from '@angular/core';
import * as d3 from 'd3';

/**
 * Assigns and persists a stable color per label within a named category, so
 * charts whose categories are dynamic (e.g. measure report types) keep the
 * same color across page refreshes and across different reports.
 *
 * Colors are drawn from d3.schemeTableau10 (the donut chart's default palette)
 * in first-seen order and stored in localStorage keyed by category. Labels
 * beyond the palette size wrap around.
 */
@Injectable({ providedIn: 'root' })
export class ChartColorService {
  private static readonly STORAGE_PREFIX = 'chart-colors:';
  private readonly palette: readonly string[] = d3.schemeTableau10;

  /**
   * Returns a label -> color map covering every label provided. Any label not
   * already assigned gets the next palette color and the category is persisted.
   */
  getColorMap(category: string, labels: string[]): Record<string, string> {
    const stored = this.load(category);
    let changed = false;

    for (const label of labels) {
      if (!stored[label]) {
        stored[label] = this.nextColor(stored);
        changed = true;
      }
    }

    if (changed) {
      this.save(category, stored);
    }

    return labels.reduce((acc, label) => {
      acc[label] = stored[label];
      return acc;
    }, {} as Record<string, string>);
  }

  /** Forget all color assignments for a category. */
  clear(category: string): void {
    try {
      window.localStorage.removeItem(this.storageKey(category));
    } catch { /* ignore storage failures */ }
  }

  private nextColor(stored: Record<string, string>): string {
    const index = Object.keys(stored).length;
    return this.palette[index % this.palette.length];
  }

  private load(category: string): Record<string, string> {
    try {
      const raw = window.localStorage.getItem(this.storageKey(category));
      return raw ? JSON.parse(raw) as Record<string, string> : {};
    } catch {
      return {};
    }
  }

  private save(category: string, map: Record<string, string>): void {
    try {
      window.localStorage.setItem(this.storageKey(category), JSON.stringify(map));
    } catch { /* ignore storage failures */ }
  }

  private storageKey(category: string): string {
    return `${ChartColorService.STORAGE_PREFIX}${category}`;
  }
}
