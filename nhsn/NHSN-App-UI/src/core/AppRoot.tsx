import { useMemo } from "react";
import { QueryClient, QueryClientProvider } from "@tanstack/react-query";
import type { ApiClient } from "./api/ApiClient";
import { ApiClientProvider } from "./api/ApiClientContext";
import { NotificationProvider } from "./notifications/NotificationProvider";
import { NHSNLink, type NHSNLinkProps } from "./NHSNLink";

export interface AppRootProps extends NHSNLinkProps {
  client: ApiClient;
}

export function AppRoot({ client, ...props }: AppRootProps) {
  const queryClient = useMemo(
    () =>
      new QueryClient({
        defaultOptions: {
          queries: { retry: false, refetchOnWindowFocus: false },
        },
      }),
    [],
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
