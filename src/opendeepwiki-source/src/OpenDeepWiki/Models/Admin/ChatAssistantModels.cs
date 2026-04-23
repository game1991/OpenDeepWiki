namespace OpenDeepWiki.Models.Admin;

/// <summary>
/// 对话助手配置 DTO
/// </summary>
public class ChatAssistantConfigDto
{
    public string Id { get; set; } = string.Empty;
    public bool IsEnabled { get; set; }
    public List<string> EnabledModelIds { get; set; } = new();
    public List<string> EnabledMcpIds { get; set; } = new();
    public List<string> EnabledSkillIds { get; set; } = new();
    public string? DefaultModelId { get; set; }
    public bool EnableImageUpload { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime? UpdatedAt { get; set; }
}

/// <summary>
/// 更新对话助手配置请求
/// </summary>
public class UpdateChatAssistantConfigRequest
{
    public bool IsEnabled { get; set; }
    public List<string> EnabledModelIds { get; set; } = new();
    public List<string> EnabledMcpIds { get; set; } = new();
    public List<string> EnabledSkillIds { get; set; } = new();
    public string? DefaultModelId { get; set; }
    public bool EnableImageUpload { get; set; }
}

/// <summary>
/// 可选项 DTO（用于模型、MCP、Skill选择列表）
/// </summary>
public class SelectableItemDto
{
    public string Id { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    public bool IsActive { get; set; }
    public bool IsSelected { get; set; }
}

/// <summary>
/// 对话助手配置选项响应
/// </summary>
public class ChatAssistantConfigOptionsDto
{
    public ChatAssistantConfigDto Config { get; set; } = new();
    public List<SelectableItemDto> AvailableModels { get; set; } = new();
    public List<SelectableItemDto> AvailableMcps { get; set; } = new();
    public List<SelectableItemDto> AvailableSkills { get; set; } = new();
}
