using System.IO.Compression;
using System.Text.RegularExpressions;
using Microsoft.EntityFrameworkCore;
using OpenDeepWiki.EFCore;
using OpenDeepWiki.Entities;
using OpenDeepWiki.Entities.Tools;
using OpenDeepWiki.Models.Admin;
using YamlDotNet.Serialization;
using YamlDotNet.Serialization.NamingConventions;

namespace OpenDeepWiki.Services.Admin;

public class AdminToolsService : IAdminToolsService
{
    private readonly IContext _context;
    private readonly ILogger<AdminToolsService> _logger;
    private readonly string _skillsBasePath;

    public AdminToolsService(IContext context, ILogger<AdminToolsService> logger, IConfiguration configuration)
    {
        _context = context;
        _logger = logger;
        _skillsBasePath = configuration["Skills:BasePath"] ?? Path.Combine(AppContext.BaseDirectory, "skills");
        if (!Directory.Exists(_skillsBasePath)) Directory.CreateDirectory(_skillsBasePath);
    }

    public async Task<List<McpConfigDto>> GetMcpConfigsAsync()
    {
        return await _context.McpConfigs.Where(m => !m.IsDeleted).OrderBy(m => m.SortOrder)
            .Select(m => new McpConfigDto
            {
                Id = m.Id, Name = m.Name, Description = m.Description, ServerUrl = m.ServerUrl,
                HasApiKey = !string.IsNullOrEmpty(m.ApiKey), IsActive = m.IsActive,
                SortOrder = m.SortOrder, CreatedAt = m.CreatedAt
            }).ToListAsync();
    }

    public async Task<McpConfigDto> CreateMcpConfigAsync(McpConfigRequest request)
    {
        var config = new McpConfig
        {
            Id = Guid.NewGuid().ToString(), Name = request.Name, Description = request.Description,
            ServerUrl = request.ServerUrl, ApiKey = request.ApiKey, IsActive = request.IsActive,
            SortOrder = request.SortOrder, CreatedAt = DateTime.UtcNow
        };
        _context.McpConfigs.Add(config);
        await _context.SaveChangesAsync();
        return new McpConfigDto
        {
            Id = config.Id, Name = config.Name, Description = config.Description,
            ServerUrl = config.ServerUrl, HasApiKey = !string.IsNullOrEmpty(config.ApiKey),
            IsActive = config.IsActive, SortOrder = config.SortOrder, CreatedAt = config.CreatedAt
        };
    }


    public async Task<bool> UpdateMcpConfigAsync(string id, McpConfigRequest request)
    {
        var config = await _context.McpConfigs.FirstOrDefaultAsync(m => m.Id == id && !m.IsDeleted);
        if (config == null) return false;
        config.Name = request.Name; config.Description = request.Description;
        config.ServerUrl = request.ServerUrl;
        if (request.ApiKey != null) config.ApiKey = request.ApiKey;
        config.IsActive = request.IsActive; config.SortOrder = request.SortOrder;
        config.UpdatedAt = DateTime.UtcNow;
        await _context.SaveChangesAsync();
        return true;
    }

    public async Task<bool> DeleteMcpConfigAsync(string id)
    {
        var config = await _context.McpConfigs.FirstOrDefaultAsync(m => m.Id == id && !m.IsDeleted);
        if (config == null) return false;
        config.IsDeleted = true; config.UpdatedAt = DateTime.UtcNow;
        await _context.SaveChangesAsync();
        return true;
    }

    public async Task<List<SkillConfigDto>> GetSkillConfigsAsync()
    {
        var skills = await _context.SkillConfigs
            .Where(s => !s.IsDeleted)
            .OrderBy(s => s.SortOrder)
            .ThenBy(s => s.Name)
            .ToListAsync();

        var result = new List<SkillConfigDto>(skills.Count);
        foreach (var skill in skills)
        {
            var dto = MapSkillConfig(skill);
            dto.Frontmatter = await LoadSkillFrontmatterAsync(skill);
            result.Add(dto);
        }

        return result;
    }

    public async Task<SkillDetailDto?> GetSkillDetailAsync(string id)
    {
        var skill = await _context.SkillConfigs.FirstOrDefaultAsync(s => s.Id == id && !s.IsDeleted);
        if (skill == null) return null;
        var skillPath = Path.Combine(_skillsBasePath, skill.FolderPath);
        var detail = new SkillDetailDto(MapSkillConfig(skill));
        detail.Frontmatter = await LoadSkillFrontmatterAsync(skill);
        var skillMdPath = Path.Combine(skillPath, "SKILL.md");
        if (File.Exists(skillMdPath)) detail.SkillMdContent = await File.ReadAllTextAsync(skillMdPath);
        detail.Scripts = ListDirectoryFiles(Path.Combine(skillPath, "scripts"));
        detail.References = ListDirectoryFiles(Path.Combine(skillPath, "references"));
        detail.Assets = ListDirectoryFiles(Path.Combine(skillPath, "assets"));
        return detail;
    }

