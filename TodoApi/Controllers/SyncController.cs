using Hangfire;
using Microsoft.AspNetCore.Mvc;
using TodoApi.Dtos;
using TodoApi.Services;
using TodoApi.Services.Sync;
using Microsoft.Extensions.Logging;
using Microsoft.AspNetCore.Http;
using System.Threading;
using System.Threading.Tasks;
using TodoApi.Dtos.ExternalApi;
using TodoApi.Services.ExternalApi;

namespace TodoApi.Controllers;

[ApiController]
[Route("api/[controller]")]
public class SyncController : ControllerBase
{
    private readonly ISyncService _syncService;
    private readonly IBackgroundJobClient _backgroundJobs;
    private readonly ILogger<SyncController> _logger;

    public SyncController(
        ISyncService syncService,
        IBackgroundJobClient backgroundJobs,
        ILogger<SyncController> logger)
    {
        _syncService = syncService;
        _backgroundJobs = backgroundJobs;
        _logger = logger;
    }

    /// <summary>
    /// Triggers a full synchronization (enqueued as background job)
    /// </summary>
    [HttpPost("trigger")]
    [ProducesResponseType(StatusCodes.Status202Accepted)]
    public IActionResult TriggerSync()
    {
        var jobId = _backgroundJobs.Enqueue<ISyncService>(
            service => service.SyncAllAsync(CancellationToken.None)
        );

        _logger.LogInformation("Sync job {JobId} enqueued", jobId);
        
        return Accepted(new
        {
            jobId,
            message = "Synchronization job enqueued successfully",
            statusUrl = $"/api/sync/status"
        });
    }

    /// <summary>
    /// Triggers synchronization for a specific TodoList
    /// </summary>
    [HttpPost("lists/{id}")]
    [ProducesResponseType(StatusCodes.Status202Accepted)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public IActionResult TriggerListSync(int id)
    {
        var jobId = _backgroundJobs.Enqueue<ISyncService>(
            service => service.SyncTodoListAsync(id, CancellationToken.None)
        );

        _logger.LogInformation("Sync job {JobId} enqueued for TodoList {ListId}", jobId, id);
        
        return Accepted(new
        {
            jobId,
            todoListId = id,
            message = $"Synchronization job enqueued for TodoList {id}"
        });
    }

    /// <summary>
    /// Gets current synchronization status
    /// </summary>
    [HttpGet("status")]
    [ProducesResponseType(typeof(SyncStatus), StatusCodes.Status200OK)]
    public async Task<ActionResult<SyncStatus>> GetSyncStatus()
    {
        var status = await _syncService.GetSyncStatusAsync();
        return Ok(status);
    }

    /// <summary>
    /// Performs immediate synchronization (not recommended for production)
    /// </summary>
    [HttpPost("immediate")]
    [ProducesResponseType(typeof(SyncResult), StatusCodes.Status200OK)]
    public async Task<ActionResult<SyncResult>> SyncImmediate()
    {
        _logger.LogWarning("Immediate sync triggered - this may take time");
        
        var result = await _syncService.SyncAllAsync(HttpContext.RequestAborted);
        
        if (result.Success)
        {
            return Ok(result);
        }
        else
        {
            return StatusCode(
                StatusCodes.Status500InternalServerError,
                result
            );
        }
    }
}