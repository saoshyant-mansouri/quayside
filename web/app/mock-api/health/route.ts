import { mockDisabled } from "../guard";

export function GET() {
  const disabled = mockDisabled();
  if (disabled) return disabled;
  return Response.json({
    status: "ok",
    chunks: 2841,
    documents: 173,
    corpusCapturedAt: "2026-10-01T12:00:00Z",
    indexWarm: true,
  });
}
