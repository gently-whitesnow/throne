import { QueryClient, QueryClientProvider } from "@tanstack/react-query";
import {
  render,
  type RenderOptions,
  type RenderResult
} from "@testing-library/react";
import type { ReactElement, ReactNode } from "react";

import { RealtimeQueryBridge } from "./realtime-query-bridge";

/**
 * Создаёт изолированный QueryClient без retry/staleTime для предсказуемых тестов.
 */
export function createTestQueryClient(): QueryClient {
  return new QueryClient({
    defaultOptions: {
      queries: {
        retry: false,
        staleTime: 0,
        gcTime: 0,
        refetchOnWindowFocus: false
      }
    }
  });
}

interface RenderWithQueryOptions extends Omit<RenderOptions, "wrapper"> {
  queryClient?: QueryClient;
  /** Mount RealtimeQueryBridge — true по умолчанию, чтобы realtime-моки
   *  доставляли события до query-cache. */
  withBridge?: boolean;
}

export function renderWithQuery(
  ui: ReactElement,
  options: RenderWithQueryOptions = {}
): RenderResult & { queryClient: QueryClient } {
  const {
    queryClient = createTestQueryClient(),
    withBridge = true,
    ...rest
  } = options;
  const Wrapper = ({ children }: { children: ReactNode }) => (
    <QueryClientProvider client={queryClient}>
      {withBridge ? <RealtimeQueryBridge /> : null}
      {children}
    </QueryClientProvider>
  );
  const result = render(ui, { wrapper: Wrapper, ...rest });
  return { ...result, queryClient };
}
