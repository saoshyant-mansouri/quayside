"use client";

import { useId, useState } from "react";
import { formatCell, plural } from "@/lib/format";
import type { SqlResult } from "@/lib/types";

function Chevron({ open }: { open: boolean }) {
  return (
    <svg
      width="14"
      height="14"
      viewBox="0 0 14 14"
      aria-hidden="true"
      className={`shrink-0 transition-transform duration-200 ${open ? "rotate-90" : ""}`}
    >
      <path
        d="m5 3 4 4-4 4"
        fill="none"
        stroke="currentColor"
        strokeWidth="1.6"
        strokeLinecap="round"
        strokeLinejoin="round"
      />
    </svg>
  );
}

function SyntheticBadge() {
  return (
    <span className="rounded bg-notice-soft px-1.5 py-0.5 text-[10px] font-semibold uppercase tracking-[0.06em] text-notice">
      Synthetic data
    </span>
  );
}

function ResultGrid({ columns, rows }: Pick<SqlResult, "columns" | "rows">) {
  if (rows.length === 0) {
    return (
      <p className="px-4 py-3 text-sm text-muted">
        The query ran and matched no rows, so there is nothing to show.
      </p>
    );
  }
  const numericColumns = columns.map((_, index) => typeof rows[0]?.[index] === "number");
  return (
    <div
      role="region"
      tabIndex={0}
      aria-label="Query result"
      className="max-h-80 overflow-auto border-t border-line"
    >
      <table className="w-full border-collapse text-left font-mono text-[12.5px]">
        <caption className="sr-only">{plural(rows.length, "row")} returned</caption>
        <thead className="sticky top-0 bg-sunken">
          <tr>
            {columns.map((column, index) => (
              <th
                key={column}
                scope="col"
                className={`whitespace-nowrap border-b border-line px-3 py-2 font-semibold text-muted ${numericColumns[index] ? "text-right" : ""}`}
              >
                {column}
              </th>
            ))}
          </tr>
        </thead>
        <tbody>
          {rows.map((row, rowIndex) => (
            <tr key={rowIndex} className="odd:bg-surface even:bg-bg/60">
              {row.map((cell, cellIndex) => (
                <td
                  key={cellIndex}
                  title={formatCell(cell)}
                  className={`max-w-[28ch] truncate whitespace-nowrap px-3 py-1.5 ${typeof cell === "number" ? "text-right tabular-nums" : ""} ${cell === null ? "text-faint" : "text-ink"}`}
                >
                  {formatCell(cell)}
                </td>
              ))}
            </tr>
          ))}
        </tbody>
      </table>
    </div>
  );
}

function SqlBlock({ sql }: { sql: string }) {
  return (
    <pre className="overflow-x-auto bg-code px-4 py-3.5 font-mono text-[12.5px] leading-relaxed text-code-ink">
      <code>{sql}</code>
    </pre>
  );
}

type RevealProps = { open: boolean; id: string; children: React.ReactNode };

function Reveal({ open, id, children }: RevealProps) {
  return (
    <div id={id} className="reveal" data-open={open} inert={!open}>
      <div>{children}</div>
    </div>
  );
}

type ToggleProps = {
  open: boolean;
  controls: string;
  label: string;
  onToggle: () => void;
  trailing?: React.ReactNode;
};

function Toggle({ open, controls, label, onToggle, trailing }: ToggleProps) {
  return (
    <button
      type="button"
      onClick={onToggle}
      aria-expanded={open}
      aria-controls={controls}
      className="flex w-full cursor-pointer flex-wrap items-center gap-x-2 gap-y-1.5 px-4 py-3 text-left text-sm font-medium text-ink"
    >
      <Chevron open={open} />
      <span className="whitespace-nowrap">{label}</span>
      {trailing ? <span className="ml-auto flex items-center gap-2 whitespace-nowrap text-xs text-muted">{trailing}</span> : null}
    </button>
  );
}

export function SqlDisclosure({ result }: { result: SqlResult }) {
  const [open, setOpen] = useState(false);
  const panelId = useId();
  const toggle = () => setOpen((current) => !current);

  if (result.rejected) {
    return (
      <section
        aria-label="Query refused by the safety check"
        className="overflow-hidden rounded-xl border border-refusal/40 bg-refusal-soft"
      >
        <div className="px-4 pt-4">
          <h3 className="text-sm font-semibold text-refusal">
            The safety check stopped this query before it ran
          </h3>
          <p className="mt-1.5 text-[15px] leading-snug text-ink">{result.rejected}</p>
          <p className="mt-2 text-[13px] leading-snug text-muted">
            Nothing was executed. Only a single read-only SELECT over approved tables is ever
            allowed, so a refusal is the guardrail working as designed.
          </p>
        </div>
        <Toggle
          open={open}
          controls={panelId}
          label="Show the SQL it tried to write"
          onToggle={toggle}
        />
        <Reveal open={open} id={panelId}>
          <SqlBlock sql={result.sql} />
        </Reveal>
      </section>
    );
  }

  return (
    <section
      aria-label="Generated SQL and result"
      className="overflow-hidden rounded-xl border border-line bg-surface shadow-card"
    >
      <Toggle
        open={open}
        controls={panelId}
        label={open ? "Hide the SQL it wrote" : "Show the SQL it wrote"}
        onToggle={toggle}
        trailing={
          <>
            <span>{plural(result.rows.length, "row")}</span>
            <SyntheticBadge />
          </>
        }
      />
      <Reveal open={open} id={panelId}>
        <SqlBlock sql={result.sql} />
        <ResultGrid columns={result.columns} rows={result.rows} />
      </Reveal>
    </section>
  );
}
