"use client";

import { ArrowUpRight } from "lucide-react";
import { useEffect, useState } from "react";
import { fetchExamples } from "@/lib/api";
import type { Example } from "@/lib/types";

type ExamplesState =
  { kind: "loading" } | { kind: "ready"; examples: Example[] } | { kind: "down" };

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
        className="font-display max-w-3xl text-[1.7rem] uppercase leading-[1.02] sm:text-4xl md:text-5xl"
      >
        Ask about MSC. See where every answer comes from.
      </h1>
      <p className="mt-5 max-w-xl text-base text-normal-text">
        Answers are drawn only from MSC&rsquo;s public website and LinkedIn posts, and each claim
        carries a numbered source you can open. Questions about shipments and ports are answered
        from a demo database of synthetic data.
      </p>

      <h2 className="mb-4 mt-10 text-xs font-semibold uppercase tracking-[0.08em] text-foreground/60">
        Try one
      </h2>

      {state.kind === "loading" ? (
        <div aria-hidden="true" className="grid gap-3 sm:grid-cols-2">
          {[0, 1, 2, 3].map((slot) => (
            <div
              key={slot}
              className="h-[96px] rounded-xl border border-border bg-foreground/5 motion-safe:animate-pulse"
            />
          ))}
        </div>
      ) : null}

      {state.kind === "down" ? (
        <p className="rounded-xl border border-border p-4 text-sm text-normal-text">
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
                className="group flex h-full w-full cursor-pointer flex-col items-start gap-3 rounded-xl border border-border p-4 text-left transition-[border-color,background-color] duration-150 hover:border-olive hover:bg-olive/10"
              >
                <span className="flex w-full items-center justify-between gap-3">
                  <span className="rounded-full bg-foreground px-3 py-1 text-xs font-bold uppercase tracking-[0.06em] text-background">
                    {example.label}
                  </span>
                  <ArrowUpRight
                    aria-hidden="true"
                    size={18}
                    className="shrink-0 text-foreground/40 transition-colors group-hover:text-foreground"
                  />
                </span>
                <span className="text-[15px] leading-snug text-foreground">{example.question}</span>
              </button>
            </li>
          ))}
        </ul>
      ) : null}
    </section>
  );
}
