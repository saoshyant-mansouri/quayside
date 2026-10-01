"use client";

import { useEffect, useState } from "react";
import { fetchHealth } from "@/lib/api";
import { formatCount, formatDay, plural } from "@/lib/format";
import type { Health } from "@/lib/types";

type HealthState = { kind: "loading" } | { kind: "ready"; health: Health } | { kind: "down" };

function Wordmark() {
  return (
    <div className="flex items-center gap-2.5">
      <svg width="26" height="26" viewBox="0 0 26 26" aria-hidden="true" className="text-accent">
        <rect x="2" y="3" width="9" height="7" rx="1.5" fill="currentColor" />
        <rect x="13" y="3" width="11" height="7" rx="1.5" fill="currentColor" opacity="0.55" />
        <rect x="2" y="12" width="14" height="7" rx="1.5" fill="currentColor" opacity="0.55" />
        <rect x="18" y="12" width="6" height="7" rx="1.5" fill="currentColor" />
        <path d="M1 23.5h24" stroke="currentColor" strokeWidth="1.5" strokeLinecap="round" />
      </svg>
      <span className="font-serif text-[1.45rem] font-medium leading-none tracking-tight">
        Quayside
      </span>
    </div>
  );
}

function HealthStatus() {
  const [state, setState] = useState<HealthState>({ kind: "loading" });

  useEffect(() => {
    const controller = new AbortController();
    fetchHealth(controller.signal)
      .then((health) => setState({ kind: "ready", health }))
      .catch(() => {
        if (!controller.signal.aborted) setState({ kind: "down" });
      });
    return () => controller.abort();
  }, []);

  if (state.kind === "loading") {
    return (
      <p role="status" className="flex items-center gap-2 text-xs text-faint">
        <span className="size-2 rounded-full bg-line-strong motion-safe:animate-pulse" />
        Checking service
      </p>
    );
  }

  if (state.kind === "down") {
    return (
      <p role="status" className="flex items-center gap-2 text-xs text-notice">
        <span className="size-2 rounded-full bg-notice" />
        Service unreachable
      </p>
    );
  }

  const { health } = state;
  const captured = formatDay(health.corpusCapturedAt);
  return (
    <p
      role="status"
      className="flex items-center gap-2 text-xs text-muted"
      title={`${plural(health.documents, "document")}${health.indexWarm ? "" : ", index warming up"}`}
    >
      <span
        className={`size-2 shrink-0 rounded-full ${health.indexWarm ? "bg-ok" : "bg-notice"}`}
      />
      <span>
        <span className="font-medium text-ink">{formatCount(health.chunks)}</span> chunks
        {captured ? (
          <>
            <span className="hidden sm:inline"> · corpus captured {captured}</span>
            <span className="sm:hidden"> · {captured}</span>
          </>
        ) : null}
      </span>
    </p>
  );
}

export function Header() {
  return (
    <header>
      <div className="mx-auto flex h-16 max-w-5xl items-center justify-between gap-4 px-4 sm:px-6">
        <Wordmark />
        <HealthStatus />
      </div>
      <div className="border-y border-notice-line bg-notice-soft text-notice">
        <p className="mx-auto max-w-5xl px-4 py-2.5 text-[13px] leading-snug sm:px-6">
          <strong className="font-semibold">Independent technical demonstration.</strong> Not
          affiliated with, endorsed by, or operated by MSC. Answers come only from public
          sources and cite each one. Any shipment or operational data shown is synthetic.
        </p>
      </div>
    </header>
  );
}
