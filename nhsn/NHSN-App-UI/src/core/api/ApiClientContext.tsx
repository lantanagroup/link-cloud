import React, {createContext, useContext} from 'react';
import type {ApiClient} from './ApiClient';

const ApiClientContext = createContext<ApiClient | null>(null);

export function ApiClientProvider({
  client,
  children
}: {
  client: ApiClient;
  children: React.ReactNode;
}) {
  return <ApiClientContext.Provider value={client}>{children}</ApiClientContext.Provider>;
}

/**
 * `core` never constructs a client — each entry point injects one at its
 * composition root. Memoize the instance: built inline in JSX it is rebuilt
 * every render and re-renders every consumer.
 */
export function useApiClient(): ApiClient {
  const client = useContext(ApiClientContext);
  if (!client) {
    throw new Error('useApiClient used outside ApiClientProvider');
  }
  return client;
}
