import React from 'react';
import { MessageContainer, NHSNLoadingIndicator } from '../../../fields';

export interface AsyncStatusProps {
  loading: boolean;
  error: string | null;
}

export function AsyncStatus({ loading, error }: AsyncStatusProps) {
  return (
    <>
      <p className="nhsn-link__visually-hidden" role="alert">
        {!loading ? error : null}
      </p>
      {loading && <NHSNLoadingIndicator />}
      {!loading && error && (
        <MessageContainer type="error" showIcon>
          <span>{error}</span>
        </MessageContainer>
      )}
    </>
  );
}
