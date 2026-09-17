import React, {useMemo, useState} from 'react';
import {useQuery} from '@tanstack/react-query';
import {useTranslation} from 'react-i18next';
import {useApiClient} from '../../../api/ApiClientContext';
import type {PreQualIssue, ReportingStatus} from '../../../api/contracts';
import {Button, MessageContainer, Modal, NHSNLoadingIndicator} from '../../../fields';
import {useNotifications} from '../../../notifications/NotificationProvider';

interface CategoryCount {
  title: string;
  count: number;
  acceptable: boolean;
}

// Matches the POC's preQualCategoryCounts(): one bar per category, in first-seen order (not
// grouped acceptable-first), so the chart reads the same left-to-right as the issue pool that
// produced it.
function categoryCounts(issues: PreQualIssue[]): CategoryCount[] {
  const byTitle = new Map<string, CategoryCount>();
  const order: string[] = [];
  issues.forEach(issue => {
    const title = issue.category.title;
    const existing = byTitle.get(title);
    if (existing) {
      existing.count += 1;
    } else {
      byTitle.set(title, {title, count: 1, acceptable: issue.category.acceptable});
      order.push(title);
    }
  });
  return order.map(title => byTitle.get(title)!);
}

// Colors match the POC's preQualCategoryCounts() exactly (counts[t].acceptable ? '#15803d' : '#c62828').
const CHART_ACCEPTABLE_COLOR = '#15803d';
const CHART_UNACCEPTABLE_COLOR = '#c62828';

/**
 * Reproduces the POC's buildBarChartSvg() layout math exactly (padding, gap, bar width cap,
 * niceMax formula) -- including groupX, which centers the bar group within the plot width rather
 * than left-anchoring it, so a handful of bars sit in the middle of the chart instead of hugging
 * the y-axis with empty space to the right.
 */
function IssuesSummaryChart({counts}: {counts: CategoryCount[]}) {
  const width = 1180;
  const height = 250;
  const padLeft = 40;
  const padTop = 16;
  const padRight = 16;
  const padBottom = 82;
  const plotWidth = width - padLeft - padRight;
  const plotHeight = height - padTop - padBottom;

  const maxValue = counts.reduce((max, entry) => Math.max(max, entry.count), 0) || 1;
  const niceMax = Math.max(4, Math.ceil(maxValue * 1.15));

  const n = counts.length;
  const gap = 18;
  const barWidth = Math.min(120, (plotWidth - gap * (n - 1)) / n);
  const groupWidth = barWidth * n + gap * (n - 1);
  const groupX = padLeft + Math.max(0, (plotWidth - groupWidth) / 2);

  const gridlineCount = 4;
  const gridlines = Array.from({length: gridlineCount + 1}, (_, t) => Math.round((niceMax * t) / gridlineCount));

  // A little viewBox margin left/below -- the first/last bar's rotated label can still pivot past
  // x=0 or below the plot when the group isn't centered enough to clear it (e.g. a full-width
  // group), which a plain 0,0..width,height viewBox would otherwise clip.
  return (
    <svg
      viewBox={`-20 0 ${width + 40} ${height + 20}`}
      className="nhsn-link__report-results-timeline-svg"
      role="img"
      aria-hidden="true">
      {gridlines.map(value => {
        const y = padTop + plotHeight - (value / niceMax) * plotHeight;
        return (
          <g key={value}>
            <line x1={padLeft} y1={y} x2={padLeft + plotWidth} y2={y} stroke="#e2e8f0" strokeWidth={1} />
            <text x={padLeft - 8} y={y + 4} textAnchor="end" fontSize="11" fill="#64748b">
              {value}
            </text>
          </g>
        );
      })}
      <line x1={padLeft} y1={padTop + plotHeight} x2={padLeft + plotWidth} y2={padTop + plotHeight} stroke="#cbd5e1" strokeWidth={1} />
      {counts.map((entry, index) => {
        const barHeight = (entry.count / niceMax) * plotHeight;
        const x = groupX + index * (barWidth + gap);
        const y = padTop + plotHeight - barHeight;
        const labelX = x + barWidth / 2;
        const labelY = padTop + plotHeight + 14;
        return (
          <g key={entry.title}>
            <rect x={x} y={y} width={barWidth} height={barHeight} rx={4} fill={entry.acceptable ? CHART_ACCEPTABLE_COLOR : CHART_UNACCEPTABLE_COLOR} />
            <text x={labelX} y={y - 6} textAnchor="middle" fontSize="12" fontWeight={700} fill="#1e293b">
              {entry.count}
            </text>
            <text x={labelX} y={labelY} textAnchor="end" fontSize="11" fill="#334155" transform={`rotate(-35 ${labelX},${labelY})`}>
              {entry.title.length > 20 ? `${entry.title.slice(0, 20)}…` : entry.title}
            </text>
          </g>
        );
      })}
    </svg>
  );
}

