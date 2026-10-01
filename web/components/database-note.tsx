import type { SqlResult } from "@/lib/types";

export function DatabaseNote({ result }: { result: SqlResult }) {
  if (result.rejected) {
    return (
      <p className="tone-sage rounded-xl px-4 py-3 text-sm leading-snug">
        <strong className="font-semibold">That question could not be run.</strong> Only read-only
        questions over approved demo data are allowed, so nothing was changed.
      </p>
    );
  }
  if (result.rows.length === 0) {
    return (
      <p className="text-[15px] leading-snug text-normal-text">
        The demo database has no matching records for that.
      </p>
    );
  }
  return null;
}
