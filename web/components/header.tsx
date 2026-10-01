import Link from "next/link";
import { AUTHOR_URL, REPOSITORY_URL, SITE_NAME } from "@/lib/site";
import { ThemeToggle } from "./theme-toggle";

const linkClass =
  "text-sm font-medium opacity-70 transition-colors hover:text-foreground hover:opacity-100";

export function Header() {
  return (
    <header>
      <nav
        aria-label="Main"
        className="fixed left-0 top-0 z-50 flex w-full items-center justify-between gap-4 border-b border-foreground/5 bg-background px-6 py-4 text-foreground md:bg-background/80 md:backdrop-blur-md"
      >
        <Link href="/" className="text-xl font-bold tracking-tight transition-opacity hover:opacity-80">
          {SITE_NAME}
        </Link>
        <div className="flex items-center gap-4 md:gap-6">
          <ul className="hidden items-center gap-6 md:flex">
            <li>
              <a href={REPOSITORY_URL} target="_blank" rel="noopener noreferrer" className={linkClass}>
                GitHub
              </a>
            </li>
            <li>
              <a href={AUTHOR_URL} target="_blank" rel="noopener noreferrer me" className={linkClass}>
                Saoshyant Mansouri
              </a>
            </li>
          </ul>
          <ThemeToggle />
        </div>
      </nav>
      <div className="tone-blush mt-[4.25rem]">
        <p className="mx-auto max-w-5xl px-4 py-2.5 text-[13px] leading-snug sm:px-6">
          <strong className="font-semibold">Independent technical demonstration.</strong> Not
          affiliated with, endorsed by, or operated by MSC. Answers come only from public
          sources and cite each one. Any shipment or operational data shown is synthetic.
        </p>
      </div>
    </header>
  );
}
