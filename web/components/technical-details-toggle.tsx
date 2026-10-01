"use client";

import { useEffect, useState } from "react";
import { useTechnicalDetails } from "@/hooks/use-technical-details";
import { fetchHealth } from "@/lib/api";
import { formatCount, formatDay, plural } from "@/lib/format";
import type { Health } from "@/lib/types";

function CorpusStats() {
  const [health, setHealth] = useState<Health | null>(null);

  useEffect(() => {
    const controller = new AbortController();
    fetchHealth(controller.signal)
      .then(setHealth)
      .catch(() => {});
    return () => controller.abort();
  }, []);

  if (!health) return null;
  const captured = formatDay(health.corpusCapturedAt);
  return (
    <p role="status" className="text-xs text-normal-text md:text-right">
      <span className="font-mono tabular-nums">
        {formatCount(health.chunks)} chunks · {plural(health.documents, "document")}
      </span>
      {captured ? <> · corpus captured {captured}</> : null}
      {health.indexWarm ? null : <> · index warming up</>}
    </p>
  );
}

export function TechnicalDetailsToggle() {
  const { enabled, setEnabled } = useTechnicalDetails();

  return (
    <div className="flex flex-col gap-1.5 md:items-end">
      <button
        type="button"
        role="switch"
        aria-checked={enabled}
        onClick={() => setEnabled(!enabled)}
        className="flex cursor-pointer items-center gap-2.5 rounded-full py-1 text-sm text-normal-text transition-colors hover:text-foreground"
      >
        <span
          aria-hidden="true"
          className={`relative h-5 w-9 shrink-0 rounded-full border transition-colors ${enabled ? "border-olive bg-olive" : "border-foreground/30 bg-transparent"}`}
        >
          <span
            className={`absolute left-0.5 top-0.5 size-3.5 rounded-full transition-transform duration-150 ${enabled ? "translate-x-4 bg-ink" : "bg-foreground/50"}`}
          />
        </span>
        Technical details
      </button>
      {enabled ? <CorpusStats /> : null}
    </div>
  );
}
