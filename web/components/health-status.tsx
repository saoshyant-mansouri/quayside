"use client";

import { useEffect, useState } from "react";
import { fetchHealth } from "@/lib/api";
import { formatCount, formatDay, plural } from "@/lib/format";
import type { Health } from "@/lib/types";

type HealthState = { kind: "loading" } | { kind: "ready"; health: Health } | { kind: "down" };

export function HealthStatus() {
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
      <p role="status" className="flex items-center gap-2 text-xs text-foreground/60">
        <span className="size-2 rounded-full bg-foreground/20 motion-safe:animate-pulse" />
        Checking service
      </p>
    );
  }

  if (state.kind === "down") {
    return (
      <p role="status" className="flex items-center gap-2 text-xs text-normal-text">
        <span className="size-2 rounded-full bg-blush ring-1 ring-foreground/30" />
        Service unreachable
      </p>
    );
  }

  const { health } = state;
  const captured = formatDay(health.corpusCapturedAt);
  return (
    <p
      role="status"
      className="flex items-center gap-2 text-xs text-normal-text"
      title={`${plural(health.documents, "document")}${health.indexWarm ? "" : ", index warming up"}`}
    >
      <span
        className={`size-2 shrink-0 rounded-full ${health.indexWarm ? "bg-olive" : "bg-blush ring-1 ring-foreground/30"}`}
      />
      <span>
        <span className="font-medium text-foreground">{formatCount(health.chunks)}</span> chunks
        {captured ? (
          <>
            <span className="hidden lg:inline"> · corpus captured {captured}</span>
            <span className="hidden sm:inline lg:hidden"> · {captured}</span>
          </>
        ) : null}
      </span>
    </p>
  );
}
