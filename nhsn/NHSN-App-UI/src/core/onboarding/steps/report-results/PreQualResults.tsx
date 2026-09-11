import React, {useMemo, useState} from 'react';
import {useTranslation} from 'react-i18next';
import type {ReportingStatus} from '../../../api/contracts';
import {Button, Modal} from '../../../fields';
import {useNotifications} from '../../../notifications/NotificationProvider';

/**
 * The onboarding POC's "Pre-Qual Validation Results" popup (PREQUAL_CATEGORIES /
 * PREQUAL_MESSAGES / PREQUAL_RESULTS_POOL in index.html), reproduced with a smaller hand-picked
 * issue pool. Report/Validation has no per-patient Pre-Qual result endpoint wired up to this BFF
 * yet -- ReportGateway.ToPatientEntry only ever approximates HasPreQualResults from
 * ReportingStatus -- so results here are deterministic, patient/measure-seeded placeholders built
 * to unblock this UI ahead of that integration. Categories and guidance text are taken as-is from
 * the POC's own fixture; delete this once Validation exposes real per-patient results.
 */
interface PreQualCategory {
  title: string;
  acceptable: boolean;
  guidance: string;
}

const PREQUAL_CATEGORIES: PreQualCategory[] = [
  {
    title: 'Unknown Extension',
    acceptable: true,
    guidance:
      'Internal: Systems are allowed to include extensions (additional data). Extensions that do not modify the meaning of the data (modifierExtensions) can be safely ignored. This is not a modifierExtension.'
  },
  {
    title: 'Unable to Validate Measure (Measure not found)',
    acceptable: false,
    guidance:
      'Internal: This appears to be an issue in the validation process and should be resolved within NHSNLink as it may be hiding other issues.'
  },
  {
    title: 'No Codes From an Extensible Binding ValueSet',
    acceptable: true,
    guidance:
      'External: The code provided is not part of the extensible ValueSet, which if it is a concept that is part of the measure, is a problem that needs to be resolved.'
  },
  {
    title: 'Additional Data Beyond NHSN Specification',
    acceptable: true,
    guidance: 'External: No impact to normal operation. Recommend reviewing during onboarding and initial testing.'
  },
  {
    title: 'Unknown Code System',
    acceptable: true,
    guidance:
      "Internal: This is an unrecognized code system and is only a concern if there is not another coding that provides a standard recognized coding Code System (which is categorized as 'Missing Standard [X] Code')"
  },
  {
    title: 'Invalid Code in Required ValueSet',
    acceptable: false,
    guidance: 'External: The code is not part of the required ValueSet. This may cause issues with measure calculation.'
  },
  {
    title: 'Unknown Local Code',
    acceptable: true,
    guidance: 'External: The code is not recognized to be part of the ValueSet.'
  },
  {
    title: 'FHIR Standard Recommendations and Best Practices',
    acceptable: true,
    guidance:
      'External: While it is encouraged to follow FHIR standards and best practices for interoperability, resolutions to these issues are not required for reporting to NHSN.'
  },
  {
    title: 'Does Not Match Preferred ValueSet',
    acceptable: true,
    guidance:
      'External: This could be indicative of a problem if the data element is part of the measure and would not enable the resource to be included in the measure calculation appropriately.'
  },
  {
    title: 'Missing Coverage Type',
    acceptable: true,
    guidance:
      'External: Coverage.class.type (when present), is required to exist. However, NHSN has deemed this missing concept acceptable to report to NHSN.'
  }
];

interface PreQualIssueTemplate {
  categoryIndex: number;
  message: string;
  expression: string;
  location: string;
}

