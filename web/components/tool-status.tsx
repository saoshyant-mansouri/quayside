import { Check } from "lucide-react";
import type { ToolEvent } from "@/lib/types";

const LABELS: Record<string, { active: string; done: string }> = {
  search_knowledge: { active: "Searching the knowledge base…", done: "Searched the knowledge base" },
  query_database: { active: "Writing SQL…", done: "Wrote and checked SQL" },
  track_container: { active: "Looking up the container…", done: "Looked up the container" },
  find_schedules: { active: "Searching sailing schedules…", done: "Searched sailing schedules" },
  get_vessel: { active: "Looking up the vessel…", done: "Looked up the vessel" },
  get_port: { active: "Looking up the port…", done: "Looked up the port" },
};

function labelFor(tool: ToolEvent): string {
  const known = LABELS[tool.name];
  if (known) return tool.status === "completed" ? known.done : known.active;
  const readable = tool.name.replaceAll("_", " ");
  return tool.status === "completed" ? `Used ${readable}` : `Running ${readable}…`;
}

type ToolStatusProps = { tools: ToolEvent[]; waiting: boolean };

export function ToolStatus({ tools, waiting }: ToolStatusProps) {
  return (
    <ul className="flex min-h-6 flex-wrap items-center gap-x-5 gap-y-1 text-[13px] text-normal-text">
      {tools.length === 0 && waiting ? (
        <li className="flex items-center gap-2">
          <span className="flex size-3 items-center justify-center">
            <span className="size-1.5 rounded-full bg-olive motion-safe:animate-pulse" />
          </span>
          Working…
        </li>
      ) : null}
      {tools.map((tool, index) => (
        <li key={`${tool.name}-${index}`} className="flex items-center gap-2">
          <span className="flex size-3 items-center justify-center">
            {tool.status === "completed" ? (
              <Check aria-hidden="true" size={12} strokeWidth={2.5} className="text-foreground" />
            ) : (
              <span className="size-1.5 rounded-full bg-olive motion-safe:animate-pulse" />
            )}
          </span>
          <span>{labelFor(tool)}</span>
        </li>
      ))}
    </ul>
  );
}
