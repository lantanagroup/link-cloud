import React from 'react';
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
}

/** The Back/Continue row every step ends with. */
export function StepActions({children}: StepActionsProps) {
  return <div className="nhsn-link__step-actions">{children}</div>;
}

export {Badge, FormSection, MessageContainer, NHSNLoadingIndicator, NoData, PageHeader, RequiredFieldNotice};
