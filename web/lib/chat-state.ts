import type { ChatEvent, Citation, DoneEvent, SqlResult, ToolEvent } from "./types";

export type AssistantPhase = "streaming" | "complete" | "stopped" | "interrupted" | "failed";

export type UserMessage = { id: string; role: "user"; text: string };

export type AssistantMessage = {
  id: string;
  role: "assistant";
  phase: AssistantPhase;
  text: string;
  citations: Citation[];
  tools: ToolEvent[];
  sql: SqlResult | null;
  done: DoneEvent | null;
  errorMessage: string | null;
};

export type ChatMessage = UserMessage | AssistantMessage;

export type ChatAction =
  | { type: "asked"; userId: string; assistantId: string; question: string }
  | { type: "event"; id: string; event: ChatEvent }
  | { type: "closed"; id: string }
  | { type: "stopped"; id: string }
  | { type: "failed"; id: string; message: string }
  | { type: "reset" };

export function newAssistantMessage(id: string): AssistantMessage {
  return {
    id,
    role: "assistant",
    phase: "streaming",
    text: "",
    citations: [],
    tools: [],
    sql: null,
    done: null,
    errorMessage: null,
  };
}

function applyEvent(message: AssistantMessage, event: ChatEvent): AssistantMessage {
  switch (event.type) {
    case "tool":
      return { ...message, tools: applyTool(message.tools, event.data) };
    case "citations":
      return { ...message, citations: event.data.citations ?? [] };
    case "sql":
      return { ...message, sql: event.data };
    case "token":
      return { ...message, text: message.text + (event.data.text ?? "") };
    case "done":
      return { ...message, phase: "complete", done: event.data };
    case "error":
      return { ...message, phase: "failed", errorMessage: event.data.message };
  }
}

function applyTool(tools: ToolEvent[], incoming: ToolEvent): ToolEvent[] {
  if (incoming.status === "started") return [...tools, incoming];
  const openIndex = tools.findLastIndex(
    (tool) => tool.name === incoming.name && tool.status === "started",
  );
  if (openIndex === -1) return [...tools, incoming];
  return tools.map((tool, index) => (index === openIndex ? incoming : tool));
}

function updateAssistant(
  messages: ChatMessage[],
  id: string,
  change: (message: AssistantMessage) => AssistantMessage,
): ChatMessage[] {
  return messages.map((message) =>
    message.id === id && message.role === "assistant" ? change(message) : message,
  );
}

function settleIfStreaming(phase: AssistantPhase) {
  return (message: AssistantMessage): AssistantMessage =>
    message.phase === "streaming" ? { ...message, phase } : message;
}

export function chatReducer(messages: ChatMessage[], action: ChatAction): ChatMessage[] {
  switch (action.type) {
    case "asked":
      return [
        ...messages,
        { id: action.userId, role: "user", text: action.question },
        newAssistantMessage(action.assistantId),
      ];
    case "event":
      return updateAssistant(messages, action.id, (message) =>
        message.phase === "streaming" ? applyEvent(message, action.event) : message,
      );
    case "closed":
      return updateAssistant(messages, action.id, settleIfStreaming("interrupted"));
    case "stopped":
      return updateAssistant(messages, action.id, settleIfStreaming("stopped"));
    case "failed":
      return updateAssistant(messages, action.id, (message) =>
        message.phase === "streaming"
          ? { ...message, phase: "failed", errorMessage: action.message }
          : message,
      );
    case "reset":
      return [];
  }
}

export function lastConversationId(messages: ChatMessage[]): string | null {
  for (let index = messages.length - 1; index >= 0; index -= 1) {
    const message = messages[index];
    if (message?.role === "assistant" && message.done) return message.done.conversationId;
  }
  return null;
}
