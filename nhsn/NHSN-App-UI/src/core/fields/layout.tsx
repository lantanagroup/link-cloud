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
  disabled?: boolean;
}

export function Button({
  children,
  onClick,
  type = 'button',
  variant = 'primary',
  disabled
}: ButtonProps) {
  return (
    <KendoButton
      type={type}
      themeColor={variant === 'primary' ? 'primary' : 'base'}
      disabled={disabled}
      onClick={onClick}>
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
  const {t} = useTranslation('common');
  return (
    <div className="nhsn-link__step-actions" aria-busy={saving || undefined}>
      {children}
      {saving && (
        <span className="nhsn-link__saving" role="status" aria-label={t('status.saving')} />
      )}
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
