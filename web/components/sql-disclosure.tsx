"use client";

import { ChevronRight } from "lucide-react";
import { useId, useState } from "react";
import { formatCell, plural } from "@/lib/format";
import type { SqlResult } from "@/lib/types";

function Chevron({ open }: { open: boolean }) {
  return (
    <ChevronRight
      aria-hidden="true"
      size={14}
      strokeWidth={2.2}
      className={`shrink-0 transition-transform duration-200 ${open ? "rotate-90" : ""}`}
    />
  );
}

function SyntheticBadge() {
  return (
    <span className="tone-blush rounded px-1.5 py-0.5 text-[10px] font-semibold uppercase tracking-[0.06em]">
      Synthetic data
    </span>
  );
}

function ResultGrid({ columns, rows }: Pick<SqlResult, "columns" | "rows">) {
  if (rows.length === 0) return null;
  const numericColumns = columns.map((_, index) => typeof rows[0]?.[index] === "number");
  return (
    <div
      role="region"
      tabIndex={0}
      aria-label="Query result"
      className="max-h-80 scroll-themed overflow-auto border-t border-border"
    >
      <table className="w-full border-collapse text-left font-mono text-[12.5px]">
        <caption className="sr-only">{plural(rows.length, "row")} returned</caption>
        <thead className="surface-sunken sticky top-0">
          <tr>
            {columns.map((column, index) => (
              <th
                key={column}
                scope="col"
                className={`whitespace-nowrap border-b border-border px-3 py-2 font-semibold text-normal-text ${numericColumns[index] ? "text-right" : ""}`}
              >
                {column}
              </th>
            ))}
          </tr>
        </thead>
        <tbody>
          {rows.map((row, rowIndex) => (
            <tr key={rowIndex} className="even:bg-foreground/[0.03]">
              {row.map((cell, cellIndex) => (
                <td
                  key={cellIndex}
                  title={formatCell(cell)}
                  className={`max-w-[28ch] truncate whitespace-nowrap px-3 py-1.5 ${typeof cell === "number" ? "text-right tabular-nums" : ""} ${cell === null ? "text-foreground/60" : "text-foreground"}`}
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
    <pre className="tone-ink scroll-themed overflow-x-auto px-4 py-3.5 font-mono text-[12.5px] leading-relaxed">
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
      className="flex w-full cursor-pointer flex-wrap items-center gap-x-2 gap-y-1.5 px-4 py-3 text-left text-sm font-medium"
    >
      <Chevron open={open} />
      <span className="whitespace-nowrap">{label}</span>
      {trailing ? (
        <span className="ml-auto flex items-center gap-2 whitespace-nowrap text-xs text-normal-text">
          {trailing}
        </span>
      ) : null}
    </button>
  );
}

export function NoRecordsNote() {
  return (
    <p className="text-[15px] leading-snug text-normal-text">
      The demo database has no matching records for that.
    </p>
  );
}

export function SqlDisclosure({ result, technical }: { result: SqlResult; technical: boolean }) {
  const [open, setOpen] = useState(false);
  const panelId = useId();
  const toggle = () => setOpen((current) => !current);

  if (result.rejected) {
    return (
      <section
        aria-label="Query refused by the safety check"
        className="tone-sage overflow-hidden rounded-xl"
      >
        <div className="px-4 pt-4">
          <h3 className="text-sm font-semibold">
            The safety check stopped this query before it ran
          </h3>
          <p className="mt-1.5 text-[15px] leading-snug">{result.rejected}</p>
          <p className="mt-2 text-[13px] leading-snug opacity-80">
            Nothing was run. Only read-only questions over approved data are ever allowed, so this
            is the safety check working as designed.
          </p>
        </div>
        {technical ? (
          <>
            <Toggle
              open={open}
              controls={panelId}
              label="Show the SQL it tried to write"
              onToggle={toggle}
            />
            <Reveal open={open} id={panelId}>
              <SqlBlock sql={result.sql} />
            </Reveal>
          </>
        ) : (
          <div className="pb-4" />
        )}
      </section>
    );
  }

  const empty = result.rows.length === 0;
  if (!technical) return empty ? <NoRecordsNote /> : null;

  return (
    <>
      {empty ? <NoRecordsNote /> : null}
      <section
        aria-label="Generated SQL and result"
        className="overflow-hidden rounded-xl border border-border"
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
    </>
  );
}
