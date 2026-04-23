using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using OpenDeepWiki.EFCore;
using OpenDeepWiki.Entities;
using OpenDeepWiki.Models.Admin;

namespace OpenDeepWiki.Services.Admin;

/// <summary>
/// 管理端对话助手配置服务实现
/// </summary>
public class AdminChatAssistantService : IAdminChatAssistantService
{
    private readonly IContext _context;
    private readonly ILogger<AdminChatAssistantService> _logger;

    public AdminChatAssistantService(IContext context, ILogger<AdminChatAssistantService> logger)
    {
        _context = context;
        _logger = logger;
    }

    public async Task<ChatAssistantConfigOptionsDto> GetConfigWithOptionsAsync()
    {
        var config = await GetOrCreateConfigAsync();
        var enabledModelIds = ParseJsonArray(config.EnabledModelIds);
        var enabledMcpIds = ParseJsonArray(config.EnabledMcpIds);
        var enabledSkillIds = ParseJsonArray(config.EnabledSkillIds);

        // 获取所有可用的模型
        var models = await _context.ModelConfigs
            .Where(m => !m.IsDeleted)
            .OrderByDescending(m => m.IsDefault)
            .ThenBy(m => m.Name)
            .Select(m => new SelectableItemDto
            {
                Id = m.Id,
                Name = m.Name,
                Description = m.Description,
                IsActive = m.IsActive,
                IsSelected = enabledModelIds.Contains(m.Id)
            })
            .ToListAsync();

        // 获取所有可用的MCP
        var mcps = await _context.McpConfigs
            .Where(m => !m.IsDeleted)
            .OrderBy(m => m.SortOrder)
            .ThenBy(m => m.Name)
            .Select(m => new SelectableItemDto
            {
                Id = m.Id,
                Name = m.Name,
                Description = m.Description,
                IsActive = m.IsActive,
                IsSelected = enabledMcpIds.Contains(m.Id)
            })
            .ToListAsync();

        // 获取所有可用的Skill
        var skills = await _context.SkillConfigs
            .Where(s => !s.IsDeleted)
            .OrderBy(s => s.SortOrder)
            .ThenBy(s => s.Name)
            .Select(s => new SelectableItemDto
            {
                Id = s.Id,
                Name = s.Name,
                Description = s.Description,
                IsActive = s.IsActive,
                IsSelected = enabledSkillIds.Contains(s.Id)
            })
            .ToListAsync();

        return new ChatAssistantConfigOptionsDto
        {
            Config = MapToDto(config),
            AvailableModels = models,
            AvailableMcps = mcps,
            AvailableSkills = skills
        };
    }

    public async Task<ChatAssistantConfigDto> GetConfigAsync()
    {
        var config = await GetOrCreateConfigAsync();
        return MapToDto(config);
    }

    public async Task<ChatAssistantConfigDto> UpdateConfigAsync(UpdateChatAssistantConfigRequest request)
    {
        var config = await GetOrCreateConfigAsync();

        config.IsEnabled = request.IsEnabled;
        config.EnabledModelIds = SerializeJsonArray(request.EnabledModelIds);
        config.EnabledMcpIds = SerializeJsonArray(request.EnabledMcpIds);
        config.EnabledSkillIds = SerializeJsonArray(request.EnabledSkillIds);
        config.DefaultModelId = request.DefaultModelId;
        config.EnableImageUpload = request.EnableImageUpload;
        config.UpdatedAt = DateTime.UtcNow;

        await _context.SaveChangesAsync();

        _logger.LogInformation("对话助手配置已更新: IsEnabled={IsEnabled}, Models={ModelCount}, MCPs={McpCount}, Skills={SkillCount}, EnableImageUpload={EnableImageUpload}",
            config.IsEnabled, request.EnabledModelIds.Count, request.EnabledMcpIds.Count, request.EnabledSkillIds.Count, config.EnableImageUpload);

        return MapToDto(config);
    }

    private async Task<ChatAssistantConfig> GetOrCreateConfigAsync()
    {
        var config = await _context.ChatAssistantConfigs
            .FirstOrDefaultAsync(c => !c.IsDeleted);

        if (config == null)
        {
            config = new ChatAssistantConfig
            {
                Id = Guid.NewGuid(),
                IsEnabled = false,
                CreatedAt = DateTime.UtcNow
            };
            _context.ChatAssistantConfigs.Add(config);
            await _context.SaveChangesAsync();
            _logger.LogInformation("创建了新的对话助手配置");
        }

        return config;
    }

    private static ChatAssistantConfigDto MapToDto(ChatAssistantConfig config)
    {
        return new ChatAssistantConfigDto
        {
            Id = config.Id.ToString(),
            IsEnabled = config.IsEnabled,
            EnabledModelIds = ParseJsonArray(config.EnabledModelIds),
            EnabledMcpIds = ParseJsonArray(config.EnabledMcpIds),
            EnabledSkillIds = ParseJsonArray(config.EnabledSkillIds),
            DefaultModelId = config.DefaultModelId,
            EnableImageUpload = config.EnableImageUpload,
            CreatedAt = config.CreatedAt,
            UpdatedAt = config.UpdatedAt
        };
    }

    private static List<string> ParseJsonArray(string? json)
    {
        if (string.IsNullOrWhiteSpace(json))
            return new List<string>();

        try
        {
            return JsonSerializer.Deserialize<List<string>>(json) ?? new List<string>();
        }
        catch
        {
            return new List<string>();
        }
    }

    private static string? SerializeJsonArray(List<string>? items)
    {
        if (items == null || items.Count == 0)
            return null;

        return JsonSerializer.Serialize(items);
    }
}
