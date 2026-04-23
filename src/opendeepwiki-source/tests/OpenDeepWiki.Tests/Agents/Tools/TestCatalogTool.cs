using System.ComponentModel;
using System.Text.Json;
using OpenDeepWiki.Entities;

namespace OpenDeepWiki.Tests.Agents.Tools;

/// <summary>
/// Test version of CatalogTool for property-based testing.
/// AI Tool for reading, writing, and editing wiki catalog structures.
/// </summary>
public class TestCatalogTool
{
    private readonly TestCatalogStorage _storage;
    
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    public TestCatalogTool(TestCatalogStorage storage)
    {
        _storage = storage ?? throw new ArgumentNullException(nameof(storage));
    }

    [Description("读取当前的 Wiki 目录结构，返回 JSON 格式的目录树")]
    public async Task<string> ReadAsync(CancellationToken cancellationToken = default)
    {
        return await _storage.GetCatalogJsonAsync(cancellationToken);
    }

    [Description("写入完整的 Wiki 目录结构，替换现有的所有目录项")]
    public async Task WriteAsync(
        [Description("JSON 格式的目录结构，包含 items 数组，每个 item 包含 title, path, order, children")] 
        string catalogJson,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(catalogJson))
        {
            throw new ArgumentException("Catalog JSON cannot be empty.", nameof(catalogJson));
        }

        try
        {
            var root = JsonSerializer.Deserialize<CatalogRoot>(catalogJson, JsonOptions);
            if (root == null)
            {
                throw new ArgumentException("Invalid catalog JSON format: deserialization returned null.", nameof(catalogJson));
            }
            
            ValidateCatalogStructure(root);
        }
        catch (JsonException ex)
        {
            throw new ArgumentException($"Invalid catalog JSON format: {ex.Message}", nameof(catalogJson), ex);
        }

        await _storage.SetCatalogAsync(catalogJson, cancellationToken);
    }

    [Description("编辑目录结构中的指定节点，合并更改并保留未修改的节点")]
    public async Task EditAsync(
        [Description("要编辑的节点路径，如 '1-overview'")] 
        string path,
        [Description("新的节点数据 JSON，包含 title, path, order, children")] 
        string nodeJson,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(path))
        {
            throw new ArgumentException("Path cannot be empty.", nameof(path));
        }

        if (string.IsNullOrWhiteSpace(nodeJson))
        {
            throw new ArgumentException("Node JSON cannot be empty.", nameof(nodeJson));
        }

        try
        {
            var node = JsonSerializer.Deserialize<CatalogItem>(nodeJson, JsonOptions);
            if (node == null)
            {
                throw new ArgumentException("Invalid node JSON format: deserialization returned null.", nameof(nodeJson));
            }
            
            ValidateCatalogItem(node);
        }
        catch (JsonException ex)
        {
            throw new ArgumentException($"Invalid node JSON format: {ex.Message}", nameof(nodeJson), ex);
        }

        await _storage.UpdateNodeAsync(path, nodeJson, cancellationToken);
    }

    private static void ValidateCatalogStructure(CatalogRoot root)
    {
        foreach (var item in root.Items)
        {
            ValidateCatalogItem(item);
        }
    }

    private static void ValidateCatalogItem(CatalogItem item)
    {
        if (string.IsNullOrWhiteSpace(item.Title))
        {
            throw new ArgumentException($"Catalog item must have a non-empty title. Path: {item.Path}");
        }

        if (string.IsNullOrWhiteSpace(item.Path))
        {
            throw new ArgumentException($"Catalog item must have a non-empty path. Title: {item.Title}");
        }

        if (item.Order < 0)
        {
            throw new ArgumentException($"Catalog item order must be >= 0. Path: {item.Path}, Order: {item.Order}");
        }

        foreach (var child in item.Children)
        {
            ValidateCatalogItem(child);
        }
    }
}
