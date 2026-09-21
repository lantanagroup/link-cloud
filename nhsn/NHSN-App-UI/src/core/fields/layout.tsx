import React from 'react';
import {useTranslation} from 'react-i18next';
import {
  Badge,
  FormSection,
  MessageContainer,
  NHSNLoadingIndicator,
  NoData,
  PageHeader,
  RequiredFieldNotice,
  TbAsterisk
} from '@nhsn/nhsn-react-core';
import {Button as KendoButton} from '@progress/kendo-react-buttons';
import {InfoTooltip} from './InfoTooltip';

export interface ButtonProps {
  children: React.ReactNode;
  onClick?: () => void;
  type?: 'button' | 'submit';
  variant?: 'primary' | 'secondary';
  size?: 'default' | 'sm';
  disabled?: boolean;
  loading?: boolean;
  /** Overrides the accessible name when the visible label alone doesn't identify which item this button acts on (e.g. a "Remove" button repeated per row). */
  'aria-label'?: string;
}

export function Button({
  children,
  onClick,
  type = 'button',
  variant = 'primary',
  size = 'default',
  disabled,
  loading,
  'aria-label': ariaLabel
}: ButtonProps) {
  const {t} = useTranslation('common');
  const isBlocked = Boolean(disabled || loading);
  return (
    <KendoButton
      type={type}
      themeColor={variant === 'primary' ? 'primary' : 'base'}
      className={size === 'sm' ? 'nhsn-link__button--sm' : undefined}
      aria-label={ariaLabel}
      disabled={isBlocked}
      onClick={isBlocked ? undefined : onClick}>
      {loading && (
        <span className="nhsn-link__button-spinner" role="status" aria-label={t('status.saving')} />
      )}
      {children}
    </KendoButton>
  );
}

export interface StepActionsProps {
  children: React.ReactNode;
  saving?: boolean;
}

/** The Back/Continue row every step ends with. */
export function StepActions({children, saving}: StepActionsProps) {
  return (
    <div className="nhsn-link__step-actions" aria-busy={saving || undefined}>
      {children}
    </div>
  );
}

export interface InlineSpinnerProps {
  /** Already translated. Announced while the action is running. */
  label: string;
}

/** Small inline "action running" affordance - distinct from the full-panel `NHSNLoadingIndicator`. */
export function InlineSpinner({label}: InlineSpinnerProps) {
  return (
    <span className="nhsn-link__inline-spinner" role="status">
      <span className="nhsn-link__inline-spinner-dial" aria-hidden="true" />
      <span className="nhsn-link__inline-spinner-label">{label}</span>
    </span>
  );
}

export interface FieldLabelProps {
  children: React.ReactNode;
  /** Shows the small green check badge after the text. Defaults to true. */
  checked?: boolean;
  /** Already translated: shows an InfoTooltip after the label when given. */
  tooltip?: string;
}

/** Bold label for a control group, with the decorative green check badge (`aria-hidden`) and an optional tooltip. */
export function FieldLabel({children, checked = true, tooltip}: FieldLabelProps) {
  return (
    <span className="nhsn-link__field-label">
      {children}
      {checked && (
        <svg
          className="nhsn-link__field-check"
          width="18"
          height="18"
          viewBox="0 0 18 18"
          aria-hidden="true">
          <circle cx="9" cy="9" r="9" />
          <path d="M5 9.3l2.4 2.4L13 6" fill="none" stroke="currentColor" strokeWidth="1.6" strokeLinecap="round" strokeLinejoin="round" />
        </svg>
      )}
      {tooltip && <InfoTooltip label={typeof children === 'string' ? children : 'More info'} content={tooltip} />}
    </span>
  );
}

/**
 * Required-field marker for a hand-rolled label - a group heading or custom
 * field (ChipMultiSelect, PatientSelection's patient-id picker, a step's own
 * label) that isn't one Kendo field's own label, so it never goes through
 * MistFormLabel (which renders this same icon automatically whenever a Kendo
 * field is `required`). The single place `size`/`strokeWidth` are set for
 * every hand-rolled marker in the app, so they stay in lockstep with each
 * other without each call site repeating the numbers.
 */
export function RequiredAsterisk() {
  return <TbAsterisk aria-hidden="true" className="nhsn-link__required-asterisk" color="red" size={8} strokeWidth={3} />;
}

export function NewTabAnnouncement({id}: {id?: string}) {
  const {t} = useTranslation('common');
  return (
    <span id={id} className="nhsn-link__visually-hidden">
      {' '}
      {t('a11y.opensInNewTab')}
    </span>
  );
}

// NVDA tries to pronounce these as words instead of spelling them out ("sloc",
// "poi" like the food). Extend this map if another short-form starts doing
// the same thing.
const ACRONYM_SPELLINGS: Record<string, string> = {
  HSLOC: 'H S L O C',
  POI: 'P O I',
  EHR: 'E H R'
};
const ACRONYM_PATTERN = new RegExp(`\\b(${Object.keys(ACRONYM_SPELLINGS).join('|')})\\b`, 'g');

/**
 * Wraps any HSLOC/POI in already-translated text so it's spelled out letter
 * by letter instead of mispronounced as a word, leaving the visible text
 * exactly as translated.
 */
export function AcronymText({children}: {children: string}) {
  return (
    <>
      {children.split(ACRONYM_PATTERN).map((part, index) =>
        ACRONYM_SPELLINGS[part] ? (
          <React.Fragment key={index}>
            <span aria-hidden="true">{part}</span>
            <span className="nhsn-link__visually-hidden">{ACRONYM_SPELLINGS[part]}</span>
          </React.Fragment>
        ) : (
          part
        )
      )}
    </>
  );
}

export function acronymTitle(node: React.ReactNode): string {
  return node as unknown as string;
}

/**
 * A table's visually-hidden `<caption>`, always ending in "Table" - NVDA's
 * continuous/Say-All reading speaks a caption's own text but doesn't reliably
 * also announce the "table" role the way landing on it via Tab/object
 * navigation does, so the word has to be part of the caption text itself.
 */
export function TableCaption({children}: {children: React.ReactNode}) {
  const {t} = useTranslation('common');
  return (
    <caption className="nhsn-link__visually-hidden">
      {children} {t('a11y.table')}
    </caption>
  );
}

export interface SidePanelLayoutProps {
  children: React.ReactNode;
}

/**
 * Places a step's main content beside an optional SidePanel. Pass the step's
 * card as the first child and, conditionally, a SidePanel as the second -
 * omit the SidePanel entirely when there's nothing to preview yet.
 */
export function SidePanelLayout({children}: SidePanelLayoutProps) {
  return <div className="nhsn-link__side-panel-layout">{children}</div>;
}

export interface SidePanelProps {
  children: React.ReactNode;
}

/** The blue-bordered detail/results panel that docks beside a step's main card. */
export function SidePanel({children}: SidePanelProps) {
  return <aside className="nhsn-link__side-panel">{children}</aside>;
}

export {Badge, FormSection, MessageContainer, NHSNLoadingIndicator, NoData, PageHeader, RequiredFieldNotice};
