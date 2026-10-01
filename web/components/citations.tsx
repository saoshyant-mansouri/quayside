import { forwardRef } from "react";
import { formatDay, hostOf, plural } from "@/lib/format";
import type { Citation } from "@/lib/types";

type CitationsProps = {
  citations: Citation[];
  citedInAnswer: Set<number>;
  activeNumber: number | null;
  settled: boolean;
  registerCard: (n: number, element: HTMLAnchorElement | null) => void;
};

const SOURCE_LABEL: Record<Citation["source"], string> = {
  linkedin: "LinkedIn",
  website: "Website",
};

type CardProps = {
  citation: Citation;
  active: boolean;
  citedInAnswer: boolean;
  settled: boolean;
};

const CitationCard = forwardRef<HTMLAnchorElement, CardProps>(function CitationCard(
  { citation, active, citedInAnswer, settled },
  ref,
) {
  const published = formatDay(citation.publishedAt);
  const host = hostOf(citation.url);
  const sourceLabel = SOURCE_LABEL[citation.source] ?? citation.source;

  return (
    <a
      ref={ref}
      href={citation.url}
      target="_blank"
      rel="noopener noreferrer"
      data-active={active}
      aria-label={`Source ${citation.n}: ${citation.title}, ${sourceLabel}${published ? `, ${published}` : ""}. Opens in a new tab.`}
      className="group flex gap-3 rounded-xl border border-border bg-background p-3 transition-[border-color,background-color,box-shadow] duration-200 hover:border-foreground/40 data-[active=true]:border-olive data-[active=true]:bg-olive/15 data-[active=true]:ring-2 data-[active=true]:ring-olive/40"
    >
      <span
        aria-hidden="true"
        className="mt-0.5 flex size-6 shrink-0 items-center justify-center rounded-md bg-foreground font-mono text-xs font-semibold text-background"
      >
        {citation.n}
      </span>
      <span className="min-w-0 flex-1">
        <span className="block text-[14px] font-medium leading-snug text-foreground group-hover:underline group-hover:underline-offset-2">
          {citation.title}
        </span>
        <span className="mt-1.5 flex flex-wrap items-center gap-x-2 gap-y-1 text-xs text-normal-text">
          <span className="surface-sunken rounded px-1.5 py-0.5 font-semibold uppercase tracking-[0.05em] text-foreground">
            {sourceLabel}
          </span>
          {published ? <span>{published}</span> : null}
          {host ? <span className="truncate text-foreground/60">{host}</span> : null}
        </span>
        {settled ? (
          <span
            className={`mt-1.5 block text-[11px] font-medium ${citedInAnswer ? "text-foreground" : "text-foreground/60"}`}
          >
            {citedInAnswer ? "Cited in the answer" : "Retrieved, not cited"}
          </span>
        ) : null}
      </span>
    </a>
  );
});

export function Citations({
  citations,
  citedInAnswer,
  activeNumber,
  settled,
  registerCard,
}: CitationsProps) {
  return (
    <div className="min-w-0">
      <h3 className="mb-2.5 flex items-baseline justify-between text-xs font-semibold uppercase tracking-[0.08em] text-foreground/60">
        <span>Sources</span>
        <span className="font-medium normal-case tracking-normal">
          {plural(citations.length, "document")}
        </span>
      </h3>
      <div
        role="region"
        tabIndex={0}
        aria-label="Sources"
        data-source-list
        className="scroll-themed scroll-mb-40 scroll-mt-24 -mx-1 max-h-80 overflow-y-auto px-1 py-1 md:max-h-[calc(100svh-17rem)]"
      >
        <ol className="flex flex-col gap-2.5">
          {citations.map((citation) => (
            <li key={citation.n}>
              <CitationCard
                ref={(element) => registerCard(citation.n, element)}
                citation={citation}
                active={activeNumber === citation.n}
                citedInAnswer={citedInAnswer.has(citation.n)}
                settled={settled}
              />
            </li>
          ))}
        </ol>
      </div>
    </div>
  );
}
