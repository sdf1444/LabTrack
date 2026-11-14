using LabTrack.Api.Domain;
using Xunit;

namespace LabTrack.Tests;

public sealed class SampleWorkflowTests
{
    public static TheoryData<SampleStatus, SampleStatus, bool> WorkflowTransitions => new()
    {
        { SampleStatus.Received, SampleStatus.InProgress, true },
        { SampleStatus.Received, SampleStatus.Rejected, true },
        { SampleStatus.InProgress, SampleStatus.Completed, true },
        { SampleStatus.InProgress, SampleStatus.Rejected, true },
        { SampleStatus.Completed, SampleStatus.InProgress, false },
        { SampleStatus.Rejected, SampleStatus.Completed, false },
        { SampleStatus.Received, SampleStatus.Completed, false }
    };

    public static TheoryData<decimal, decimal?, decimal?, string> ReferenceFlags => new()
    {
        { 3.8m, 3.9m, 7.8m, "Low" },
        { 3.9m, 3.9m, 7.8m, "Within range" },
        { 5.2m, 3.9m, 7.8m, "Within range" },
        { 7.8m, 3.9m, 7.8m, "Within range" },
        { 8.1m, 3.9m, 7.8m, "High" },
        { 8.1m, null, 7.8m, "High" },
        { 2.2m, 3.9m, null, "Low" }
    };

    public static TheoryData<decimal, bool> StoredResultValues => new()
    {
        { 99_999_999.9999m, true },
        { -99_999_999.9999m, true },
        { 1.2345m, true },
        { 1.23450m, true },
        { 1.23456m, false },
        { 100_000_000m, false },
        { -100_000_000m, false }
    };

    [Theory]
    [MemberData(nameof(WorkflowTransitions))]
    public void CanTransition_allows_only_defined_workflow_edges(
        SampleStatus current, SampleStatus next, bool expected)
    {
        Assert.Equal(expected, SampleWorkflow.CanTransition(current, next));
    }

    [Theory]
    [MemberData(nameof(ReferenceFlags))]
    public void ResultFlag_compares_value_to_optional_reference_limits(
        decimal value, decimal? low, decimal? high, string expected)
    {
        Assert.Equal(expected, SampleWorkflow.ResultFlag(value, low, high));
    }

    [Theory]
    [MemberData(nameof(StoredResultValues))]
    public void IsRepresentableResultValue_matches_database_numeric_precision(decimal value, bool expected)
    {
        Assert.Equal(expected, SampleWorkflow.IsRepresentableResultValue(value));
    }
}
