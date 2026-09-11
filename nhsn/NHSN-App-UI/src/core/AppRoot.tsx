import React, {useMemo} from 'react';
import {QueryClient, QueryClientProvider} from '@tanstack/react-query';
import type {ApiClient} from './api/ApiClient';
import {ApiClientProvider} from './api/ApiClientContext';
import {NotificationProvider} from './notifications/NotificationProvider';
import {NHSNLink, type NHSNLinkProps} from './NHSNLink';

export interface AppRootProps extends NHSNLinkProps {
  client: ApiClient;
}

/**
 * The providers both entry points need, in one place so the embed and the
 * shell cannot drift apart.
 *
 * The QueryClient is here because `DataGrid2` calls `useQuery` unconditionally
 * and therefore needs a client in context. We supply our own rather than the
 * package's `MistQueryClientProvider`, which persists to localStorage —
 * writing grid data into the host page's storage is not ours to do. Nothing we
 * write uses react-query directly; data comes through `ApiClient`.
 */
export function AppRoot({client, ...props}: AppRootProps) {
  const queryClient = useMemo(
    () =>
      new QueryClient({
        defaultOptions: {queries: {retry: false, refetchOnWindowFocus: false}}
      }),
    []
  );

  return (
    <QueryClientProvider client={queryClient}>
      <ApiClientProvider client={client}>
        <NotificationProvider>
          <NHSNLink {...props} />
        </NotificationProvider>
      </ApiClientProvider>
    </QueryClientProvider>
  );
}
