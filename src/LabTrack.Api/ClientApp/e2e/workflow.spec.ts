import AxeBuilder from "@axe-core/playwright";
import { expect, test, type Page } from "@playwright/test";

const user = {
  id: "c6636575-56cc-472c-9079-97a959442880",
  email: "labadmin@example.test",
  roles: ["LabAdmin"],
};

const sampleId = "8d48f99a-673a-4c44-9601-cef842b388b2";
const createdAt = "2026-03-16T12:05:00Z";

test("registers and processes a sample, then signs out to the login page", async ({ page }) => {
  let authenticated = false;
  let sample: Record<string, unknown> | undefined;

  await page.route("**/api/auth/me", async (route) => {
    await route.fulfill(
      authenticated
        ? { status: 200, json: user }
        : { status: 401, json: { title: "Unauthorized" } },
    );
  });
  await page.route("**/api/auth/login", async (route) => {
    authenticated = true;
    await route.fulfill({ status: 200, json: user });
  });
  await page.route("**/api/auth/logout", async (route) => {
    authenticated = false;
    await route.fulfill({ status: 204 });
  });
  await page.route("**/api/samples**", async (route) => {
    const request = route.request();
    const path = new URL(request.url()).pathname;

    if (path === "/api/samples" && request.method() === "GET") {
      await route.fulfill({
        status: 200,
        json: sample ? [sampleSummary(sample)] : [],
      });
      return;
    }
    if (path === "/api/samples" && request.method() === "POST") {
      const payload = request.postDataJSON() as {
        accessionNumber: string;
        patientReference: string;
        specimenType: string;
        collectedAt: string;
      };
      sample = {
        id: sampleId,
        ...payload,
        status: "Received",
        resultCount: 0,
        createdAt,
        updatedAt: createdAt,
        revision: 1,
        results: [],
        events: [
          {
            id: "2f43813e-3077-4851-a2c7-118ed0bdf15e",
            eventType: "Registered",
            description: "Sample registered",
            occurredAt: createdAt,
          },
        ],
      };
      await route.fulfill({ status: 201, json: sample });
      return;
    }
    if (path === `/api/samples/${sampleId}` && request.method() === "GET" && sample) {
      await route.fulfill({ status: 200, json: sample });
      return;
    }
    if (path === `/api/samples/${sampleId}/status` && request.method() === "PATCH" && sample) {
      const payload = request.postDataJSON() as { status: string };
      sample = {
        ...sample,
        status: payload.status,
        revision: Number(sample.revision) + 1,
        updatedAt: new Date().toISOString(),
      };
      await route.fulfill({ status: 200, json: sample });
      return;
    }
    if (path === `/api/samples/${sampleId}/results` && request.method() === "POST" && sample) {
      const payload = request.postDataJSON() as {
        analyteCode: string;
        analyteName: string;
        value: number;
        unit: string;
        referenceLow: number | null;
        referenceHigh: number | null;
      };
      const result = {
        id: "238bb314-ec12-4aa3-94c5-a25355d7d2a0",
        ...payload,
        flag: "High",
        recordedAt: new Date().toISOString(),
      };
      sample = {
        ...sample,
        revision: Number(sample.revision) + 1,
        resultCount: 1,
        updatedAt: new Date().toISOString(),
        results: [result],
      };
      await route.fulfill({ status: 200, json: sample });
      return;
    }
    await route.fulfill({ status: 404, json: { title: "Not found" } });
  });

  await page.goto("/");
  const loginHeading = page.getByRole("heading", { name: "Sign in to LabTrack" });
  await expect(loginHeading).toBeVisible();
  await expect(loginHeading).toBeFocused();
  await expectNoAccessibilityViolations(page);
  await page.keyboard.press("Tab");
  await expect(page.getByLabel("Email")).toBeFocused();
  await page.keyboard.type(user.email);
  await page.keyboard.press("Tab");
  await expect(page.getByLabel("Password")).toBeFocused();
  await page.keyboard.type("StrongPassword123!");
  await page.keyboard.press("Tab");
  await expect(page.getByRole("button", { name: "Sign in" })).toBeFocused();
  await page.keyboard.press("Enter");
  await expect(page.getByRole("heading", { name: "Sample workflow" })).toBeVisible();
  await expect(page.getByText("No samples found")).toBeVisible();
  await expectNoAccessibilityViolations(page);

  await page.getByRole("button", { name: "Register sample" }).click();
  const dialog = page.getByRole("dialog", { name: "Register a sample" });
  await expect(dialog).toBeVisible();
  await expect(page.getByRole("button", { name: "Close" })).toBeFocused();
  await expectNoAccessibilityViolations(page);
  await page.keyboard.press("Tab");
  await expect(page.getByLabel("Accession number")).toBeFocused();
  await page.keyboard.type("LAB-E2E-0042");
  await page.getByRole("textbox", { name: "Patient reference", exact: true }).fill("SYN-E2E-42");
  await page.getByLabel("Specimen type").selectOption("Serum");
  await page.getByRole("button", { name: "Register sample" }).last().click();
  await expect(page.getByRole("button", { name: "Register sample" }).first()).toBeFocused();
  await expect(page.getByRole("heading", { name: "LAB-E2E-0042" })).toBeVisible();
  await expect(page.locator(".toast.show")).toHaveCSS("opacity", "1");
  await expectNoAccessibilityViolations(page);

  await page.getByLabel("Filter by status").selectOption("Received");
  await page.getByRole("button", { name: "LAB-E2E-0042 SYN-E2E-42", exact: true }).click();
  await page.getByRole("button", { name: "Start processing" }).click();
  await expect(page.getByText("Select a sample")).toBeVisible();
  await page.getByLabel("Filter by status").selectOption("");
  await page.getByRole("button", { name: "LAB-E2E-0042 SYN-E2E-42", exact: true }).click();
  await expect(page.getByLabel("Analyte code")).toBeVisible();
  await expectNoAccessibilityViolations(page);
  await page.getByLabel("Analyte code").fill("CRP");
  await page.getByLabel("Analyte name").fill("C-reactive protein");
  await page.getByLabel("Result value").fill("12");
  await page.getByLabel("Unit").fill("mg/L");
  await page.getByLabel("Reference lower limit").fill("0");
  await page.getByLabel("Reference upper limit").fill("5");
  await page.getByRole("button", { name: "＋ Add result" }).click();
  await expect(page.getByText("High")).toBeVisible();

  await page.getByRole("button", { name: "Complete sample" }).click();
  await expect(page.getByText("Completed", { exact: true }).first()).toBeVisible();
  await page.getByRole("button", { name: "Sign out" }).click();
  await expect(page.getByRole("heading", { name: "Sign in to LabTrack" })).toBeVisible();
  await expect(page).toHaveURL(/\/login$/);
});

function sampleSummary(sample: Record<string, unknown>) {
  return {
    id: sample.id,
    accessionNumber: sample.accessionNumber,
    patientReference: sample.patientReference,
    specimenType: sample.specimenType,
    collectedAt: sample.collectedAt,
    status: sample.status,
    resultCount: sample.resultCount,
    updatedAt: sample.updatedAt,
    revision: sample.revision,
  };
}

async function expectNoAccessibilityViolations(page: Page) {
  const results = await new AxeBuilder({ page })
    .withTags(["wcag2a", "wcag2aa", "wcag21a", "wcag21aa", "wcag22aa"])
    .analyze();
  expect(results.violations).toEqual([]);
}
