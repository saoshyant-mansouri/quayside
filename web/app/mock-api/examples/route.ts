import { mockDisabled } from "../guard";

export function GET() {
  const disabled = mockDisabled();
  if (disabled) return disabled;
  return Response.json([
    { label: "Grounded RAG", question: "What is MSC's position on alternative marine fuels?" },
    { label: "NL to SQL", question: "Which five ports had the most import containers last quarter?" },
    { label: "Tool call", question: "Track container MSCU1234567." },
    { label: "Vessels", question: "Which vessels are in the demo fleet?" },
  ]);
}
