using System.ComponentModel.DataAnnotations;
using LabTrack.Api.Domain;

namespace LabTrack.Api.Contracts;

public sealed record CreateSampleRequest(
    [Required, StringLength(40, MinimumLength = 3)] string AccessionNumber,
    [Required, StringLength(40, MinimumLength = 2)] string PatientReference,
    [Required, StringLength(60, MinimumLength = 2)] string SpecimenType,
    DateTimeOffset CollectedAt);

public sealed record ChangeStatusRequest(SampleStatus Status, int ExpectedRevision);

public sealed record AddResultRequest(
    [Required, StringLength(30, MinimumLength = 2)] string AnalyteCode,
    [Required, StringLength(80, MinimumLength = 2)] string AnalyteName,
    decimal Value,
    [Required, StringLength(20, MinimumLength = 1)] string Unit,
    decimal? ReferenceLow,
    decimal? ReferenceHigh,
    int ExpectedRevision);

public sealed record ResultResponse(
    Guid Id, string AnalyteCode, string AnalyteName, decimal Value, string Unit,
    decimal? ReferenceLow, decimal? ReferenceHigh, string Flag, DateTimeOffset RecordedAt);

public sealed record SampleEventResponse(Guid Id, string EventType, string Description, DateTimeOffset OccurredAt);

public sealed record SampleSummaryResponse(
    Guid Id, string AccessionNumber, string PatientReference, string SpecimenType,
    DateTimeOffset CollectedAt, SampleStatus Status, int ResultCount, DateTimeOffset UpdatedAt, int Revision);

public sealed record SampleDetailResponse(
    Guid Id, string AccessionNumber, string PatientReference, string SpecimenType,
    DateTimeOffset CollectedAt, SampleStatus Status, DateTimeOffset CreatedAt,
    DateTimeOffset UpdatedAt, int Revision, IReadOnlyList<ResultResponse> Results,
    IReadOnlyList<SampleEventResponse> Events);
