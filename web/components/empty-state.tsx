"use client";

import { useEffect, useState } from "react";
import { fetchExamples } from "@/lib/api";
import type { Example } from "@/lib/types";

type ExamplesState = { kind: "loading" } | { kind: "ready"; examples: Example[] } | { kind: "down" };

type EmptyStateProps = { onAsk: (question: string) => void };

export function EmptyState({ onAsk }: EmptyStateProps) {
  const [state, setState] = useState<ExamplesState>({ kind: "loading" });

  useEffect(() => {
    const controller = new AbortController();
    fetchExamples(controller.signal)
      .then((examples) => setState({ kind: "ready", examples }))
      .catch(() => {
        if (!controller.signal.aborted) setState({ kind: "down" });
      });
    return () => controller.abort();
  }, []);

  return (
    <section aria-labelledby="intro-heading" className="pb-8 pt-10 sm:pt-16">
      <h1
        id="intro-heading"
        className="max-w-2xl font-serif text-[2rem] font-normal leading-[1.12] tracking-tight sm:text-5xl"
      >
        Ask about MSC. See where every answer comes from.
      </h1>
      <p className="mt-5 max-w-xl text-base text-muted">
        Answers are drawn only from MSC&rsquo;s public website and LinkedIn posts. Each claim
        carries a numbered source you can open, and when the assistant writes SQL, the query and
        its result are shown rather than hidden.
      </p>

      <h2 className="mb-3 mt-10 text-xs font-semibold uppercase tracking-[0.08em] text-faint">
        Try one
      </h2>

      {state.kind === "loading" ? (
        <div aria-hidden="true" className="grid gap-3 sm:grid-cols-2">
          {[0, 1, 2, 3].map((slot) => (
            <div
              key={slot}
              className="h-[104px] rounded-xl border border-line bg-surface motion-safe:animate-pulse"
            />
          ))}
        </div>
      ) : null}

      {state.kind === "down" ? (
        <p className="rounded-xl border border-line bg-surface p-4 text-sm text-muted">
          Example questions are unavailable right now. You can still type your own below.
        </p>
      ) : null}

      {state.kind === "ready" ? (
        <ul className="grid gap-3 sm:grid-cols-2">
          {state.examples.map((example) => (
            <li key={example.question}>
              <button
                type="button"
                onClick={() => onAsk(example.question)}
                className="group flex h-full w-full cursor-pointer flex-col items-start gap-3 rounded-xl border border-line bg-surface p-4 text-left shadow-card transition-[border-color,transform] duration-150 hover:border-accent motion-safe:hover:-translate-y-px"
              >
                <span className="rounded-full bg-accent-soft px-2.5 py-0.5 text-[11px] font-semibold uppercase tracking-[0.06em] text-accent">
                  {example.label}
                </span>
                <span className="text-[15px] leading-snug text-ink">{example.question}</span>
              </button>
            </li>
          ))}
        </ul>
      ) : null}
    </section>
  );
}
