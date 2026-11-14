import { useState, type FormEvent } from "react";
import type { AddResultInput, SampleDetail, SampleStatus, SampleSummary } from "../api/contracts";
import { sampleStatusSchema } from "../api/contracts";

const prettyStatus = (status: SampleStatus) => (status === "InProgress" ? "In progress" : status);
const statusClass = (status: SampleStatus) => `status-${status.toLowerCase()}`;
const formatTime = (value: string) =>
  new Intl.DateTimeFormat(undefined, {
    dateStyle: "medium",
    timeStyle: "short",
  }).format(new Date(value));
const shortTime = (value: string) =>
  new Intl.DateTimeFormat(undefined, {
    month: "short",
    day: "numeric",
    hour: "2-digit",
    minute: "2-digit",
  }).format(new Date(value));

export function Metrics({ samples }: { samples: SampleSummary[] }) {
  const counts = [
    ["All samples", samples.length, "Recent records"],
    [
      "Received",
      samples.filter((sample) => sample.status === "Received").length,
      "Awaiting processing",
    ],
    [
      "In progress",
      samples.filter((sample) => sample.status === "InProgress").length,
      "Being analysed",
    ],
    [
      "Completed",
      samples.filter((sample) => sample.status === "Completed").length,
      "Results recorded",
    ],
  ] as const;

  return (
    <section className="metrics" aria-label="Sample totals">
      {counts.map(([label, count, hint]) => (
        <article className="metric-card" key={label}>
          <span className="metric-label">{label}</span>
          <strong>{count}</strong>
          <span className="metric-hint">{hint}</span>
        </article>
      ))}
    </section>
  );
}

type SampleListProps = {
  samples: SampleSummary[];
  selectedId: string | null;
  loading: boolean;
  error?: string;
  search: string;
  status: SampleStatus | "";
  onSearchChange: (value: string) => void;
  onStatusChange: (value: SampleStatus | "") => void;
  onSelect: (id: string) => void;
};

export function SampleList({
  samples,
  selectedId,
  loading,
  error,
  search,
  status,
  onSearchChange,
  onStatusChange,
  onSelect,
}: SampleListProps) {
  function handleStatusChange(value: string) {
    const parsed = value === "" ? undefined : sampleStatusSchema.safeParse(value);
    onStatusChange(parsed === undefined ? "" : parsed.success ? parsed.data : "");
  }

  return (
    <div className="list-panel panel">
      <div className="panel-heading">
        <div>
          <h2>Sample register</h2>
          <p>Search by accession or patient reference</p>
        </div>
        <span className="record-count">
          {samples.length} {samples.length === 1 ? "record" : "records"}
        </span>
      </div>
      <div className="filters">
        <label className="search-box">
          <span aria-hidden="true">⌕</span>
          <input
            type="search"
            aria-label="Search samples by accession or patient reference"
            placeholder="Search samples…"
            autoComplete="off"
            value={search}
            onChange={(event) => onSearchChange(event.target.value)}
          />
        </label>
        <label className="sr-only" htmlFor="status-filter">
          Filter by status
        </label>
        <select
          id="status-filter"
          value={status}
          onChange={(event) => handleStatusChange(event.target.value)}
        >
          <option value="">All statuses</option>
          <option value="Received">Received</option>
          <option value="InProgress">In progress</option>
          <option value="Completed">Completed</option>
          <option value="Rejected">Rejected</option>
        </select>
      </div>
      {loading && (
        <p className="loading" role="status">
          Loading samples…
        </p>
      )}
      <div className="table-wrap">
        <table>
          <thead>
            <tr>
              <th>Accession</th>
              <th>Specimen</th>
              <th>Status</th>
              <th>Results</th>
              <th>Updated</th>
            </tr>
          </thead>
          <tbody>
            {error ? (
              <tr>
                <td colSpan={5}>
                  <div className="inline-error" role="alert">
                    {error}. Check that the API and database are available, then refresh.
                  </div>
                </td>
              </tr>
            ) : (
              samples.map((sample) => (
                <tr key={sample.id} className={sample.id === selectedId ? "selected" : ""}>
                  <td>
                    <button
                      className="sample-select"
                      type="button"
                      aria-pressed={sample.id === selectedId}
                      onClick={() => onSelect(sample.id)}
                    >
                      <span className="accession">{sample.accessionNumber}</span>
                      <span className="patient">{sample.patientReference}</span>
                    </button>
                  </td>
                  <td>{sample.specimenType}</td>
                  <td>
                    <span className={`status-pill ${statusClass(sample.status)}`}>
                      {prettyStatus(sample.status)}
                    </span>
                  </td>
                  <td>{sample.resultCount}</td>
                  <td>{shortTime(sample.updatedAt)}</td>
                </tr>
              ))
            )}
          </tbody>
        </table>
        {!loading && !error && samples.length === 0 && (
          <div className="empty-state">
            <div className="empty-icon" aria-hidden="true">
              ⌕
            </div>
            <strong>No samples found</strong>
            <span>Try another search, or register a specimen.</span>
          </div>
        )}
      </div>
    </div>
  );
}

