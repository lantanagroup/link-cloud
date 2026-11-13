import {describe, expect, it} from 'vitest';
import {buildHoursMinutesDuration, parseHoursMinutesDuration} from './duration';

describe('buildHoursMinutesDuration', () => {
  it('formats hours and minutes as PT{h}H{m}M', () => {
    expect(buildHoursMinutesDuration(2, 30)).toBe('PT2H30M');
  });

  it('normalizes minutes overflow into hours', () => {
    expect(buildHoursMinutesDuration(1, 90)).toBe('PT2H30M');
  });

  it('clamps negative inputs to zero', () => {
    expect(buildHoursMinutesDuration(-1, -5)).toBe('PT0H0M');
  });
});

describe('parseHoursMinutesDuration', () => {
  it('parses a well-formed duration', () => {
    expect(parseHoursMinutesDuration('PT2H30M')).toEqual({hours: 2, minutes: 30});
  });

  it('returns undefined for an undefined input', () => {
    expect(parseHoursMinutesDuration(undefined)).toBeUndefined();
  });

  it('returns undefined for a malformed duration', () => {
    expect(parseHoursMinutesDuration('P1DT2H30M')).toBeUndefined();
  });

  it('round-trips through build', () => {
    const duration = buildHoursMinutesDuration(4, 15);
    expect(parseHoursMinutesDuration(duration)).toEqual({hours: 4, minutes: 15});
  });
});
