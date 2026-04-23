using System.Diagnostics;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using OpenDeepWiki.EFCore;
using OpenDeepWiki.Entities;
using OpenDeepWiki.Services.Repositories;
using OpenDeepWiki.Services.Wiki;

namespace OpenDeepWiki.Services.MindMap;

/// <summary>
/// 思维导图生成后台服务
/// 独立于文档生成流程，异步处理思维导图生成任务
/// 查询已完成处理但尚未生成思维导图的仓库
/// </summary>
public class MindMapWorker : BackgroundService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<MindMapWorker> _logger;
    private static readonly TimeSpan PollingInterval = TimeSpan.FromSeconds(30);

    public MindMapWorker(
        IServiceScopeFactory scopeFactory,
        ILogger<MindMapWorker> logger)
    {
        _scopeFactory = scopeFactory;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("MindMap worker started. Polling interval: {PollingInterval}s",
            PollingInterval.TotalSeconds);

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await ProcessPendingMindMapsAsync(stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                _logger.LogInformation("MindMap worker is shutting down");
                break;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "MindMap processing loop failed unexpectedly");
            }

            await Task.Delay(PollingInterval, stoppingToken);
        }

        _logger.LogInformation("MindMap worker stopped");
    }

    private async Task ProcessPendingMindMapsAsync(CancellationToken stoppingToken)
    {
        using var scope = _scopeFactory.CreateScope();
        var context = scope.ServiceProvider.GetService<IContext>();
        var repositoryAnalyzer = scope.ServiceProvider.GetService<IRepositoryAnalyzer>();
        var wikiGenerator = scope.ServiceProvider.GetService<IWikiGenerator>();
        var processingLogService = scope.ServiceProvider.GetService<IProcessingLogService>();

        if (context == null || repositoryAnalyzer == null || wikiGenerator == null)
        {
            _logger.LogWarning("Required services not registered, skip mindmap processing");
            return;
        }

        while (!stoppingToken.IsCancellationRequested)
        {
            // 查询已完成处理但思维导图状态为 Pending 或 Failed 的分支语言
            var branchLanguage = await context.BranchLanguages
                .Include(bl => bl.RepositoryBranch)
                .ThenInclude(rb => rb!.Repository)
                .Where(bl => !bl.IsDeleted &&
                             bl.RepositoryBranch != null &&
                             !bl.RepositoryBranch.IsDeleted &&
                             bl.RepositoryBranch.Repository != null &&
                             !bl.RepositoryBranch.Repository.IsDeleted &&
                             bl.RepositoryBranch.Repository.Status == RepositoryStatus.Completed &&
                             (bl.MindMapStatus == MindMapStatus.Pending || bl.MindMapStatus == MindMapStatus.Failed))
                .OrderBy(bl => bl.CreatedAt)
                .FirstOrDefaultAsync(stoppingToken);

            if (branchLanguage == null)
            {
                _logger.LogDebug("No pending mindmap tasks found");
                break;
            }

            await ProcessMindMapAsync(
                branchLanguage,
                context,
                repositoryAnalyzer,
                wikiGenerator,
                processingLogService,
                stoppingToken);
        }
    }

    private async Task ProcessMindMapAsync(
        BranchLanguage branchLanguage,
        IContext context,
        IRepositoryAnalyzer repositoryAnalyzer,
        IWikiGenerator wikiGenerator,
        IProcessingLogService? processingLogService,
        CancellationToken stoppingToken)
    {
        var repository = branchLanguage.RepositoryBranch!.Repository!;
        var branch = branchLanguage.RepositoryBranch;
        var stopwatch = Stopwatch.StartNew();

        _logger.LogInformation(
            "Starting mindmap generation. BranchLanguageId: {BranchLanguageId}, Repository: {Org}/{Repo}, Language: {Lang}",
            branchLanguage.Id, repository.OrgName, repository.RepoName, branchLanguage.LanguageCode);

        // 设置当前仓库ID到WikiGenerator（用于日志记录）
        if (wikiGenerator is WikiGenerator generator)
        {
            generator.SetCurrentRepository(repository.Id);
        }

        // 记录开始生成
        if (processingLogService != null)
        {
            await processingLogService.LogAsync(
                repository.Id,
                ProcessingStep.MindMap,
                $"开始生成思维导图: {branchLanguage.LanguageCode}",
                cancellationToken: stoppingToken);
        }

        try
        {
            // 准备工作区
            var workspace = await repositoryAnalyzer.PrepareWorkspaceAsync(
                repository,
                branch!.BranchName,
                branch.LastCommitId,
                stoppingToken);

            try
            {
                // 执行思维导图生成
                await wikiGenerator.GenerateMindMapAsync(workspace, branchLanguage, stoppingToken);

                stopwatch.Stop();
                _logger.LogInformation(
                    "MindMap generation completed. BranchLanguageId: {BranchLanguageId}, Duration: {Duration}ms",
                    branchLanguage.Id, stopwatch.ElapsedMilliseconds);

                // 记录完成
                if (processingLogService != null)
                {
                    await processingLogService.LogAsync(
                        repository.Id,
                        ProcessingStep.MindMap,
                        $"思维导图生成完成: {branchLanguage.LanguageCode}，耗时 {stopwatch.ElapsedMilliseconds}ms",
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
            throw;
        }
        catch (Exception ex)
        {
            stopwatch.Stop();
            _logger.LogError(ex,
                "MindMap generation failed. BranchLanguageId: {BranchLanguageId}, Duration: {Duration}ms",
                branchLanguage.Id, stopwatch.ElapsedMilliseconds);

            // 记录失败（MindMapStatus 已在 WikiGenerator.GenerateMindMapAsync 中更新）
            if (processingLogService != null)
            {
                await processingLogService.LogAsync(
                    repository.Id,
                    ProcessingStep.MindMap,
                    $"思维导图生成失败: {branchLanguage.LanguageCode} - {ex.Message}",
                    cancellationToken: stoppingToken);
            }
        }
    }
}
