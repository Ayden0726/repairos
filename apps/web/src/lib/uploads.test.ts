import { describe, expect, it } from "vitest";
import { sanitizeOriginalName } from "./uploads";

describe("sanitizeOriginalName", () => {
  it("strips path-like and odd characters", () => {
    expect(sanitizeOriginalName("../../etc/passwd")).toBe(".._.._etc_passwd");
    expect(sanitizeOriginalName("cracked screen.jpg")).toBe("cracked screen.jpg");
  });
});
