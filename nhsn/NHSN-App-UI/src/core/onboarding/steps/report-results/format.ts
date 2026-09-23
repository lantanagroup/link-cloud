// toLocaleDateString()/toLocaleString() format by the runtime's own locale, so the same report
// renders a different date shape depending on which machine or browser opened the page. These
// build the string by hand instead, so the format is fixed everywhere and Create Date reads as
// the same YYYY-MM-DD style as Start/End Date, just with a time appended.
export function formatDate(iso: string): string {
  const parsed = new Date(iso);
  if (Number.isNaN(parsed.getTime())) {
    return iso;
  }
  const year = parsed.getFullYear();
  const month = String(parsed.getMonth() + 1).padStart(2, '0');
  const day = String(parsed.getDate()).padStart(2, '0');
  return `${year}-${month}-${day}`;
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
