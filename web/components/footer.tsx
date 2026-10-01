import { ArrowUpRight, Github } from "lucide-react";
import { TechnicalDetailsToggle } from "./technical-details-toggle";
import { AUTHOR_NAME, AUTHOR_URL, REPOSITORY_URL, SITE_NAME } from "@/lib/site";

export function Footer() {
  return (
    <footer className="border-t border-foreground/10 bg-background px-4 py-8 text-sm text-normal-text sm:px-6">
      <div className="mx-auto flex max-w-5xl flex-col gap-4 md:flex-row md:items-end md:justify-between">
        <div className="space-y-1.5">
          <p className="font-display text-lg uppercase leading-none text-foreground">{SITE_NAME}</p>
          <p>
            Built by{" "}
            <a
              href={AUTHOR_URL}
              target="_blank"
              rel="noopener noreferrer me"
              className="font-medium text-foreground underline decoration-olive decoration-2 underline-offset-4 hover:bg-olive/25"
            >
              {AUTHOR_NAME}
            </a>
            . A retrieval-augmented generation system on Azure, in C# and .NET.
          </p>
        </div>
        <div className="flex flex-col gap-3 md:items-end">
          <ul className="flex flex-wrap items-center gap-x-6 gap-y-2">
            <li>
              <a
                href={REPOSITORY_URL}
                target="_blank"
                rel="noopener noreferrer"
                className="flex items-center gap-1.5 opacity-80 transition-opacity hover:text-foreground hover:opacity-100"
              >
                <Github aria-hidden="true" size={16} />
                Source on GitHub
              </a>
            </li>
            <li>
              <a
                href={AUTHOR_URL}
                target="_blank"
                rel="noopener noreferrer me"
                className="flex items-center gap-1.5 opacity-80 transition-opacity hover:text-foreground hover:opacity-100"
              >
                mhdmansouri.com
                <ArrowUpRight aria-hidden="true" size={16} />
              </a>
            </li>
          </ul>
          <TechnicalDetailsToggle />
        </div>
      </div>
    </footer>
  );
}