function WorkflowActions({
  sample,
  busy,
  onChangeStatus,
}: {
  sample: SampleDetail;
  busy: boolean;
  onChangeStatus: (status: SampleStatus) => void;
}) {
  const buttons = [];
  if (sample.status === "Received") {
    buttons.push(
      <button
        key="start"
        className="button button-primary button-small"
        disabled={busy}
        onClick={() => onChangeStatus("InProgress")}
      >
        Start processing
      </button>,
    );
  }
  if (sample.status === "InProgress") {
    buttons.push(
      <button
        key="complete"
        className="button button-primary button-small"
        disabled={busy || sample.results.length === 0}
        title={sample.results.length ? undefined : "Record a result first"}
        onClick={() => onChangeStatus("Completed")}
      >
        Complete sample
      </button>,
    );
  }
  if (sample.status === "Received" || sample.status === "InProgress") {
    buttons.push(
      <button
        key="reject"
        className="button button-quiet button-small"
        disabled={busy}
        onClick={() => onChangeStatus("Rejected")}
      >
        Reject sample
      </button>,
    );
  }
  return buttons.length ? (
    <div className="workflow-actions">{buttons}</div>
  ) : (
    <p className="muted-count">This sample is in a final state.</p>
  );
}

function ResultForm({
  sample,
  submitting,
  onSubmit,
}: {
  sample: SampleDetail;
  submitting: boolean;
  onSubmit: (input: AddResultInput) => Promise<void>;
}) {
  const [error, setError] = useState("");
  if (sample.status !== "InProgress") return null;

  async function handleSubmit(event: FormEvent<HTMLFormElement>) {
    event.preventDefault();
    const form = event.currentTarget;
    const data = new FormData(form);
    const value = data.get("value");
    const referenceLow = data.get("referenceLow");
    const referenceHigh = data.get("referenceHigh");
    if (
      typeof value !== "string" ||
      typeof referenceLow !== "string" ||
      typeof referenceHigh !== "string"
    ) {
      setError("Enter valid result values.");
      return;
    }
    const text = (key: string) => {
      const field = data.get(key);
      if (typeof field !== "string") throw new Error(`Missing ${key} field.`);
      return field;
    };

    try {
      const input = {
        analyteCode: text("analyteCode"),
        analyteName: text("analyteName"),
        value: Number(value),
        unit: text("unit"),
        referenceLow: referenceLow === "" ? null : Number(referenceLow),
        referenceHigh: referenceHigh === "" ? null : Number(referenceHigh),
      };
      await onSubmit(input);
      form.reset();
      setError("");
    } catch (requestError) {
      setError(requestError instanceof Error ? requestError.message : "Unable to save the result.");
    }
  }

  return (
    <form className="result-form" onSubmit={handleSubmit}>
      <h4>Record a test result</h4>
      <div className="form-grid">
        <input
          name="analyteCode"
          aria-label="Analyte code"
          placeholder="Code (e.g. CRP)"
          required
          minLength={2}
          maxLength={30}
        />
        <input
          name="analyteName"
          aria-label="Analyte name"
          placeholder="Analyte name"
          required
          minLength={2}
          maxLength={80}
        />
        <input
          name="value"
          aria-label="Result value"
          placeholder="Value"
          type="number"
          step="0.0001"
          required
        />
        <input
          name="unit"
          aria-label="Unit"
          placeholder="Unit (e.g. mg/L)"
          required
          maxLength={20}
        />
        <input
          name="referenceLow"
          aria-label="Reference lower limit"
          placeholder="Reference low"
          type="number"
          step="0.0001"
        />
        <input
          name="referenceHigh"
          aria-label="Reference upper limit"
          placeholder="Reference high"
          type="number"
          step="0.0001"
        />
      </div>
      {error && (
        <p className="form-error" role="alert">
          {error}
        </p>
      )}
      <button className="button button-quiet button-small" type="submit" disabled={submitting}>
        ＋ Add result
      </button>
    </form>
  );
}

