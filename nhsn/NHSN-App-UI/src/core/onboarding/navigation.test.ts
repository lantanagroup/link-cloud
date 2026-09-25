import {describe, expect, it} from 'vitest';
import {buildStepPath, parseStepPath} from './navigation';

/**
 * Embedded, popstate also fires for the host's own navigation, so "is this
 * path ours?" has to be answered precisely — a false positive moves the
 * wizard when the NHSN App changes its own route.
 */
describe('parseStepPath', () => {
  it('parses a step under the mounted base', () => {
    expect(parseStepPath('/nhsnlink/onboarding/fhir', '/nhsnlink')).toEqual({stepId: 'fhir'});
  });

  it('parses a sub-view with its id', () => {
    expect(parseStepPath('/nhsnlink/onboarding/report-results/detail/R-1', '/nhsnlink')).toEqual({
      stepId: 'report-results',
      view: {stepId: 'report-results', view: 'detail', params: {id: 'R-1'}}
    });
  });

  it('returns undefined for a host path outside our base', () => {
    expect(parseStepPath('/patient-safety/dashboard', '/nhsnlink')).toBeUndefined();
  });

  it('returns undefined for our base but not our route', () => {
    expect(parseStepPath('/nhsnlink/configuration', '/nhsnlink')).toBeUndefined();
  });

  it('returns undefined for an unknown step id rather than guessing', () => {
    expect(parseStepPath('/nhsnlink/onboarding/typo', '/nhsnlink')).toBeUndefined();
  });

  it('handles a root base', () => {
    expect(parseStepPath('/onboarding/welcome', '/')).toEqual({stepId: 'welcome'});
  });
});

describe('buildStepPath', () => {
  it('round-trips a step', () => {
    const path = buildStepPath({stepId: 'census'}, '/nhsnlink');
    expect(path).toBe('/nhsnlink/onboarding/census');
    expect(parseStepPath(path, '/nhsnlink')).toEqual({stepId: 'census'});
  });

  it('round-trips a sub-view', () => {
    const target = {
      stepId: 'report-results' as const,
      view: {stepId: 'report-results' as const, view: 'detail', params: {id: 'R 1'}}
    };
    const path = buildStepPath(target, '/nhsnlink');
    expect(parseStepPath(path, '/nhsnlink')).toEqual(target);
  });
});
