export type SseMessage = { event: string; data: string };

const LINE_BREAK = /\r\n|\r|\n/;

export async function* parseSse(
  body: ReadableStream<Uint8Array>,
): AsyncGenerator<SseMessage> {
  const reader = body.getReader();
  const decoder = new TextDecoder();
  let buffer = "";
  let eventName = "";
  let dataLines: string[] = [];

  const takeMessage = (): SseMessage | null => {
    const message =
      dataLines.length > 0
        ? { event: eventName || "message", data: dataLines.join("\n") }
        : null;
    eventName = "";
    dataLines = [];
    return message;
  };

  const applyLine = (line: string): SseMessage | null => {
    if (line === "") return takeMessage();
    if (line.startsWith(":")) return null;
    const colon = line.indexOf(":");
    const field = colon === -1 ? line : line.slice(0, colon);
    let value = colon === -1 ? "" : line.slice(colon + 1);
    if (value.startsWith(" ")) value = value.slice(1);
    if (field === "event") eventName = value;
    if (field === "data") dataLines.push(value);
    return null;
  };

  try {
    for (;;) {
      const { done, value } = await reader.read();
      buffer += done ? decoder.decode() : decoder.decode(value, { stream: true });

      for (;;) {
        const match = LINE_BREAK.exec(buffer);
        if (!match) break;
        const endsBufferWithCarriageReturn =
          match[0] === "\r" && match.index + 1 === buffer.length;
        if (endsBufferWithCarriageReturn && !done) break;
        const line = buffer.slice(0, match.index);
        buffer = buffer.slice(match.index + match[0].length);
        const message = applyLine(line);
        if (message) yield message;
      }

      if (done) break;
    }

    if (buffer !== "") applyLine(buffer);
    const trailing = takeMessage();
    if (trailing) yield trailing;
  } finally {
    await reader.cancel().catch(() => undefined);
  }
}
