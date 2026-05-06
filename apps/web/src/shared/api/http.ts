const DEFAULT_BASE_URL = "/api/v1";

const baseUrl = (
  (import.meta.env.VITE_API_BASE_URL as string | undefined) ?? DEFAULT_BASE_URL
).replace(/\/$/, "");

export function apiUrl(path: string): string {
  return `${baseUrl}${path}`;
}

export class HttpError extends Error {
  public readonly code?: string;
  public readonly extensions: Record<string, unknown>;

  constructor(
    public readonly status: number,
    public readonly url: string,
    message: string,
    body?: Record<string, unknown>
  ) {
    super(message);
    this.name = "HttpError";
    this.extensions = body ?? {};
    if (typeof body?.code === "string") {
      this.code = body.code;
    }
  }
}

let redirectedToLogin = false;

function redirectToLoginOnce(): void {
  if (redirectedToLogin) return;
  redirectedToLogin = true;
  if (typeof window === "undefined") return;
  const here =
    window.location.pathname + window.location.search + window.location.hash;
  window.location.href = `/login/?returnTo=${encodeURIComponent(here)}`;
}

async function parseError(
  url: string,
  response: Response,
  method: string
): Promise<HttpError> {
  if (response.status === 401) {
    redirectToLoginOnce();
  }
  let body: Record<string, unknown> | undefined;
  let text = "";
  try {
    text = await response.text();
    body = text ? (JSON.parse(text) as Record<string, unknown>) : undefined;
  } catch {
    body = undefined;
  }
  const detail =
    (body && typeof body.detail === "string" ? body.detail : "") ||
    text ||
    response.statusText;
  return new HttpError(
    response.status,
    url,
    `${method} ${url} failed (${String(response.status)}): ${detail}`,
    body
  );
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
    throw await parseError(url, response, "GET");
  }

  return (await response.json()) as T;
}

export async function httpPost<TResponse>(
  path: string,
  body: unknown,
  signal?: AbortSignal
): Promise<TResponse> {
  const url = `${baseUrl}${path}`;
  const response = await fetch(url, {
    method: "POST",
    headers: {
      Accept: "application/json",
      "Content-Type": "application/json"
    },
    body: JSON.stringify(body),
    signal
  });

  if (!response.ok) {
    throw await parseError(url, response, "POST");
  }

  if (response.status === 204) {
    return undefined as TResponse;
  }

  return (await response.json()) as TResponse;
}

export async function httpPut<TResponse>(
  path: string,
  body: unknown,
  signal?: AbortSignal
): Promise<TResponse> {
  const url = `${baseUrl}${path}`;
  const response = await fetch(url, {
    method: "PUT",
    headers: {
      Accept: "application/json",
      "Content-Type": "application/json"
    },
    body: JSON.stringify(body),
    signal
  });

  if (!response.ok) {
    throw await parseError(url, response, "PUT");
  }

  if (response.status === 204) {
    return undefined as TResponse;
  }

  return (await response.json()) as TResponse;
}

export async function httpPostForm<TResponse>(
  path: string,
  body: FormData,
  signal?: AbortSignal
): Promise<TResponse> {
  const url = `${baseUrl}${path}`;
  const response = await fetch(url, {
    method: "POST",
    headers: { Accept: "application/json" },
    body,
    signal
  });

  if (!response.ok) {
    throw await parseError(url, response, "POST");
  }

  return (await response.json()) as TResponse;
}

export async function httpGetBlob(
  path: string,
  signal?: AbortSignal
): Promise<Blob> {
  const url = `${baseUrl}${path}`;
  const response = await fetch(url, {
    method: "GET",
    signal
  });

  if (!response.ok) {
    throw await parseError(url, response, "GET");
  }

  return await response.blob();
}

export async function httpDelete(
  path: string,
  signal?: AbortSignal
): Promise<void> {
  const url = `${baseUrl}${path}`;
  const response = await fetch(url, {
    method: "DELETE",
    headers: { Accept: "application/json" },
    signal
  });

  if (!response.ok) {
    throw await parseError(url, response, "DELETE");
  }
}
