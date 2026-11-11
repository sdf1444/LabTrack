using LabTrack.Api.Contracts;
using LabTrack.Api.Domain;
using LabTrack.Api.Infrastructure;
using Microsoft.EntityFrameworkCore;
using Npgsql;

namespace LabTrack.Api.Application;

public sealed class SampleService(LabTrackDbContext db, TimeProvider clock)
{
    public async Task<IReadOnlyList<SampleSummaryResponse>> ListAsync(string? search, SampleStatus? status, CancellationToken ct)
    {
        var query = db.Samples.AsNoTracking().AsQueryable();
        if (status is not null) query = query.Where(x => x.Status == status);
        if (!string.IsNullOrWhiteSpace(search))
        {
            var term = search.Trim().ToLower();
            query = query.Where(x => x.AccessionNumber.ToLower().Contains(term) || x.PatientReference.ToLower().Contains(term));
        }

        return await query.OrderByDescending(x => x.UpdatedAt).Take(100)
            .Select(x => new SampleSummaryResponse(x.Id, x.AccessionNumber, x.PatientReference,
                x.SpecimenType, x.CollectedAt, x.Status, x.Results.Count, x.UpdatedAt, x.Revision))
            .ToListAsync(ct);
    }

    public async Task<SampleDetailResponse> GetAsync(Guid id, CancellationToken ct)
    {
        var sample = await db.Samples.AsNoTracking().Include(x => x.Results).Include(x => x.Events)
            .SingleOrDefaultAsync(x => x.Id == id, ct);
        return sample is null ? throw new ApiException(404, "Sample was not found.") : ToDetail(sample);
    }

    public async Task<SampleDetailResponse> CreateAsync(CreateSampleRequest request, CancellationToken ct)
    {
        var accession = request.AccessionNumber.Trim().ToUpperInvariant();
        if (await db.Samples.AnyAsync(x => x.AccessionNumber == accession, ct))
            throw new ApiException(409, "That accession number is already in use.");

        var now = clock.GetUtcNow();
        var sample = new LabSample
        {
            AccessionNumber = accession,
            PatientReference = request.PatientReference.Trim(),
            SpecimenType = request.SpecimenType.Trim(),
            CollectedAt = request.CollectedAt.ToUniversalTime(),
            Status = SampleStatus.Received,
            CreatedAt = now,
            UpdatedAt = now,
            Revision = 1
        };
        sample.Events.Add(new SampleEvent { EventType = "Registered", Description = "Sample registered", OccurredAt = now });
        db.Samples.Add(sample);
        try { await db.SaveChangesAsync(ct); }
        catch (DbUpdateException exception) when (IsUniqueConstraintViolation(
            exception, LabTrackDbContext.AccessionNumberUniqueIndexName))
        {
            throw new ApiException(409, "That accession number is already in use.");
        }
        return ToDetail(sample);
    }

    public async Task<SampleDetailResponse> ChangeStatusAsync(Guid id, ChangeStatusRequest request, CancellationToken ct)
    {
        var sample = await LoadForUpdateAsync(id, ct);
        CheckRevision(sample, request.ExpectedRevision);
        if (!SampleWorkflow.CanTransition(sample.Status, request.Status))
            throw new ApiException(400, $"Cannot move a sample from {sample.Status} to {request.Status}.");
        if (request.Status == SampleStatus.Completed && sample.Results.Count == 0)
            throw new ApiException(400, "Add at least one result before completing the sample.");

        var previous = sample.Status;
        sample.Status = request.Status;
        sample.Revision++;
        sample.UpdatedAt = clock.GetUtcNow();
        var sampleEvent = new SampleEvent
        {
            LabSampleId = sample.Id,
            EventType = "StatusChanged",
            Description = $"Status changed from {previous} to {sample.Status}",
            OccurredAt = sample.UpdatedAt
        };
        sample.Events.Add(sampleEvent);
        db.Events.Add(sampleEvent);
        await SaveWithConcurrencyAsync(ct);
        return ToDetail(sample);
    }

