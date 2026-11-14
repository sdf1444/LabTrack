import { useMutation, useQuery, useQueryClient } from "@tanstack/react-query";
import { useEffect, useState } from "react";
import {
  changeSampleStatus,
  getErrorMessage,
  getSample,
  getSamples,
  addSampleResult,
  registerSample,
} from "../api/client";
import type { AddResultInput, CreateSampleRequest, SampleStatus } from "../api/contracts";
import { RegistrationDialog } from "./RegistrationDialog";
import { FocusedHeading } from "./FocusedHeading";
import { Metrics, SampleDetailView, SampleList } from "./SampleViews";

export function SampleDashboard() {
  const queryClient = useQueryClient();
  const [search, setSearch] = useState("");
  const [debouncedSearch, setDebouncedSearch] = useState("");
  const [status, setStatus] = useState<SampleStatus | "">("");
  const [selectedId, setSelectedId] = useState<string | null>(null);
  const [dialogOpen, setDialogOpen] = useState(false);
  const [toast, setToast] = useState("");

  useEffect(() => {
    const timer = window.setTimeout(() => setDebouncedSearch(search), 220);
    return () => window.clearTimeout(timer);
  }, [search]);

  useEffect(() => {
    if (!toast) return undefined;
    const timer = window.setTimeout(() => setToast(""), 2800);
    return () => window.clearTimeout(timer);
  }, [toast]);

  const samplesQuery = useQuery({
    queryKey: ["samples", debouncedSearch, status],
    queryFn: ({ signal }) => getSamples({ search: debouncedSearch, status }, signal),
  });
  const samples = samplesQuery.data ?? [];

  const sampleQuery = useQuery({
    queryKey: ["sample", selectedId],
    queryFn: ({ signal }) => {
      if (!selectedId) throw new Error("A sample must be selected before loading its details.");
      return getSample(selectedId, signal);
    },
    enabled: selectedId !== null,
  });

  const invalidateSamples = async (id?: string) => {
    await queryClient.invalidateQueries({ queryKey: ["samples"] });
    if (id) await queryClient.invalidateQueries({ queryKey: ["sample", id] });
  };

  const statusMutation = useMutation({
    mutationFn: ({
      id,
      nextStatus,
      revision,
    }: {
      id: string;
      nextStatus: SampleStatus;
      revision: number;
    }) => changeSampleStatus(id, nextStatus, revision),
    onSuccess: async (sample, variables) => {
      if (status && status !== variables.nextStatus) setSelectedId(null);
      setToast(
        `Sample ${variables.nextStatus === "InProgress" ? "processing started" : variables.nextStatus.toLowerCase()}.`,
      );
      await invalidateSamples(sample.id);
    },
    onError: (error: Error) => setToast(getErrorMessage(error)),
  });

  const resultMutation = useMutation({
    mutationFn: ({
      id,
      input,
      revision,
    }: {
      id: string;
      input: AddResultInput;
      revision: number;
    }) => addSampleResult(id, input, revision),
    onSuccess: async (sample) => {
      setToast("Test result recorded.");
      await invalidateSamples(sample.id);
    },
    onError: (error: Error) => setToast(getErrorMessage(error)),
  });

  const registrationMutation = useMutation({
    mutationFn: (input: CreateSampleRequest) => registerSample(input),
    onSuccess: async (sample) => {
      setSearch("");
      setDebouncedSearch("");
      setStatus("");
      setSelectedId(sample.id);
      setToast("Sample registered.");
      await invalidateSamples(sample.id);
    },
  });

  function handleStatusChange(nextStatus: SampleStatus) {
    const sample = sampleQuery.data;
    if (!sample || statusMutation.isPending) return;
    if (nextStatus === "Rejected" && !window.confirm(`Reject sample ${sample.accessionNumber}?`))
      return;
    statusMutation.mutate({
      id: sample.id,
      nextStatus,
      revision: sample.revision,
    });
  }

  async function handleResultSubmit(input: AddResultInput) {
    const sample = sampleQuery.data;
    if (!sample) throw new Error("Select a sample before recording a result.");
    await resultMutation.mutateAsync({
      id: sample.id,
      input,
      revision: sample.revision,
    });
  }

  async function handleRegister(input: CreateSampleRequest) {
    return registrationMutation.mutateAsync(input);
  }

  return (
    <>
      <main className="shell">
        <section className="page-heading">
          <div>
            <p className="eyebrow">LABORATORY OPERATIONS</p>
            <FocusedHeading>Sample workflow</FocusedHeading>
            <p className="subtitle">
              Register specimens, record results and track each step to completion.
            </p>
          </div>
          <button
            className="button button-primary"
            type="button"
            onClick={() => setDialogOpen(true)}
          >
            <span aria-hidden="true">＋</span> Register sample
          </button>
        </section>
        <Metrics samples={samples} />
        <section className="workspace">
          <SampleList
            samples={samples}
            selectedId={selectedId}
            loading={samplesQuery.isPending}
            error={samplesQuery.isError ? getErrorMessage(samplesQuery.error) : undefined}
            search={search}
            status={status}
            onSearchChange={(value) => {
              setSearch(value);
              setSelectedId(null);
            }}
            onStatusChange={(value) => {
              setStatus(value);
              setSelectedId(null);
            }}
            onSelect={setSelectedId}
          />
          <aside className="detail-panel panel" aria-label="Selected sample details">
            <SampleDetailView
              sample={sampleQuery.data}
              loading={sampleQuery.isPending && selectedId !== null}
              error={sampleQuery.isError ? getErrorMessage(sampleQuery.error) : undefined}
              busy={statusMutation.isPending || resultMutation.isPending}
              onChangeStatus={handleStatusChange}
              onSubmitResult={handleResultSubmit}
            />
          </aside>
        </section>
        <p className="demo-note">
          <span aria-hidden="true">ⓘ</span> Portfolio demo with synthetic records only. Not for real
          patient data or clinical use.
        </p>
      </main>
      {dialogOpen && (
        <RegistrationDialog onClose={() => setDialogOpen(false)} onRegistered={handleRegister} />
      )}
      <div className={`toast ${toast ? "show" : ""}`} role="status" aria-live="polite">
        {toast}
      </div>
    </>
  );
}
