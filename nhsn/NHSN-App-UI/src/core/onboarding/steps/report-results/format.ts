// toLocaleDateString()/toLocaleString() format by the runtime's own locale, so the same report
// renders a different date shape depending on which machine or browser opened the page. These
// build the string by hand instead, so the format is fixed everywhere: MM-DD-YYYY, the same as the
// Generate Report date pickers, with Create Date just appending a time.

// A bare calendar date ("2026-09-01", how Start/End Date arrive). `new Date()` reads that as UTC
// midnight, which renders as the previous day anywhere west of UTC - so it's taken as-is instead.
const DATE_ONLY = /^(\d{4})-(\d{2})-(\d{2})$/;

export function formatDate(iso: string): string {
  const dateOnly = iso.match(DATE_ONLY);
  if (dateOnly) {
    return `${dateOnly[2]}-${dateOnly[3]}-${dateOnly[1]}`;
  }
  const parsed = new Date(iso);
  if (Number.isNaN(parsed.getTime())) {
    return iso;
  }
  const year = parsed.getFullYear();
  const month = String(parsed.getMonth() + 1).padStart(2, '0');
  const day = String(parsed.getDate()).padStart(2, '0');
  return `${month}-${day}-${year}`;
}

export function formatDateTime(iso: string): string {
  const parsed = new Date(iso);
  if (Number.isNaN(parsed.getTime())) {
    return iso;
  }
  const hours = String(parsed.getHours()).padStart(2, '0');
  const minutes = String(parsed.getMinutes()).padStart(2, '0');
  const seconds = String(parsed.getSeconds()).padStart(2, '0');
  return `${formatDate(iso)}, ${hours}:${minutes}:${seconds}`;
}
