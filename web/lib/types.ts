export type CitationSource = "linkedin" | "website";

export type Citation = {
  n: number;
  title: string;
  url: string;
  source: CitationSource;
  publishedAt: string | null;
};

export type ToolStatus = "started" | "completed";

export type ToolEvent = {
  name: string;
  status: ToolStatus;
  detail?: Record<string, unknown>;
};

export type SqlResult = {
  sql: string;
  columns: string[];
  rows: unknown[][];
  rejected: string | null;
};

export type Grounding = { cited: number; uncited: number };

export type DoneEvent = {
  conversationId: string;
  latencyMs: number;
  cached: boolean;
  usage?: { prompt: number; completion: number };
  grounding?: Grounding;
};

export type ChatEvent =
  | { type: "tool"; data: ToolEvent }
  | { type: "citations"; data: { citations: Citation[] } }
  | { type: "sql"; data: SqlResult }
  | { type: "token"; data: { text: string } }
  | { type: "done"; data: DoneEvent }
  | { type: "error"; data: { message: string } };

export type Health = {
  status: string;
  chunks: number;
  documents: number;
  corpusCapturedAt: string;
  indexWarm: boolean;
};

export type Example = { label: string; question: string };
