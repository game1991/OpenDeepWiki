using System.Text.Json;
using FsCheck;
using FsCheck.Fluent;
using FsCheck.Xunit;
using Microsoft.EntityFrameworkCore;
using OpenDeepWiki.EFCore;
using OpenDeepWiki.Entities;
using Xunit;

namespace OpenDeepWiki.Tests.Agents.Tools;

/// <summary>
/// Property-based tests for CatalogTool Read/Write round-trip.
/// Feature: repository-wiki-generation, Property 6: Catalog Tool Read/Write Round-Trip
/// Validates: Requirements 12.1, 12.2, 12.4
/// </summary>
public class CatalogToolPropertyTests : IDisposable
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    /// <summary>
    /// In-memory test database context for testing.
    /// </summary>
    private class TestDbContext : MasterDbContext
    {
        public TestDbContext(DbContextOptions<TestDbContext> options)
            : base(options)
        {
        }
    }

    private readonly TestDbContext _context;
    private readonly string _branchLanguageId;

    public CatalogToolPropertyTests()
    {
        var options = new DbContextOptionsBuilder<TestDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options;

        _context = new TestDbContext(options);
        _context.Database.EnsureCreated();

        _branchLanguageId = Guid.NewGuid().ToString();
    }

    public void Dispose()
    {
        _context.Database.EnsureDeleted();
        _context.Dispose();
    }

    /// <summary>
    /// Generates valid URL-friendly paths like "1-overview", "2-architecture".
    /// </summary>
    private static Gen<string> GenerateValidPath(int index)
    {
        var suffixes = Gen.Elements("overview", "architecture", "components", "api", "guide", "setup", "config", "reference", "tutorial");
        return suffixes.Select(suffix => $"{index}-{suffix}");
    }

    /// <summary>
    /// Generates a valid non-empty title.
    /// </summary>
    private static Gen<string> GenerateValidTitle()
    {
        return Gen.Elements("Overview", "Architecture", "Components", "API Reference", "Getting Started", "Configuration", "Setup Guide", "Tutorial", "FAQ");
    }

    /// <summary>
    /// Generates a CatalogItem with unique paths for children.
    /// </summary>
    private static Gen<CatalogItem> GenerateCatalogItemWithUniqueChildren(string pathPrefix, int order, int maxDepth)
    {
        if (maxDepth <= 0)
        {
            return GenerateValidTitle().Select(title =>
                new CatalogItem
                {
                    Title = title,
                    Path = $"{pathPrefix}-{order}",
                    Order = order,
                    Children = new List<CatalogItem>()
                });
        }

        return GenerateValidTitle().SelectMany(title =>
            Gen.Choose(0, 2).SelectMany(childCount =>
            {
                if (childCount == 0)
                {
                    return Gen.Constant(new CatalogItem
                    {
                        Title = title,
                        Path = $"{pathPrefix}-{order}",
                        Order = order,
                        Children = new List<CatalogItem>()
                    });
                }

                // Generate children sequentially
                return GenerateChildrenList($"{pathPrefix}-{order}", childCount, maxDepth - 1)
                    .Select(children => new CatalogItem
                    {
                        Title = title,
                        Path = $"{pathPrefix}-{order}",
                        Order = order,
                        Children = children
                    });
            }));
    }

    /// <summary>
    /// Generates a list of children with unique paths.
    /// </summary>
    private static Gen<List<CatalogItem>> GenerateChildrenList(string pathPrefix, int count, int maxDepth)
    {
        if (count == 0)
        {
            return Gen.Constant(new List<CatalogItem>());
        }

        if (count == 1)
        {
            return GenerateCatalogItemWithUniqueChildren(pathPrefix, 1, maxDepth)
                .Select(item => new List<CatalogItem> { item });
        }

        return GenerateCatalogItemWithUniqueChildren(pathPrefix, 1, maxDepth)
            .SelectMany(first => GenerateChildrenList(pathPrefix, count - 1, maxDepth)
                .Select(rest =>
                {
                    var result = new List<CatalogItem> { first };
                    // Adjust order for remaining items
                    for (int i = 0; i < rest.Count; i++)
                    {
                        rest[i].Order = i + 2;
                        rest[i].Path = $"{pathPrefix}-{i + 2}";
                    }
                    result.AddRange(rest);
                    return result;
                }));
    }

    /// <summary>
    /// Generates a valid CatalogRoot with unique paths.
    /// </summary>
    private static Gen<CatalogRoot> GenerateCatalogRootWithUniquePaths()
    {
        return Gen.Choose(1, 3).SelectMany(itemCount =>
        {
            return GenerateRootItemsList(itemCount)
                .Select(items => new CatalogRoot
                {
                    Items = items
                });
        });
    }

    /// <summary>
    /// Generates a list of root items with unique paths.
    /// </summary>
    private static Gen<List<CatalogItem>> GenerateRootItemsList(int count)
    {
        if (count == 0)
        {
            return Gen.Constant(new List<CatalogItem>());
        }

        if (count == 1)
        {
            return GenerateCatalogItemWithUniqueChildren("root", 1, 1)
                .Select(item => new List<CatalogItem> { item });
        }

        return GenerateCatalogItemWithUniqueChildren("root", 1, 1)
            .SelectMany(first => GenerateRootItemsList(count - 1)
                .Select(rest =>
                {
                    var result = new List<CatalogItem> { first };
                    // Adjust order and path for remaining items
                    for (int i = 0; i < rest.Count; i++)
                    {
                        rest[i].Order = i + 2;
                        rest[i].Path = $"root-{i + 2}";
                    }
                    result.AddRange(rest);
                    return result;
                }));
    }

    /// <summary>
    /// Property 6: Catalog Tool Read/Write Round-Trip
    /// For any valid catalog JSON, writing via CatalogTool.Write then reading via CatalogTool.Read 
    /// SHALL return equivalent JSON structure.
    /// Validates: Requirements 12.1, 12.2, 12.4
    /// </summary>
    [Property(MaxTest = 100)]
    public Property CatalogTool_WriteRead_RoundTrip_ShouldPreserveStructure()
    {
        return Prop.ForAll(
            GenerateCatalogRootWithUniquePaths().ToArbitrary(),
            root =>
            {
                // Create fresh context for each test to avoid conflicts
                var options = new DbContextOptionsBuilder<TestDbContext>()
                    .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
                    .Options;

                using var context = new TestDbContext(options);
                context.Database.EnsureCreated();

                var branchLanguageId = Guid.NewGuid().ToString();
                var storage = new TestCatalogStorage(context, branchLanguageId);
                var tool = new TestCatalogTool(storage);

                // Serialize the root to JSON
                var inputJson = JsonSerializer.Serialize(root, JsonOptions);

                // Write via CatalogTool
                tool.WriteAsync(inputJson).GetAwaiter().GetResult();

                // Read via CatalogTool
                var outputJson = tool.ReadAsync().GetAwaiter().GetResult();

                // Deserialize both for comparison
                var inputRoot = JsonSerializer.Deserialize<CatalogRoot>(inputJson, JsonOptions);
                var outputRoot = JsonSerializer.Deserialize<CatalogRoot>(outputJson, JsonOptions);

                // Verify structural equality
                return inputRoot != null && outputRoot != null && inputRoot.Equals(outputRoot);
            });
    }

    /// <summary>
    /// Property 6: Catalog Tool Read/Write Round-Trip
    /// For any valid catalog, multiple writes should replace previous content.
    /// Validates: Requirements 12.2
    /// </summary>
    [Property(MaxTest = 50)]
    public Property CatalogTool_MultipleWrites_ShouldReplaceContent()
    {
        return Prop.ForAll(
            GenerateCatalogRootWithUniquePaths().ToArbitrary(),
            GenerateCatalogRootWithUniquePaths().ToArbitrary(),
            (root1, root2) =>
            {
                var options = new DbContextOptionsBuilder<TestDbContext>()
                    .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
                    .Options;

                using var context = new TestDbContext(options);
                context.Database.EnsureCreated();

                var branchLanguageId = Guid.NewGuid().ToString();
                var storage = new TestCatalogStorage(context, branchLanguageId);
                var tool = new TestCatalogTool(storage);

                // Write first catalog
                var json1 = JsonSerializer.Serialize(root1, JsonOptions);
                tool.WriteAsync(json1).GetAwaiter().GetResult();

                // Write second catalog (should replace)
                var json2 = JsonSerializer.Serialize(root2, JsonOptions);
                tool.WriteAsync(json2).GetAwaiter().GetResult();

                // Read should return second catalog
                var outputJson = tool.ReadAsync().GetAwaiter().GetResult();
                var outputRoot = JsonSerializer.Deserialize<CatalogRoot>(outputJson, JsonOptions);

                return outputRoot != null && root2.Equals(outputRoot);
            });
    }

    /// <summary>
    /// Property 6: Catalog Tool Read/Write Round-Trip
    /// Reading from empty storage should return empty items array.
    /// Validates: Requirements 12.1
    /// </summary>
    [Fact]
    public async Task CatalogTool_ReadEmpty_ShouldReturnEmptyItems()
    {
        var storage = new TestCatalogStorage(_context, _branchLanguageId);
        var tool = new TestCatalogTool(storage);

        var json = await tool.ReadAsync();
        var root = JsonSerializer.Deserialize<CatalogRoot>(json, JsonOptions);

        Assert.NotNull(root);
        Assert.Empty(root.Items);
    }

    /// <summary>
    /// Property 6: Catalog Tool Read/Write Round-Trip
    /// Writing invalid JSON should throw ArgumentException.
    /// Validates: Requirements 12.4
    /// </summary>
    [Fact]
    public async Task CatalogTool_WriteInvalidJson_ShouldThrowArgumentException()
    {
        var storage = new TestCatalogStorage(_context, _branchLanguageId);
        var tool = new TestCatalogTool(storage);

        await Assert.ThrowsAsync<ArgumentException>(() => tool.WriteAsync("invalid json"));
    }

    /// <summary>
    /// Property 6: Catalog Tool Read/Write Round-Trip
    /// Writing empty string should throw ArgumentException.
    /// Validates: Requirements 12.4
    /// </summary>
    [Fact]
    public async Task CatalogTool_WriteEmptyString_ShouldThrowArgumentException()
    {
        var storage = new TestCatalogStorage(_context, _branchLanguageId);
        var tool = new TestCatalogTool(storage);

        await Assert.ThrowsAsync<ArgumentException>(() => tool.WriteAsync(""));
    }
}
