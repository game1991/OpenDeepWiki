namespace OpenDeepWiki.Models.Admin;

/// <summary>
/// MCP 配置请求
/// </summary>
public class McpConfigRequest
{
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    public string ServerUrl { get; set; } = string.Empty;
    public string? ApiKey { get; set; }
    public bool IsActive { get; set; } = true;
    public int SortOrder { get; set; }
}

/// <summary>
/// MCP 配置 DTO
/// </summary>
public class McpConfigDto
{
    public string Id { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    public string ServerUrl { get; set; } = string.Empty;
    public bool HasApiKey { get; set; }
    public bool IsActive { get; set; }
    public int SortOrder { get; set; }
    public DateTime CreatedAt { get; set; }
}

/// <summary>
/// Skill 配置 DTO（遵循 Agent Skills 标准）
/// </summary>
public class SkillConfigDto
{
    public string Id { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string? License { get; set; }
    public string? Compatibility { get; set; }
    public string? AllowedTools { get; set; }
    public string FolderPath { get; set; } = string.Empty;
    public bool IsActive { get; set; }
    public int SortOrder { get; set; }
    public string? Author { get; set; }
    public string Version { get; set; } = "1.0.0";
    public string Source { get; set; } = "local";
    public string? SourceUrl { get; set; }
    public bool HasScripts { get; set; }
    public bool HasReferences { get; set; }
    public bool HasAssets { get; set; }
    public long SkillMdSize { get; set; }
    public long TotalSize { get; set; }
    public DateTime CreatedAt { get; set; }
    /// <summary>
    /// Parsed SKILL.md frontmatter for UI display (dynamic parameters, metadata, etc.)
    /// </summary>
    public Dictionary<string, object?> Frontmatter { get; set; } = new();
}

/// <summary>
/// Skill 详情 DTO（包含 SKILL.md 内容）
/// </summary>
public class SkillDetailDto : SkillConfigDto
{
    public SkillDetailDto()
    {
    }

    public SkillDetailDto(SkillConfigDto source)
    {
        Id = source.Id;
        Name = source.Name;
        Description = source.Description;
        License = source.License;
        Compatibility = source.Compatibility;
        AllowedTools = source.AllowedTools;
        FolderPath = source.FolderPath;
        IsActive = source.IsActive;
        SortOrder = source.SortOrder;
        Author = source.Author;
        Version = source.Version;
        Source = source.Source;
        SourceUrl = source.SourceUrl;
        HasScripts = source.HasScripts;
        HasReferences = source.HasReferences;
        HasAssets = source.HasAssets;
        SkillMdSize = source.SkillMdSize;
        TotalSize = source.TotalSize;
        CreatedAt = source.CreatedAt;
        Frontmatter = new Dictionary<string, object?>(source.Frontmatter);
    }

    /// <summary>
    /// SKILL.md 完整内容
    /// </summary>
    public string SkillMdContent { get; set; } = string.Empty;

    /// <summary>
    /// scripts 目录下的文件列表
    /// </summary>
    public List<SkillFileInfo> Scripts { get; set; } = new();

    /// <summary>
    /// references 目录下的文件列表
    /// </summary>
    public List<SkillFileInfo> References { get; set; } = new();

    /// <summary>
    /// assets 目录下的文件列表
    /// </summary>
    public List<SkillFileInfo> Assets { get; set; } = new();
}

/// <summary>
/// Skill 文件信息
/// </summary>
public class SkillFileInfo
{
    public string FileName { get; set; } = string.Empty;
    public string RelativePath { get; set; } = string.Empty;
    public long Size { get; set; }
    public DateTime LastModified { get; set; }
}

/// <summary>
/// Skill 更新请求（仅更新管理字段）
/// </summary>
public class SkillUpdateRequest
{
    public bool? IsActive { get; set; }
    public int? SortOrder { get; set; }
}

/// <summary>
/// 模型配置请求
/// </summary>
public class ModelConfigRequest
{
    public string Name { get; set; } = string.Empty;
    public string Provider { get; set; } = string.Empty;
    public string ModelId { get; set; } = string.Empty;
    public string? Endpoint { get; set; }
    public string? ApiKey { get; set; }
    public bool IsDefault { get; set; }
    public bool IsActive { get; set; } = true;
    public string? Description { get; set; }
}

/// <summary>
/// 模型配置 DTO
/// </summary>
public class ModelConfigDto
{
    public string Id { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string Provider { get; set; } = string.Empty;
    public string ModelId { get; set; } = string.Empty;
    public string? Endpoint { get; set; }
    public bool HasApiKey { get; set; }
    public bool IsDefault { get; set; }
    public bool IsActive { get; set; }
    public string? Description { get; set; }
    public DateTime CreatedAt { get; set; }
}
