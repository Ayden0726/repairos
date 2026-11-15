/**
 * Integration workflow against a live app + Postgres.
 * Skipped unless TECHBENCH_BASE_URL is set (e.g. http://127.0.0.1:3847).
 *
 * Credentials come from the environment — never hardcode secrets in source.
 */
import { describe, expect, it } from "vitest";
import { readFileSync } from "fs";
import path from "path";

const base = process.env.TECHBENCH_BASE_URL;
const email = process.env.TECHBENCH_TEST_EMAIL || process.env.SEED_OWNER_EMAIL;
const password =
  process.env.TECHBENCH_TEST_PASSWORD || process.env.SEED_OWNER_PASSWORD;
const pin = process.env.TECHBENCH_TEST_PIN || process.env.SEED_OWNER_PIN;

function cookieFrom(res: Response): string {
  const anyHeaders = res.headers as Headers & { getSetCookie?: () => string[] };
  const parts =
    typeof anyHeaders.getSetCookie === "function"
      ? anyHeaders.getSetCookie()
      : [res.headers.get("set-cookie") || ""].filter(Boolean);
  return parts.map((c) => c.split(";")[0]).join("; ");
}

describe.skipIf(!base || !email || !password)("M1 workflow", () => {
  it("rejects bad login and requires auth", async () => {
    const bad = await fetch(`${base}/api/auth/login`, {
      method: "POST",
      headers: { "Content-Type": "application/json" },
      body: JSON.stringify({
        mode: "password",
        email,
        password: "not-the-real-password-xx",
      }),
    });
    expect(bad.status).toBe(401);

    const unauth = await fetch(`${base}/api/tickets`);
    expect(unauth.status).toBe(401);
  });

  it("PIN login works when configured", async () => {
    if (!pin) return;
    const res = await fetch(`${base}/api/auth/login`, {
      method: "POST",
      headers: { "Content-Type": "application/json" },
      body: JSON.stringify({ mode: "pin", pin }),
    });
    expect(res.status).toBe(200);
    expect(cookieFrom(res)).toContain("techbench_session");
  });

  it("login → customer → device → intake → note → status → photo → search", async () => {
    const login = await fetch(`${base}/api/auth/login`, {
      method: "POST",
      headers: { "Content-Type": "application/json" },
      body: JSON.stringify({
        mode: "password",
        email,
        password,
      }),
    });
    expect(login.status).toBe(200);
    const cookie = cookieFrom(login);
    expect(cookie).toContain("techbench_session");

    const headers = {
      "Content-Type": "application/json",
      Cookie: cookie,
    };

    const phone = `04${String(Date.now()).slice(-8)}`;
    const custRes = await fetch(`${base}/api/customers`, {
      method: "POST",
      headers,
      body: JSON.stringify({
        firstName: "Casey",
        lastName: "Brooks",
        phone,
        email: "casey.brooks@example.com",
      }),
    });
    expect(custRes.status).toBe(201);
    const { customer } = await custRes.json();

    const search = await fetch(
      `${base}/api/customers?q=${encodeURIComponent(phone)}`,
      { headers: { Cookie: cookie } },
    );
    expect(search.status).toBe(200);
    const found = await search.json();
    expect(found.customers.some((c: { id: string }) => c.id === customer.id)).toBe(
      true,
    );

    const deviceRes = await fetch(`${base}/api/devices`, {
      method: "POST",
      headers,
      body: JSON.stringify({
        customerId: customer.id,
        category: "phone",
        brand: "Samsung",
        model: "Galaxy A54",
        imei: "359827061234567",
      }),
    });
    expect(deviceRes.status).toBe(201);
    const { device } = await deviceRes.json();

    const ticketRes = await fetch(`${base}/api/tickets`, {
      method: "POST",
      headers,
      body: JSON.stringify({
        customerId: customer.id,
        deviceId: device.id,
        reportedFault: "Won't charge; cable and port look clean.",
        conditionPowersOn: true,
        conditionCracks: false,
        accessoryCharger: true,
        labourFeeAud: "35.00",
        estimateAud: "89.00",
      }),
    });
    expect(ticketRes.status).toBe(201);
    const { ticket } = await ticketRes.json();
    expect(ticket.labourFeeCents).toBe("3500");
    expect(ticket.estimateCents).toBe("8900");
    expect(ticket.status).toBe("received");
    expect(ticket.ticketNumber).toMatch(/^TB-\d{4}-\d{6}$/);

    const noteRes = await fetch(`${base}/api/tickets/${ticket.id}/notes`, {
      method: "POST",
      headers,
      body: JSON.stringify({ body: "Customer will call after 3pm." }),
    });
    expect(noteRes.status).toBe(201);

    const statusRes = await fetch(`${base}/api/tickets/${ticket.id}/status`, {
      method: "POST",
      headers,
      body: JSON.stringify({
        status: "diagnosing",
        comment: "On the bench",
      }),
    });
    expect(statusRes.status).toBe(200);

    const png = Buffer.from(
      "iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAYAAAAfFcSJAAAADUlEQVR42mP8z8BQDwAEhQGAhKmMIQAAAABJRU5ErkJggg==",
      "base64",
    );
    const form = new FormData();
    form.append(
      "file",
      new Blob([png], { type: "image/png" }),
      "intake.png",
    );
    const photoRes = await fetch(`${base}/api/tickets/${ticket.id}/photos`, {
      method: "POST",
      headers: { Cookie: cookie },
      body: form,
    });
    expect(photoRes.status).toBe(201);
    const { photo } = await photoRes.json();

    const photoGet = await fetch(
      `${base}/api/tickets/${ticket.id}/photos/${photo.id}`,
      { headers: { Cookie: cookie } },
    );
    expect(photoGet.status).toBe(200);

    const photoUnauth = await fetch(
      `${base}/api/tickets/${ticket.id}/photos/${photo.id}`,
    );
    expect(photoUnauth.status).toBe(401);

    const list = await fetch(
      `${base}/api/tickets?q=${encodeURIComponent(ticket.ticketNumber)}`,
      { headers: { Cookie: cookie } },
    );
    const listed = await list.json();
    expect(listed.tickets.some((t: { id: string }) => t.id === ticket.id)).toBe(
      true,
    );

    const detail = await fetch(`${base}/api/tickets/${ticket.id}`, {
      headers: { Cookie: cookie },
    });
    const d = await detail.json();
    expect(d.ticket.notes.length).toBeGreaterThan(0);
    expect(d.ticket.statusHistory.length).toBeGreaterThanOrEqual(2);

    const exe = new FormData();
    exe.append(
      "file",
      new Blob([readFileSync(path.join(__dirname, "workflow.test.ts"))], {
        type: "application/octet-stream",
      }),
      "evil.exe",
    );
    const badPhoto = await fetch(`${base}/api/tickets/${ticket.id}/photos`, {
      method: "POST",
      headers: { Cookie: cookie },
      body: exe,
    });
    expect(badPhoto.status).toBe(400);
  });
});
