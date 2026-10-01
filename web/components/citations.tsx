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
      className="group flex gap-3 rounded-xl border border-line bg-surface p-3 shadow-card transition-[border-color,background-color,box-shadow] duration-200 hover:border-accent data-[active=true]:border-accent data-[active=true]:bg-accent-soft data-[active=true]:shadow-[0_0_0_3px_var(--accent-soft)]"
    >
      <span
        aria-hidden="true"
        className="mt-0.5 flex size-6 shrink-0 items-center justify-center rounded-md bg-ink font-mono text-xs font-semibold text-bg"
      >
        {citation.n}
      </span>
      <span className="min-w-0 flex-1">
        <span className="block text-[14px] font-medium leading-snug text-ink group-hover:underline group-hover:underline-offset-2">
          {citation.title}
        </span>
        <span className="mt-1.5 flex flex-wrap items-center gap-x-2 gap-y-1 text-xs text-muted">
          <span className="rounded bg-sunken px-1.5 py-0.5 font-semibold uppercase tracking-[0.05em] text-ink">
            {sourceLabel}
          </span>
          {published ? <span>{published}</span> : null}
          {host ? <span className="truncate text-faint">{host}</span> : null}
        </span>
        {settled ? (
          <span
            className={`mt-1.5 block text-[11px] font-medium ${citedInAnswer ? "text-ok" : "text-faint"}`}
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
    <section aria-label="Sources" className="min-w-0">
      <h3 className="mb-2.5 flex items-baseline justify-between text-xs font-semibold uppercase tracking-[0.08em] text-faint">
        <span>Sources</span>
        <span className="font-medium normal-case tracking-normal">
          {plural(citations.length, "document")}
        </span>
      </h3>
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
    </section>
  );
}