    private SkillConfigDto MapSkillConfig(SkillConfig skill)
    {
        return new SkillConfigDto
        {
            Id = skill.Id,
            Name = skill.Name,
            Description = skill.Description,
            License = skill.License,
            Compatibility = skill.Compatibility,
            AllowedTools = skill.AllowedTools,
            FolderPath = skill.FolderPath,
            IsActive = skill.IsActive,
            SortOrder = skill.SortOrder,
            Author = skill.Author,
            Version = skill.Version,
            Source = skill.Source.ToString().ToLower(),
            SourceUrl = skill.SourceUrl,
            HasScripts = skill.HasScripts,
            HasReferences = skill.HasReferences,
            HasAssets = skill.HasAssets,
            SkillMdSize = skill.SkillMdSize,
            TotalSize = skill.TotalSize,
            CreatedAt = skill.CreatedAt
        };
    }

    private async Task<Dictionary<string, object?>> LoadSkillFrontmatterAsync(SkillConfig skill)
    {
        try
        {
            var skillMdPath = Path.Combine(_skillsBasePath, skill.FolderPath, "SKILL.md");
            if (!File.Exists(skillMdPath))
            {
                return new Dictionary<string, object?>();
            }

            var content = await File.ReadAllTextAsync(skillMdPath);
            var (frontmatter, _) = ParseSkillMd(content);
            return frontmatter;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to load SKILL.md frontmatter for {Skill}", skill.Name);
            return new Dictionary<string, object?>();
        }
    }


    public async Task<SkillConfigDto> UploadSkillAsync(Stream zipStream, string fileName)
    {
        var tempDir = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
        Directory.CreateDirectory(tempDir);
        try
        {
            using (var archive = new ZipArchive(zipStream, ZipArchiveMode.Read))
                archive.ExtractToDirectory(tempDir);
            var skillMdPath = FindSkillMd(tempDir) ?? throw new InvalidOperationException("压缩包中未找到 SKILL.md");
            var skillRootDir = Path.GetDirectoryName(skillMdPath)!;
            var (frontmatter, _) = ParseSkillMd(await File.ReadAllTextAsync(skillMdPath));
            if (!frontmatter.TryGetValue("name", out var nameObj) || string.IsNullOrEmpty(nameObj?.ToString()))
                throw new InvalidOperationException("SKILL.md 缺少 name 字段");
            var name = nameObj.ToString()!;
            if (!Regex.IsMatch(name, @"^[a-z0-9]+(-[a-z0-9]+)*$"))
                throw new InvalidOperationException("name 格式无效");
            if (!frontmatter.TryGetValue("description", out var descObj) || string.IsNullOrEmpty(descObj?.ToString()))
                throw new InvalidOperationException("SKILL.md 缺少 description 字段");
            if (await _context.SkillConfigs.AnyAsync(s => s.Name == name && !s.IsDeleted))
                throw new InvalidOperationException($"已存在同名 Skill: {name}");
            var targetPath = Path.Combine(_skillsBasePath, name);
            if (Directory.Exists(targetPath)) Directory.Delete(targetPath, true);
            Directory.Move(skillRootDir, targetPath);
            var config = new SkillConfig
            {
                Id = Guid.NewGuid().ToString(), Name = name, Description = descObj.ToString()!,
                License = frontmatter.TryGetValue("license", out var l) ? l?.ToString() : null,
                Compatibility = frontmatter.TryGetValue("compatibility", out var c) ? c?.ToString() : null,
                AllowedTools = frontmatter.TryGetValue("allowed-tools", out var t) ? t?.ToString() : null,
                FolderPath = name, IsActive = true, SortOrder = 0, Version = "1.0.0", Source = SkillSource.Local,
                HasScripts = Directory.Exists(Path.Combine(targetPath, "scripts")),
                HasReferences = Directory.Exists(Path.Combine(targetPath, "references")),
                HasAssets = Directory.Exists(Path.Combine(targetPath, "assets")),
                SkillMdSize = new FileInfo(Path.Combine(targetPath, "SKILL.md")).Length,
                TotalSize = CalculateDirectorySize(targetPath), CreatedAt = DateTime.UtcNow
            };
            if (frontmatter.TryGetValue("metadata", out var meta) && meta is Dictionary<object, object> metaDict)
            {
                if (metaDict.TryGetValue("author", out var a)) config.Author = a?.ToString();
                if (metaDict.TryGetValue("version", out var v)) config.Version = v?.ToString() ?? "1.0.0";
            }
            _context.SkillConfigs.Add(config);
            await _context.SaveChangesAsync();
            _logger.LogInformation("上传 Skill: {Name}", name);
            return new SkillConfigDto
            {
                Id = config.Id, Name = config.Name, Description = config.Description, License = config.License,
                Compatibility = config.Compatibility, AllowedTools = config.AllowedTools, FolderPath = config.FolderPath,
                IsActive = config.IsActive, SortOrder = config.SortOrder, Author = config.Author, Version = config.Version,
                Source = config.Source.ToString().ToLower(), HasScripts = config.HasScripts,
                HasReferences = config.HasReferences, HasAssets = config.HasAssets,
                SkillMdSize = config.SkillMdSize, TotalSize = config.TotalSize, CreatedAt = config.CreatedAt
            };
        }
        finally { if (Directory.Exists(tempDir)) try { Directory.Delete(tempDir, true); } catch { } }
    }

