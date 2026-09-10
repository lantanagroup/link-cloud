import React, {useState} from 'react';
import {useTranslation} from 'react-i18next';
import type {ReportingStatus} from '../../../api/contracts';
import {Button, Modal} from '../../../fields';

/**
 * The onboarding POC's "Patient Status Timeline" -- the full conceptual pipeline behind Link's
 * five reporting statuses, reproduced node-for-node (ids, labels, grid position and description
 * text all copied from the POC's own PATIENT_STATUS_TIMELINE_NODES).
 *
 * Link does not capture an initial-vs-supplemental distinction anywhere -- a patient's
 * AcquisitionEvaluatedAt/NormalizationEvaluatedAt are each a single timestamp, not one per pass --
 * so this cannot be driven from finer real signals than ReportingStatus itself. That said, the POC
 * does not either: it hard-codes one fixed node path per final status (see
 * PATIENT_STATUS_TIMELINE_PATHS in index.html) rather than deriving partial progress, and its
 * "Critical Failure" node has no entry in that path table at all -- it is drawn for a complete
 * diagram but never actually reached. TIMELINE_PATH_BY_STATUS below is that same scheme, keyed by
 * Link's real ReportingStatus values instead of the POC's simulated ones, with one addition:
 * PatientIdentified (a real Link status the POC's fixture never modeled, since it only ever
 * simulated finished reports) maps to just the first node, reached and nothing past it.
 */
interface TimelineNode {
  id: string;
  label: string;
  col: number;
  row: number;
  description: string;
}

const TIMELINE_NODES: TimelineNode[] = [
  {
    id: 'initial_acquisition',
    label: 'Initial Data Acquisition',
    col: 0,
    row: 1,
    description:
      "The patient's initial data are being required from the facility FHIR server. This data will determine if the patient is eligible for measures your facility is reporting for."
  },
  {
    id: 'initial_normalization',
    label: 'Initial Normalization',
    col: 1,
    row: 1,
    description:
      'Applies any configured mappings (Example: HSLOC Location mappings, Encounter CPT/SNOMED mappings) to the initial acquired resources.'
  },
  {
    id: 'initial_measure_eval',
    label: 'Initial Measure Evaluation',
    col: 2,
    row: 1,
    description: 'Evaluates to see if the patient meets the initial criteria of the measure they are being reported for.'
  },
  {
    id: 'supplemental_acquisition',
    label: 'Supplemental Data Acquisition',
    col: 3,
    row: 1,
    description:
      'The patient has met the initial criteria of the measure. NHSNLink will now acquire supplemental FHIR resources related to the measure the patient is being reporting for.'
  },
  {
    id: 'supplemental_normalization',
    label: 'Supplemental Normalization',
    col: 4,
    row: 1,
    description:
      'Applies any configured mappings (Example: HSLOC Location mappings, Encounter CPT/SNOMED mappings) to the supplemental acquired resources.'
  },
  {
    id: 'supplemental_measure_eval',
    label: 'Supplemental Measure Evaluation',
    col: 5,
    row: 1,
    description: 'Performs a second evaluation of the patient that includes the additionally acquired supplemental resources.'
  },
  {
    id: 'pending_validation',
    label: 'Pending Validation',
    col: 6,
    row: 1,
    description: 'The patient met the criteria of the measure and is having their data validated to ensure it meets all compliance rules.'
  },
  {
    id: 'not_eligible',
    label: 'Not Eligible',
    col: 3,
    row: 0,
    description: 'The patient was evaluated and does not meet the criteria of the measure.'
  },
  {
    id: 'passed_validation',
    label: 'Passed Validation',
    col: 7,
    row: 0,
    description: 'The patient met the criteria of the measure and their data meets all compliance rules.'
  },
  {
    id: 'failed_validation',
    label: 'Failed Validation',
    col: 7,
    row: 2,
    description: 'The patient met the criteria of the measure and their data does not meet all of the compliance rules.'
  },
  {
    id: 'critical_failure',
    label: 'Critical Failure',
    col: 3,
    row: 2,
    description: 'A critical failure has occurred in the reporting process.'
  }
];

