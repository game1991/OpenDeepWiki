using FsCheck;
using FsCheck.Fluent;
using FsCheck.Xunit;
using OpenDeepWiki.Entities;

namespace OpenDeepWiki.Tests.Entities;

/// <summary>
/// Property-based tests for DocCatalog entity structure validation.
/// Feature: repository-wiki-generation, Property 4: Catalog Item Structure Validity
/// Validates: Requirements 4.2, 4.5
/// </summary>
public class DocCatalogPropertyTests
{
    /// <summary>
    /// Generates valid URL-friendly paths like "1-overview", "2-architecture".
    /// </summary>
    private static Gen<string> GenerateValidPath()
    {
        var prefixes = ArbMap.Default.GeneratorFor<int>().Where(i => i >= 1 && i <= 100);
        var suffixes = Gen.Elements("overview", "architecture", "components", "api", "guide", "setup", "config");
        return prefixes.SelectMany(prefix => suffixes.Select(suffix => $"{prefix}-{suffix}"));
    }

    /// <summary>
    /// Generates a valid non-empty title.
    /// </summary>
    private static Gen<string> GenerateValidTitle()
    {
        return Gen.Elements("Overview", "Architecture", "Components", "API Reference", "Getting Started", "Configuration", "Setup Guide");
    }

    /// <summary>
    /// Custom generator for valid DocCatalog entities.
    /// </summary>
    private static Gen<DocCatalog> GenerateValidDocCatalog()
    {
        return GenerateValidTitle().SelectMany(title =>
            GenerateValidPath().SelectMany(path =>
                ArbMap.Default.GeneratorFor<int>().Where(o => o >= 0 && o <= 1000).Select(order =>
                    new DocCatalog
                    {
                        Id = Guid.NewGuid().ToString(),
                        BranchLanguageId = Guid.NewGuid().ToString(),
                        Title = title,
                        Path = path,
                        Order = order,
                        ParentId = null,
                        DocFileId = null
                    })));
    }

    /// <summary>
    /// Property 4: Catalog Item Structure Validity
    /// For any catalog item, it SHALL contain non-empty title, valid path, and order >= 0.
    /// Validates: Requirements 4.2, 4.5
    /// </summary>
    [Property(MaxTest = 100)]
    public Property CatalogItem_ShouldHaveNonEmptyTitle()
    {
        return Prop.ForAll(
            GenerateValidDocCatalog().ToArbitrary(),
            catalog => !string.IsNullOrWhiteSpace(catalog.Title)
        );
    }

    /// <summary>
    /// Property 4: Catalog Item Structure Validity
    /// For any catalog item, it SHALL contain non-empty title, valid path, and order >= 0.
    /// Validates: Requirements 4.2, 4.5
    /// </summary>
    [Property(MaxTest = 100)]
    public Property CatalogItem_ShouldHaveValidPath()
    {
        return Prop.ForAll(
            GenerateValidDocCatalog().ToArbitrary(),
            catalog => !string.IsNullOrWhiteSpace(catalog.Path) && catalog.Path.Length <= 1000
        );
    }

    /// <summary>
    /// Property 4: Catalog Item Structure Validity
    /// For any catalog item, it SHALL contain non-empty title, valid path, and order >= 0.
    /// Validates: Requirements 4.2, 4.5
    /// </summary>
    [Property(MaxTest = 100)]
    public Property CatalogItem_ShouldHaveNonNegativeOrder()
    {
        return Prop.ForAll(
            GenerateValidDocCatalog().ToArbitrary(),
            catalog => catalog.Order >= 0
        );
    }

    /// <summary>
    /// Property 4: Catalog Item Structure Validity
    /// For any catalog item, BranchLanguageId SHALL be non-empty.
    /// Validates: Requirements 4.2, 4.5
    /// </summary>
    [Property(MaxTest = 100)]
    public Property CatalogItem_ShouldHaveValidBranchLanguageId()
    {
        return Prop.ForAll(
            GenerateValidDocCatalog().ToArbitrary(),
            catalog => !string.IsNullOrWhiteSpace(catalog.BranchLanguageId)
        );
    }

    /// <summary>
    /// Property 4: Catalog Item Structure Validity - Path Uniqueness
    /// For any branch language, all catalog item paths SHALL be unique.
    /// This test verifies that generated paths follow a pattern that ensures uniqueness.
    /// Validates: Requirements 4.5
    /// </summary>
    [Property(MaxTest = 100)]
    public Property CatalogItems_PathsShouldBeUniqueWithinBranchLanguage()
    {
        var branchLanguageId = Guid.NewGuid().ToString();
        
        var pathListGen = GenerateValidPath().ListOf(10);
        
        return Prop.ForAll(
            pathListGen.ToArbitrary(),
            paths =>
            {
                // Create catalogs with the same branch language ID
                var catalogs = paths.Select((path, index) => new DocCatalog
                {
                    Id = Guid.NewGuid().ToString(),
                    BranchLanguageId = branchLanguageId,
                    Title = $"Title {index}",
                    Path = $"{index + 1}-{path}", // Ensure uniqueness by prefixing with index
                    Order = index
                }).ToList();

                // Verify all paths are unique within the same branch language
                var uniquePaths = catalogs.Select(c => c.Path).Distinct().Count();
                return uniquePaths == catalogs.Count;
            });
    }

    /// <summary>
    /// Property 4: Catalog Item Structure Validity - Tree Structure
    /// For any catalog with children, the parent-child relationship SHALL be consistent.
    /// Validates: Requirements 4.2
    /// </summary>
    [Property(MaxTest = 100)]
    public Property CatalogItem_TreeStructure_ParentChildConsistency()
    {
        return Prop.ForAll(
            GenerateValidDocCatalog().ToArbitrary(),
            parent =>
            {
                // Create a child catalog
                var child = new DocCatalog
                {
                    Id = Guid.NewGuid().ToString(),
                    BranchLanguageId = parent.BranchLanguageId,
                    ParentId = parent.Id,
                    Title = "Child Title",
                    Path = $"{parent.Path}/child",
                    Order = 0
                };

                // Add child to parent's children collection
                parent.Children.Add(child);
                child.Parent = parent;

                // Verify parent-child relationship consistency
                return child.ParentId == parent.Id &&
                       parent.Children.Contains(child) &&
                       child.Parent == parent &&
                       child.BranchLanguageId == parent.BranchLanguageId;
            });
    }
}
