import * as XLSX from 'xlsx';

/** Excel worksheet tab names are capped at 31 characters. */
const MAX_SHEET_NAME_LENGTH = 31;

export interface XlsxSheet {
  name: string;
  headers: string[];
  rows: Array<Array<string | number>>;
}

/**
 * Builds a single .xlsx workbook out of one or more sheets, using the `xlsx` package already
 * bundled for CensusStep's export. Reused for the Query Plan and Acquisition Log modals' own
 * "Export to Excel" buttons (one sheet) and for "Export Report Summary" (several sheets in one
 * workbook, in place of the onboarding POC's zip of separate .xlsx files).
 */
export function buildXlsxBlob(sheets: XlsxSheet[]): Blob {
  const workbook = XLSX.utils.book_new();
  sheets.forEach(sheet => {
    const worksheet = XLSX.utils.aoa_to_sheet([sheet.headers, ...sheet.rows]);
    XLSX.utils.book_append_sheet(workbook, worksheet, sheet.name.slice(0, MAX_SHEET_NAME_LENGTH));
  });
  const content = XLSX.write(workbook, {bookType: 'xlsx', type: 'array'});
  return new Blob([content], {type: 'application/vnd.openxmlformats-officedocument.spreadsheetml.sheet'});
}

export function downloadBlob(blob: Blob, filename: string): void {
  const url = URL.createObjectURL(blob);
  const link = document.createElement('a');
  link.href = url;
  link.download = filename;
  link.click();
  URL.revokeObjectURL(url);
}
