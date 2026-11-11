const $ = (selector, root = document) => root.querySelector(selector);
const listBody = $("#sample-rows");
const detailPanel = $("#detail-panel");
const searchInput = $("#search");
const statusFilter = $("#status-filter");
let samples = [];
let selectedId = null;
let searchTimer;
let toastTimer;

const escapeHtml = (value) => String(value ?? "").replace(/[&<>"']/g, (char) => ({
  "&": "&amp;", "<": "&lt;", ">": "&gt;", '"': "&quot;", "'": "&#39;"
}[char]));
const prettyStatus = (status) => status === "InProgress" ? "In progress" : status;
const statusClass = (status) => `status-${String(status).toLowerCase()}`;
const formatTime = (value) => new Intl.DateTimeFormat(undefined, { dateStyle: "medium", timeStyle: "short" }).format(new Date(value));
const shortTime = (value) => new Intl.DateTimeFormat(undefined, { month: "short", day: "numeric", hour: "2-digit", minute: "2-digit" }).format(new Date(value));

async function api(path, options = {}) {
  const response = await fetch(path, {
    ...options,
    headers: { "Content-Type": "application/json", ...(options.headers || {}) }
  });
  if (!response.ok) {
    let message = `Request failed (${response.status})`;
    try { message = (await response.json()).title || message; } catch { /* keep fallback */ }
    throw new Error(message);
  }
  return response.status === 204 ? null : response.json();
}

function showToast(message) {
  const toast = $("#toast");
  toast.textContent = message;
  toast.classList.add("show");
  clearTimeout(toastTimer);
  toastTimer = setTimeout(() => toast.classList.remove("show"), 2800);
}

function renderCounts() {
  $("#count-all").textContent = samples.length;
  $("#count-received").textContent = samples.filter((x) => x.status === "Received").length;
  $("#count-progress").textContent = samples.filter((x) => x.status === "InProgress").length;
  $("#count-completed").textContent = samples.filter((x) => x.status === "Completed").length;
  $("#record-count").textContent = `${samples.length} ${samples.length === 1 ? "record" : "records"}`;
}

function renderList() {
  renderCounts();
  $("#empty-state").hidden = samples.length > 0;
  listBody.innerHTML = samples.map((sample) => `
    <tr data-id="${sample.id}" class="${sample.id === selectedId ? "selected" : ""}" tabindex="0" aria-label="Open sample ${escapeHtml(sample.accessionNumber)}">
      <td><div class="accession">${escapeHtml(sample.accessionNumber)}</div><div class="patient">${escapeHtml(sample.patientReference)}</div></td>
      <td>${escapeHtml(sample.specimenType)}</td>
      <td><span class="status-pill ${statusClass(sample.status)}">${escapeHtml(prettyStatus(sample.status))}</span></td>
      <td>${sample.resultCount}</td><td>${escapeHtml(shortTime(sample.updatedAt))}</td>
    </tr>`).join("");
}

async function loadSamples() {
  listBody.innerHTML = '<tr><td colspan="5" class="loading">Loading samples…</td></tr>';
  try {
    const query = new URLSearchParams();
    if (searchInput.value.trim()) query.set("search", searchInput.value.trim());
    if (statusFilter.value) query.set("status", statusFilter.value);
    samples = await api(`/api/samples?${query.toString()}`);
    if (selectedId && !samples.some((x) => x.id === selectedId)) selectedId = null;
    renderList();
    if (selectedId) await loadDetail(selectedId);
    else renderEmptyDetail();
  } catch (error) {
    listBody.innerHTML = `<tr><td colspan="5"><div class="inline-error">${escapeHtml(error.message)}. Check that the API and database are available, then refresh.</div></td></tr>`;
  }
}

function renderEmptyDetail() {
  detailPanel.innerHTML = '<div class="detail-placeholder"><div class="empty-icon">◫</div><strong>Select a sample</strong><span>Its workflow, results and audit history will appear here.</span></div>';
}

