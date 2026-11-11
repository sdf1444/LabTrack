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
        { 5.2m, 3.9m, 7.8m, "Within range" },
        { 8.1m, 3.9m, 7.8m, "High" },
        { 8.1m, null, 7.8m, "High" },
        { 2.2m, 3.9m, null, "Low" }
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
}
