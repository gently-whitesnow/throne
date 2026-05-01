const DEFAULT_BASE_URL = "/api/v1";

const baseUrl = (
  (import.meta.env.VITE_API_BASE_URL as string | undefined) ?? DEFAULT_BASE_URL
).replace(/\/$/, "");

export class HttpError extends Error {
  constructor(
    public readonly status: number,
    public readonly url: string,
    message: string
  ) {
    super(message);
    this.name = "HttpError";
  }
}

export async function httpGet<T>(
  path: string,
  signal?: AbortSignal
): Promise<T> {
  const url = `${baseUrl}${path}`;
  const response = await fetch(url, {
    method: "GET",
    headers: { Accept: "application/json" },
    signal
  });

  if (!response.ok) {
    const detail = await response.text().catch(() => "");
    throw new HttpError(
      response.status,
      url,
      `GET ${url} failed (${String(response.status)}): ${detail || response.statusText}`
    );
  }

  return (await response.json()) as T;
}
