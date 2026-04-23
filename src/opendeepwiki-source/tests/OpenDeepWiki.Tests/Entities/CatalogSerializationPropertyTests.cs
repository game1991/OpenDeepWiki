using System.Text.Json;
using FsCheck;
using FsCheck.Fluent;
using FsCheck.Xunit;
using OpenDeepWiki.Entities;

namespace OpenDeepWiki.Tests.Entities;

/// <summary>
/// Property-based tests for CatalogItem JSON serialization round-trip.
/// Feature: repository-wiki-generation, Property 5: Catalog Serialization Round-Trip
/// Validates: Requirements 6.1, 6.2, 6.3
/// </summary>
public class CatalogSerializationPropertyTests
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = false,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    /// <summary>
    /// Generates valid URL-friendly paths like "1-overview", "2-architecture".
    /// </summary>
    private static Gen<string> GenerateValidPath()
    {
        var prefixes = ArbMap.Default.GeneratorFor<int>().Where(i => i >= 1 && i <= 100);
        var suffixes = Gen.Elements("overview", "architecture", "components", "api", "guide", "setup", "config", "reference", "tutorial");
        return prefixes.SelectMany(prefix => suffixes.Select(suffix => $"{prefix}-{suffix}"));
    }

    /// <summary>
    /// Generates a valid non-empty title.
    /// </summary>
    private static Gen<string> GenerateValidTitle()
    {
        return Gen.Elements("Overview", "Architecture", "Components", "API Reference", "Getting Started", "Configuration", "Setup Guide", "Tutorial", "FAQ");
    }

    /// <summary>
    /// Generates a valid CatalogItem without children (leaf node).
    /// </summary>
    private static Gen<CatalogItem> GenerateLeafCatalogItem()
    {
        return GenerateValidTitle().SelectMany(title =>
            GenerateValidPath().SelectMany(path =>
                ArbMap.Default.GeneratorFor<int>().Where(o => o >= 0 && o <= 100).Select(order =>
                    new CatalogItem
                    {
                        Title = title,
                        Path = path,
                        Order = order,
                        Children = new List<CatalogItem>()
                    })));
    }

    /// <summary>
    /// Generates a CatalogItem with optional children (tree structure).
    /// </summary>
    private static Gen<CatalogItem> GenerateCatalogItemWithChildren(int maxDepth)
    {
        if (maxDepth <= 0)
        {
            return GenerateLeafCatalogItem();
        }

        return GenerateValidTitle().SelectMany(title =>
            GenerateValidPath().SelectMany(path =>
                ArbMap.Default.GeneratorFor<int>().Where(o => o >= 0 && o <= 100).SelectMany(order =>
                    Gen.Choose(0, 3).SelectMany(childCount =>
                        GenerateCatalogItemWithChildren(maxDepth - 1)
                            .ListOf(childCount)
                            .Select(children => new CatalogItem
                            {
                                Title = title,
                                Path = path,
                                Order = order,
                                Children = children.ToList()
                            })))));
    }

    /// <summary>
    /// Generates a valid CatalogRoot with items.
    /// </summary>
    private static Gen<CatalogRoot> GenerateCatalogRoot()
    {
        return Gen.Choose(0, 5).SelectMany(itemCount =>
            GenerateCatalogItemWithChildren(2)
                .ListOf(itemCount)
                .Select(items => new CatalogRoot
                {
                    Items = items.ToList()
                }));
    }

    /// <summary>
    /// Property 5: Catalog Serialization Round-Trip
    /// For any valid CatalogItem, serializing to JSON then deserializing SHALL produce an equivalent structure.
    /// Validates: Requirements 6.1, 6.2, 6.3
    /// </summary>
    [Property(MaxTest = 100)]
    public Property CatalogItem_SerializationRoundTrip_ShouldPreserveStructure()
    {
        return Prop.ForAll(
            GenerateCatalogItemWithChildren(2).ToArbitrary(),
            item =>
            {
                // Serialize to JSON
                var json = JsonSerializer.Serialize(item, JsonOptions);
                
                // Deserialize back
                var restored = JsonSerializer.Deserialize<CatalogItem>(json, JsonOptions);
                
                // Verify equality
                return item.Equals(restored);
            });
    }

    /// <summary>
    /// Property 5: Catalog Serialization Round-Trip
    /// For any valid CatalogRoot, serializing to JSON then deserializing SHALL produce an equivalent structure.
    /// Validates: Requirements 6.1, 6.2, 6.3
    /// </summary>
    [Property(MaxTest = 100)]
    public Property CatalogRoot_SerializationRoundTrip_ShouldPreserveStructure()
    {
        return Prop.ForAll(
            GenerateCatalogRoot().ToArbitrary(),
            root =>
            {
                // Serialize to JSON
                var json = JsonSerializer.Serialize(root, JsonOptions);
                
                // Deserialize back
                var restored = JsonSerializer.Deserialize<CatalogRoot>(json, JsonOptions);
                
                // Verify equality
                return root.Equals(restored);
            });
    }

    /// <summary>
    /// Property 5: Catalog Serialization Round-Trip
    /// For any valid CatalogItem tree, the JSON structure SHALL contain expected properties.
    /// Validates: Requirements 6.1, 6.2
    /// </summary>
    [Property(MaxTest = 100)]
    public Property CatalogItem_Serialization_ShouldContainExpectedProperties()
    {
        return Prop.ForAll(
            GenerateLeafCatalogItem().ToArbitrary(),
            item =>
            {
                var json = JsonSerializer.Serialize(item, JsonOptions);
                
                // Verify JSON contains expected property names
                return json.Contains("\"title\"") &&
                       json.Contains("\"path\"") &&
                       json.Contains("\"order\"") &&
                       json.Contains("\"children\"");
            });
    }

    /// <summary>
    /// Property 5: Catalog Serialization Round-Trip
    /// For any nested CatalogItem tree, children SHALL be preserved after round-trip.
    /// Validates: Requirements 6.1, 6.2, 6.3
    /// </summary>
    [Property(MaxTest = 100)]
    public Property CatalogItem_NestedChildren_ShouldBePreservedAfterRoundTrip()
    {
        return Prop.ForAll(
            GenerateCatalogItemWithChildren(3).ToArbitrary(),
            item =>
            {
                var json = JsonSerializer.Serialize(item, JsonOptions);
                var restored = JsonSerializer.Deserialize<CatalogItem>(json, JsonOptions);
                
                // Count total nodes in original and restored
                int CountNodes(CatalogItem? node)
                {
                    if (node == null) return 0;
                    return 1 + node.Children.Sum(CountNodes);
                }
                
                return CountNodes(item) == CountNodes(restored);
            });
    }
}