const PREQUAL_ISSUE_POOL: PreQualIssueTemplate[] = [
  {
    categoryIndex: 0,
    message: "Extension url 'http://hl7.org/fhir/5.0/StructureDefinition/extension-MeasureReport.supplementalData' is not valid (invalid Version '5.0')",
    expression: "Bundle.entry[0].resource.ofType(MeasureReport).extension[1][url='http://hl7.org/fhir/5.0/StructureDefinition/extension-MeasureReport.supplementalData']",
    location: '17:44'
  },
  {
    categoryIndex: 0,
    message: "Extension url 'http://hl7.org/fhir/5.0/StructureDefinition/extension-MeasureReport.supplementalData' is not valid (invalid Version '5.0')",
    expression: "Bundle.entry[0].resource.ofType(MeasureReport).extension[2][url='http://hl7.org/fhir/5.0/StructureDefinition/extension-MeasureReport.supplementalData']",
    location: '25:0'
  },
  {
    categoryIndex: 2,
    message:
      "None of the codings provided are in the value set 'US Core Vital Signs ValueSet' (http://hl7.org/fhir/us/core/ValueSet/us-core-vital-signs|6.1.0), and a coding should come from this value set unless it has no suitable code (codes = http://loinc.org#55284-4)",
    expression: 'Bundle.entry[3].resource.ofType(Observation).code.coding[0]',
    location: '112:8'
  },
  {
    categoryIndex: 4,
    message:
      "None of the codings provided are in the value set 'LOINC Codes' (http://hl7.org/fhir/ValueSet/observation-codes|4.0.1), and a coding should come from this value set unless it has no suitable code (codes = http://loinc.org#8480-6)",
    expression: 'Bundle.entry[4].resource.ofType(Observation).code.coding[1]',
    location: '118:15'
  },
  {
    categoryIndex: 4,
    message:
      "None of the codings provided are in the value set 'Observation Interpretation Codes' (http://hl7.org/fhir/ValueSet/observation-interpretation|4.0.1), and a coding should come from this value set unless it has no suitable code (codes = http://terminology.hl7.org/CodeSystem/v3-ObservationInterpretation#N)",
    expression: 'Bundle.entry[4].resource.ofType(Observation).interpretation[0].coding[0]',
    location: '119:20'
  },
  {
    categoryIndex: 5,
    message: "The code is not part of the required ValueSet 'US Core Condition Codes' (http://hl7.org/fhir/us/core/ValueSet/us-core-condition-code|6.1.0)",
    expression: 'Bundle.entry[6].resource.ofType(Condition).code.coding[0]',
    location: '203:22'
  },
  {
    categoryIndex: 5,
    message: "The code is not part of the required ValueSet 'US Core Condition Codes' (http://hl7.org/fhir/us/core/ValueSet/us-core-condition-code|6.1.0)",
    expression: 'Bundle.entry[7].resource.ofType(Condition).code.coding[0]',
    location: '214:2'
  },
  {
    categoryIndex: 6,
    message: 'The code is not recognized to be part of the ValueSet.',
    expression: 'Bundle.entry[2].resource.ofType(Encounter).type[0].coding[0]',
    location: '88:4'
  },
  {
    categoryIndex: 7,
    message: 'Best practice recommendation: a Patient resource SHOULD have a narrative.',
    expression: 'Bundle.entry[0].resource.ofType(Patient)',
    location: '3:0'
  },
  {
    categoryIndex: 8,
    message: "Coding does not match the preferred ValueSet 'US Core Observation Category'.",
    expression: 'Bundle.entry[5].resource.ofType(Observation).category[0].coding[0]',
    location: '132:10'
  },
  {
    categoryIndex: 9,
    message: 'Coverage.class.type is not present.',
    expression: 'Bundle.entry[8].resource.ofType(Coverage).class[0]',
    location: '241:0'
  },
  {
    categoryIndex: 1,
    message:
      "The Measure 'http://www.cdc.gov/nhsn/fhirportal/dqm/ig/Measure/NHSNAcuteCareHospitalMonthlyInitialPopulation|1.0.0-dev' could not be resolved, so no validation can be performed against the Measure",
    expression: 'Bundle.entry[0].resource.ofType(MeasureReport).measure',
    location: '5:0'
  }
];

export interface PreQualIssue {
  category: PreQualCategory;
  message: string;
  expression: string;
  location: string;
}