interface CategorySummary {
  title: string;
  count: number;
  guidance: string;
}

function summarizeByCategory(issues: PreQualIssue[], acceptable: boolean): CategorySummary[] {
  const byTitle = new Map<string, CategorySummary>();
  issues
    .filter(issue => issue.category.acceptable === acceptable)
    .forEach(issue => {
      const existing = byTitle.get(issue.category.title);
      if (existing) {
        existing.count += 1;
      } else {
        byTitle.set(issue.category.title, {title: issue.category.title, count: 1, guidance: issue.category.guidance});
      }
    });
  return Array.from(byTitle.values());
}

type TFunc = ReturnType<typeof useTranslation>['t'];

function PreQualCategoryTable({entries, onSelect, t}: {entries: CategorySummary[]; onSelect: (title: string) => void; t: TFunc}) {
  if (entries.length === 0) {
    return <p>{t('onboarding:reportResults.detail.preQual.noIssues')}</p>;
  }
  return (
    <div className="nhsn-link__report-results-table-scroll">
      {/* --fixed + colgroup: an auto-layout table resists shrinking below its unwrapped content
          width, which is wide enough here to force the whole (flex-shrunk) modal into horizontal
          overflow -- the same reason every other report-results table already uses this pair. */}
      <table className="nhsn-link__report-results-table nhsn-link__report-results-table--fixed">
        <colgroup>
          <col style={{width: '28%'}} />
          <col style={{width: '14%'}} />
          <col style={{width: '58%'}} />
        </colgroup>
        <thead>
          <tr>
            <th scope="col">{t('onboarding:reportResults.detail.preQual.category')}</th>
            <th scope="col">{t('onboarding:reportResults.detail.preQual.numberOfIssues')}</th>
            <th scope="col">{t('onboarding:reportResults.detail.preQual.guidance')}</th>
          </tr>
        </thead>
        <tbody>
          {entries.map(entry => (
            <tr key={entry.title}>
              <td>
                <button type="button" className="nhsn-link__report-results-link" onClick={() => onSelect(entry.title)}>
                  {entry.title}
                </button>
              </td>
              <td>{entry.count}</td>
              <td>{entry.guidance}</td>
            </tr>
          ))}
        </tbody>
      </table>
    </div>
  );
}

type XlsxCell = string | number;

interface XlsxSection {
  title: string;
  headers: XlsxCell[];
  rows: XlsxCell[][];
}

/**
 * A minimal .xlsx writer -- ported directly from the onboarding POC's own zipStore() /
 * buildSectionedWorksheetXml() (index.html), not a library. The `xlsx` package this app already
 * depends on elsewhere (CensusStep) is the SheetJS Community Edition, which silently drops cell
 * styling on write (verified: a `.s` style assignment produces no `s="..."` attribute and a
 * single default font in styles.xml) -- real bold/colored section headers need either a paid
 * SheetJS tier or hand-written OOXML. The POC chose the latter specifically to avoid a paid
 * dependency, so this ports that same approach rather than adding one now.
 */

// ---------- minimal ZIP (store method, no compression) ----------
const CRC32_TABLE = (() => {
  const table: number[] = [];
  for (let n = 0; n < 256; n++) {
    let c = n;
    for (let k = 0; k < 8; k++) {
      c = c & 1 ? 0xedb88320 ^ (c >>> 1) : c >>> 1;
    }
    table[n] = c >>> 0;
  }
  return table;
})();

function crc32(bytes: Uint8Array): number {
  let crc = 0 ^ -1;
  for (let i = 0; i < bytes.length; i++) {
    crc = (crc >>> 8) ^ CRC32_TABLE[(crc ^ bytes[i]) & 0xff];
  }
  return (crc ^ -1) >>> 0;
}

interface ZipFile {
  name: string;
  data: string;
}

