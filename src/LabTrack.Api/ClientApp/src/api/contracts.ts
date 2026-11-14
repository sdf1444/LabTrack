import { z } from "zod";

const dateTimeSchema = z.iso.datetime({ offset: true });

export const sampleStatusSchema = z.enum(["Received", "InProgress", "Completed", "Rejected"]);

export const userSchema = z.object({
  id: z.uuid(),
  email: z.string(),
  roles: z.array(z.string()),
});

export const resultSchema = z.object({
  id: z.uuid(),
  analyteCode: z.string(),
  analyteName: z.string(),
  value: z.number().finite(),
  unit: z.string(),
  referenceLow: z.number().finite().nullable(),
  referenceHigh: z.number().finite().nullable(),
  flag: z.string(),
  recordedAt: dateTimeSchema,
});

export const sampleEventSchema = z.object({
  id: z.uuid(),
  eventType: z.string(),
  description: z.string(),
  occurredAt: dateTimeSchema,
});

export const sampleSummarySchema = z.object({
  id: z.uuid(),
  accessionNumber: z.string(),
  patientReference: z.string(),
  specimenType: z.string(),
  collectedAt: dateTimeSchema,
  status: sampleStatusSchema,
  resultCount: z.number().int().nonnegative(),
  updatedAt: dateTimeSchema,
  revision: z.number().int().positive(),
});

export const sampleDetailSchema = sampleSummarySchema.extend({
  createdAt: dateTimeSchema,
  results: z.array(resultSchema),
  events: z.array(sampleEventSchema),
});

export const samplesSchema = z.array(sampleSummarySchema);

export const loginRequestSchema = z.object({
  email: z.email(),
  password: z.string().min(1),
  rememberMe: z.boolean(),
});

export const createSampleRequestSchema = z.object({
  accessionNumber: z.string().trim().min(3).max(40),
  patientReference: z.string().trim().min(2).max(40),
  specimenType: z.string().trim().min(2).max(60),
  collectedAt: dateTimeSchema,
});

export const addResultInputSchema = z.object({
  analyteCode: z.string().trim().min(2).max(30),
  analyteName: z.string().trim().min(2).max(80),
  value: z.number().finite(),
  unit: z.string().trim().min(1).max(20),
  referenceLow: z.number().finite().nullable(),
  referenceHigh: z.number().finite().nullable(),
});

export type SampleStatus = z.infer<typeof sampleStatusSchema>;
export type User = z.infer<typeof userSchema>;
export type SampleSummary = z.infer<typeof sampleSummarySchema>;
export type SampleDetail = z.infer<typeof sampleDetailSchema>;
export type SampleResult = z.infer<typeof resultSchema>;
export type SampleEvent = z.infer<typeof sampleEventSchema>;
export type CreateSampleRequest = z.infer<typeof createSampleRequestSchema>;
export type AddResultInput = z.infer<typeof addResultInputSchema>;