    public async Task<bool> UpdateSkillAsync(string id, SkillUpdateRequest request)
    {
        var config = await _context.SkillConfigs.FirstOrDefaultAsync(s => s.Id == id && !s.IsDeleted);
        if (config == null) return false;
        if (request.IsActive.HasValue) config.IsActive = request.IsActive.Value;
        if (request.SortOrder.HasValue) config.SortOrder = request.SortOrder.Value;
        config.UpdatedAt = DateTime.UtcNow;
        await _context.SaveChangesAsync();
        return true;
    }

    public async Task<bool> DeleteSkillAsync(string id)
    {
        var config = await _context.SkillConfigs.FirstOrDefaultAsync(s => s.Id == id && !s.IsDeleted);
        if (config == null) return false;
        var skillPath = Path.Combine(_skillsBasePath, config.FolderPath);
        if (Directory.Exists(skillPath)) Directory.Delete(skillPath, true);
        config.IsDeleted = true; config.UpdatedAt = DateTime.UtcNow;
        await _context.SaveChangesAsync();
        _logger.LogInformation("删除 Skill: {Name}", config.Name);
        return true;
    }

    public async Task<string?> GetSkillFileContentAsync(string id, string filePath)
    {
        var config = await _context.SkillConfigs.FirstOrDefaultAsync(s => s.Id == id && !s.IsDeleted);
        if (config == null) return null;
        var normalizedPath = Path.GetFullPath(Path.Combine(_skillsBasePath, config.FolderPath, filePath));
        var skillBasePath = Path.GetFullPath(Path.Combine(_skillsBasePath, config.FolderPath));
        if (!normalizedPath.StartsWith(skillBasePath)) throw new UnauthorizedAccessException("非法路径");
        return File.Exists(normalizedPath) ? await File.ReadAllTextAsync(normalizedPath) : null;
    }


    public async Task RefreshSkillsFromDiskAsync()
    {
        if (!Directory.Exists(_skillsBasePath)) return;
        var existingNames = (await _context.SkillConfigs.Where(s => !s.IsDeleted).ToListAsync()).Select(s => s.Name).ToHashSet();
        foreach (var dir in Directory.GetDirectories(_skillsBasePath))
        {
            var skillMdPath = Path.Combine(dir, "SKILL.md");
            if (!File.Exists(skillMdPath)) continue;
            var folderName = Path.GetFileName(dir);
            if (existingNames.Contains(folderName)) continue;
            try
            {
                var (frontmatter, _) = ParseSkillMd(await File.ReadAllTextAsync(skillMdPath));
                if (!frontmatter.TryGetValue("name", out var nameObj)) continue;
                var name = nameObj?.ToString();
                if (string.IsNullOrEmpty(name) || name != folderName) continue;
                if (!frontmatter.TryGetValue("description", out var descObj)) continue;
                var config = new SkillConfig
                {
                    Id = Guid.NewGuid().ToString(), Name = name, Description = descObj?.ToString() ?? "",
                    License = frontmatter.TryGetValue("license", out var l) ? l?.ToString() : null,
                    Compatibility = frontmatter.TryGetValue("compatibility", out var c) ? c?.ToString() : null,
                    AllowedTools = frontmatter.TryGetValue("allowed-tools", out var t) ? t?.ToString() : null,
                    FolderPath = folderName, IsActive = true,
                    HasScripts = Directory.Exists(Path.Combine(dir, "scripts")),
                    HasReferences = Directory.Exists(Path.Combine(dir, "references")),
                    HasAssets = Directory.Exists(Path.Combine(dir, "assets")),
                    SkillMdSize = new FileInfo(skillMdPath).Length,
                    TotalSize = CalculateDirectorySize(dir), CreatedAt = DateTime.UtcNow
                };
                _context.SkillConfigs.Add(config);
                _logger.LogInformation("发现 Skill: {Name}", name);
            }
            catch (Exception ex) { _logger.LogWarning(ex, "解析失败: {Path}", dir); }
        }
        await _context.SaveChangesAsync();
    }