function zipStore(files: ZipFile[]): Blob {
  const encoder = new TextEncoder();
  const localParts: Uint8Array[] = [];
  const centralParts: Uint8Array[] = [];
  let offset = 0;

  files.forEach(file => {
    const nameBytes = encoder.encode(file.name);
    const dataBytes = encoder.encode(file.data);
    const crc = crc32(dataBytes);

    const local = new Uint8Array(30 + nameBytes.length);
    const ldv = new DataView(local.buffer);
    ldv.setUint32(0, 0x04034b50, true);
    ldv.setUint16(4, 20, true);
    ldv.setUint16(6, 0, true);
    ldv.setUint16(8, 0, true);
    ldv.setUint16(10, 0, true);
    ldv.setUint16(12, 0x21, true);
    ldv.setUint32(14, crc, true);
    ldv.setUint32(18, dataBytes.length, true);
    ldv.setUint32(22, dataBytes.length, true);
    ldv.setUint16(26, nameBytes.length, true);
    ldv.setUint16(28, 0, true);
    local.set(nameBytes, 30);
    localParts.push(local, dataBytes);

    const central = new Uint8Array(46 + nameBytes.length);
    const cdv = new DataView(central.buffer);
    cdv.setUint32(0, 0x02014b50, true);
    cdv.setUint16(4, 20, true);
    cdv.setUint16(6, 20, true);
    cdv.setUint16(8, 0, true);
    cdv.setUint16(10, 0, true);
    cdv.setUint16(12, 0, true);
    cdv.setUint16(14, 0x21, true);
    cdv.setUint32(16, crc, true);
    cdv.setUint32(20, dataBytes.length, true);
    cdv.setUint32(24, dataBytes.length, true);
    cdv.setUint16(28, nameBytes.length, true);
    cdv.setUint16(30, 0, true);
    cdv.setUint16(32, 0, true);
    cdv.setUint16(34, 0, true);
    cdv.setUint16(36, 0, true);
    cdv.setUint32(38, 0, true);
    cdv.setUint32(42, offset, true);
    central.set(nameBytes, 46);
    centralParts.push(central);

    offset += local.length + dataBytes.length;
  });

  const centralStart = offset;
  const centralSize = centralParts.reduce((sum, part) => sum + part.length, 0);

  const eocd = new Uint8Array(22);
  const edv = new DataView(eocd.buffer);
  edv.setUint32(0, 0x06054b50, true);
  edv.setUint16(4, 0, true);
  edv.setUint16(6, 0, true);
  edv.setUint16(8, files.length, true);
  edv.setUint16(10, files.length, true);
  edv.setUint32(12, centralSize, true);
  edv.setUint32(16, centralStart, true);
  edv.setUint16(20, 0, true);

  return new Blob(
    [...localParts, ...centralParts, eocd] as BlobPart[],
    {type: 'application/vnd.openxmlformats-officedocument.spreadsheetml.sheet'}
  );
}

