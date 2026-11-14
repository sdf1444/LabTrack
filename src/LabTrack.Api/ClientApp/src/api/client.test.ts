import { z } from "zod";
import { afterEach, expect, test, vi } from "vitest";
import { getCurrentUser, getSamples, NetworkError } from "./client";

afterEach(() => {
  vi.unstubAllGlobals();
});

test("rejects malformed API responses instead of trusting a TypeScript-only contract", async () => {
  vi.stubGlobal(
    "fetch",
    vi.fn<typeof fetch>().mockResolvedValue(
      new Response(JSON.stringify([{ id: "not-a-guid" }]), {
        status: 200,
        headers: { "Content-Type": "application/json" },
      }),
    ),
  );

  await expect(getSamples({ search: "", status: "" })).rejects.toBeInstanceOf(z.ZodError);
});

test("preserves Problem Details messages and HTTP status for authentication failures", async () => {
  vi.stubGlobal(
    "fetch",
    vi.fn<typeof fetch>().mockResolvedValue(
      new Response(JSON.stringify({ title: "Invalid email or password." }), {
        status: 401,
        headers: { "Content-Type": "application/problem+json" },
      }),
    ),
  );

  await expect(getCurrentUser()).rejects.toMatchObject({
    message: "Invalid email or password.",
    status: 401,
    category: "authentication",
  });
});

test.each([
  [403, "authorization"],
  [422, "validation"],
  [409, "conflict"],
  [500, "server"],
] as const)("categorizes HTTP %i responses as %s", async (status, category) => {
  vi.stubGlobal(
    "fetch",
    vi.fn<typeof fetch>().mockResolvedValue(
      new Response(JSON.stringify({ title: "Request rejected." }), {
        status,
        headers: { "Content-Type": "application/problem+json" },
      }),
    ),
  );

  await expect(getCurrentUser()).rejects.toMatchObject({
    status,
    category,
  });
});

test("turns fetch connection failures into a user-actionable network error", async () => {
  vi.stubGlobal("fetch", vi.fn<typeof fetch>().mockRejectedValue(new TypeError("Failed to fetch")));

  await expect(getCurrentUser()).rejects.toBeInstanceOf(NetworkError);
});
