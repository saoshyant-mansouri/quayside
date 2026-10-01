"use client";

import { Clock, ShieldCheck } from "lucide-react";
import { memo, useCallback, useMemo, useRef, useState } from "react";
import { useTechnicalDetails } from "@/hooks/use-technical-details";
import type { AssistantMessage as AssistantMessageData } from "@/lib/chat-state";
import { citedNumbers } from "@/lib/citation-markers";
import { formatLatency, plural } from "@/lib/format";
import { OPERATIONAL_TOOLS, isBareEmptyResult, isRefusal } from "@/lib/refusal";
import { AnswerMarkdown } from "./answer-markdown";
import { Citations } from "./citations";
import { SqlDisclosure } from "./sql-disclosure";
import { ToolStatus } from "./tool-status";

const LIST_EDGE_PX = 8;
const NAVBAR_CLEARANCE_PX = 80;
const COMPOSER_CLEARANCE_PX = 140;

function prefersReducedMotion(): boolean {
  return window.matchMedia("(prefers-reduced-motion: reduce)").matches;
}

function PhaseNote({ message }: { message: AssistantMessageData }) {
  if (message.phase === "failed") {
    return (
      <p role="alert" className="tone-blush rounded-xl px-4 py-3 text-sm">
        <strong className="font-semibold">This answer could not be completed.</strong>{" "}
        {message.errorMessage}
      </p>
    );
  }
  if (message.phase === "stopped") {
    return <p className="text-sm text-normal-text">Stopped. What is shown above is incomplete.</p>;
  }
  if (message.phase === "interrupted") {
    return (
      <p className="text-sm text-normal-text">
        The connection closed before the answer finished, so it may be incomplete.
      </p>
    );
  }
  return null;
}

function RefusalNote() {
  return (
    <section
      aria-label="Why there is no answer"
      className="tone-sage flex gap-3 rounded-xl px-4 py-3 text-sm leading-snug"
    >
      <ShieldCheck aria-hidden="true" size={20} className="mt-0.5 shrink-0" />
      <p>
        <strong className="font-semibold">No source found. Answered without guessing.</strong>{" "}
        MSC&rsquo;s public sources do not contain this, and making it up would be worse than saying
        so.
      </p>
    </section>
  );
}

function MessageFooter({
  message,
  technical,
}: {
  message: AssistantMessageData;
  technical: boolean;
}) {
  const { done, tools, sql, citations } = message;
  if (!done) return null;
  const uncited = done.grounding?.uncited ?? 0;
  const cited = done.grounding?.cited ?? 0;
  const refused = isRefusal(message);
  const usedOperationalData =
    Boolean(sql) || tools.some((tool) => OPERATIONAL_TOOLS.has(tool.name));

  if (!technical && uncited === 0 && !usedOperationalData) return null;

  return (
    <footer className="space-y-2.5 border-t border-border pt-3">
      {technical ? (
        <ul className="flex flex-wrap items-center gap-2 text-xs">
          <li className="flex items-center gap-1.5 rounded-full border border-border px-2.5 py-1 text-normal-text">
            <Clock aria-hidden="true" size={12} />
            <span>
              Answered in{" "}
              <span className="font-mono font-semibold tabular-nums text-foreground">
                {formatLatency(done.latencyMs)}
              </span>
            </span>
          </li>
          {done.cached ? (
            <li
              title="Served from the semantic answer cache"
              className="tone-olive rounded-full px-2.5 py-1 font-semibold"
            >
              Cached
            </li>
          ) : null}
          {uncited > 0 ? (
            <li className="tone-blush rounded-full px-2.5 py-1 font-semibold">
              {plural(uncited, "uncited statement")}
            </li>
          ) : cited > 0 && !refused ? (
            <li className="tone-sage rounded-full px-2.5 py-1 font-medium">
              Every statement cited ({cited})
            </li>
          ) : null}
          {citations.length === 0 ? (
            <li className="rounded-full border border-border px-2.5 py-1 text-normal-text">
              No sources retrieved
            </li>
          ) : null}
        </ul>
      ) : null}
      {uncited > 0 ? (
        <p className="text-[13px] leading-snug text-foreground">
          {uncited === 1
            ? "One sentence in this answer could not be tied to a retrieved source."
            : `${uncited} sentences in this answer could not be tied to a retrieved source.`}{" "}
          Treat {uncited === 1 ? "it" : "them"} with caution.
        </p>
      ) : null}
      {usedOperationalData ? (
        <p className="text-[13px] leading-snug text-normal-text">
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
  const { enabled: technical } = useTechnicalDetails();
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
    const behavior = prefersReducedMotion() ? "auto" : "smooth";
    const list = card.closest<HTMLElement>("[data-source-list]");
    if (list) {
      const listBox = list.getBoundingClientRect();
      const cardBox = card.getBoundingClientRect();
      if (cardBox.top < listBox.top) {
        list.scrollBy({ top: cardBox.top - listBox.top - LIST_EDGE_PX, behavior });
      } else if (cardBox.bottom > listBox.bottom) {
        list.scrollBy({ top: cardBox.bottom - listBox.bottom + LIST_EDGE_PX, behavior });
      }
      const { top, bottom } = listBox;
      if (top < NAVBAR_CLEARANCE_PX || bottom > window.innerHeight - COMPOSER_CLEARANCE_PX) {
        list.scrollIntoView({ block: "nearest", behavior });
      }
    }
    card.focus({ preventScroll: true });
  }, []);

  return (
    <article aria-busy={streaming} aria-label="Answer" className="space-y-4">
      <ToolStatus tools={message.tools} waiting={streaming && message.text === ""} />

      <div
        className={hasCitations ? "grid gap-x-10 gap-y-6 md:grid-cols-[minmax(0,1fr)_19rem]" : ""}
      >
        <div className="min-w-0 space-y-5 md:col-start-1 md:row-start-1">
          {message.text !== "" && !isBareEmptyResult(message) ? (
            <AnswerMarkdown
              text={message.text}
              citations={message.citations}
              streaming={streaming}
              activeNumber={activeNumber}
              onSelectCitation={selectCitation}
            />
          ) : null}
          {message.sql ? <SqlDisclosure result={message.sql} technical={technical} /> : null}
          {isRefusal(message) ? <RefusalNote /> : null}
          <PhaseNote message={message} />
          <MessageFooter message={message} technical={technical} />
        </div>

        {hasCitations ? (
          <div className="min-w-0 md:sticky md:top-24 md:col-start-2 md:row-start-1 md:self-start">
            <Citations
              citations={message.citations}
              citedInAnswer={citedInAnswer}
              activeNumber={activeNumber}
              settled={technical && message.phase !== "streaming"}
              registerCard={registerCard}
            />
          </div>
        ) : null}
      </div>
    </article>
  );
}

export const AssistantMessage = memo(AssistantMessageView);
