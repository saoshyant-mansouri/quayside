"use client";

import { Moon, Sun } from "lucide-react";
import { useTheme } from "@/hooks/use-theme";

export function ThemeToggle() {
  const { theme, toggleTheme, mounted } = useTheme();
  const label = mounted && theme === "dark" ? "Switch to light theme" : "Switch to dark theme";

  return (
    <button
      type="button"
      onClick={toggleTheme}
      aria-label={mounted ? label : "Switch theme"}
      className="cursor-pointer rounded-full p-2 text-foreground transition-colors hover:bg-foreground/5"
    >
      <Sun aria-hidden="true" size={20} className="hidden dark:block" />
      <Moon aria-hidden="true" size={20} className="dark:hidden" />
    </button>
  );
}