    public async Task<SampleDetailResponse> AddResultAsync(Guid id, AddResultRequest request, CancellationToken ct)
    {
        var sample = await LoadForUpdateAsync(id, ct);
        CheckRevision(sample, request.ExpectedRevision);
        if (sample.Status != SampleStatus.InProgress)
            throw new ApiException(400, "Results can only be recorded while a sample is in progress.");
        if (request.ReferenceLow is not null && request.ReferenceHigh is not null && request.ReferenceLow > request.ReferenceHigh)
            throw new ApiException(400, "The lower reference limit cannot exceed the upper limit.");
        var code = request.AnalyteCode.Trim().ToUpperInvariant();
        if (sample.Results.Any(x => x.AnalyteCode == code))
            throw new ApiException(409, "A result for this analyte has already been recorded.");

        var now = clock.GetUtcNow();
        var result = new TestResult
        {
            LabSampleId = sample.Id,
            AnalyteCode = code,
            AnalyteName = request.AnalyteName.Trim(),
            Value = request.Value,
            Unit = request.Unit.Trim(),
            ReferenceLow = request.ReferenceLow,
            ReferenceHigh = request.ReferenceHigh,
            Flag = SampleWorkflow.ResultFlag(request.Value, request.ReferenceLow, request.ReferenceHigh),
            RecordedAt = now
        };
        sample.Results.Add(result);
        db.Results.Add(result);
        sample.Revision++;
        sample.UpdatedAt = now;
        var sampleEvent = new SampleEvent
        {
            LabSampleId = sample.Id,
            EventType = "ResultRecorded",
            Description = $"Result recorded for {code}",
            OccurredAt = now
        };
        sample.Events.Add(sampleEvent);
        db.Events.Add(sampleEvent);
        await SaveWithConcurrencyAsync(ct);
        return ToDetail(sample);
    }

    private async Task<LabSample> LoadForUpdateAsync(Guid id, CancellationToken ct) =>
        await db.Samples.Include(x => x.Results).Include(x => x.Events)
            .SingleOrDefaultAsync(x => x.Id == id, ct)
        ?? throw new ApiException(404, "Sample was not found.");

    private static void CheckRevision(LabSample sample, int expectedRevision)
    {
        if (expectedRevision != sample.Revision)
            throw new ApiException(409, "This sample changed since you opened it. Refresh and try again.");
    }

    private static bool IsUniqueConstraintViolation(DbUpdateException exception, string constraintName) =>
        exception.GetBaseException() is PostgresException postgresException &&
        postgresException.SqlState == PostgresErrorCodes.UniqueViolation &&
        postgresException.ConstraintName == constraintName;

    private async Task SaveWithConcurrencyAsync(CancellationToken ct)
    {
        try { await db.SaveChangesAsync(ct); }
        catch (DbUpdateConcurrencyException) { throw new ApiException(409, "This sample changed concurrently. Refresh and try again."); }
        catch (DbUpdateException exception) when (IsUniqueConstraintViolation(
            exception, LabTrackDbContext.ResultAnalyteUniqueIndexName))
        {
            throw new ApiException(409, "A result for this analyte has already been recorded.");
        }
    }

    private static SampleDetailResponse ToDetail(LabSample sample) => new(
        sample.Id, sample.AccessionNumber, sample.PatientReference, sample.SpecimenType,
        sample.CollectedAt, sample.Status, sample.CreatedAt, sample.UpdatedAt, sample.Revision,
        sample.Results.OrderBy(x => x.AnalyteName).Select(x => new ResultResponse(x.Id, x.AnalyteCode,
            x.AnalyteName, x.Value, x.Unit, x.ReferenceLow, x.ReferenceHigh, x.Flag, x.RecordedAt)).ToArray(),
        sample.Events.OrderByDescending(x => x.OccurredAt).Select(x => new SampleEventResponse(
            x.Id, x.EventType, x.Description, x.OccurredAt)).ToArray());
}
