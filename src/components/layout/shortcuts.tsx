"use client";

import { useEffect } from "react";
import { useRouter } from "next/navigation";

export function Shortcuts({ defaultTicketView }: { defaultTicketView: string }) {
  const router = useRouter();
  useEffect(() => {
    const onKey = (e: KeyboardEvent) => {
      const tag = (e.target as HTMLElement)?.tagName;
      if (tag === "INPUT" || tag === "TEXTAREA" || (e.target as HTMLElement)?.isContentEditable) return;
      if (e.key.toLowerCase() === "n" && !e.metaKey && !e.ctrlKey) {
        e.preventDefault();
        router.push("/tickets/new");
      }
      if (e.key.toLowerCase() === "g") {
        const next = (ev: KeyboardEvent) => {
          if (ev.key.toLowerCase() === "t") router.push(defaultTicketView === "table" ? "/tickets" : "/tickets/board");
          if (ev.key.toLowerCase() === "c") router.push("/customers");
          if (ev.key.toLowerCase() === "d") router.push("/");
          window.removeEventListener("keydown", next);
        };
        window.addEventListener("keydown", next, { once: true });
      }
    };
    window.addEventListener("keydown", onKey);
    return () => window.removeEventListener("keydown", onKey);
  }, [router, defaultTicketView]);
  return null;
}