function workflowButtons(sample) {
  const buttons = [];
  if (sample.status === "Received") buttons.push(`<button class="button button-primary button-small" data-action="status" data-status="InProgress">Start processing</button>`);
  if (sample.status === "InProgress") buttons.push(`<button class="button button-primary button-small" data-action="status" data-status="Completed" ${sample.results.length ? "" : "disabled title=\"Record a result first\""}>Complete sample</button>`);
  if (["Received", "InProgress"].includes(sample.status)) buttons.push(`<button class="button button-quiet button-small" data-action="status" data-status="Rejected">Reject sample</button>`);
  return buttons.length ? `<div class="workflow-actions">${buttons.join("")}</div>` : '<p class="muted-count">This sample is in a final state.</p>';
}

function resultForm(sample) {
  if (sample.status !== "InProgress") return "";
  return `<form class="result-form" id="result-form"><h4>Record a test result</h4>
    <div class="form-grid">
      <input name="analyteCode" aria-label="Analyte code" placeholder="Code (e.g. CRP)" required minlength="2" maxlength="30">
      <input name="analyteName" aria-label="Analyte name" placeholder="Analyte name" required minlength="2" maxlength="80">
      <input name="value" aria-label="Result value" placeholder="Value" type="number" step="0.0001" required>
      <input name="unit" aria-label="Unit" placeholder="Unit (e.g. mg/L)" required maxlength="20">
      <input name="referenceLow" aria-label="Reference lower limit" placeholder="Reference low" type="number" step="0.0001">
      <input name="referenceHigh" aria-label="Reference upper limit" placeholder="Reference high" type="number" step="0.0001">
    </div><button class="button button-quiet button-small" type="submit">＋ Add result</button></form>`;
}

function renderDetail(sample) {
  const resultRows = sample.results.length ? sample.results.map((result) => `
    <tr><td>${escapeHtml(result.analyteCode)}</td><td>${escapeHtml(result.value)} ${escapeHtml(result.unit)}</td>
    <td><span class="flag ${result.flag === "Within range" ? "" : "out"}">${escapeHtml(result.flag)}</span></td></tr>`).join("") : '<tr><td colspan="3" class="muted-count">No results recorded yet.</td></tr>';
  const events = sample.events.map((event) => `<div class="event"><span class="event-marker"></span><div><strong>${escapeHtml(event.eventType)}</strong><p>${escapeHtml(event.description)}</p><time>${escapeHtml(formatTime(event.occurredAt))}</time></div></div>`).join("");
  detailPanel.innerHTML = `<div class="detail-content">
    <div class="detail-top"><div><h2>${escapeHtml(sample.accessionNumber)}</h2><p>Patient ref. ${escapeHtml(sample.patientReference)}</p></div><span class="status-pill ${statusClass(sample.status)}">${escapeHtml(prettyStatus(sample.status))}</span></div>
    <div class="detail-type"><div class="detail-kv"><span>Specimen</span><strong>${escapeHtml(sample.specimenType)}</strong></div><div class="detail-kv"><span>Collected</span><strong>${escapeHtml(formatTime(sample.collectedAt))}</strong></div></div>
    <div class="section-line"><h3>Workflow</h3><span class="muted-count">Revision ${sample.revision}</span></div>${workflowButtons(sample)}
    <div class="section-line"><h3>Test results</h3><span class="muted-count">${sample.results.length} ${sample.results.length === 1 ? "result" : "results"}</span></div>
    <div class="result-table"><table><thead><tr><th>Analyte</th><th>Result</th><th>Flag</th></tr></thead><tbody>${resultRows}</tbody></table></div>${resultForm(sample)}
    <div class="section-line"><h3>Audit history</h3><span class="muted-count">Newest first</span></div><div class="event-list">${events}</div>
  </div>`;
}

