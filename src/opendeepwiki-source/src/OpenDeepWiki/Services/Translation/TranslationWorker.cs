using System.Diagnostics;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using OpenDeepWiki.EFCore;
using OpenDeepWiki.Entities;
using OpenDeepWiki.Services.Repositories;
using OpenDeepWiki.Services.Wiki;

namespace OpenDeepWiki.Services.Translation;

/// <summary>
/// 翻译后台服务
/// 独立于文档生成流程，自动发现并处理翻译任务
/// </summary>
public class TranslationWorker : BackgroundService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<TranslationWorker> _logger;
    private static readonly TimeSpan PollingInterval = TimeSpan.FromSeconds(30);

    public TranslationWorker(
        IServiceScopeFactory scopeFactory,
        ILogger<TranslationWorker> logger)
    {
        _scopeFactory = scopeFactory;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("Translation worker started. Polling interval: {PollingInterval}s",
            PollingInterval.TotalSeconds);

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await ProcessPendingTasksAsync(stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                _logger.LogInformation("Translation worker is shutting down");
                break;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Translation processing loop failed unexpectedly");
            }

            await Task.Delay(PollingInterval, stoppingToken);
        }

        _logger.LogInformation("Translation worker stopped");
    }

    private async Task ProcessPendingTasksAsync(CancellationToken stoppingToken)
    {
        using var scope = _scopeFactory.CreateScope();
        var translationService = scope.ServiceProvider.GetService<ITranslationService>();
        var context = scope.ServiceProvider.GetService<IContext>();
        var repositoryAnalyzer = scope.ServiceProvider.GetService<IRepositoryAnalyzer>();
        var wikiGenerator = scope.ServiceProvider.GetService<IWikiGenerator>();
        var processingLogService = scope.ServiceProvider.GetService<IProcessingLogService>();
        var wikiOptions = scope.ServiceProvider.GetService<IOptions<WikiGeneratorOptions>>()?.Value;

        if (translationService == null || context == null || repositoryAnalyzer == null || 
            wikiGenerator == null || wikiOptions == null)
        {
            _logger.LogWarning("Required services not registered, skip translation processing");
            return;
        }

        // 先扫描并创建需要的翻译任务
        await ScanAndCreateTranslationTasksAsync(context, wikiOptions, stoppingToken);

        // 然后处理待处理的任务
        while (!stoppingToken.IsCancellationRequested)
        {
            var task = await translationService.GetNextPendingTaskAsync(stoppingToken);
            if (task == null)
            {
                _logger.LogDebug("No pending translation tasks found");
                break;
            }

            await ProcessTaskAsync(
                task,
                translationService,
                context,
                repositoryAnalyzer,
                wikiGenerator,
                processingLogService,
                stoppingToken);
        }
    }

    /// <summary>
    /// 扫描已完成的仓库，自动创建需要的翻译任务
    /// </summary>
    private async Task ScanAndCreateTranslationTasksAsync(
        IContext context,
        WikiGeneratorOptions wikiOptions,
        CancellationToken stoppingToken)
    {
        // 查询已完成处理的仓库的所有分支语言
        var branchLanguages = await context.BranchLanguages
            .Include(bl => bl.RepositoryBranch)
            .ThenInclude(rb => rb!.Repository)
            .Where(bl => !bl.IsDeleted &&
                         bl.RepositoryBranch != null &&
                         !bl.RepositoryBranch.IsDeleted &&
                         bl.RepositoryBranch.Repository != null &&
                         !bl.RepositoryBranch.Repository.IsDeleted &&
                         bl.RepositoryBranch.Repository.Status == RepositoryStatus.Completed)
            .ToListAsync(stoppingToken);

        var createdCount = 0;

        foreach (var branchLanguage in branchLanguages)
        {
            stoppingToken.ThrowIfCancellationRequested();

            var translationLanguages = wikiOptions.GetTranslationLanguages(branchLanguage.LanguageCode);
            if (translationLanguages.Count == 0)
            {
                continue;
            }

            var repository = branchLanguage.RepositoryBranch!.Repository!;

            foreach (var targetLang in translationLanguages)
            {
                // 检查目标语言是否已存在
                var existingLang = await context.BranchLanguages
                    .AnyAsync(l => l.RepositoryBranchId == branchLanguage.RepositoryBranchId &&
                                   l.LanguageCode == targetLang &&
                                   !l.IsDeleted, stoppingToken);

                if (existingLang)
                {
                    continue;
                }

                // 检查是否已有翻译任务（任何状态）
                var existingTask = await context.TranslationTasks
                    .FirstOrDefaultAsync(t => t.RepositoryBranchId == branchLanguage.RepositoryBranchId &&
                                              t.TargetLanguageCode == targetLang &&
                                              !t.IsDeleted, stoppingToken);

                if (existingTask != null)
                {
                    // 如果是待处理或处理中的任务，跳过
                    if (existingTask.Status == TranslationTaskStatus.Pending ||
                        existingTask.Status == TranslationTaskStatus.Processing)
                    {
                        continue;
                    }

                    // 如果是已完成的任务，跳过（目标语言应该已存在）
                    if (existingTask.Status == TranslationTaskStatus.Completed)
                    {
                        continue;
                    }

                    // 如果是失败的任务且未超过最大重试次数，重置为待处理
                    if (existingTask.Status == TranslationTaskStatus.Failed &&
                        existingTask.RetryCount < existingTask.MaxRetryCount)
                    {
                        existingTask.Status = TranslationTaskStatus.Pending;
                        existingTask.ErrorMessage = null;
                        createdCount++;

                        _logger.LogDebug("Translation task reset to pending. TargetLang: {TargetLang}, Repository: {Org}/{Repo}, RetryCount: {RetryCount}",
                            targetLang, repository.OrgName, repository.RepoName, existingTask.RetryCount);
                    }

                    continue;
                }

                // 创建翻译任务
                var task = new TranslationTask
                {
                    Id = Guid.NewGuid().ToString(),
                    RepositoryId = repository.Id,
                    RepositoryBranchId = branchLanguage.RepositoryBranchId,
                    SourceBranchLanguageId = branchLanguage.Id,
                    TargetLanguageCode = targetLang.ToLowerInvariant(),
                    Status = TranslationTaskStatus.Pending
                };

                context.TranslationTasks.Add(task);
                createdCount++;

                _logger.LogDebug("Translation task created. TargetLang: {TargetLang}, Repository: {Org}/{Repo}",
                    targetLang, repository.OrgName, repository.RepoName);
            }
        }

        if (createdCount > 0)
        {
            await context.SaveChangesAsync(stoppingToken);
            _logger.LogInformation("Created {Count} translation tasks from scan", createdCount);
        }
    }

    private async Task ProcessTaskAsync(
        TranslationTask task,
        ITranslationService translationService,
        IContext context,
        IRepositoryAnalyzer repositoryAnalyzer,
        IWikiGenerator wikiGenerator,
        IProcessingLogService? processingLogService,
        CancellationToken stoppingToken)
    {
        var stopwatch = Stopwatch.StartNew();

        _logger.LogInformation(
            "Starting translation task. TaskId: {TaskId}, TargetLang: {TargetLang}, RetryCount: {RetryCount}",
            task.Id, task.TargetLanguageCode, task.RetryCount);

        // 标记为处理中
        await translationService.MarkAsProcessingAsync(task.Id, stoppingToken);

        // 设置当前仓库ID到WikiGenerator（用于日志记录）
        if (wikiGenerator is WikiGenerator generator)
        {
            generator.SetCurrentRepository(task.RepositoryId);
        }

        // 记录开始翻译
        if (processingLogService != null)
        {
            await processingLogService.LogAsync(
                task.RepositoryId,
                ProcessingStep.Translation,
                $"开始翻译任务: {task.TargetLanguageCode}",
                cancellationToken: stoppingToken);
        }

        try
        {
            // 获取仓库信息
            var repository = await context.Repositories
                .FirstOrDefaultAsync(r => r.Id == task.RepositoryId && !r.IsDeleted, stoppingToken);

            if (repository == null)
            {
                throw new InvalidOperationException($"Repository not found: {task.RepositoryId}");
            }

            // 获取分支信息
            var branch = await context.RepositoryBranches
                .FirstOrDefaultAsync(b => b.Id == task.RepositoryBranchId && !b.IsDeleted, stoppingToken);

            if (branch == null)
            {
                throw new InvalidOperationException($"Branch not found: {task.RepositoryBranchId}");
            }

            // 获取源语言信息
            var sourceBranchLanguage = await context.BranchLanguages
                .FirstOrDefaultAsync(l => l.Id == task.SourceBranchLanguageId && !l.IsDeleted, stoppingToken);

            if (sourceBranchLanguage == null)
            {
                throw new InvalidOperationException($"Source branch language not found: {task.SourceBranchLanguageId}");
            }

            // 准备工作区
            var workspace = await repositoryAnalyzer.PrepareWorkspaceAsync(
                repository,
                branch.BranchName,
                branch.LastCommitId,
                stoppingToken);

            try
            {
                // 执行翻译
                await wikiGenerator.TranslateWikiAsync(
                    workspace,
                    sourceBranchLanguage,
                    task.TargetLanguageCode,
                    stoppingToken);

                // 标记为完成
                await translationService.MarkAsCompletedAsync(task.Id, stoppingToken);

                stopwatch.Stop();
                _logger.LogInformation(
                    "Translation task completed. TaskId: {TaskId}, TargetLang: {TargetLang}, Duration: {Duration}ms",
                    task.Id, task.TargetLanguageCode, stopwatch.ElapsedMilliseconds);

                // 记录完成
                if (processingLogService != null)
                {
                    await processingLogService.LogAsync(
                        task.RepositoryId,
                        ProcessingStep.Translation,
                        $"翻译完成: {task.TargetLanguageCode}，耗时 {stopwatch.ElapsedMilliseconds}ms",
                        cancellationToken: stoppingToken);
                }
            }
            finally
            {
                // 清理工作区
                await repositoryAnalyzer.CleanupWorkspaceAsync(workspace, stoppingToken);
            }
        }
        catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
        {
            // 取消时重置为待处理
            await translationService.MarkAsFailedAsync(task.Id, "Task cancelled", stoppingToken);
            throw;
        }
        catch (Exception ex)
        {
            stopwatch.Stop();
            _logger.LogError(ex,
                "Translation task failed. TaskId: {TaskId}, TargetLang: {TargetLang}, Duration: {Duration}ms",
                task.Id, task.TargetLanguageCode, stopwatch.ElapsedMilliseconds);

            // 标记为失败（会自动重试）
            await translationService.MarkAsFailedAsync(task.Id, ex.Message, stoppingToken);

            // 记录失败
            if (processingLogService != null)
            {
                await processingLogService.LogAsync(
                    task.RepositoryId,
                    ProcessingStep.Translation,
                    $"翻译失败: {task.TargetLanguageCode} - {ex.Message}",
                    cancellationToken: stoppingToken);
            }
        }
    }
}
