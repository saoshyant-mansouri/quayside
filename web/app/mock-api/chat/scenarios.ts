import type { Citation, SqlResult } from "@/lib/types";

export type Scenario = {
  tools: string[];
  citations: Citation[];
  sql: SqlResult | null;
  answer: string;
  grounding: { cited: number; uncited: number };
};

const mockCitations: Citation[] = [
  {
    n: 1,
    title: "Mock source: sustainability overview",
    url: "https://example.com/mock/sustainability-overview",
    source: "website",
    publishedAt: "2025-06-12",
  },
  {
    n: 2,
    title: "Mock source: fleet renewal announcement",
    url: "https://example.com/mock/fleet-renewal",
    source: "linkedin",
    publishedAt: "2025-11-04",
  },
  {
    n: 3,
    title: "Mock source: newbuild programme and dual-fuel design notes",
    url: "https://example.com/mock/newbuild-programme",
    source: "website",
    publishedAt: "2025-03-28",
  },
  {
    n: 4,
    title: "Mock source: shore power at berth",
    url: "https://example.com/mock/shore-power",
    source: "linkedin",
    publishedAt: null,
  },
];

export const fuelsScenario: Scenario = {
  tools: ["search_knowledge"],
  citations: mockCitations,
  sql: null,
  answer:
    "This is **scripted mock output** for testing the interface, not a real answer. " +
    "Alternative fuels are presented as one lever among several rather than a single bet [1]. " +
    "New vessels are described with dual-fuel capability [2][3], and the same material mentions `methanol-ready` design features [3].\n\n" +
    "- Dual-fuel newbuilds [2]\n- Biofuel blends on existing ships [1]\n- Shore power at berth [4]\n\n" +
    "A marker with no matching source, like [9], stays plain text.",
  grounding: { cited: 5, uncited: 0 },
};

export const portsScenario: Scenario = {
  tools: ["search_knowledge", "query_database"],
  citations: mockCitations.slice(0, 2),
  sql: {
    sql:
      "SELECT TOP (5) p.PortName, COUNT(*) AS ImportContainers\n" +
      "FROM dbo.PortCall pc\n" +
      "JOIN dbo.Port p ON p.PortId = pc.PortId\n" +
      "JOIN dbo.ContainerMovement cm ON cm.PortCallId = pc.PortCallId\n" +
      "WHERE cm.Direction = 'Import' AND pc.ArrivedAt >= '2026-07-01'\n" +
      "GROUP BY p.PortName\nORDER BY ImportContainers DESC;",
    columns: ["PortName", "ImportContainers"],
    rows: [
      ["Rotterdam", 18420],
      ["Antwerp", 15377],
      ["Valencia", 12904],
      ["Genoa", 9811],
      ["Le Havre", 8032],
    ],
    rejected: null,
  },
  answer:
    "Scripted mock output. In the synthetic demo database, Rotterdam had the most import containers last quarter at 18,420, followed by Antwerp and Valencia. " +
    "Background on how terminal throughput is reported comes from the public material [1].",
  grounding: { cited: 1, uncited: 0 },
};

export const trackScenario: Scenario = {
  tools: ["track_container"],
  citations: [],
  sql: null,
  answer:
    "Scripted mock output. Container MSCU1234567 is shown in the synthetic demo data as discharged at Antwerp and awaiting customs release. " +
    "Estimated gate-out is tomorrow morning.",
  grounding: { cited: 0, uncited: 2 },
};

export const refusalScenario: Scenario = {
  tools: ["search_knowledge"],
  citations: [],
  sql: null,
  answer:
    "I could not find MSC's net profit for 2024 in the public sources I have, so I will not guess a figure. " +
    "Try asking about something the published material covers, such as the fleet or sustainability commitments.",
  grounding: { cited: 0, uncited: 0 },
};

export const rejectedSqlScenario: Scenario = {
  tools: ["query_database"],
  citations: [],
  sql: {
    sql: "DELETE FROM dbo.Booking WHERE CreatedAt < '2020-01-01'",
    columns: [],
    rows: [],
    rejected: "Only a single SELECT statement is permitted; DELETE is not allowed.",
  },
  answer:
    "I cannot run that. The request would modify data, and this assistant only ever reads from the synthetic demo database.",
  grounding: { cited: 0, uncited: 0 },
};

export function pickScenario(message: string): Scenario {
  const text = message.toLowerCase();
  if (/\b(delete|drop|update|remove)\b/.test(text)) return rejectedSqlScenario;
  if (text.includes("port")) return portsScenario;
  if (text.includes("track") || text.includes("mscu")) return trackScenario;
  if (text.includes("profit")) return refusalScenario;
  return fuelsScenario;
}
