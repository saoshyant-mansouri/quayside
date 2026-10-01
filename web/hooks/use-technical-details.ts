"use client";

import { useCallback, useSyncExternalStore } from "react";
import { TECHNICAL_DETAILS_STORAGE_KEY } from "@/lib/technical-details";

const listeners = new Set<() => void>();

function read(): boolean {
  try {
    return localStorage.getItem(TECHNICAL_DETAILS_STORAGE_KEY) === "1";
  } catch {
    return false;
  }
}

let current: boolean | null = null;

function getSnapshot(): boolean {
  current ??= read();
  return current;
}

function subscribe(listener: () => void) {
  listeners.add(listener);
  const onStorage = (event: StorageEvent) => {
    if (event.key === TECHNICAL_DETAILS_STORAGE_KEY) {
      current = read();
      listener();
    }
  };
  window.addEventListener("storage", onStorage);
  return () => {
    listeners.delete(listener);
    window.removeEventListener("storage", onStorage);
  };
}

export function useTechnicalDetails() {
  const enabled = useSyncExternalStore(subscribe, getSnapshot, () => false);

  const setEnabled = useCallback((next: boolean) => {
    current = next;
    try {
      localStorage.setItem(TECHNICAL_DETAILS_STORAGE_KEY, next ? "1" : "0");
    } catch {}
    listeners.forEach((listener) => listener());
  }, []);

  return { enabled, setEnabled };
}
