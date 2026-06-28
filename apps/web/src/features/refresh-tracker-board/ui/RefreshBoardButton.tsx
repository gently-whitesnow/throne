import { useQueryClient } from "@tanstack/react-query";
import { RefreshCw } from "lucide-react";

import { intentContextsQueryKeys, intentsQueryKeys } from "@/entities/intent";
import { useForceRefreshTaskTrackerBoard } from "@/entities/task-tracker";
import { errorMessage } from "@/shared/lib";

interface RefreshBoardButtonProps {
  tracker: string;
  boardId: string;
  ariaLabel: string;
}

/**
 * Board-group «Обновить»: runs the periodic board sync now (server reuses the polling path). On
 * success it nudges the rail counts and the board's intent list; refreshed card content also arrives
 * over realtime, so this is just a "don't wait for the next tick" trigger.
 */
export function RefreshBoardButton({
  tracker,
  boardId,
  ariaLabel
}: RefreshBoardButtonProps) {
  const queryClient = useQueryClient();
  const mutation = useForceRefreshTaskTrackerBoard();

  const handleClick = () => {
    mutation.mutate(
      { tracker, board: boardId },
      {
        onSuccess: () => {
          void queryClient.invalidateQueries({
            queryKey: intentContextsQueryKeys.all
          });
          void queryClient.invalidateQueries({
            queryKey: intentsQueryKeys.lists()
          });
        }
      }
    );
  };

  const title = mutation.isError
    ? errorMessage(mutation.error, { base: "Не удалось обновить доску" })
    : ariaLabel;

  return (
    <button
      type="button"
      onClick={(e) => {
        e.stopPropagation();
        handleClick();
      }}
      disabled={mutation.isPending}
      aria-label={ariaLabel}
      title={title}
      className={`flex h-6 w-6 items-center justify-center rounded transition-colors hover:bg-base-300/60 hover:text-base-content ${
        mutation.isError ? "text-error" : "text-base-content/60"
      }`}
    >
      <RefreshCw
        aria-hidden
        size={14}
        strokeWidth={2}
        className={mutation.isPending ? "animate-spin" : undefined}
      />
    </button>
  );
}
