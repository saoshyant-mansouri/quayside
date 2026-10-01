"use client";

import { ShieldCheck } from "lucide-react";
import { memo, useCallback, useRef, useState } from "react";
import type { AssistantMessage as AssistantMessageData } from "@/lib/chat-state";
import { OPERATIONAL_TOOLS, isBareEmptyResult, isRefusal } from "@/lib/refusal";
import { AnswerMarkdown } from "./answer-markdown";
import { Citations } from "./citations";
import { DatabaseNote } from "./database-note";
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

function MessageFooter({ message }: { message: AssistantMessageData }) {
  const { done, tools, sql } = message;
  if (!done) return null;
  const uncited = done.grounding?.uncited ?? 0;
  const usedOperationalData =
    Boolean(sql) || tools.some((tool) => OPERATIONAL_TOOLS.has(tool.name));

  if (uncited === 0 && !usedOperationalData) return null;

  return (
    <footer className="space-y-2.5 border-t border-border pt-3">
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
  const streaming = message.phase === "streaming";
  const hasCitations = message.citations.length > 0;

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
          {message.sql ? <DatabaseNote result={message.sql} /> : null}
          {isRefusal(message) ? <RefusalNote /> : null}
          <PhaseNote message={message} />
          <MessageFooter message={message} />
        </div>

        {hasCitations ? (
          <div className="min-w-0 md:sticky md:top-24 md:col-start-2 md:row-start-1 md:self-start">
            <Citations
              citations={message.citations}
              activeNumber={activeNumber}
              registerCard={registerCard}
            />
          </div>
        ) : null}
      </div>
    </article>
  );
}

export const AssistantMessage = memo(AssistantMessageView);
