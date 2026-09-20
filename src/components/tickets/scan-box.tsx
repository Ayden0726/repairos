"use client";

import { useRouter } from "next/navigation";
import { useState } from "react";
import { Input } from "@/components/ui/input";
import { Button } from "@/components/ui/button";

export function ScanBox() {
  const [value, setValue] = useState("");
  const router = useRouter();
  return (
    <form
      onSubmit={async (e) => {
        e.preventDefault();
        const res = await fetch(`/api/scan?code=${encodeURIComponent(value)}`);
        const json = await res.json();
        if (json.id) router.push(`/tickets/${json.id}`);
      }}
      className="space-y-3"
    >
      <Input
        autoFocus
        value={value}
        onChange={(e) => setValue(e.target.value)}
        placeholder="Scan or type ticket number"
      />
      <Button type="submit">Open ticket</Button>
    </form>
  );
}
