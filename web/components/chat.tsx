"use client";

import { useEffect, useReducer, useRef } from "react";
import { ApiError, streamChat } from "@/lib/api";
import { chatReducer, lastConversationId } from "@/lib/chat-state";
import { AssistantMessage } from "./assistant-message";
import { Composer } from "./composer";
import { EmptyState } from "./empty-state";
import { Header } from "./header";

const NEAR_BOTTOM_PX = 160;
let nextMessageId = 0;
const makeMessageId = () => `message-${(nextMessageId += 1)}`;

const isNearBottom = () =>
  window.innerHeight + window.scrollY >= document.documentElement.scrollHeight - NEAR_BOTTOM_PX;

export function Chat() {
  const [messages, dispatch] = useReducer(chatReducer, []);
  const abortRef = useRef<AbortController | null>(null);
  const followStreamRef = useRef(true);
  const streaming = messages.some(
    (message) => message.role === "assistant" && message.phase === "streaming",
  );

  useEffect(() => {
    const onScroll = () => {
      followStreamRef.current = isNearBottom();
    };
    window.addEventListener("scroll", onScroll, { passive: true });
    return () => window.removeEventListener("scroll", onScroll);
  }, []);

  useEffect(() => () => abortRef.current?.abort(), []);

  useEffect(() => {
    if (messages.length > 0 && followStreamRef.current) {
      window.scrollTo({ top: document.documentElement.scrollHeight });
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
    <div className="flex min-h-dvh flex-col">
      <Header />
      <main className="mx-auto w-full max-w-5xl flex-1 px-4 pb-10 sm:px-6">
        {messages.length === 0 ? <EmptyState onAsk={send} /> : null}
        <div role="log" aria-label="Conversation" className="flex flex-col gap-6 pt-8">
          {messages.map((message) =>
            message.role === "user" ? (
              <h2
                key={message.id}
                className="max-w-3xl mt-6 border-t border-line pt-8 font-serif text-[1.65rem] font-normal leading-tight tracking-tight first:mt-0 first:border-t-0 first:pt-0 sm:text-3xl"
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
