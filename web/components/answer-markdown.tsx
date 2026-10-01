import { memo, useMemo } from "react";
import ReactMarkdown, { type Components } from "react-markdown";
import rehypeSanitize, { defaultSchema } from "rehype-sanitize";
import remarkGfm from "remark-gfm";
import {
  citationNumberFromHref,
  remarkCitationMarkers,
  withoutDanglingMarker,
} from "@/lib/citation-markers";
import type { Citation } from "@/lib/types";

const sanitizeSchema = {
  ...defaultSchema,
  protocols: { ...defaultSchema.protocols, href: ["http", "https", "mailto", "cite"] },
};

const rehypePlugins = [[rehypeSanitize, sanitizeSchema]] as Parameters<
  typeof ReactMarkdown
>[0]["rehypePlugins"];

const EXTERNAL_HREF = /^(https?:|mailto:)/i;

type AnswerMarkdownProps = {
  text: string;
  citations: Citation[];
  streaming: boolean;
  activeNumber: number | null;
  onSelectCitation: (n: number) => void;
};

function AnswerMarkdownView({
  text,
  citations,
  streaming,
  activeNumber,
  onSelectCitation,
}: AnswerMarkdownProps) {
  const citationNumbers = citations.map((citation) => citation.n);
  const numbersKey = citationNumbers.join(",");

  const remarkPlugins = useMemo(
    () => [remarkGfm, remarkCitationMarkers(numbersKey ? numbersKey.split(",").map(Number) : [])],
    [numbersKey],
  );

  const components = useMemo<Components>(() => {
    const titleOf = new Map(citations.map((citation) => [citation.n, citation.title]));
    return {
      a({ href, children }) {
        const n = citationNumberFromHref(href);
        if (n !== null) {
          return (
            <button
              type="button"
              onClick={() => onSelectCitation(n)}
              aria-label={`Source ${n}: ${titleOf.get(n) ?? ""}`}
              aria-pressed={activeNumber === n}
              className="ml-0.5 inline-flex min-w-[1.6em] cursor-pointer items-center justify-center rounded-md bg-olive/25 px-1 align-baseline text-[0.68em] font-semibold leading-[1.7] text-foreground transition-colors hover:bg-olive hover:text-ink aria-pressed:bg-olive aria-pressed:text-ink"
            >
              {children}
            </button>
          );
        }
        if (href && EXTERNAL_HREF.test(href)) {
          return (
            <a href={href} target="_blank" rel="noopener noreferrer">
              {children}
            </a>
          );
        }
        return <span>{children}</span>;
      },
    };
  }, [citations, activeNumber, onSelectCitation]);

  return (
    <div className="answer-prose">
      <ReactMarkdown
        remarkPlugins={remarkPlugins}
        rehypePlugins={rehypePlugins}
        components={components}
        disallowedElements={["img"]}
        urlTransform={(url) => url}
      >
        {streaming ? withoutDanglingMarker(text) : text}
      </ReactMarkdown>
    </div>
  );
}

export const AnswerMarkdown = memo(AnswerMarkdownView);
