"use client";

import { useRef, useState } from "react";
import { Button } from "@/components/ui/button";

export function SignaturePad({ ticketId }: { ticketId: string }) {
  const canvasRef = useRef<HTMLCanvasElement>(null);
  const [busy, setBusy] = useState(false);
  const [ok, setOk] = useState(false);

  function pos(e: React.PointerEvent<HTMLCanvasElement>) {
    const canvas = canvasRef.current!;
    const r = canvas.getBoundingClientRect();
    return { x: e.clientX - r.left, y: e.clientY - r.top };
  }

  return (
    <div>
      <canvas
        ref={canvasRef}
        width={520}
        height={160}
        className="w-full touch-none rounded-md border border-input bg-background"
        onPointerDown={(e) => {
          const ctx = canvasRef.current?.getContext("2d");
          if (!ctx) return;
          const { x, y } = pos(e);
          ctx.beginPath();
          ctx.moveTo(x, y);
          (e.target as HTMLCanvasElement).setPointerCapture(e.pointerId);
        }}
        onPointerMove={(e) => {
          if (e.buttons !== 1) return;
          const ctx = canvasRef.current?.getContext("2d");
          if (!ctx) return;
          const { x, y } = pos(e);
          ctx.lineTo(x, y);
          ctx.strokeStyle = "#0f172a";
          ctx.lineWidth = 2;
          ctx.stroke();
        }}
      />
      <div className="mt-2 flex gap-2">
        <Button
          type="button"
          variant="outline"
          size="sm"
          onClick={() => {
            const ctx = canvasRef.current?.getContext("2d");
            if (ctx && canvasRef.current) ctx.clearRect(0, 0, canvasRef.current.width, canvasRef.current.height);
            setOk(false);
          }}
        >
          Clear
        </Button>
        <Button
          type="button"
          size="sm"
          disabled={busy}
          onClick={async () => {
            const canvas = canvasRef.current;
            if (!canvas) return;
            setBusy(true);
            await fetch(`/api/tickets/${ticketId}/signature`, {
              method: "POST",
              headers: { "content-type": "application/json" },
              body: JSON.stringify({ image: canvas.toDataURL("image/png"), purpose: "PICKUP" }),
            });
            setBusy(false);
            setOk(true);
          }}
        >
          Save pickup signature
        </Button>
        {ok ? <span className="self-center text-xs text-muted-foreground">Saved</span> : null}
      </div>
    </div>
  );
}
