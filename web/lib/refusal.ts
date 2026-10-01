import type { AssistantMessage } from "./chat-state";

export const OPERATIONAL_TOOLS = new Set([
  "query_database",
  "track_container",
  "find_schedules",
  "get_vessel",
  "get_port",
]);

const REFUSAL_OPENING =
  /^\s*(?:i|we)\s+(?:could\s?not|couldn['’]t|cannot|can['’]t|am\s+unable\s+to|was\s+unable\s+to|did\s+not|didn['’]t|do\s+not|don['’]t)\b[^.\n]{0,80}?\b(?:find|have|locate)\b/i;

export const isRefusal = (message: AssistantMessage): boolean =>
  message.phase === "complete" &&
  message.done !== null &&
  (message.done.grounding?.uncited ?? 0) === 0 &&
  message.sql === null &&
  !message.tools.some((tool) => OPERATIONAL_TOOLS.has(tool.name)) &&
  REFUSAL_OPENING.test(message.text);

export const isRefusalExample = (label: string): boolean => /refus/i.test(label);