// String hash + LCG, matching the seeding style already used in ReportResultsStep (measureColor,
// demoDisplayReportingStatus) -- stable per patient/measure so the popup doesn't reshuffle on
// re-render, without needing a real per-patient result to seed from.
function hashString(value: string): number {
  let hash = 0;
  for (let i = 0; i < value.length; i++) {
    hash = (hash * 31 + value.charCodeAt(i)) >>> 0;
  }
  return hash;
}

function pickDeterministic<T>(items: T[], count: number, seed: number): T[] {
  if (items.length === 0) {
    return [];
  }
  const target = Math.min(count, items.length);
  const used = new Set<number>();
  const result: T[] = [];
  let cursor = seed;
  while (result.length < target) {
    cursor = (cursor * 1103515245 + 12345) >>> 0;
    const index = cursor % items.length;
    if (!used.has(index)) {
      used.add(index);
      result.push(items[index]);
    }
  }
  return result;
}

// A Passed Validation patient must never surface an Unacceptable-category issue -- the same hard
// guarantee the POC's getPreQualRowsFor() enforces regardless of how the issue pool was seeded.
export function preQualIssuesFor(patientId: string, measureName: string | undefined, reportingStatus: ReportingStatus): PreQualIssue[] {
  if (reportingStatus !== 'PassedValidation' && reportingStatus !== 'FailedValidation') {
    return [];
  }

  const seed = hashString(`${patientId}|${measureName ?? ''}`);
  const acceptableTemplates = PREQUAL_ISSUE_POOL.filter(template => PREQUAL_CATEGORIES[template.categoryIndex].acceptable);
  const unacceptableTemplates = PREQUAL_ISSUE_POOL.filter(template => !PREQUAL_CATEGORIES[template.categoryIndex].acceptable);

  const acceptableCount = 3 + (seed % 5);
  const selected = pickDeterministic(acceptableTemplates, acceptableCount, seed);

  if (reportingStatus === 'FailedValidation') {
    const unacceptableCount = 1 + (seed % 2);
    selected.push(...pickDeterministic(unacceptableTemplates, unacceptableCount, seed + 1));
  }

  return selected.map(template => ({
    category: PREQUAL_CATEGORIES[template.categoryIndex],
    message: template.message,
    expression: template.expression,
    location: template.location
  }));
}

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
            <th>{t('onboarding:reportResults.detail.preQual.category')}</th>
            <th>{t('onboarding:reportResults.detail.preQual.numberOfIssues')}</th>
            <th>{t('onboarding:reportResults.detail.preQual.guidance')}</th>
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
  const {notifyError} = useNotifications();
  const [selectedCategory, setSelectedCategory] = useState<string | null>(null);

  const issues = useMemo(() => preQualIssuesFor(patientId, measureName, reportingStatus), [patientId, measureName, reportingStatus]);
  const chartCounts = useMemo(() => categoryCounts(issues), [issues]);
  const unacceptableSummary = useMemo(() => summarizeByCategory(issues, false), [issues]);
  const acceptableSummary = useMemo(() => summarizeByCategory(issues, true), [issues]);
  // PassedValidation patients never carry unacceptable issues (see preQualIssuesFor), but the
  // section is hidden by status too so it doesn't flash empty before disappearing.
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
                <th>{t('onboarding:reportResults.detail.preQual.message')}</th>
                <th>{t('onboarding:reportResults.detail.preQual.expression')}</th>
                <th>{t('onboarding:reportResults.detail.preQual.location')}</th>
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

      <h3 className="nhsn-link__report-results-detail-section-title">{t('onboarding:reportResults.detail.preQual.issuesSummary')}</h3>
      <IssuesSummaryChart counts={chartCounts} />

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

      <div className="nhsn-link__report-results-patient-detail-actions">
        <Button variant="secondary" onClick={handleDownload}>
          <DownloadIcon />
          {t('onboarding:reportResults.detail.preQual.downloadResults')}
        </Button>
      </div>
    </Modal>
  );
}

export default PreQualResultsModal;
