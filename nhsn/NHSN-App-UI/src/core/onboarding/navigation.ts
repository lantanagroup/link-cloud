import type {StepTarget} from './types';
import {isStepId} from './types';

/**
 * URL mirroring for the step machine.
 *
 * There is no router. React Router assumes it owns the window URL; embedded,
 * the NHSN App owns it, may rewrite it at the gateway, and may run its own
 * router. We write our step to the URL so back/forward and reload hold the
 * user's place, and we read it back only through resolveStep().
 */

export function normalizeBaseUrl(baseUrl: string): string {
  if (!baseUrl || baseUrl === '/') {
    return '/';
  }
  const trimmed = baseUrl.endsWith('/') ? baseUrl.slice(0, -1) : baseUrl;
  return trimmed.startsWith('/') ? trimmed : `/${trimmed}`;
}

export function buildStepPath(target: StepTarget, baseUrl: string): string {
  const base = normalizeBaseUrl(baseUrl);
  const prefix = base === '/' ? '' : base;
  const segments = [prefix, 'onboarding', target.stepId];

  if (target.view) {
    segments.push(target.view.view);
    const id = target.view.params?.id;
    if (id) {
      segments.push(encodeURIComponent(id));
    }
  }

  return segments.join('/') || '/';
}

/**
 * Returns undefined when the path is not ours.
 *
 * Embedded, popstate also fires for the host's own navigation, so an
 * unparseable path must leave the wizard where it is rather than moving it.
 */
export function parseStepPath(pathname: string, baseUrl: string): StepTarget | undefined {
  const base = normalizeBaseUrl(baseUrl);
  let rest = pathname;

  if (base !== '/') {
    if (!pathname.startsWith(base)) {
      return undefined;
    }
    rest = pathname.slice(base.length);
  }

  const parts = rest.split('/').filter(Boolean);
  if (parts[0] !== 'onboarding') {
    return undefined;
  }

  const stepId = parts[1];
  if (!stepId || !isStepId(stepId)) {
    return undefined;
  }

  const view = parts[2];
  if (!view) {
    return {stepId};
  }

  const id = parts[3] ? decodeURIComponent(parts[3]) : undefined;
  return {
    stepId,
    view: {stepId, view, params: id ? {id} : undefined}
  };
}

export function sameTarget(a: StepTarget | undefined, b: StepTarget | undefined): boolean {
  if (!a || !b) {
    return a === b;
  }
  return (
    a.stepId === b.stepId &&
    a.view?.view === b.view?.view &&
    a.view?.params?.id === b.view?.params?.id
  );
}
