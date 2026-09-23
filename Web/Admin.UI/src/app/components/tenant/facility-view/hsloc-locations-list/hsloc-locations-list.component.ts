import {Component, computed, Input, OnChanges, OnDestroy, signal} from '@angular/core';
import {MatTree, MatTreeModule} from '@angular/material/tree';
import {MatButtonModule} from '@angular/material/button';
import {MatIconModule} from '@angular/material/icon';
import {MatTooltipModule} from '@angular/material/tooltip';
import {MatProgressBarModule} from '@angular/material/progress-bar';
import {Subscription} from 'rxjs';
import {FacilityLocation, FacilityLocationsService} from '../../../../services/gateway/normalization/facility-locations.service';

export interface LocationNode extends FacilityLocation {
  children: LocationNode[];
  mapped: boolean;
}

export function buildLocationTree(locations: FacilityLocation[]): LocationNode[] {
  const nodes = new Map(locations.map(location => [location.locationId, {
    ...location, children: [], mapped: location.mappings.some(mapping => mapping.hslocId != null)
  } as LocationNode]));
  const parents = new Map<string, string>();
  for (const node of nodes.values()) {
    if (node.partOfId && nodes.has(node.partOfId)) parents.set(node.locationId, node.partOfId);
  }
  const visited = new Set<string>();
  for (const node of nodes.values()) {
    const path = new Set<string>();
    let current: string | undefined = node.locationId;
    while (current !== undefined && !visited.has(current)) {
      if (path.has(current)) {
        parents.delete(current);
        break;
      }
      path.add(current);
      current = parents.get(current);
    }
    path.forEach(locationId => visited.add(locationId));
  }
  const roots: LocationNode[] = [];
  for (const node of nodes.values()) {
    const parentId = parents.get(node.locationId);
    if (parentId !== undefined) nodes.get(parentId)!.children.push(node);
    else roots.push(node);
  }
  return roots;
}

@Component({
  selector: 'app-hsloc-locations-list',
  templateUrl: './hsloc-locations-list.component.html',
  styleUrls: ['./hsloc-locations-list.component.scss'],
  imports: [MatTreeModule, MatButtonModule, MatIconModule, MatTooltipModule, MatProgressBarModule]
})
export class HslocLocationsListComponent implements OnChanges, OnDestroy {
  @Input() facilityId: string = '';
  readonly nodes = signal<LocationNode[]>([]);
  readonly loading = signal(false);
  readonly failed = signal(false);
  readonly selected = signal<LocationNode | null>(null);
  readonly childrenAccessor = (node: LocationNode) => node.children;
  readonly branches = computed(() => {
    const pending = [...this.nodes()];
    const branches: LocationNode[] = [];
    while (pending.length) {
      const node = pending.pop()!;
      if (node.children.length) {
        branches.push(node);
        pending.push(...node.children);
      }
    }
    return branches;
  });
  private request?: Subscription;

  constructor(private service: FacilityLocationsService) {}

  allExpanded(tree: MatTree<LocationNode>): boolean {
    return this.branches().length > 0 && this.branches().every(node => tree.isExpanded(node));
  }

  toggleAll(tree: MatTree<LocationNode>): void {
    if (this.allExpanded(tree)) tree.collapseAll();
    else this.branches().forEach(node => tree.expand(node));
  }

  ngOnChanges(): void {
    this.load();
  }

  ngOnDestroy(): void {
    this.request?.unsubscribe();
  }

  load(): void {
    this.request?.unsubscribe();
    this.nodes.set([]);
    this.selected.set(null);
    this.failed.set(false);
    this.loading.set(false);
    if (!this.facilityId.trim()) return;
    this.loading.set(true);
    this.request = this.service.getForFacility(this.facilityId).subscribe({
      next: response => {
        this.nodes.set(buildLocationTree(response.records));
        this.loading.set(false);
      },
      error: () => {
        this.failed.set(true);
        this.loading.set(false);
      }
    });
  }
}