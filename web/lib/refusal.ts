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

const BARE_EMPTY_RESULT = /\b(?:0|zero|no)\b[^.\n]{0,40}\b(?:rows?|records?|results?|matches)\b/i;
const BARE_EMPTY_RESULT_MAX_CHARS = 160;

export const isBareEmptyResult = (message: AssistantMessage): boolean =>
  message.sql !== null &&
  message.sql.rejected === null &&
  message.sql.rows.length === 0 &&
  message.text.length < BARE_EMPTY_RESULT_MAX_CHARS &&
  (message.phase === "streaming" || BARE_EMPTY_RESULT.test(message.text));
