import type { NextRequest } from "next/server";
import { mockDisabled } from "../guard";
import { pickScenario, type Scenario } from "./scenarios";

const TOKEN_DELAY_MS = 55;
const STEP_DELAY_MS = 350;
const answeredBefore = new Set<string>();

const encoder = new TextEncoder();
const sleep = (milliseconds: number) => new Promise((resolve) => setTimeout(resolve, milliseconds));

function tokenize(text: string): string[] {
  return text.match(/\s*\S+\s?/g) ?? [text];
}

function frame(event: string, payload: unknown, style: "plain" | "crlf" | "multiline"): string {
  if (style === "multiline") {
    const lines = JSON.stringify(payload, null, 1).split("\n");
    return `event: ${event}\n${lines.map((line) => `data: ${line}`).join("\n")}\n\n`;
  }
  const lineBreak = style === "crlf" ? "\r\n" : "\n";
  return `event: ${event}${lineBreak}data: ${JSON.stringify(payload)}${lineBreak}${lineBreak}`;
}

export async function POST(request: NextRequest) {
  const disabled = mockDisabled();
  if (disabled) return disabled;

  const body = (await request.json()) as { message?: string; conversationId?: string | null };
  const message = body.message ?? "";
  const scenario: Scenario = pickScenario(message);
  const cached = answeredBefore.has(message);
  answeredBefore.add(message);
  const startedAt = Date.now();

  const stream = new ReadableStream<Uint8Array>({
    async start(controller) {
      const aborted = () => request.signal.aborted;

      const send = async (text: string, splitMidEvent: boolean) => {
        if (aborted()) return;
        const cut = splitMidEvent ? Math.floor(text.length / 2) : text.length;
        controller.enqueue(encoder.encode(text.slice(0, cut)));
        if (cut < text.length) {
          await sleep(5);
          if (aborted()) return;
          controller.enqueue(encoder.encode(text.slice(cut)));
        }
      };

      try {
        for (const name of scenario.tools) {
          await send(frame("tool", { name, status: "started" }, "plain"), false);
          await sleep(cached ? 20 : STEP_DELAY_MS);
          await send(frame("tool", { name, status: "completed" }, "plain"), false);
        }
        if (scenario.citations.length > 0) {
          await send(frame("citations", { citations: scenario.citations }, "crlf"), false);
        }
        if (scenario.sql) await send(frame("sql", scenario.sql, "multiline"), true);

        for (const text of tokenize(scenario.answer)) {
          if (aborted()) break;
          await send(frame("token", { text }, "plain"), true);
          await sleep(cached ? 4 : TOKEN_DELAY_MS);
        }

        if (!aborted() && !message.toLowerCase().includes("truncate")) {
          await send(
            frame(
              "done",
              {
                conversationId: body.conversationId ?? "mock-conversation",
                latencyMs: cached ? 96 : Date.now() - startedAt,
                cached,
                usage: { prompt: 2140, completion: 260 },
                grounding: scenario.grounding,
              },
              "plain",
            ),
            false,
          );
        }
      } finally {
        if (!aborted()) controller.close();
      }
    },
  });

  return new Response(stream, {
    headers: {
      "Content-Type": "text/event-stream; charset=utf-8",
      "Cache-Control": "no-cache, no-transform",
      Connection: "keep-alive",
    },
  });
}
