namespace LabTrack.Api.Domain;

public sealed class SampleEvent
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid LabSampleId { get; set; }
    public string EventType { get; set; } = "";
    public string Description { get; set; } = "";
    public DateTimeOffset OccurredAt { get; set; }
}
