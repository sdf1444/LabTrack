using LabTrack.Api.Application;
using LabTrack.Api.Contracts;
using LabTrack.Api.Domain;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace LabTrack.Api.Controllers;

[ApiController]
[Authorize(Policy = LabTrackRoles.StaffPolicy)]
[Route("api/samples")]
public sealed class SamplesController(ISampleService samples) : ControllerBase
{
    [HttpGet]
    public async Task<ActionResult<IReadOnlyList<SampleSummaryResponse>>> List(
        [FromQuery] string? search, [FromQuery] SampleStatus? status, CancellationToken ct) =>
        Ok(await samples.ListAsync(search, status, ct));

    [HttpGet("{id:guid}")]
    public async Task<ActionResult<SampleDetailResponse>> GetById(Guid id, CancellationToken ct) =>
        Ok(await samples.GetAsync(id, ct));

    [HttpPost]
    public async Task<ActionResult<SampleDetailResponse>> Create(CreateSampleRequest request, CancellationToken ct)
    {
        var created = await samples.CreateAsync(request, ct);
        return CreatedAtAction(nameof(GetById), new { id = created.Id }, created);
    }

    [HttpPatch("{id:guid}/status")]
    public async Task<ActionResult<SampleDetailResponse>> ChangeStatus(
        Guid id, ChangeStatusRequest request, CancellationToken ct) =>
        Ok(await samples.ChangeStatusAsync(id, request, ct));

    [HttpPost("{id:guid}/results")]
    public async Task<ActionResult<SampleDetailResponse>> AddResult(
        Guid id, AddResultRequest request, CancellationToken ct) =>
        Ok(await samples.AddResultAsync(id, request, ct));
}
