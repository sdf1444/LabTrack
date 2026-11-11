using LabTrack.Api.Domain;
using Microsoft.EntityFrameworkCore;

namespace LabTrack.Api.Infrastructure;

public static class DatabaseInitializer
{
    public static async Task InitializeAsync(LabTrackDbContext db, CancellationToken ct = default)
    {
        await db.Database.EnsureCreatedAsync(ct);
        if (await db.Samples.AnyAsync(ct)) return;

        var now = DateTimeOffset.UtcNow;
        var received = Sample("LAB-2026-0041", "SYN-1041", "Serum", SampleStatus.Received, now.AddHours(-2), now);
        var working = Sample("LAB-2026-0040", "SYN-1040", "Plasma", SampleStatus.InProgress, now.AddHours(-5), now.AddMinutes(-35));
        working.Results.Add(Result(working.Id, "GLU", "Glucose", 6.1m, "mmol/L", 3.9m, 7.8m, now.AddMinutes(-35)));
        var complete = Sample("LAB-2026-0039", "SYN-1039", "Whole blood", SampleStatus.Completed, now.AddDays(-1), now.AddMinutes(-90));
        complete.Results.Add(Result(complete.Id, "HGB", "Haemoglobin", 118m, "g/L", 120m, 160m, now.AddMinutes(-95)));
        complete.Results.Add(Result(complete.Id, "WBC", "White cell count", 7.2m, "10^9/L", 4m, 11m, now.AddMinutes(-92)));
        db.Samples.AddRange(received, working, complete);
        await db.SaveChangesAsync(ct);
    }

    private static LabSample Sample(string accession, string patient, string specimen, SampleStatus status,
        DateTimeOffset collected, DateTimeOffset updated)
    {
        var sample = new LabSample
        {
            AccessionNumber = accession, PatientReference = patient, SpecimenType = specimen,
            CollectedAt = collected, Status = status, CreatedAt = collected, UpdatedAt = updated,
            Revision = status == SampleStatus.Received ? 1 : 2
        };
        sample.Events.Add(new SampleEvent { EventType = "Registered", Description = "Sample registered", OccurredAt = collected });
        if (status != SampleStatus.Received)
            sample.Events.Add(new SampleEvent { EventType = "StatusChanged", Description = $"Status changed from Received to {status}", OccurredAt = updated.AddMinutes(-10) });
        return sample;
    }

    private static TestResult Result(Guid sampleId, string code, string name, decimal value,
        string unit, decimal low, decimal high, DateTimeOffset at) => new()
    {
        LabSampleId = sampleId, AnalyteCode = code, AnalyteName = name, Value = value,
        Unit = unit, ReferenceLow = low, ReferenceHigh = high,
        Flag = SampleWorkflow.ResultFlag(value, low, high), RecordedAt = at
    };
}
