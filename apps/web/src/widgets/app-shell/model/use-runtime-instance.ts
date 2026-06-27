import { useMutation } from "@tanstack/react-query";

import { stopRuntimeInstance } from "./runtime-instance-api";

export function useRuntimeInstance() {
  const shutdown = useMutation({
    mutationFn: stopRuntimeInstance
  });

  return {
    isStopping: shutdown.isPending,
    stop: shutdown.mutate
  };
}
