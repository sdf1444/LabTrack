namespace LabTrack.Api.Domain;

public sealed class TestResult
{
    public const int ValuePrecision = 12;
    public const int ValueScale = 4;
    public const decimal MaximumRepresentableValue = 99_999_999.9999m;

    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid LabSampleId { get; set; }
    public string AnalyteCode { get; set; } = "";
    public string AnalyteName { get; set; } = "";
    public decimal Value { get; set; }
    public string Unit { get; set; } = "";
    public decimal? ReferenceLow { get; set; }
    public decimal? ReferenceHigh { get; set; }
    public string Flag { get; set; } = "Within range";
    public DateTimeOffset RecordedAt { get; set; }
}