async function loadDetail(id) {
  try {
    const sample = await api(`/api/samples/${id}`);
    selectedId = id;
    renderDetail(sample);
    renderList();
  } catch (error) {
    detailPanel.innerHTML = `<div class="detail-content"><div class="inline-error">${escapeHtml(error.message)}</div></div>`;
  }
}

listBody.addEventListener("click", (event) => {
  const row = event.target.closest("tr[data-id]");
  if (row) loadDetail(row.dataset.id);
});
listBody.addEventListener("keydown", (event) => {
  const row = event.target.closest("tr[data-id]");
  if (row && (event.key === "Enter" || event.key === " ")) { event.preventDefault(); loadDetail(row.dataset.id); }
});
searchInput.addEventListener("input", () => { clearTimeout(searchTimer); searchTimer = setTimeout(loadSamples, 220); });
statusFilter.addEventListener("change", loadSamples);

detailPanel.addEventListener("click", async (event) => {
  const button = event.target.closest("button[data-action='status']");
  if (!button || !selectedId) return;
  const sample = await api(`/api/samples/${selectedId}`).catch((error) => { showToast(error.message); return null; });
  if (!sample) return;
  const status = button.dataset.status;
  if (status === "Rejected" && !confirm(`Reject sample ${sample.accessionNumber}?`)) return;
  button.disabled = true;
  try {
    await api(`/api/samples/${selectedId}/status`, { method: "PATCH", body: JSON.stringify({ status, expectedRevision: sample.revision }) });
    showToast(`Sample ${status === "InProgress" ? "processing started" : status.toLowerCase()}.`);
    await loadSamples();
    if (selectedId) await loadDetail(selectedId);
  } catch (error) { showToast(error.message); button.disabled = false; }
});

detailPanel.addEventListener("submit", async (event) => {
  if (event.target.id !== "result-form") return;
  event.preventDefault();
  const form = event.target;
  const sample = await api(`/api/samples/${selectedId}`).catch((error) => { showToast(error.message); return null; });
  if (!sample) return;
  const data = new FormData(form);
  const nullableNumber = (value) => value === "" ? null : Number(value);
  const payload = {
    analyteCode: data.get("analyteCode"), analyteName: data.get("analyteName"),
    value: Number(data.get("value")), unit: data.get("unit"),
    referenceLow: nullableNumber(data.get("referenceLow")),
    referenceHigh: nullableNumber(data.get("referenceHigh")), expectedRevision: sample.revision
  };
  try {
    await api(`/api/samples/${selectedId}/results`, { method: "POST", body: JSON.stringify(payload) });
    showToast("Test result recorded.");
    await loadSamples();
    if (selectedId) await loadDetail(selectedId);
  } catch (error) { showToast(error.message); }
});

const dialog = $("#register-dialog");
$("#new-sample-button").addEventListener("click", () => {
  const local = new Date(Date.now() - new Date().getTimezoneOffset() * 60000).toISOString().slice(0, 16);
  $("#register-form").elements.collectedAt.value = local;
  $("#register-error").textContent = "";
  dialog.showModal();
});
document.addEventListener("click", (event) => { if (event.target.matches("[data-close-dialog]")) dialog.close(); });
$("#register-form").addEventListener("submit", async (event) => {
  event.preventDefault();
  const form = event.currentTarget;
  const data = new FormData(form);
  const payload = {
    accessionNumber: data.get("accessionNumber"), patientReference: data.get("patientReference"),
    specimenType: data.get("specimenType"), collectedAt: new Date(data.get("collectedAt")).toISOString()
  };
  const submit = form.querySelector("[type='submit']");
  submit.disabled = true;
  $("#register-error").textContent = "";
  try {
    const created = await api("/api/samples", { method: "POST", body: JSON.stringify(payload) });
    dialog.close();
    form.reset();
    searchInput.value = "";
    statusFilter.value = "";
    selectedId = created.id;
    await loadSamples();
    await loadDetail(created.id);
    showToast("Sample registered.");
  } catch (error) { $("#register-error").textContent = error.message; }
  finally { submit.disabled = false; }
});

loadSamples();
