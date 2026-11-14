import { useEffect, useRef, useState, type FormEvent } from "react";
import type { CreateSampleRequest, SampleDetail } from "../api/contracts";

function localDateTimeValue(): string {
  return new Date(Date.now() - new Date().getTimezoneOffset() * 60_000).toISOString().slice(0, 16);
}

export function RegistrationDialog({
  onClose,
  onRegistered,
}: {
  onClose: () => void;
  onRegistered: (input: CreateSampleRequest) => Promise<SampleDetail>;
}) {
  const dialogRef = useRef<HTMLDialogElement>(null);
  const [error, setError] = useState("");
  const [submitting, setSubmitting] = useState(false);
  const [collectedAt] = useState(localDateTimeValue);

  useEffect(() => {
    const dialog = dialogRef.current;
    if (dialog && !dialog.open) dialog.showModal();
    return () => {
      if (dialog?.open) dialog.close();
    };
  }, []);

  function closeDialog() {
    const dialog = dialogRef.current;
    if (dialog?.open) dialog.close();
    onClose();
  }

  async function handleSubmit(event: FormEvent<HTMLFormElement>) {
    event.preventDefault();
    const data = new FormData(event.currentTarget);
    const collected = data.get("collectedAt");
    if (typeof collected !== "string") {
      setError("Enter a collection date and time.");
      return;
    }
    setSubmitting(true);
    setError("");
    try {
      await onRegistered({
        accessionNumber: String(data.get("accessionNumber") ?? ""),
        patientReference: String(data.get("patientReference") ?? ""),
        specimenType: String(data.get("specimenType") ?? ""),
        collectedAt: new Date(collected).toISOString(),
      });
      closeDialog();
    } catch (requestError) {
      setError(
        requestError instanceof Error ? requestError.message : "Unable to register the sample.",
      );
    } finally {
      setSubmitting(false);
    }
  }

  return (
    <dialog
      ref={dialogRef}
      className="dialog"
      onCancel={(event) => {
        event.preventDefault();
        closeDialog();
      }}
      aria-labelledby="register-title"
    >
      <form onSubmit={handleSubmit}>
        <div className="dialog-head">
          <div>
            <p className="eyebrow">NEW RECORD</p>
            <h2 id="register-title">Register a sample</h2>
          </div>
          <button className="icon-button" type="button" onClick={closeDialog} aria-label="Close">
            ×
          </button>
        </div>
        <label>
          Accession number
          <input
            name="accessionNumber"
            required
            minLength={3}
            maxLength={40}
            placeholder="LAB-2026-0042"
          />
        </label>
        <label>
          Patient reference
          <input
            name="patientReference"
            required
            minLength={2}
            maxLength={40}
            placeholder="SYN-1042"
          />
        </label>
        <div className="form-row">
          <label>
            Specimen type
            <select name="specimenType" required defaultValue="">
              <option value="">Choose type</option>
              <option>Serum</option>
              <option>Plasma</option>
              <option>Whole blood</option>
              <option>Urine</option>
              <option>Other</option>
            </select>
          </label>
          <label>
            Collected at
            <input name="collectedAt" type="datetime-local" required defaultValue={collectedAt} />
          </label>
        </div>
        {error && (
          <p className="form-error" role="alert">
            {error}
          </p>
        )}
        <div className="dialog-actions">
          <button className="button button-quiet" type="button" onClick={closeDialog}>
            Cancel
          </button>
          <button className="button button-primary" type="submit" disabled={submitting}>
            {submitting ? "Registering…" : "Register sample"}
          </button>
        </div>
      </form>
    </dialog>
  );
}
