import { apiUrl, httpPost } from "@/shared/api";

const LIVENESS_POLL_INTERVAL_MS = 300;
const LIVENESS_POLL_TIMEOUT_MS = 10_000;

export async function stopRuntimeInstance(): Promise<void> {
  try {
    await httpPost<null>("/runtime/shutdown", {});
  } catch {
    // Сервер мог начать остановку до ответа — всё равно ждём его падения и перезагружаемся.
  }
  await reloadWhenServerDown();
}

async function reloadWhenServerDown(): Promise<void> {
  const start = performance.now();
  while (performance.now() - start < LIVENESS_POLL_TIMEOUT_MS) {
    await delay(LIVENESS_POLL_INTERVAL_MS);
    try {
      await fetch(apiUrl("/runtime/instance"), { cache: "no-store" });
    } catch {
      break;
    }
  }
  window.location.reload();
}

function delay(ms: number): Promise<void> {
  return new Promise((resolve) => setTimeout(resolve, ms));
}
