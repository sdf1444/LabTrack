namespace LabTrack.Api.Domain;

public sealed class LabSample
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string AccessionNumber { get; set; } = "";
    public string PatientReference { get; set; } = "";
    public string SpecimenType { get; set; } = "";
    public DateTimeOffset CollectedAt { get; set; }
    public SampleStatus Status { get; set; } = SampleStatus.Received;
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset UpdatedAt { get; set; }
    public int Revision { get; set; } = 1;
    public List<TestResult> Results { get; set; } = [];
    public List<SampleEvent> Events { get; set; } = [];
}
