namespace OpenDeepWiki.Services.Repositories;

/// <summary>
/// 增量更新配置选项
/// </summary>
public class IncrementalUpdateOptions
{
    /// <summary>
    /// 配置节名称
    /// </summary>
    public const string SectionName = "IncrementalUpdate";

    /// <summary>
    /// 轮询间隔（秒）
    /// </summary>
    public int PollingIntervalSeconds { get; set; } = 60;

    /// <summary>
    /// 默认更新检查间隔（分钟）
    /// </summary>
    public int DefaultUpdateIntervalMinutes { get; set; } = 60;

    /// <summary>
    /// 最小更新检查间隔（分钟）
    /// </summary>
    public int MinUpdateIntervalMinutes { get; set; } = 5;

    /// <summary>
    /// 最大重试次数
    /// </summary>
    public int MaxRetryAttempts { get; set; } = 3;

    /// <summary>
    /// 重试基础延迟（毫秒）
    /// </summary>
    public int RetryBaseDelayMs { get; set; } = 1000;

    /// <summary>
    /// 手动触发任务优先级
    /// </summary>
    public int ManualTriggerPriority { get; set; } = 100;
}
