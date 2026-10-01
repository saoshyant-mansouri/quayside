"use client";

import { memo, useCallback, useMemo, useRef, useState } from "react";
import type { AssistantMessage as AssistantMessageData } from "@/lib/chat-state";
import { citedNumbers } from "@/lib/citation-markers";
import { formatLatency, plural } from "@/lib/format";
import { AnswerMarkdown } from "./answer-markdown";
import { Citations } from "./citations";
import { SqlDisclosure } from "./sql-disclosure";
import { ToolStatus } from "./tool-status";

const OPERATIONAL_TOOLS = new Set([
  "query_database",
  "track_container",
  "find_schedules",
  "get_vessel",
  "get_port",
]);

function prefersReducedMotion(): boolean {
  return window.matchMedia("(prefers-reduced-motion: reduce)").matches;
}

function AnswerSkeleton() {
  return (
    <div aria-hidden="true" className="space-y-3 pt-1">
      <div className="h-4 w-11/12 rounded bg-line motion-safe:animate-pulse" />
      <div className="h-4 w-9/12 rounded bg-line motion-safe:animate-pulse" />
    </div>
  );
}

function PhaseNote({ message }: { message: AssistantMessageData }) {
  if (message.phase === "failed") {
    return (
      <p role="alert" className="rounded-xl border border-notice-line bg-notice-soft px-4 py-3 text-sm text-notice">
        <strong className="font-semibold">This answer could not be completed.</strong>{" "}
        {message.errorMessage}
      </p>
    );
  }
  if (message.phase === "stopped") {
    return <p className="text-sm text-muted">Stopped. What is shown above is incomplete.</p>;
  }
  if (message.phase === "interrupted") {
    return (
      <p className="text-sm text-muted">
        The connection closed before the answer finished, so it may be incomplete.
      </p>
    );
  }
  return null;
}

function MessageFooter({ message }: { message: AssistantMessageData }) {
  const { done, tools, sql, citations } = message;
  if (!done) return null;
  const uncited = done.grounding?.uncited ?? 0;
  const cited = done.grounding?.cited ?? 0;
  const usedOperationalData = Boolean(sql) || tools.some((tool) => OPERATIONAL_TOOLS.has(tool.name));

  return (
    <footer className="space-y-2.5 border-t border-line pt-3">
      <ul className="flex flex-wrap items-center gap-2 text-xs">
        <li className="flex items-center gap-1.5 rounded-full border border-line bg-surface px-2.5 py-1 text-muted">
          <svg width="12" height="12" viewBox="0 0 12 12" aria-hidden="true">
            <circle cx="6" cy="6" r="4.7" fill="none" stroke="currentColor" strokeWidth="1.3" />
            <path d="M6 3.4V6l1.8 1.1" fill="none" stroke="currentColor" strokeWidth="1.3" strokeLinecap="round" />
          </svg>
          <span>
            Answered in{" "}
            <span className="font-mono font-semibold tabular-nums text-ink">
              {formatLatency(done.latencyMs)}
            </span>
          </span>
        </li>
        {done.cached ? (
          <li
            title="Served from the semantic answer cache"
            className="rounded-full border border-accent/40 bg-accent-soft px-2.5 py-1 font-semibold text-accent"
          >
            Cached
          </li>
        ) : null}
        {uncited > 0 ? (
          <li className="rounded-full border border-notice-line bg-notice-soft px-2.5 py-1 font-semibold text-notice">
            {plural(uncited, "uncited statement")}
          </li>
        ) : cited > 0 ? (
          <li className="rounded-full border border-line bg-surface px-2.5 py-1 text-ok">
            Every statement cited ({cited})
          </li>
        ) : null}
        {citations.length === 0 ? (
          <li className="rounded-full border border-line bg-surface px-2.5 py-1 text-muted">
            No sources retrieved
          </li>
        ) : null}
      </ul>
      {uncited > 0 ? (
        <p className="text-[13px] leading-snug text-notice">
          {uncited === 1
            ? "One sentence in this answer could not be tied to a retrieved source."
            : `${uncited} sentences in this answer could not be tied to a retrieved source.`}{" "}
          Treat {uncited === 1 ? "it" : "them"} with caution.
        </p>
      ) : null}
      {usedOperationalData ? (
        <p className="text-[13px] leading-snug text-muted">
          Operational and shipment data in this answer is synthetic demonstration data.
        </p>
      ) : null}
    </footer>
  );
}

type AssistantMessageProps = { message: AssistantMessageData };

function AssistantMessageView({ message }: AssistantMessageProps) {
  const [activeNumber, setActiveNumber] = useState<number | null>(null);
  const cardsRef = useRef(new Map<number, HTMLAnchorElement>());
  const streaming = message.phase === "streaming";
  const hasCitations = message.citations.length > 0;
  const citedInAnswer = useMemo(() => citedNumbers(message.text), [message.text]);

  const registerCard = useCallback((n: number, element: HTMLAnchorElement | null) => {
    if (element) cardsRef.current.set(n, element);
    else cardsRef.current.delete(n);
  }, []);

  const selectCitation = useCallback((n: number) => {
    setActiveNumber(n);
    const card = cardsRef.current.get(n);
    if (!card) return;
    card.scrollIntoView({
      block: "nearest",
      behavior: prefersReducedMotion() ? "auto" : "smooth",
    });
    card.focus({ preventScroll: true });
  }, []);

  return (
    <article aria-busy={streaming} aria-label="Answer" className="space-y-4">
      <ToolStatus tools={message.tools} waiting={streaming && message.text === ""} />

      <div
        className={
          hasCitations ? "grid gap-x-10 gap-y-5 md:grid-cols-[minmax(0,1fr)_19rem]" : ""
        }
      >
        {hasCitations ? (
          <div className="md:order-2 md:self-start md:sticky md:top-4">
            <Citations
              citations={message.citations}
              citedInAnswer={citedInAnswer}
              activeNumber={activeNumber}
              settled={message.phase !== "streaming"}
              registerCard={registerCard}
            />
          </div>
        ) : null}

        <div className="min-w-0 space-y-5 md:order-1">
          {message.text === "" && streaming ? <AnswerSkeleton /> : null}
          {message.text !== "" ? (
            <AnswerMarkdown
              text={message.text}
              citations={message.citations}
              streaming={streaming}
              activeNumber={activeNumber}
              onSelectCitation={selectCitation}
            />
          ) : null}
          {message.sql ? <SqlDisclosure result={message.sql} /> : null}
          <PhaseNote message={message} />
          <MessageFooter message={message} />
        </div>
      </div>
    </article>
  );
}

export const AssistantMessage = memo(AssistantMessageView);
