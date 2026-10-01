import { Github } from "lucide-react";
import { AUTHOR_NAME, AUTHOR_URL, REPOSITORY_URL } from "@/lib/site";

const linkClass =
  "font-medium text-foreground underline decoration-olive decoration-2 underline-offset-4 hover:bg-olive/25";

export function Footer() {
  return (
    <footer className="border-t border-foreground/10 bg-background px-4 py-8 text-sm text-normal-text sm:px-6">
      <div className="mx-auto flex max-w-5xl flex-col gap-3 sm:flex-row sm:items-center sm:justify-between">
        <p>
          Built by{" "}
          <a
            href={AUTHOR_URL}
            target="_blank"
            rel="noopener noreferrer me"
            className={linkClass}
          >
            {AUTHOR_NAME}
          </a>
        </p>
        <a
          href={REPOSITORY_URL}
          target="_blank"
          rel="noopener noreferrer"
          className="flex items-center gap-1.5 opacity-80 transition-opacity hover:text-foreground hover:opacity-100"
        >
          <Github aria-hidden="true" size={16} />
          Source on GitHub
        </a>
      </div>
    </footer>
  );
}
