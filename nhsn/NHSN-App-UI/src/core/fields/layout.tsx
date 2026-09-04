import React from 'react';
import {useTranslation} from 'react-i18next';
import {
  Badge,
  FormSection,
  MessageContainer,
  NHSNLoadingIndicator,
  NoData,
  PageHeader,
  RequiredFieldNotice
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
  return (
    <KendoButton
      type={type}
      themeColor={variant === 'primary' ? 'primary' : 'base'}
      className={size === 'sm' ? 'nhsn-link__button--sm' : undefined}
      disabled={disabled || loading}
      aria-label={ariaLabel}
      onClick={onClick}>
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
