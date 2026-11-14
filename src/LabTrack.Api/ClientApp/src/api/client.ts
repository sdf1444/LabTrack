import { z } from "zod";
import {
  addResultInputSchema,
  createSampleRequestSchema,
  loginRequestSchema,
  sampleDetailSchema,
  samplesSchema,
  userSchema,
  type AddResultInput,
  type CreateSampleRequest,
  type SampleDetail,
  type SampleStatus,
  type SampleSummary,
  type User,
} from "./contracts";

const problemDetailsSchema = z.object({
  title: z.string().optional(),
  detail: z.string().optional(),
});

export class ApiError extends Error {
  readonly category: ApiErrorCategory;

  constructor(
    message: string,
    readonly status: number,
  ) {
    super(message);
    this.name = "ApiError";
    this.category = getApiErrorCategory(status);
  }
}

export type ApiErrorCategory =
  "authentication" | "authorization" | "validation" | "conflict" | "server" | "http";

export class NetworkError extends Error {
  constructor() {
    super("Network request failed. Check your connection and try again.");
    this.name = "NetworkError";
  }
}

function getApiErrorCategory(status: number): ApiErrorCategory {
  if (status === 401) return "authentication";
  if (status === 403) return "authorization";
  if (status === 409) return "conflict";
  if (status === 400 || status === 422) return "validation";
  if (status >= 500) return "server";
  return "http";
}

export function getErrorMessage(error: unknown): string {
  return error instanceof Error ? error.message : "An unexpected error occurred.";
}

async function errorMessage(response: Response): Promise<string> {
  const fallback = `Request failed (${response.status})`;
  const body = await response.text();
  if (!body) return fallback;

  try {
    const parsed: unknown = JSON.parse(body);
    const problem = problemDetailsSchema.safeParse(parsed);
    return problem.success ? problem.data.title || problem.data.detail || fallback : fallback;
  } catch (error) {
    if (!(error instanceof SyntaxError)) throw error;
    return fallback;
  }
}

function requestHeaders(options: RequestInit): Headers {
  const headers = new Headers(options.headers);
  if (options.body && !headers.has("Content-Type")) {
    headers.set("Content-Type", "application/json");
  }
  return headers;
}

async function sendRequest(path: string, options: RequestInit): Promise<Response> {
  try {
    return await fetch(path, {
      ...options,
      credentials: "same-origin",
      headers: requestHeaders(options),
    });
  } catch (error) {
    if (error instanceof Error && error.name === "AbortError") throw error;
    if (error instanceof TypeError) throw new NetworkError();
    throw error;
  }
}

async function requestJson<T>(
  path: string,
  schema: z.ZodType<T>,
  options: RequestInit = {},
): Promise<T> {
  const response = await sendRequest(path, options);
  if (!response.ok) {
    throw new ApiError(await errorMessage(response), response.status);
  }
  return schema.parse(await response.json());
}

async function requestVoid(path: string, options: RequestInit = {}): Promise<void> {
  const response = await sendRequest(path, options);
  if (!response.ok) {
    throw new ApiError(await errorMessage(response), response.status);
  }
}

export function getCurrentUser(signal?: AbortSignal): Promise<User> {
  return requestJson("/api/auth/me", userSchema, { signal });
}

export function signIn(email: string, password: string): Promise<User> {
  const body = loginRequestSchema.parse({ email, password, rememberMe: false });
  return requestJson("/api/auth/login", userSchema, {
    method: "POST",
    body: JSON.stringify(body),
  });
}

export function signOut(): Promise<void> {
  return requestVoid("/api/auth/logout", { method: "POST" });
}

export function getSamples(
  filters: { search: string; status: SampleStatus | "" },
  signal?: AbortSignal,
): Promise<SampleSummary[]> {
  const query = new URLSearchParams();
  if (filters.search.trim()) query.set("search", filters.search.trim());
  if (filters.status) query.set("status", filters.status);
  return requestJson(`/api/samples?${query.toString()}`, samplesSchema, { signal });
}

export function getSample(id: string, signal?: AbortSignal): Promise<SampleDetail> {
  return requestJson(`/api/samples/${id}`, sampleDetailSchema, { signal });
}

export function registerSample(input: CreateSampleRequest): Promise<SampleDetail> {
  const body = createSampleRequestSchema.parse(input);
  return requestJson("/api/samples", sampleDetailSchema, {
    method: "POST",
    body: JSON.stringify(body),
  });
}

export function changeSampleStatus(
  id: string,
  status: SampleStatus,
  expectedRevision: number,
): Promise<SampleDetail> {
  return requestJson(`/api/samples/${id}/status`, sampleDetailSchema, {
    method: "PATCH",
    body: JSON.stringify({ status, expectedRevision }),
  });
}

export function addSampleResult(
  id: string,
  input: AddResultInput,
  expectedRevision: number,
): Promise<SampleDetail> {
  const body = addResultInputSchema.parse(input);
  return requestJson(`/api/samples/${id}/results`, sampleDetailSchema, {
    method: "POST",
    body: JSON.stringify({ ...body, expectedRevision }),
  });
}
