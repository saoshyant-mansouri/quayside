import { parseSse } from "./sse";
import type { ChatEvent, Example } from "./types";

export const API_BASE = process.env.NEXT_PUBLIC_USE_MOCK === "1" ? "/mock-api" : "/api";

const EVENT_NAMES = new Set(["tool", "citations", "sql", "token", "done", "error"]);

export class ApiError extends Error {}

type ChatRequest = {
  message: string;
  conversationId: string | null;
  signal: AbortSignal;
};

export async function* streamChat({
  message,
  conversationId,
  signal,
}: ChatRequest): AsyncGenerator<ChatEvent> {
  const response = await fetch(`${API_BASE}/chat`, {
    method: "POST",
    headers: { "Content-Type": "application/json", Accept: "text/event-stream" },
    body: JSON.stringify({ message, conversationId }),
    signal,
  });

  if (!response.ok || !response.body) {
    throw new ApiError("The service did not respond properly. Please try again in a moment.");
  }

  for await (const { event, data } of parseSse(response.body)) {
    if (!EVENT_NAMES.has(event)) continue;
    const parsed = parseJson(data);
    if (parsed === null) continue;
    yield { type: event, data: parsed } as ChatEvent;
  }
}

function parseJson(text: string): unknown {
  try {
    const value: unknown = JSON.parse(text);
    return typeof value === "object" ? value : null;
  } catch {
    return null;
  }
}

async function getJson<T>(path: string, signal?: AbortSignal): Promise<T> {
  const response = await fetch(`${API_BASE}${path}`, { signal });
  if (!response.ok) throw new ApiError(`Status ${response.status}`);
  return (await response.json()) as T;
}

export const fetchExamples = (signal?: AbortSignal) => getJson<Example[]>("/examples", signal);
