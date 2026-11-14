using LabTrack.Api.Contracts;
using LabTrack.Api.Domain;

namespace LabTrack.Api.Application;

public interface ISampleService
{
    Task<IReadOnlyList<SampleSummaryResponse>> ListAsync(
        string? search, SampleStatus? status, CancellationToken cancellationToken);

    Task<SampleDetailResponse> GetAsync(Guid id, CancellationToken cancellationToken);

    Task<SampleDetailResponse> CreateAsync(
        CreateSampleRequest request, CancellationToken cancellationToken);

    Task<SampleDetailResponse> ChangeStatusAsync(
        Guid id, ChangeStatusRequest request, CancellationToken cancellationToken);

    Task<SampleDetailResponse> AddResultAsync(
        Guid id, AddResultRequest request, CancellationToken cancellationToken);
}
