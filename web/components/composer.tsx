"use client";

import { ArrowUp, Square } from "lucide-react";
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
    <div data-composer className="sticky bottom-0 z-10 border-t border-foreground/5 bg-background md:bg-background/80 md:backdrop-blur-md">
      <div className="mx-auto max-w-5xl px-4 pb-[max(0.75rem,env(safe-area-inset-bottom))] pt-3 sm:px-6">
        <form
          aria-label="Ask a question"
          onSubmit={(event) => {
            event.preventDefault();
            submit();
          }}
          className="flex items-end gap-2 rounded-3xl border border-foreground/20 bg-background p-2 transition-colors focus-within:border-foreground focus-within:ring-2 focus-within:ring-olive/50"
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
            className="max-h-40 min-h-11 flex-1 resize-none bg-transparent px-3 py-2.5 text-base leading-6 text-foreground [field-sizing:content] placeholder:text-foreground/60 focus-visible:outline-none"
          />
          {streaming ? (
            <button
              type="button"
              onClick={stop}
              className="flex h-11 shrink-0 cursor-pointer items-center gap-2 rounded-full border border-foreground/20 px-4 text-sm font-semibold text-foreground transition-colors hover:bg-foreground/5"
            >
              <Square aria-hidden="true" size={12} fill="currentColor" />
              Stop
            </button>
          ) : (
            <button
              type="submit"
              disabled={!canSend}
              className="tone-olive flex h-11 shrink-0 cursor-pointer items-center gap-1.5 rounded-full px-5 text-sm font-semibold transition-[filter,opacity] hover:brightness-95 disabled:cursor-not-allowed disabled:opacity-40"
            >
              Ask
              <ArrowUp aria-hidden="true" size={16} />
            </button>
          )}
        </form>
        <div className="mt-2 flex items-center justify-between gap-4 text-[11px] leading-snug text-foreground/60">
          <p>Independent demonstration, not affiliated with MSC. Operational data is synthetic.</p>
          {hasConversation ? (
            <button
              type="button"
              onClick={onReset}
              className="shrink-0 cursor-pointer font-medium text-normal-text underline underline-offset-2 hover:text-foreground"
            >
              New conversation
            </button>
          ) : null}
        </div>
      </div>
    </div>
  );
}
