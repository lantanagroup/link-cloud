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

export {Badge, FormSection, MessageContainer, NHSNLoadingIndicator, NoData, PageHeader, RequiredFieldNotice};
