using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace OpenDeepWiki.Entities;

/// <summary>
/// Token 消耗记录实体
/// </summary>
public class TokenUsage : AggregateRoot<string>
{
    /// <summary>
    /// 关联的仓库ID（可选）
    /// </summary>
    [StringLength(36)]
    public string? RepositoryId { get; set; }

    /// <summary>
    /// 关联的用户ID（可选）
    /// </summary>
    [StringLength(36)]
    public string? UserId { get; set; }

    /// <summary>
    /// 输入 Token 数量
    /// </summary>
    public int InputTokens { get; set; }

    /// <summary>
    /// 输出 Token 数量
    /// </summary>
    public int OutputTokens { get; set; }

    /// <summary>
    /// 使用的模型名称
    /// </summary>
    [StringLength(100)]
    public string? ModelName { get; set; }

    /// <summary>
    /// 操作类型（catalog, content, chat 等）
    /// </summary>
    [StringLength(50)]
    public string? Operation { get; set; }

    /// <summary>
    /// 记录时间
    /// </summary>
    public DateTime RecordedAt { get; set; } = DateTime.UtcNow;

    /// <summary>
    /// 关联的仓库导航属性
    /// </summary>
    [ForeignKey("RepositoryId")]
    public virtual Repository? Repository { get; set; }

    /// <summary>
    /// 关联的用户导航属性
    /// </summary>
    [ForeignKey("UserId")]
    public virtual User? User { get; set; }
}
