import { QueryClient, QueryClientProvider } from "@tanstack/react-query";
import { render, screen } from "@testing-library/react";
import userEvent from "@testing-library/user-event";
import { MemoryRouter } from "react-router-dom";
import { afterEach, expect, test, vi } from "vitest";
import App from "./App";

const currentUser = {
  id: "c6636575-56cc-472c-9079-97a959442880",
  email: "labadmin@example.test",
  roles: ["LabAdmin"],
};

function jsonResponse(body: unknown, status = 200): Response {
  return new Response(JSON.stringify(body), {
    status,
    headers: { "Content-Type": "application/json" },
  });
}

afterEach(() => {
  vi.unstubAllGlobals();
});

test("signs in, opens the sample dashboard, and returns to login after sign-out", async () => {
  const fetchMock = vi.fn<typeof fetch>(async (input, init) => {
    const path = new URL(String(input), "http://localhost").pathname;
    const method = init?.method ?? "GET";

    if (path === "/api/auth/me") return jsonResponse({ title: "Unauthorized" }, 401);
    if (path === "/api/auth/login" && method === "POST") return jsonResponse(currentUser);
    if (path === "/api/auth/logout" && method === "POST")
      return new Response(null, { status: 204 });
    if (path === "/api/samples") return jsonResponse([]);
    return jsonResponse({ title: "Not found" }, 404);
  });
  vi.stubGlobal("fetch", fetchMock);

  const queryClient = new QueryClient({
    defaultOptions: { queries: { retry: false, staleTime: 30_000 }, mutations: { retry: false } },
  });
  const user = userEvent.setup();
  render(
    <QueryClientProvider client={queryClient}>
      <MemoryRouter initialEntries={["/"]}>
        <App />
      </MemoryRouter>
    </QueryClientProvider>,
  );

  await user.type(await screen.findByLabelText("Email"), currentUser.email);
  await user.type(screen.getByLabelText("Password"), "StrongPassword123!");
  await user.click(screen.getByRole("button", { name: "Sign in" }));
  expect(await screen.findByRole("heading", { name: "Sample workflow" })).toBeInTheDocument();

  await user.click(screen.getByRole("button", { name: "Sign out" }));
  expect(await screen.findByRole("heading", { name: "Sign in to LabTrack" })).toBeInTheDocument();
  expect(fetchMock).toHaveBeenCalledWith(
    "/api/auth/logout",
    expect.objectContaining({ method: "POST", credentials: "same-origin" }),
  );
});
