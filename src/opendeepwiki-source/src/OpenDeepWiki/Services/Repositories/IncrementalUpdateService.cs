using System.Diagnostics;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using OpenDeepWiki.EFCore;
using OpenDeepWiki.Entities;
using OpenDeepWiki.Services.Notifications;
using OpenDeepWiki.Services.Wiki;

namespace OpenDeepWiki.Services.Repositories;

/// <summary>
/// 增量更新服务实现
/// 封装增量更新的核心业务逻辑
/// </summary>
public class IncrementalUpdateService : IIncrementalUpdateService
{
    private readonly IRepositoryAnalyzer _repositoryAnalyzer;
    private readonly IWikiGenerator _wikiGenerator;
    private readonly ISubscriberNotificationService _notificationService;
    private readonly IContext _context;
    private readonly IncrementalUpdateOptions _options;
    private readonly ILogger<IncrementalUpdateService> _logger;

    public IncrementalUpdateService(
        IRepositoryAnalyzer repositoryAnalyzer,
        IWikiGenerator wikiGenerator,
        ISubscriberNotificationService notificationService,
        IContext context,
        IOptions<IncrementalUpdateOptions> options,
        ILogger<IncrementalUpdateService> logger)
    {
        _repositoryAnalyzer = repositoryAnalyzer;
        _wikiGenerator = wikiGenerator;
        _notificationService = notificationService;
        _context = context;
        _options = options.Value;
        _logger = logger;
    }

    /// <inheritdoc />
    public async Task<UpdateCheckResult> CheckForUpdatesAsync(
        string repositoryId,
        string branchId,
        CancellationToken cancellationToken = default)
    {
        var stopwatch = Stopwatch.StartNew();

        _logger.LogInformation(
            "Checking for updates. RepositoryId: {RepositoryId}, BranchId: {BranchId}",
            repositoryId, branchId);

        // 获取仓库和分支信息
        var repository = await _context.Repositories
            .FirstOrDefaultAsync(r => r.Id == repositoryId, cancellationToken);

        if (repository == null)
        {
            _logger.LogWarning("Repository not found. RepositoryId: {RepositoryId}", repositoryId);
            return new UpdateCheckResult { NeedsUpdate = false };
        }

        var branch = await _context.RepositoryBranches
            .FirstOrDefaultAsync(b => b.Id == branchId, cancellationToken);

        if (branch == null)
        {
            _logger.LogWarning("Branch not found. BranchId: {BranchId}", branchId);
            return new UpdateCheckResult { NeedsUpdate = false };
        }

        var previousCommitId = branch.LastCommitId;

        try
        {
            // 准备工作区（clone 或 pull）
            var workspace = await PrepareWorkspaceWithRetryAsync(
                repository, branch.BranchName, previousCommitId, cancellationToken);

            var currentCommitId = workspace.CommitId;

            // 如果 commit ID 相同，无需更新
            if (previousCommitId == currentCommitId)
            {
                _logger.LogInformation(
                    "No changes detected. RepositoryId: {RepositoryId}, CommitId: {CommitId}",
                    repositoryId, currentCommitId);

                return new UpdateCheckResult
                {
                    NeedsUpdate = false,
                    PreviousCommitId = previousCommitId,
                    CurrentCommitId = currentCommitId,
                    ChangedFiles = Array.Empty<string>()
                };
            }

            // 获取变更文件列表
            var changedFiles = await _repositoryAnalyzer.GetChangedFilesAsync(
                workspace, previousCommitId, currentCommitId, cancellationToken);

            stopwatch.Stop();

            _logger.LogInformation(
                "Update check completed. RepositoryId: {RepositoryId}, PreviousCommit: {PreviousCommit}, " +
                "CurrentCommit: {CurrentCommit}, ChangedFiles: {ChangedFilesCount}, Duration: {Duration}ms",
                repositoryId, previousCommitId ?? "none", currentCommitId,
                changedFiles.Length, stopwatch.ElapsedMilliseconds);

            return new UpdateCheckResult
            {
                NeedsUpdate = changedFiles.Length > 0 || string.IsNullOrEmpty(previousCommitId),
                PreviousCommitId = previousCommitId,
                CurrentCommitId = currentCommitId,
                ChangedFiles = changedFiles
            };
        }
        catch (Exception ex)
        {
            stopwatch.Stop();
            _logger.LogError(ex,
                "Failed to check for updates. RepositoryId: {RepositoryId}, BranchId: {BranchId}, Duration: {Duration}ms",
                repositoryId, branchId, stopwatch.ElapsedMilliseconds);
            throw;
        }
    }

