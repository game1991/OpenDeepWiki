using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using OpenDeepWiki.EFCore;
using OpenDeepWiki.Entities;

namespace OpenDeepWiki.Tests.Agents.Tools;

/// <summary>
/// Test version of CatalogStorage for property-based testing.
/// Provides storage operations for wiki catalog structures.
/// </summary>
public class TestCatalogStorage
{
    private readonly IContext _context;
    private readonly string _branchLanguageId;
    
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    public TestCatalogStorage(IContext context, string branchLanguageId)
    {
        _context = context ?? throw new ArgumentNullException(nameof(context));
        _branchLanguageId = branchLanguageId ?? throw new ArgumentNullException(nameof(branchLanguageId));
    }

    public async Task<string> GetCatalogJsonAsync(CancellationToken cancellationToken = default)
    {
        var catalogs = await _context.DocCatalogs
            .Where(c => c.BranchLanguageId == _branchLanguageId && !c.IsDeleted)
            .OrderBy(c => c.Order)
            .ToListAsync(cancellationToken);

        var root = BuildCatalogTree(catalogs);
        return JsonSerializer.Serialize(root, JsonOptions);
    }

    public async Task SetCatalogAsync(string catalogJson, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(catalogJson))
        {
            throw new ArgumentException("Catalog JSON cannot be empty.", nameof(catalogJson));
        }

        var root = JsonSerializer.Deserialize<CatalogRoot>(catalogJson, JsonOptions);
        if (root == null)
        {
            throw new ArgumentException("Invalid catalog JSON format.", nameof(catalogJson));
        }

        var existingCatalogs = await _context.DocCatalogs
            .Where(c => c.BranchLanguageId == _branchLanguageId && !c.IsDeleted)
            .ToListAsync(cancellationToken);

        foreach (var catalog in existingCatalogs)
        {
            catalog.MarkAsDeleted();
        }

        await CreateCatalogItemsAsync(root.Items, null, cancellationToken);
        await _context.SaveChangesAsync(cancellationToken);
    }

    public async Task UpdateNodeAsync(string path, string nodeJson, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(path))
        {
            throw new ArgumentException("Path cannot be empty.", nameof(path));
        }

        if (string.IsNullOrWhiteSpace(nodeJson))
        {
            throw new ArgumentException("Node JSON cannot be empty.", nameof(nodeJson));
        }

        var updatedItem = JsonSerializer.Deserialize<CatalogItem>(nodeJson, JsonOptions);
        if (updatedItem == null)
        {
            throw new ArgumentException("Invalid node JSON format.", nameof(nodeJson));
        }

        var existingCatalog = await _context.DocCatalogs
            .FirstOrDefaultAsync(c => c.BranchLanguageId == _branchLanguageId && 
                                      c.Path == path && 
                                      !c.IsDeleted, cancellationToken);

        if (existingCatalog == null)
        {
            throw new InvalidOperationException($"Catalog node with path '{path}' not found.");
        }

        existingCatalog.Title = updatedItem.Title;
        existingCatalog.Order = updatedItem.Order;
        existingCatalog.UpdateTimestamp();

        if (updatedItem.Children.Count > 0)
        {
            var existingChildren = await _context.DocCatalogs
                .Where(c => c.ParentId == existingCatalog.Id && !c.IsDeleted)
                .ToListAsync(cancellationToken);

            foreach (var child in existingChildren)
            {
                child.MarkAsDeleted();
            }

            await CreateCatalogItemsAsync(updatedItem.Children, existingCatalog.Id, cancellationToken);
        }

        await _context.SaveChangesAsync(cancellationToken);
    }

    private CatalogRoot BuildCatalogTree(List<DocCatalog> catalogs)
    {
        var root = new CatalogRoot();
        var rootItems = catalogs.Where(c => c.ParentId == null).OrderBy(c => c.Order);

        foreach (var item in rootItems)
        {
            root.Items.Add(BuildCatalogItemWithChildren(item, catalogs));
        }

        return root;
    }

    private CatalogItem BuildCatalogItemWithChildren(DocCatalog catalog, List<DocCatalog> allCatalogs)
    {
        var item = new CatalogItem
        {
            Title = catalog.Title,
            Path = catalog.Path,
            Order = catalog.Order,
            Children = new List<CatalogItem>()
        };

        var children = allCatalogs
            .Where(c => c.ParentId == catalog.Id)
            .OrderBy(c => c.Order);

        foreach (var child in children)
        {
            item.Children.Add(BuildCatalogItemWithChildren(child, allCatalogs));
        }

        return item;
    }

    private async Task CreateCatalogItemsAsync(List<CatalogItem> items, string? parentId, CancellationToken cancellationToken)
    {
        foreach (var item in items)
        {
            var catalog = new DocCatalog
            {
                Id = Guid.NewGuid().ToString(),
                BranchLanguageId = _branchLanguageId,
                ParentId = parentId,
                Title = item.Title,
                Path = item.Path,
                Order = item.Order
            };

            _context.DocCatalogs.Add(catalog);

            if (item.Children.Count > 0)
            {
                await CreateCatalogItemsAsync(item.Children, catalog.Id, cancellationToken);
            }
        }
    }
}