// ---------- minimal OOXML parts ----------
function xmlEscape(value: string): string {
  return value.replace(/[&<>"']/g, char => ({'&': '&amp;', '<': '&lt;', '>': '&gt;', '"': '&quot;', "'": '&apos;'})[char]!);
}

function colLetter(idx: number): string {
  let s = '';
  let n = idx + 1;
  while (n > 0) {
    const rem = (n - 1) % 26;
    s = String.fromCharCode(65 + rem) + s;
    n = Math.floor((n - 1) / 26);
  }
  return s;
}

const CONTENT_TYPES_XML =
  '<?xml version="1.0" encoding="UTF-8" standalone="yes"?>' +
  '<Types xmlns="http://schemas.openxmlformats.org/package/2006/content-types">' +
  '<Default Extension="rels" ContentType="application/vnd.openxmlformats-package.relationships+xml"/>' +
  '<Default Extension="xml" ContentType="application/xml"/>' +
  '<Override PartName="/xl/workbook.xml" ContentType="application/vnd.openxmlformats-officedocument.spreadsheetml.sheet.main+xml"/>' +
  '<Override PartName="/xl/styles.xml" ContentType="application/vnd.openxmlformats-officedocument.spreadsheetml.styles+xml"/>' +
  '<Override PartName="/xl/worksheets/sheet1.xml" ContentType="application/vnd.openxmlformats-officedocument.spreadsheetml.worksheet+xml"/>' +
  '</Types>';

const RELS_XML =
  '<?xml version="1.0" encoding="UTF-8" standalone="yes"?>' +
  '<Relationships xmlns="http://schemas.openxmlformats.org/package/2006/relationships">' +
  '<Relationship Id="rId1" Type="http://schemas.openxmlformats.org/officeDocument/2006/relationships/officeDocument" Target="xl/workbook.xml"/>' +
  '</Relationships>';

function buildWorkbookXml(sheetName: string): string {
  return (
    '<?xml version="1.0" encoding="UTF-8" standalone="yes"?>' +
    '<workbook xmlns="http://schemas.openxmlformats.org/spreadsheetml/2006/main" xmlns:r="http://schemas.openxmlformats.org/officeDocument/2006/relationships">' +
    `<sheets><sheet name="${xmlEscape(sheetName)}" sheetId="1" r:id="rId1"/></sheets>` +
    '</workbook>'
  );
}

const WORKBOOK_RELS_XML =
  '<?xml version="1.0" encoding="UTF-8" standalone="yes"?>' +
  '<Relationships xmlns="http://schemas.openxmlformats.org/package/2006/relationships">' +
  '<Relationship Id="rId1" Type="http://schemas.openxmlformats.org/officeDocument/2006/relationships/worksheet" Target="worksheets/sheet1.xml"/>' +
  '<Relationship Id="rId2" Type="http://schemas.openxmlformats.org/officeDocument/2006/relationships/styles" Target="styles.xml"/>' +
  '</Relationships>';

// Font 1 (section headers): bold, blue, matches the POC's own STYLES_XML exactly (#0B5CAB).
// Font 2 (column headers): bold, default color.
const STYLES_XML =
  '<?xml version="1.0" encoding="UTF-8" standalone="yes"?>' +
  '<styleSheet xmlns="http://schemas.openxmlformats.org/spreadsheetml/2006/main">' +
  '<fonts count="3">' +
  '<font><sz val="11"/><name val="Calibri"/></font>' +
  '<font><b/><sz val="12"/><color rgb="FF0B5CAB"/><name val="Calibri"/></font>' +
  '<font><b/><sz val="11"/><name val="Calibri"/></font>' +
  '</fonts>' +
  '<fills count="1"><fill><patternFill patternType="none"/></fill></fills>' +
  '<borders count="1"><border/></borders>' +
  '<cellStyleXfs count="1"><xf numFmtId="0" fontId="0"/></cellStyleXfs>' +
  '<cellXfs count="3">' +
  '<xf numFmtId="0" fontId="0" xfId="0"/>' +
  '<xf numFmtId="0" fontId="1" xfId="0" applyFont="1"/>' +
  '<xf numFmtId="0" fontId="2" xfId="0" applyFont="1"/>' +
  '</cellXfs>' +
  '</styleSheet>';

const STYLE_SECTION_HEADER = 1;
const STYLE_COLUMN_HEADER = 2;

// Matches the POC's buildSectionedWorksheetXml(): title lines (line 0 styled as a section
// header), a blank row, then each section as [styled title] [styled header row] [data rows, or a
// single "(none)" row when empty], with a blank row after every section.
function buildSectionedWorksheetXml(titleLines: string[], sections: XlsxSection[]): string {
  function cell(ref: string, text: XlsxCell, styleIdx?: number): string {
    const s = styleIdx ? ` s="${styleIdx}"` : '';
    return `<c r="${ref}"${s} t="inlineStr"><is><t xml:space="preserve">${xmlEscape(String(text))}</t></is></c>`;
  }

  const rowsXml: string[] = [];
  let r = 1;
  let maxCols = 1;

  titleLines.forEach((line, i) => {
    rowsXml.push(`<row r="${r}">${cell(`A${r}`, line, i === 0 ? STYLE_SECTION_HEADER : undefined)}</row>`);
    r++;
  });
  r++;

  sections.forEach(section => {
    rowsXml.push(`<row r="${r}">${cell(`A${r}`, section.title, STYLE_SECTION_HEADER)}</row>`);
    r++;

    rowsXml.push(`<row r="${r}">${section.headers.map((h, i) => cell(colLetter(i) + r, h, STYLE_COLUMN_HEADER)).join('')}</row>`);
    r++;

    if (section.rows.length === 0) {
      rowsXml.push(`<row r="${r}">${cell(`A${r}`, '(none)')}</row>`);
      r++;
    } else {
      section.rows.forEach(dataRow => {
        rowsXml.push(`<row r="${r}">${dataRow.map((value, i) => cell(colLetter(i) + r, value)).join('')}</row>`);
        r++;
      });
    }
    maxCols = Math.max(maxCols, section.headers.length);
    r++;
  });

  const lastCol = colLetter(maxCols - 1);
  let colsXml = '';
  for (let c = 0; c < maxCols; c++) {
    colsXml += `<col min="${c + 1}" max="${c + 1}" width="32" customWidth="1"/>`;
  }

  return (
    '<?xml version="1.0" encoding="UTF-8" standalone="yes"?>' +
    '<worksheet xmlns="http://schemas.openxmlformats.org/spreadsheetml/2006/main">' +
    `<dimension ref="A1:${lastCol}${r - 1}"/>` +
    `<cols>${colsXml}</cols>` +
    `<sheetData>${rowsXml.join('')}</sheetData>` +
    '</worksheet>'
  );
}

function buildSectionedXlsx(sheetName: string, titleLines: string[], sections: XlsxSection[]): Blob {
  const worksheetXml = buildSectionedWorksheetXml(titleLines, sections);
  return zipStore([
    {name: '[Content_Types].xml', data: CONTENT_TYPES_XML},
    {name: '_rels/.rels', data: RELS_XML},
    {name: 'xl/workbook.xml', data: buildWorkbookXml(sheetName)},
    {name: 'xl/_rels/workbook.xml.rels', data: WORKBOOK_RELS_XML},
    {name: 'xl/styles.xml', data: STYLES_XML},
    {name: 'xl/worksheets/sheet1.xml', data: worksheetXml}
  ]);
}

// Matches the onboarding POC's buildPreQualResultsXlsx(): a single "Pre-Qual Results" sheet with
// every section (Issues Summary, Unacceptable/Acceptable Categories, All Issues) stacked in one
// column, section/title rows bold-and-blue and header rows bold, exactly like the POC.
function buildPreQualXlsxBlob(
  patientId: string,
  measureName: string,
  reportId: string,
  chartCounts: CategoryCount[],
  unacceptableSummary: CategorySummary[],
  acceptableSummary: CategorySummary[],
  issues: PreQualIssue[]
): Blob {
  const titleLines = ['NHSN Link - Pre-Qual Validation Results', `Patient: ${patientId}`, `Measure: ${measureName}`, `Report Id: ${reportId}`];

  const sections: XlsxSection[] = [
    {title: 'Issues Summary', headers: ['Title', 'Number of Issues'], rows: chartCounts.map(entry => [entry.title, entry.count])},
    {
      title: 'Unacceptable Categories',
      headers: ['Category', 'Number of Issues', 'Guidance'],
      rows: unacceptableSummary.map(entry => [entry.title, entry.count, entry.guidance])
    },
    {
      title: 'Acceptable Categories',
      headers: ['Category', 'Number of Issues', 'Guidance'],
      rows: acceptableSummary.map(entry => [entry.title, entry.count, entry.guidance])
    },
    {
      title: 'All Issues',
      headers: ['Category', 'Message', 'Expression', 'Location'],
      rows: issues.map(issue => [issue.category.title, issue.message, issue.expression, issue.location])
    }
  ];

  return buildSectionedXlsx('Pre-Qual Results', titleLines, sections);
}

function DownloadIcon() {
  return (
    <svg width="14" height="14" viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2" strokeLinecap="round" strokeLinejoin="round" aria-hidden="true">
      <path d="M12 3v12" />
      <path d="M7 10l5 5 5-5" />
      <path d="M5 21h14" />
    </svg>
  );
}

export interface PreQualResultsModalProps {
  open: boolean;
  onClose: () => void;
  patientId: string;
  measureName?: string;
  reportingStatus: ReportingStatus;
  reportId: string;
}

export function PreQualResultsModal({open, onClose, patientId, measureName, reportingStatus, reportId}: PreQualResultsModalProps) {
  const {t} = useTranslation(['onboarding', 'common']);
  const api = useApiClient();
  const {notifyError} = useNotifications();
  const [selectedCategory, setSelectedCategory] = useState<string | null>(null);

  const {
    data: issues = [],
    isLoading,
    error: queryError
  } = useQuery({
    queryKey: ['preQualResults', reportId, patientId],
    queryFn: () => api.getPatientPreQualResults(reportId, patientId),
    enabled: open
  });
  const loadError = queryError
    ? queryError instanceof Error
      ? queryError.message
      : t('onboarding:reportResults.messages.loadError')
    : null;

  const chartCounts = useMemo(() => categoryCounts(issues), [issues]);
  const unacceptableSummary = useMemo(() => summarizeByCategory(issues, false), [issues]);
  const acceptableSummary = useMemo(() => summarizeByCategory(issues, true), [issues]);
  const showUnacceptableSection = reportingStatus !== 'PassedValidation';

  function handleClose() {
    setSelectedCategory(null);
    onClose();
  }

  function handleDownload() {
    try {
      const blob = buildPreQualXlsxBlob(patientId, measureName ?? '', reportId, chartCounts, unacceptableSummary, acceptableSummary, issues);
      const url = URL.createObjectURL(blob);
      const link = document.createElement('a');
      link.href = url;
      link.download = `${patientId}_${(measureName ?? 'measure').replace(/[^a-z0-9]+/gi, '_')}_PreQual_Results.xlsx`;
      link.click();
      URL.revokeObjectURL(url);
    } catch (cause) {
      notifyError(cause instanceof Error ? cause.message : t('onboarding:reportResults.detail.downloadUnavailable'));
    }
  }

  if (selectedCategory) {
    const detailRows = issues.filter(issue => issue.category.title === selectedCategory);
    return (
      <Modal
        open={open}
        title={selectedCategory}
        onClose={handleClose}
        size="xlarge"
        footer={
          <Button variant="secondary" onClick={() => setSelectedCategory(null)}>
            {t('onboarding:reportResults.detail.preQual.back')}
          </Button>
        }>
        <p className="nhsn-link__subtitle">
          {t('onboarding:reportResults.detail.preQual.detailSubtitle', {count: detailRows.length, patientId, measure: measureName ?? ''})}
        </p>
        <div className="nhsn-link__report-results-table-scroll">
          <table className="nhsn-link__report-results-table nhsn-link__report-results-table--fixed">
            <colgroup>
              <col style={{width: '40%'}} />
              <col style={{width: '40%'}} />
              <col style={{width: '20%'}} />
            </colgroup>
            <thead>
              <tr>
                <th scope="col">{t('onboarding:reportResults.detail.preQual.message')}</th>
                <th scope="col">{t('onboarding:reportResults.detail.preQual.expression')}</th>
                <th scope="col">{t('onboarding:reportResults.detail.preQual.location')}</th>
              </tr>
            </thead>
            <tbody>
              {detailRows.map((row, index) => (
                <tr key={index}>
                  <td>{row.message}</td>
                  <td style={{wordBreak: 'break-all'}}>{row.expression}</td>
                  <td>{row.location}</td>
                </tr>
              ))}
            </tbody>
          </table>
        </div>
      </Modal>
    );
  }

  return (
    <Modal
      open={open}
      title={t('onboarding:reportResults.detail.preQual.title')}
      onClose={handleClose}
      size="xlarge"
      footer={
        <Button variant="secondary" onClick={handleClose}>
          {t('common:actions.close')}
        </Button>
      }>
      <p className="nhsn-link__subtitle">{t('onboarding:reportResults.detail.preQual.subtitle', {patientId, measure: measureName ?? ''})}</p>

      {isLoading && <NHSNLoadingIndicator />}
      {!isLoading && loadError && (
        <MessageContainer type="error" showIcon>
          <span role="alert">{loadError}</span>
        </MessageContainer>
      )}

      {!isLoading && !loadError && (
        <>
          <h3 className="nhsn-link__report-results-detail-section-title">{t('onboarding:reportResults.detail.preQual.issuesSummary')}</h3>
          {chartCounts.length > 0 ? (
            <IssuesSummaryChart counts={chartCounts} />
          ) : (
            <p>{t('onboarding:reportResults.detail.preQual.noIssues')}</p>
          )}

          {showUnacceptableSection && (
            <>
              <h3 className="nhsn-link__report-results-detail-section-title">
                {t('onboarding:reportResults.detail.preQual.unacceptableCategories')}
              </h3>
              <PreQualCategoryTable entries={unacceptableSummary} onSelect={setSelectedCategory} t={t} />
            </>
          )}

          <h3 className="nhsn-link__report-results-detail-section-title">{t('onboarding:reportResults.detail.preQual.acceptableCategories')}</h3>
          <PreQualCategoryTable entries={acceptableSummary} onSelect={setSelectedCategory} t={t} />
        </>
      )}

      <div className="nhsn-link__report-results-patient-detail-actions">
        <Button variant="secondary" onClick={handleDownload} disabled={isLoading || Boolean(loadError)}>
          <DownloadIcon />
          {t('onboarding:reportResults.detail.preQual.downloadResults')}
        </Button>
      </div>
    </Modal>
  );
}

export default PreQualResultsModal;