    /// <inheritdoc />
    public async Task<IncrementalUpdateResult> ProcessIncrementalUpdateAsync(
        string repositoryId,
        string branchId,
        CancellationToken cancellationToken = default)
    {
        var stopwatch = Stopwatch.StartNew();

        _logger.LogInformation(
            "Processing incremental update. RepositoryId: {RepositoryId}, BranchId: {BranchId}",
            repositoryId, branchId);

        try
        {
            // 检查更新
            var checkResult = await CheckForUpdatesAsync(repositoryId, branchId, cancellationToken);

            if (!checkResult.NeedsUpdate)
            {
                stopwatch.Stop();
                _logger.LogInformation(
                    "No update needed. RepositoryId: {RepositoryId}, Duration: {Duration}ms",
                    repositoryId, stopwatch.ElapsedMilliseconds);

                return new IncrementalUpdateResult
                {
                    Success = true,
                    PreviousCommitId = checkResult.PreviousCommitId,
                    CurrentCommitId = checkResult.CurrentCommitId,
                    ChangedFilesCount = 0,
                    UpdatedDocumentsCount = 0,
                    Duration = stopwatch.Elapsed
                };
            }

            // 获取仓库和分支信息
            var repository = await _context.Repositories
                .FirstOrDefaultAsync(r => r.Id == repositoryId, cancellationToken);

            var branch = await _context.RepositoryBranches
                .FirstOrDefaultAsync(b => b.Id == branchId, cancellationToken);

            if (repository == null || branch == null)
            {
                throw new InvalidOperationException(
                    $"Repository or branch not found. RepositoryId: {repositoryId}, BranchId: {branchId}");
            }

            // 获取分支语言
            var branchLanguages = await _context.BranchLanguages
                .Where(bl => bl.RepositoryBranchId == branchId)
                .ToListAsync(cancellationToken);

            // 准备工作区
            var workspace = await PrepareWorkspaceWithRetryAsync(
                repository, branch.BranchName, checkResult.PreviousCommitId, cancellationToken);

            var updatedDocumentsCount = 0;

            // 对每个语言执行增量更新
            foreach (var branchLanguage in branchLanguages)
            {
                cancellationToken.ThrowIfCancellationRequested();

                _logger.LogInformation(
                    "Updating wiki for language {LanguageCode}. RepositoryId: {RepositoryId}",
                    branchLanguage.LanguageCode, repositoryId);

                await _wikiGenerator.IncrementalUpdateAsync(
                    workspace,
                    branchLanguage,
                    checkResult.ChangedFiles ?? Array.Empty<string>(),
                    cancellationToken);

                updatedDocumentsCount++;
            }

            // 更新分支的 LastCommitId
            branch.LastCommitId = checkResult.CurrentCommitId;
            branch.LastProcessedAt = DateTime.UtcNow;
            await _context.SaveChangesAsync(cancellationToken);

            // 更新仓库的 LastUpdateCheckAt
            repository.LastUpdateCheckAt = DateTime.UtcNow;
            await _context.SaveChangesAsync(cancellationToken);

            // 通知订阅者
            await NotifySubscribersSafelyAsync(
                repository, branch, checkResult, cancellationToken);

            stopwatch.Stop();

            _logger.LogInformation(
                "Incremental update completed. RepositoryId: {RepositoryId}, " +
                "ChangedFiles: {ChangedFilesCount}, UpdatedLanguages: {UpdatedLanguagesCount}, Duration: {Duration}ms",
                repositoryId, checkResult.ChangedFiles?.Length ?? 0,
                updatedDocumentsCount, stopwatch.ElapsedMilliseconds);

            return new IncrementalUpdateResult
            {
                Success = true,
                PreviousCommitId = checkResult.PreviousCommitId,
                CurrentCommitId = checkResult.CurrentCommitId,
                ChangedFilesCount = checkResult.ChangedFiles?.Length ?? 0,
                UpdatedDocumentsCount = updatedDocumentsCount,
                Duration = stopwatch.Elapsed
            };
        }
        catch (Exception ex)
        {
            stopwatch.Stop();
            _logger.LogError(ex,
                "Incremental update failed. RepositoryId: {RepositoryId}, BranchId: {BranchId}, Duration: {Duration}ms",
                repositoryId, branchId, stopwatch.ElapsedMilliseconds);

            return new IncrementalUpdateResult
            {
                Success = false,
                ErrorMessage = ex.Message,
                Duration = stopwatch.Elapsed
            };
        }
    }

    /// <inheritdoc />
    public async Task<string> TriggerManualUpdateAsync(
        string repositoryId,
        string branchId,
        CancellationToken cancellationToken = default)
    {
        _logger.LogInformation(
            "Manual update triggered. RepositoryId: {RepositoryId}, BranchId: {BranchId}",
            repositoryId, branchId);

        // 检查是否已存在相同仓库/分支的待处理或处理中任务
        var existingTask = await _context.IncrementalUpdateTasks
            .Where(t => t.RepositoryId == repositoryId
                        && t.BranchId == branchId
                        && (t.Status == IncrementalUpdateStatus.Pending
                            || t.Status == IncrementalUpdateStatus.Processing))
            .FirstOrDefaultAsync(cancellationToken);

        if (existingTask != null)
        {
            _logger.LogInformation(
                "Existing task found. TaskId: {TaskId}, Status: {Status}",
                existingTask.Id, existingTask.Status);
            return existingTask.Id;
        }

        // 获取分支信息以获取上次 CommitId
        var branch = await _context.RepositoryBranches
            .FirstOrDefaultAsync(b => b.Id == branchId, cancellationToken);

        // 创建高优先级任务
        var task = new IncrementalUpdateTask
        {
            Id = Guid.NewGuid().ToString(),
            RepositoryId = repositoryId,
            BranchId = branchId,
            PreviousCommitId = branch?.LastCommitId,
            Status = IncrementalUpdateStatus.Pending,
            Priority = _options.ManualTriggerPriority,
            IsManualTrigger = true,
            CreatedAt = DateTime.UtcNow
        };

        _context.IncrementalUpdateTasks.Add(task);
        await _context.SaveChangesAsync(cancellationToken);

        _logger.LogInformation(
            "Manual update task created. TaskId: {TaskId}, Priority: {Priority}",
            task.Id, task.Priority);

        return task.Id;
    }

