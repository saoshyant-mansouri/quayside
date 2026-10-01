"use client";

import { useEffect, useReducer, useRef } from "react";
import { ApiError, streamChat } from "@/lib/api";
import { chatReducer, lastConversationId } from "@/lib/chat-state";
import { AssistantMessage } from "./assistant-message";
import { Composer } from "./composer";
import { EmptyState } from "./empty-state";

const NEAR_BOTTOM_PX = 160;
const MAIN_BOTTOM_PADDING_PX = 40;
let nextMessageId = 0;
const makeMessageId = () => `message-${(nextMessageId += 1)}`;

const followTarget = (root: HTMLElement, log: HTMLElement) => {
  const composer = root.querySelector("[data-composer]");
  return (
    log.getBoundingClientRect().bottom +
    window.scrollY +
    MAIN_BOTTOM_PADDING_PX +
    (composer?.getBoundingClientRect().height ?? 0)
  );
};

const isNearBottom = (root: HTMLElement, log: HTMLElement) =>
  window.innerHeight + window.scrollY >= followTarget(root, log) - NEAR_BOTTOM_PX;

export function Chat() {
  const [messages, dispatch] = useReducer(chatReducer, []);
  const rootRef = useRef<HTMLDivElement>(null);
  const logRef = useRef<HTMLDivElement>(null);
  const abortRef = useRef<AbortController | null>(null);
  const followStreamRef = useRef(true);
  const streaming = messages.some(
    (message) => message.role === "assistant" && message.phase === "streaming",
  );

  useEffect(() => {
    const onScroll = () => {
      if (rootRef.current && logRef.current) {
        followStreamRef.current = isNearBottom(rootRef.current, logRef.current);
      }
    };
    window.addEventListener("scroll", onScroll, { passive: true });
    return () => window.removeEventListener("scroll", onScroll);
  }, []);

  useEffect(() => () => abortRef.current?.abort(), []);

  useEffect(() => {
    if (messages.length > 0 && followStreamRef.current && rootRef.current && logRef.current) {
      window.scrollTo({ top: followTarget(rootRef.current, logRef.current) - window.innerHeight });
    }
  }, [messages]);

  async function send(question: string) {
    const text = question.trim();
    if (!text || streaming) return;

    const controller = new AbortController();
    abortRef.current = controller;
    const assistantId = makeMessageId();
    followStreamRef.current = true;
    dispatch({ type: "asked", userId: makeMessageId(), assistantId, question: text });

    try {
      const events = streamChat({
        message: text,
        conversationId: lastConversationId(messages),
        signal: controller.signal,
      });
      for await (const event of events) {
        dispatch({ type: "event", id: assistantId, event });
      }
      dispatch({ type: "closed", id: assistantId });
    } catch (error) {
      if (controller.signal.aborted) {
        dispatch({ type: "stopped", id: assistantId });
      } else {
        const message =
          error instanceof ApiError
            ? error.message
            : "The API could not be reached. Check your connection and try again.";
        dispatch({ type: "failed", id: assistantId, message });
      }
    } finally {
      if (abortRef.current === controller) abortRef.current = null;
    }
  }

  function stop() {
    abortRef.current?.abort();
  }

  function reset() {
    abortRef.current?.abort();
    dispatch({ type: "reset" });
  }

  return (
    <div ref={rootRef} className="flex flex-1 flex-col">
      <main className="mx-auto w-full max-w-5xl flex-1 px-4 pb-10 sm:px-6">
        {messages.length === 0 ? <EmptyState onAsk={send} /> : null}
        <div ref={logRef} role="log" aria-label="Conversation" className="flex flex-col gap-6 pt-8">
          {messages.map((message) =>
            message.role === "user" ? (
              <h2
                key={message.id}
                className="font-display mt-6 max-w-3xl border-t border-border pt-8 text-xl leading-tight first:mt-0 first:border-t-0 first:pt-0 sm:text-2xl"
              >
                <span className="sr-only">You asked: </span>
                {message.text}
              </h2>
            ) : (
              <AssistantMessage key={message.id} message={message} />
            ),
          )}
        </div>
      </main>
      <Composer
        streaming={streaming}
        hasConversation={messages.length > 0}
        onSend={send}
        onStop={stop}
        onReset={reset}
      />
    </div>
  );
}
