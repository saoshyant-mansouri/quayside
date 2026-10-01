"use client";

import { useRef, useState } from "react";

type ComposerProps = {
  streaming: boolean;
  hasConversation: boolean;
  onSend: (question: string) => void;
  onStop: () => void;
  onReset: () => void;
};

export function Composer({ streaming, hasConversation, onSend, onStop, onReset }: ComposerProps) {
  const [draft, setDraft] = useState("");
  const textareaRef = useRef<HTMLTextAreaElement>(null);
  const canSend = draft.trim().length > 0 && !streaming;

  function submit() {
    if (!canSend) return;
    onSend(draft);
    setDraft("");
    textareaRef.current?.focus();
  }

  function stop() {
    onStop();
    textareaRef.current?.focus();
  }

  return (
    <div className="sticky bottom-0 z-10 border-t border-line bg-bg/90 backdrop-blur">
      <div className="mx-auto max-w-5xl px-4 pb-[max(0.75rem,env(safe-area-inset-bottom))] pt-3 sm:px-6">
        <form
          onSubmit={(event) => {
            event.preventDefault();
            submit();
          }}
          className="flex items-end gap-2 rounded-2xl border border-line-strong bg-surface p-2 shadow-card transition-colors focus-within:border-accent focus-within:ring-2 focus-within:ring-accent/25"
        >
          <label htmlFor="question" className="sr-only">
            Ask a question about MSC
          </label>
          <textarea
            id="question"
            ref={textareaRef}
            value={draft}
            rows={1}
            placeholder="Ask a question about MSC"
            onChange={(event) => setDraft(event.target.value)}
            onKeyDown={(event) => {
              if (event.key === "Enter" && !event.shiftKey && !event.nativeEvent.isComposing) {
                event.preventDefault();
                submit();
              }
            }}
            className="max-h-40 min-h-11 flex-1 resize-none bg-transparent px-3 py-2.5 text-base leading-6 text-ink [field-sizing:content] placeholder:text-faint focus-visible:outline-none"
          />
          {streaming ? (
            <button
              type="button"
              onClick={stop}
              className="flex h-11 shrink-0 cursor-pointer items-center gap-2 rounded-xl border border-line-strong bg-sunken px-4 text-sm font-semibold text-ink transition-colors hover:border-ink"
            >
              <span aria-hidden="true" className="size-2.5 rounded-[2px] bg-ink" />
              Stop
            </button>
          ) : (
            <button
              type="submit"
              disabled={!canSend}
              className="h-11 shrink-0 cursor-pointer rounded-xl bg-accent px-5 text-sm font-semibold text-accent-ink transition-opacity disabled:cursor-not-allowed disabled:opacity-40"
            >
              Ask
            </button>
          )}
        </form>
        <div className="mt-2 flex items-center justify-between gap-4 text-[11px] leading-snug text-faint">
          <p>Independent demonstration, not affiliated with MSC. Operational data is synthetic.</p>
          {hasConversation ? (
            <button
              type="button"
              onClick={onReset}
              className="shrink-0 cursor-pointer font-medium text-muted underline underline-offset-2 hover:text-ink"
            >
              New conversation
            </button>
          ) : null}
        </div>
      </div>
    </div>
  );
}
