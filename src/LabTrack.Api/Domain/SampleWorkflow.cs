namespace LabTrack.Api.Domain;

public static class SampleWorkflow
{
    public static bool CanTransition(SampleStatus current, SampleStatus next) =>
        (current, next) is
            (SampleStatus.Received, SampleStatus.InProgress) or
            (SampleStatus.Received, SampleStatus.Rejected) or
            (SampleStatus.InProgress, SampleStatus.Completed) or
            (SampleStatus.InProgress, SampleStatus.Rejected);

    public static string ResultFlag(decimal value, decimal? referenceLow, decimal? referenceHigh)
    {
        if (referenceLow is not null && value < referenceLow) return "Low";
        if (referenceHigh is not null && value > referenceHigh) return "High";
        return "Within range";
    }

    public static bool IsRepresentableResultValue(decimal value) =>
        value >= -TestResult.MaximumRepresentableValue &&
        value <= TestResult.MaximumRepresentableValue &&
        decimal.Round(value, TestResult.ValueScale) == value;
}
