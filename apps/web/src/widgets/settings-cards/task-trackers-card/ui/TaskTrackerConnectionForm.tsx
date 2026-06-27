import { AlertCircle, Link2, Unlink } from "lucide-react";
import { useEffect, useState } from "react";

import {
  useDeleteTaskTrackerConnection,
  useSetTaskTrackerConnection,
  type TaskTrackerConnection
} from "@/entities/task-tracker";
import { Button } from "@/shared/ui";

interface TaskTrackerConnectionFormProps {
  connection: TaskTrackerConnection;
}

/**
 * Inline-форма подключения трекера: base URL + API-токен.
 *
 * Токен никогда не приходит с бэка, поэтому поле не предзаполняется. PUT
 * возвращает 200 даже при `invalid`/`unreachable` — итоговое состояние читаем
 * из обновлённого `connection.state`/`connection.error`, а не из ошибки мутации.
 */
export function TaskTrackerConnectionForm({
  connection
}: TaskTrackerConnectionFormProps) {
  const setConnection = useSetTaskTrackerConnection();
  const deleteConnection = useDeleteTaskTrackerConnection();
  const tracker = connection.tracker;

  const [baseUrl, setBaseUrl] = useState(connection.base_url ?? "");
  const [token, setToken] = useState("");
  const [validationError, setValidationError] = useState<string | null>(null);

  useEffect(() => {
    setBaseUrl(connection.base_url ?? "");
  }, [connection.base_url]);

  const connected = connection.state === "connected";
  const baseUrlId = `task-tracker-base-url-${tracker}`;
  const tokenId = `task-tracker-token-${tracker}`;

  const handleConnect = () => {
    const url = baseUrl.trim();
    const validation = validateConnection(url, token);
    if (validation !== null) {
      setValidationError(validation);
      return;
    }
    setValidationError(null);
    setConnection.mutate(
      { tracker, request: { base_url: url, token: token.trim() } },
      {
        onSuccess: () => {
          setToken("");
        }
      }
    );
  };

  const handleDisconnect = () => {
    deleteConnection.mutate(tracker);
  };

  return (
    <div className="flex flex-col gap-2">
      {connected ? (
        <div className="flex flex-wrap items-center justify-between gap-2 rounded-md border border-success/30 bg-success/10 px-3 py-2">
          <span className="text-sm text-success">
            Подключено:{" "}
            <code className="font-mono">{connection.base_url ?? "—"}</code>
          </span>
          <Button
            aria-label="Отключить трекер"
            data-testid={`task-tracker-disconnect-${tracker}`}
            icon={<Unlink aria-hidden size={14} strokeWidth={2} />}
            disabled={deleteConnection.isPending}
            onClick={handleDisconnect}
          >
            {deleteConnection.isPending ? "Отключаем…" : "Отключить"}
          </Button>
        </div>
      ) : null}

      <div className="flex flex-col gap-1">
        <label
          htmlFor={baseUrlId}
          className="text-xs font-semibold text-base-content/70"
        >
          Base URL
        </label>
        <input
          id={baseUrlId}
          data-testid={baseUrlId}
          type="url"
          className="input input-sm input-bordered font-mono text-xs"
          value={baseUrl}
          placeholder="https://mycompany.kaiten.ru"
          onChange={(event) => {
            setBaseUrl(event.target.value);
            if (validationError !== null) setValidationError(null);
          }}
          disabled={setConnection.isPending}
        />
      </div>

      <div className="flex flex-col gap-1">
        <label
          htmlFor={tokenId}
          className="text-xs font-semibold text-base-content/70"
        >
          API-токен
        </label>
        <div className="flex flex-wrap items-center gap-2">
          <input
            id={tokenId}
            data-testid={tokenId}
            type="password"
            autoComplete="off"
            className="input input-sm input-bordered flex-1 font-mono text-xs"
            value={token}
            placeholder={
              connected ? "Введите новый токен для обновления" : "API-токен"
            }
            onChange={(event) => {
              setToken(event.target.value);
              if (validationError !== null) setValidationError(null);
            }}
            disabled={setConnection.isPending}
          />
          <Button
            aria-label="Подключить трекер"
            data-testid={`task-tracker-connect-${tracker}`}
            variant="primary"
            icon={<Link2 aria-hidden size={14} strokeWidth={2} />}
            disabled={setConnection.isPending}
            onClick={handleConnect}
          >
            {setConnection.isPending
              ? "Подключаем…"
              : connected
                ? "Обновить"
                : "Подключить"}
          </Button>
        </div>
      </div>

      <ConnectionFeedback
        validationError={validationError}
        connection={connection}
        mutationError={setConnection.error}
      />
    </div>
  );
}

interface ConnectionFeedbackProps {
  validationError: string | null;
  connection: TaskTrackerConnection;
  mutationError: Error | null;
}

function ConnectionFeedback({
  validationError,
  connection,
  mutationError
}: ConnectionFeedbackProps) {
  const probeError =
    connection.state === "invalid" || connection.state === "unreachable"
      ? (connection.error ??
        (connection.state === "invalid"
          ? "Токен отклонён трекером."
          : "Трекер недоступен."))
      : null;

  const message =
    validationError ??
    probeError ??
    (mutationError instanceof Error
      ? `Не удалось подключить: ${mutationError.message}`
      : null);

  if (message === null) return null;

  return (
    <p
      role="alert"
      data-testid={`task-tracker-error-${connection.tracker}`}
      className="m-0 flex items-start gap-1.5 text-xs text-error"
    >
      <AlertCircle aria-hidden size={14} strokeWidth={2} className="mt-0.5" />
      <span>{message}</span>
    </p>
  );
}

function validateConnection(baseUrl: string, token: string): string | null {
  if (baseUrl.length === 0)
    return "Введите base URL, например https://mycompany.kaiten.ru.";
  if (!/^https?:\/\//i.test(baseUrl))
    return "Укажите base URL со схемой (https://).";
  if (token.trim().length === 0) return "Введите API-токен.";
  return null;
}
