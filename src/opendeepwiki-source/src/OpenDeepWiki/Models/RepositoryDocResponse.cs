namespace OpenDeepWiki.Models;

/// <summary>
/// 仓库文档响应
/// </summary>
public class RepositoryDocResponse
{
    /// <summary>
    /// 文档是否存在
    /// </summary>
    public bool Exists { get; set; } = true;

    /// <summary>
    /// 文档路径（slug）
    /// </summary>
    public string Slug { get; set; } = string.Empty;

    /// <summary>
    /// Markdown 内容
    /// </summary>
    public string Content { get; set; } = string.Empty;

    /// <summary>
    /// 来源文件列表
    /// 记录生成此文档时读取的源代码文件路径
    /// </summary>
    public List<string> SourceFiles { get; set; } = [];
}