export function SampleDetailView({
  sample,
  loading,
  error,
  busy,
  onChangeStatus,
  onSubmitResult,
}: {
  sample?: SampleDetail;
  loading: boolean;
  error?: string;
  busy: boolean;
  onChangeStatus: (status: SampleStatus) => void;
  onSubmitResult: (input: AddResultInput) => Promise<void>;
}) {
  if (loading)
    return (
      <p className="loading" role="status">
        Loading sample details…
      </p>
    );
  if (error)
    return (
      <div className="detail-content">
        <div className="inline-error" role="alert">
          {error}
        </div>
      </div>
    );
  if (!sample) {
    return (
      <div className="detail-placeholder">
        <div className="empty-icon" aria-hidden="true">
          ◫
        </div>
        <strong>Select a sample</strong>
        <span>Its workflow, results and audit history will appear here.</span>
      </div>
    );
  }

  return (
    <div className="detail-content">
      <div className="detail-top">
        <div>
          <h2>{sample.accessionNumber}</h2>
          <p>Patient ref. {sample.patientReference}</p>
        </div>
        <span className={`status-pill ${statusClass(sample.status)}`}>
          {prettyStatus(sample.status)}
        </span>
      </div>
      <div className="detail-type">
        <div className="detail-kv">
          <span>Specimen</span>
          <strong>{sample.specimenType}</strong>
        </div>
        <div className="detail-kv">
          <span>Collected</span>
          <strong>{formatTime(sample.collectedAt)}</strong>
        </div>
      </div>
      <div className="section-line">
        <h3>Workflow</h3>
        <span className="muted-count">Revision {sample.revision}</span>
      </div>
      <WorkflowActions sample={sample} busy={busy} onChangeStatus={onChangeStatus} />
      <div className="section-line">
        <h3>Test results</h3>
        <span className="muted-count">
          {sample.results.length} {sample.results.length === 1 ? "result" : "results"}
        </span>
      </div>
      <div className="result-table">
        <table>
          <thead>
            <tr>
              <th>Analyte</th>
              <th>Result</th>
              <th>Flag</th>
            </tr>
          </thead>
          <tbody>
            {sample.results.length ? (
              sample.results.map((result) => (
                <tr key={result.id}>
                  <td>{result.analyteCode}</td>
                  <td>
                    {result.value} {result.unit}
                  </td>
                  <td>
                    <span className={`flag ${result.flag === "Within range" ? "" : "out"}`}>
                      {result.flag}
                    </span>
                  </td>
                </tr>
              ))
            ) : (
              <tr>
                <td colSpan={3} className="muted-count">
                  No results recorded yet.
                </td>
              </tr>
            )}
          </tbody>
        </table>
      </div>
      <ResultForm sample={sample} submitting={busy} onSubmit={onSubmitResult} />
      <div className="section-line">
        <h3>Audit history</h3>
        <span className="muted-count">Newest first</span>
      </div>
      <div className="event-list">
        {sample.events.map((event) => (
          <div className="event" key={event.id}>
            <span className="event-marker" aria-hidden="true" />
            <div>
              <strong>{event.eventType}</strong>
              <p>{event.description}</p>
              <time dateTime={event.occurredAt}>{formatTime(event.occurredAt)}</time>
            </div>
          </div>
        ))}
      </div>
    </div>
  );
}
