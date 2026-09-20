export type ChargeInput = {
  amount: number;
  currency: "AUD";
  reference: string;
  description: string;
};

export type ChargeResult = {
  ok: boolean;
  pending?: boolean;
  providerRef?: string;
  error?: string;
};

export interface PaymentProvider {
  key: string;
  charge(input: ChargeInput): Promise<ChargeResult>;
}

class ManualProvider implements PaymentProvider {
  key = "manual";
  async charge(input: ChargeInput): Promise<ChargeResult> {
    return { ok: true, providerRef: `manual-${input.reference}-${Date.now()}` };
  }
}

class SquareProvider implements PaymentProvider {
  key = "square";
  constructor(
    private accessToken: string,
    private locationId: string,
    private environment: "sandbox" | "production",
  ) {}
  async charge(input: ChargeInput): Promise<ChargeResult> {
    const base =
      this.environment === "production" ? "https://connect.squareup.com" : "https://connect.squareupsandbox.com";
    const res = await fetch(`${base}/v2/payments`, {
      method: "POST",
      headers: {
        "content-type": "application/json",
        authorization: `Bearer ${this.accessToken}`,
        "Square-Version": "2024-12-18",
      },
      body: JSON.stringify({
        idempotency_key: `${input.reference}-${Date.now()}`,
        amount_money: { amount: Math.round(input.amount * 100), currency: "AUD" },
        location_id: this.locationId,
        note: input.description,
        autocomplete: false,
      }),
    });
    const json = (await res.json()) as { payment?: { id: string; status: string }; errors?: Array<{ detail: string }> };
    if (!res.ok) {
      return { ok: false, error: json.errors?.[0]?.detail ?? "Square payment failed." };
    }
    const status = json.payment?.status;
    if (status === "COMPLETED") return { ok: true, providerRef: json.payment?.id };
    if (status === "PENDING" || status === "APPROVED") {
      return { ok: false, pending: true, providerRef: json.payment?.id, error: "Waiting for Square confirmation." };
    }
    return { ok: false, error: `Square status ${status}` };
  }
}

export function manualPayments(): PaymentProvider {
  return new ManualProvider();
}

export function squarePayments(accessToken: string, locationId: string, environment: "sandbox" | "production"): PaymentProvider {
  return new SquareProvider(accessToken, locationId, environment);
}
