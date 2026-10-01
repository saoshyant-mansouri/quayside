const MARKER = /\[(\d{1,3})\]/g;
const CITE_PROTOCOL = "cite:";

type MarkdownNode = {
  type: string;
  value?: string;
  url?: string;
  children?: MarkdownNode[];
};

export const citeHref = (n: number) => `${CITE_PROTOCOL}${n}`;

export function citationNumberFromHref(href: string | undefined): number | null {
  if (!href?.startsWith(CITE_PROTOCOL)) return null;
  const n = Number(href.slice(CITE_PROTOCOL.length));
  return Number.isInteger(n) ? n : null;
}

export function withoutDanglingMarker(text: string): string {
  return text.replace(/\[\d{0,3}$/, "");
}

function splitTextNode(node: MarkdownNode, known: Set<number>): MarkdownNode[] {
  const value = node.value ?? "";
  const pieces: MarkdownNode[] = [];
  let cursor = 0;
  for (const match of value.matchAll(MARKER)) {
    const n = Number(match[1]);
    if (!known.has(n)) continue;
    if (match.index > cursor) pieces.push({ type: "text", value: value.slice(cursor, match.index) });
    pieces.push({
      type: "link",
      url: citeHref(n),
      children: [{ type: "text", value: match[0] }],
    });
    cursor = match.index + match[0].length;
  }
  if (pieces.length === 0) return [node];
  if (cursor < value.length) pieces.push({ type: "text", value: value.slice(cursor) });
  return pieces;
}

function linkMarkers(node: MarkdownNode, known: Set<number>) {
  if (!node.children) return;
  node.children = node.children.flatMap((child) => {
    if (child.type === "text") return splitTextNode(child, known);
    if (child.type !== "link") linkMarkers(child, known);
    return [child];
  });
}

export function remarkCitationMarkers(knownNumbers: number[]) {
  const known = new Set(knownNumbers);
  return () => (tree: MarkdownNode) => linkMarkers(tree, known);
}