    public async Task<List<ModelConfigDto>> GetModelConfigsAsync()
    {
        return await _context.ModelConfigs.Where(m => !m.IsDeleted).OrderByDescending(m => m.IsDefault).ThenBy(m => m.Name)
            .Select(m => new ModelConfigDto
            {
                Id = m.Id, Name = m.Name, Provider = m.Provider, ModelId = m.ModelId, Endpoint = m.Endpoint,
                HasApiKey = !string.IsNullOrEmpty(m.ApiKey), IsDefault = m.IsDefault, IsActive = m.IsActive,
                Description = m.Description, CreatedAt = m.CreatedAt
            }).ToListAsync();
    }

    public async Task<ModelConfigDto> CreateModelConfigAsync(ModelConfigRequest request)
    {
        var config = new ModelConfig
        {
            Id = Guid.NewGuid().ToString(), Name = request.Name, Provider = request.Provider,
            ModelId = request.ModelId, Endpoint = request.Endpoint, ApiKey = request.ApiKey,
            IsDefault = request.IsDefault, IsActive = request.IsActive, Description = request.Description,
            CreatedAt = DateTime.UtcNow
        };
        _context.ModelConfigs.Add(config);
        await _context.SaveChangesAsync();
        return new ModelConfigDto
        {
            Id = config.Id, Name = config.Name, Provider = config.Provider, ModelId = config.ModelId,
            Endpoint = config.Endpoint, HasApiKey = !string.IsNullOrEmpty(config.ApiKey),
            IsDefault = config.IsDefault, IsActive = config.IsActive, Description = config.Description,
            CreatedAt = config.CreatedAt
        };
    }

    public async Task<bool> UpdateModelConfigAsync(string id, ModelConfigRequest request)
    {
        var config = await _context.ModelConfigs.FirstOrDefaultAsync(m => m.Id == id && !m.IsDeleted);
        if (config == null) return false;
        config.Name = request.Name; config.Provider = request.Provider; config.ModelId = request.ModelId;
        config.Endpoint = request.Endpoint;
        if (request.ApiKey != null) config.ApiKey = request.ApiKey;
        config.IsDefault = request.IsDefault; config.IsActive = request.IsActive;
        config.Description = request.Description; config.UpdatedAt = DateTime.UtcNow;
        await _context.SaveChangesAsync();
        return true;
    }

    public async Task<bool> DeleteModelConfigAsync(string id)
    {
        var config = await _context.ModelConfigs.FirstOrDefaultAsync(m => m.Id == id && !m.IsDeleted);
        if (config == null) return false;
        config.IsDeleted = true; config.UpdatedAt = DateTime.UtcNow;
        await _context.SaveChangesAsync();
        return true;
    }

    private static string? FindSkillMd(string directory)
    {
        var skillMd = Path.Combine(directory, "SKILL.md");
        if (File.Exists(skillMd)) return skillMd;
        foreach (var subDir in Directory.GetDirectories(directory))
        {
            skillMd = Path.Combine(subDir, "SKILL.md");
            if (File.Exists(skillMd)) return skillMd;
        }
        return null;
    }

    private static (Dictionary<string, object?> frontmatter, string body) ParseSkillMd(string content)
    {
        var frontmatter = new Dictionary<string, object?>();
        var body = content;
        if (content.StartsWith("---"))
        {
            var endIndex = content.IndexOf("---", 3);
            if (endIndex > 0)
            {
                var yamlContent = content[3..endIndex].Trim();
                body = content[(endIndex + 3)..].Trim();
                try
                {
                    var deserializer = new DeserializerBuilder().WithNamingConvention(HyphenatedNamingConvention.Instance).Build();
                    frontmatter = deserializer.Deserialize<Dictionary<string, object?>>(yamlContent) ?? new();
                }
                catch { }
            }
        }
        return (frontmatter, body);
    }

    private static List<SkillFileInfo> ListDirectoryFiles(string directory)
    {
        var files = new List<SkillFileInfo>();
        if (!Directory.Exists(directory)) return files;
        foreach (var file in Directory.GetFiles(directory, "*", SearchOption.AllDirectories))
        {
            var info = new FileInfo(file);
            files.Add(new SkillFileInfo { FileName = info.Name, RelativePath = Path.GetRelativePath(directory, file), Size = info.Length, LastModified = info.LastWriteTimeUtc });
        }
        return files;
    }

    private static long CalculateDirectorySize(string directory) =>
        !Directory.Exists(directory) ? 0 : Directory.GetFiles(directory, "*", SearchOption.AllDirectories).Sum(f => new FileInfo(f).Length);
}