const TIMELINE_EDGES: Array<{from: string; to: string; dashed?: boolean}> = [
  {from: 'initial_acquisition', to: 'initial_normalization'},
  {from: 'initial_normalization', to: 'initial_measure_eval'},
  {from: 'initial_measure_eval', to: 'supplemental_acquisition'},
  {from: 'initial_measure_eval', to: 'not_eligible'},
  {from: 'supplemental_acquisition', to: 'supplemental_normalization'},
  {from: 'supplemental_normalization', to: 'supplemental_measure_eval'},
  {from: 'supplemental_measure_eval', to: 'pending_validation'},
  {from: 'pending_validation', to: 'passed_validation'},
  {from: 'pending_validation', to: 'failed_validation'},
  {from: 'initial_acquisition', to: 'critical_failure', dashed: true},
  {from: 'initial_normalization', to: 'critical_failure', dashed: true},
  {from: 'initial_measure_eval', to: 'critical_failure', dashed: true},
  {from: 'supplemental_acquisition', to: 'critical_failure', dashed: true},
  {from: 'supplemental_normalization', to: 'critical_failure', dashed: true},
  {from: 'supplemental_measure_eval', to: 'critical_failure', dashed: true},
  {from: 'pending_validation', to: 'critical_failure', dashed: true}
];

const CHAIN = ['initial_acquisition', 'initial_normalization', 'initial_measure_eval', 'supplemental_acquisition', 'supplemental_normalization', 'supplemental_measure_eval', 'pending_validation'];

const TIMELINE_PATH_BY_STATUS: Record<ReportingStatus, string[]> = {
  PatientIdentified: ['initial_acquisition'],
  NotReportable: [...CHAIN.slice(0, 3), 'not_eligible'],
  PendingValidation: [...CHAIN],
  PassedValidation: [...CHAIN, 'passed_validation'],
  FailedValidation: [...CHAIN, 'failed_validation']
};

const NODE_WIDTH = 150;
const NODE_HEIGHT = 58;
const COL_PITCH = 176;
const ROW_PITCH = 118;

function nodeXY(node: TimelineNode) {
  return {x: node.col * COL_PITCH, y: node.row * ROW_PITCH};
}

type NodeState = 'pending' | 'done' | 'current';

function nodePalette(nodeId: string, state: NodeState): {bg: string; border: string; text: string} {
  if (state === 'pending') {
    return {bg: '#f8fafc', border: '#cbd5e1', text: '#94a3b8'};
  }
  if (state === 'done') {
    return {bg: '#e3f8ea', border: '#15803d', text: '#0d5c2e'};
  }
  if (nodeId === 'not_eligible') {
    return {bg: '#fef3e2', border: '#b3720a', text: '#7a4d06'};
  }
  if (nodeId === 'passed_validation') {
    return {bg: '#e3f8ea', border: '#15803d', text: '#0d5c2e'};
  }
  if (nodeId === 'failed_validation' || nodeId === 'critical_failure') {
    return {bg: '#fde2e2', border: '#c62828', text: '#8f1d1d'};
  }
  return {bg: '#eaf2fc', border: '#0b5cab', text: '#0b5cab'};
}

export interface PatientStatusTimelineModalProps {
  open: boolean;
  onClose: () => void;
  patientId: string;
  measureName?: string;
  reportingStatus: ReportingStatus;
}