    /// <summary>
    /// 带重试逻辑的工作区准备
    /// </summary>
    private async Task<RepositoryWorkspace> PrepareWorkspaceWithRetryAsync(
        Repository repository,
        string branchName,
        string? previousCommitId,
        CancellationToken cancellationToken)
    {
        var retryCount = 0;
        Exception? lastException = null;

        while (retryCount < _options.MaxRetryAttempts)
        {
            cancellationToken.ThrowIfCancellationRequested();

            try
            {
                return await _repositoryAnalyzer.PrepareWorkspaceAsync(
                    repository, branchName, previousCommitId, cancellationToken);
            }
            catch (Exception ex)
            {
                lastException = ex;
                retryCount++;

                _logger.LogWarning(ex,
                    "Workspace preparation failed. Attempt {Attempt}/{MaxAttempts}, Repository: {Org}/{Repo}",
                    retryCount, _options.MaxRetryAttempts, repository.OrgName, repository.RepoName);

                if (retryCount < _options.MaxRetryAttempts)
                {
                    // 检查是否是工作区损坏
                    if (IsWorkspaceCorrupted(ex))
                    {
                        _logger.LogInformation(
                            "Workspace appears corrupted, cleaning up. Repository: {Org}/{Repo}",
                            repository.OrgName, repository.RepoName);

                        await CleanupCorruptedWorkspaceAsync(repository, cancellationToken);
                    }

                    // 指数退避
                    var delay = _options.RetryBaseDelayMs * (int)Math.Pow(2, retryCount - 1);
                    _logger.LogInformation(
                        "Retrying in {Delay}ms. Repository: {Org}/{Repo}",
                        delay, repository.OrgName, repository.RepoName);

                    await Task.Delay(delay, cancellationToken);
                }
            }
        }

        throw new InvalidOperationException(
            $"Failed to prepare workspace after {_options.MaxRetryAttempts} attempts",
            lastException);
    }

    /// <summary>
    /// 检查异常是否表示工作区损坏
    /// </summary>
    private static bool IsWorkspaceCorrupted(Exception ex)
    {
        var message = ex.Message.ToLowerInvariant();
        return message.Contains("corrupt")
               || message.Contains("invalid")
               || message.Contains("not a git repository")
               || message.Contains("bad object")
               || message.Contains("broken");
    }

    /// <summary>
    /// 清理损坏的工作区
    /// </summary>
    private async Task CleanupCorruptedWorkspaceAsync(
        Repository repository,
        CancellationToken cancellationToken)
    {
        try
        {
            // 创建一个临时工作区对象用于清理
            var workspace = new RepositoryWorkspace
            {
                Organization = repository.OrgName,
                RepositoryName = repository.RepoName
            };

            await _repositoryAnalyzer.CleanupWorkspaceAsync(workspace, cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex,
                "Failed to cleanup corrupted workspace. Repository: {Org}/{Repo}",
                repository.OrgName, repository.RepoName);
        }
    }

    /// <summary>
    /// 安全地通知订阅者（不影响主流程）
    /// </summary>
    private async Task NotifySubscribersSafelyAsync(
        Repository repository,
        RepositoryBranch branch,
        UpdateCheckResult checkResult,
        CancellationToken cancellationToken)
    {
        try
        {
            var notification = new RepositoryUpdateNotification
            {
                RepositoryId = repository.Id,
                RepositoryName = $"{repository.OrgName}/{repository.RepoName}",
                BranchName = branch.BranchName,
                Summary = $"Updated with {checkResult.ChangedFiles?.Length ?? 0} changed files",
                ChangedFilesCount = checkResult.ChangedFiles?.Length ?? 0,
                UpdatedAt = DateTime.UtcNow,
                CommitId = checkResult.CurrentCommitId ?? string.Empty
            };

            await _notificationService.NotifySubscribersAsync(notification, cancellationToken);
        }
        catch (Exception ex)
        {
            // 通知失败不影响主流程
            _logger.LogWarning(ex,
                "Failed to notify subscribers. RepositoryId: {RepositoryId}",
                repository.Id);
        }
    }
}
