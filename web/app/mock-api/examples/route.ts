import { mockDisabled } from "../guard";

export function GET() {
  const disabled = mockDisabled();
  if (disabled) return disabled;
  return Response.json([
    { label: "Grounded RAG", question: "What is MSC's position on alternative marine fuels?" },
    { label: "NL to SQL", question: "Which five ports had the most import containers last quarter?" },
    { label: "Tool call", question: "Track container MSCU1234567." },
    { label: "Correct refusal", question: "What was MSC's net profit in 2024?" },
  ]);
}