export function PatientStatusTimelineModal({open, onClose, patientId, measureName, reportingStatus}: PatientStatusTimelineModalProps) {
  const {t} = useTranslation(['onboarding', 'common']);
  const path = TIMELINE_PATH_BY_STATUS[reportingStatus];
  const currentId = path[path.length - 1];
  const doneSet = new Set(path.slice(0, -1));
  const [selectedNodeId, setSelectedNodeId] = useState(currentId);

  const nodeById = new Map(TIMELINE_NODES.map(node => [node.id, node]));
  const maxCol = Math.max(...TIMELINE_NODES.map(node => node.col));
  const maxRow = Math.max(...TIMELINE_NODES.map(node => node.row));
  const width = maxCol * COL_PITCH + NODE_WIDTH;
  const height = maxRow * ROW_PITCH + NODE_HEIGHT;

  const selectedNode = nodeById.get(selectedNodeId) ?? nodeById.get(currentId);

  return (
    <Modal
      open={open}
      title={t('onboarding:reportResults.detail.patientTimeline.title')}
      onClose={onClose}
      size="large"
      footer={
        <Button variant="secondary" onClick={onClose}>
          {t('common:actions.close')}
        </Button>
      }>
      <p className="nhsn-link__report-results-timeline-subtitle">
        {t('onboarding:reportResults.detail.patientTimeline.subtitle', {patientId, measure: measureName ?? ''})}
      </p>

      <svg
        viewBox={`-24 -34 ${width + 48} ${height + 68}`}
        className="nhsn-link__report-results-timeline-svg"
        role="img"
        aria-label={t('onboarding:reportResults.detail.patientTimeline.title')}>
        <defs>
          <marker id="nhsn-link-timeline-arrow" markerWidth="8" markerHeight="8" refX="6" refY="3" orient="auto">
            <path d="M0,0 L6,3 L0,6 Z" fill="#94a3b8" />
          </marker>
        </defs>

        {TIMELINE_EDGES.map(edge => {
          const from = nodeById.get(edge.from)!;
          const to = nodeById.get(edge.to)!;
          const fromXY = nodeXY(from);
          const toXY = nodeXY(to);
          let x1: number, y1: number, x2: number, y2: number;
          if (from.row === to.row) {
            x1 = fromXY.x + NODE_WIDTH;
            y1 = fromXY.y + NODE_HEIGHT / 2;
            x2 = toXY.x;
            y2 = toXY.y + NODE_HEIGHT / 2;
          } else if (to.row < from.row) {
            x1 = fromXY.x + NODE_WIDTH / 2;
            y1 = fromXY.y;
            x2 = toXY.x + NODE_WIDTH / 2;
            y2 = toXY.y + NODE_HEIGHT;
          } else {
            x1 = fromXY.x + NODE_WIDTH / 2;
            y1 = fromXY.y + NODE_HEIGHT;
            x2 = toXY.x + NODE_WIDTH / 2;
            y2 = toXY.y;
          }
          const active = (doneSet.has(edge.from) || edge.from === currentId) && (doneSet.has(edge.to) || edge.to === currentId);
          const stroke = active && !edge.dashed ? '#15803d' : '#cbd5e1';
          return (
            <line
              key={`${edge.from}-${edge.to}`}
              x1={x1}
              y1={y1}
              x2={x2}
              y2={y2}
              stroke={stroke}
              strokeWidth={2}
              strokeDasharray={edge.dashed ? '5,5' : undefined}
              markerEnd="url(#nhsn-link-timeline-arrow)"
            />
          );
        })}

        {TIMELINE_NODES.map(node => {
          const xy = nodeXY(node);
          const state: NodeState = node.id === currentId ? 'current' : doneSet.has(node.id) ? 'done' : 'pending';
          const palette = nodePalette(node.id, state);
          return (
            <g
              key={node.id}
              role="button"
              tabIndex={0}
              aria-label={node.label}
              onClick={() => setSelectedNodeId(node.id)}
              onKeyDown={event => {
                if (event.key === 'Enter' || event.key === ' ') {
                  event.preventDefault();
                  setSelectedNodeId(node.id);
                }
              }}
              style={{cursor: 'pointer'}}>
              <rect
                x={xy.x}
                y={xy.y}
                width={NODE_WIDTH}
                height={NODE_HEIGHT}
                rx={9}
                fill={palette.bg}
                stroke={selectedNodeId === node.id ? '#1d4ed8' : palette.border}
                strokeWidth={node.id === currentId || selectedNodeId === node.id ? 3 : 1.5}
              />
              <foreignObject x={xy.x + 4} y={xy.y + 2} width={NODE_WIDTH - 8} height={NODE_HEIGHT - 4}>
                <div
                  style={{
                    display: 'flex',
                    alignItems: 'center',
                    justifyContent: 'center',
                    height: '100%',
                    textAlign: 'center',
                    fontSize: '11.5px',
                    fontWeight: 600,
                    lineHeight: 1.25,
                    color: palette.text,
                    padding: '0 2px'
                  }}>
                  {state === 'done' ? '✓ ' : ''}
                  {node.label}
                </div>
              </foreignObject>
              {node.id === currentId && (
                <text x={xy.x + NODE_WIDTH / 2} y={xy.y - 10} textAnchor="middle" fontSize={10} fontWeight={700} fill={palette.border} letterSpacing="0.05em">
                  {t('onboarding:reportResults.detail.patientTimeline.current')}
                </text>
              )}
            </g>
          );
        })}
      </svg>

      {selectedNode && (
        <div className="nhsn-link__report-results-timeline-detail">
          <h3 className="nhsn-link__report-results-detail-section-title">{selectedNode.label}</h3>
          <p>{selectedNode.description}</p>
        </div>
      )}
    </Modal>
  );
}

export default PatientStatusTimelineModal;
