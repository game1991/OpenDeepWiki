using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using OpenDeepWiki.EFCore;
using OpenDeepWiki.Entities;
using OpenDeepWiki.Infrastructure;

namespace OpenDeepWiki.Services.Repositories;

/// <summary>
/// 处理日志API服务
/// </summary>
[MiniApi(Route = "/api/v1/repos")]
[Tags("处理日志")]
public class ProcessingLogApiService(IContext context, IProcessingLogService processingLogService)
{
    /// <summary>
    /// 获取仓库处理日志
    /// </summary>
    [HttpGet("/{owner}/{repo}/processing-logs")]
    public async Task<IResult> GetProcessingLogsAsync(
        string owner,
        string repo,
        [FromQuery] DateTime? since,
        [FromQuery] int limit = 100)
    {
        (owner, repo) = RepositoryRouteDecoder.DecodeOwnerAndRepo(owner, repo);

        if (limit <= 0) limit = 100;
        if (limit > 500) limit = 500;

        var repository = await context.Repositories
            .AsNoTracking()
            .FirstOrDefaultAsync(r => r.OrgName == owner && r.RepoName == repo);

        if (repository is null)
        {
            return Results.NotFound(new { error = "仓库不存在" });
        }

        var response = await processingLogService.GetLogsAsync(repository.Id, since, limit);

        return Results.Ok(new ProcessingLogApiResponse
        {
            Status = repository.Status,
            StatusName = repository.Status.ToString(),
            CurrentStep = response.CurrentStep,
            CurrentStepName = response.CurrentStep.ToString(),
            TotalDocuments = response.TotalDocuments,
            CompletedDocuments = response.CompletedDocuments,
            StartedAt = response.StartedAt,
            Logs = response.Logs.Select(log => new ProcessingLogApiItem
            {
                Id = log.Id,
                Step = log.Step,
                StepName = log.Step.ToString(),
                Message = log.Message,
                IsAiOutput = log.IsAiOutput,
                ToolName = log.ToolName,
                CreatedAt = log.CreatedAt
            }).ToList()
        });
    }

}

/// <summary>
/// 处理日志API响应
/// </summary>
public class ProcessingLogApiResponse
{
    public RepositoryStatus Status { get; set; }
    public string StatusName { get; set; } = string.Empty;
    public ProcessingStep CurrentStep { get; set; }
    public string CurrentStepName { get; set; } = string.Empty;
    public int TotalDocuments { get; set; }
    public int CompletedDocuments { get; set; }
    public DateTime? StartedAt { get; set; }
    public List<ProcessingLogApiItem> Logs { get; set; } = new();
}

/// <summary>
/// 处理日志API项
/// </summary>
public class ProcessingLogApiItem
{
    public string Id { get; set; } = string.Empty;
    public ProcessingStep Step { get; set; }
    public string StepName { get; set; } = string.Empty;
    public string Message { get; set; } = string.Empty;
    public bool IsAiOutput { get; set; }
    public string? ToolName { get; set; }
    public DateTime CreatedAt { get; set; }
}
