/**
 * ISO 8601 durations of the form `PT{h}H{m}M` — hours and minutes only, no
 * days or seconds. Used by the patients-of-interest acquisition frequency.
 */
export interface HoursMinutes {
  hours: number;
  minutes: number;
}

export function buildHoursMinutesDuration(hours: number, minutes: number): string {
  const totalMinutes = Math.max(0, hours) * 60 + Math.max(0, minutes);
  const normalizedHours = Math.floor(totalMinutes / 60);
  const normalizedMinutes = totalMinutes % 60;
  return `PT${normalizedHours}H${normalizedMinutes}M`;
}

export function parseHoursMinutesDuration(duration?: string): HoursMinutes | undefined {
  const match = duration?.match(/^PT(\d+)H(\d+)M$/);
  if (!match) {
    return undefined;
  }
  return {hours: Number(match[1]), minutes: Number(match[2])};
}
