import { useMutation, useQuery } from "@tanstack/react-query";

import {
  fetchRuntimeInstance,
  shutdownRuntimeInstance
} from "./runtime-instance-api";

export function useRuntimeInstance() {
  const instance = useQuery({
    queryKey: ["runtime-instance"],
    queryFn: ({ signal }) => fetchRuntimeInstance(signal)
  });

  const shutdown = useMutation({
    mutationFn: shutdownRuntimeInstance
  });

  return {
    isEphemeral: instance.data?.ephemeral === true,
    isLoading: instance.isLoading,
    isStopping: shutdown.isPending,
    stop: shutdown.mutate
  };
}
