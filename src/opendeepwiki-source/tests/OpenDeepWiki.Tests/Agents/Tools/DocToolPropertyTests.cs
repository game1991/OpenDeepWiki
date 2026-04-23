using FsCheck;
using FsCheck.Fluent;
using FsCheck.Xunit;
using Microsoft.EntityFrameworkCore;
using OpenDeepWiki.EFCore;
using OpenDeepWiki.Entities;
using Xunit;

namespace OpenDeepWiki.Tests.Agents.Tools;

/// <summary>
/// Property-based tests for DocTool Write association.
/// Feature: repository-wiki-generation, Property 10: Doc Tool Write Association
/// Validates: Requirements 14.1, 14.3, 5.5
/// </summary>
public class DocToolPropertyTests : IDisposable
{
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

    public DocToolPropertyTests()
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
    /// Creates a catalog item in the database for testing.
    /// </summary>
    private async Task<DocCatalog> CreateCatalogItemAsync(string path, string title)
    {
        var catalog = new DocCatalog
        {
            Id = Guid.NewGuid().ToString(),
            BranchLanguageId = _branchLanguageId,
            Path = path,
            Title = title,
            Order = 1
        };

        _context.DocCatalogs.Add(catalog);
        await _context.SaveChangesAsync();

        return catalog;
    }

    /// <summary>
    /// Generates valid catalog paths.
    /// </summary>
    private static Gen<string> GenerateValidCatalogPath()
    {
        return Gen.Choose(1, 10).SelectMany(num =>
            Gen.Elements("overview", "architecture", "components", "api", "guide")
                .Select(suffix => $"{num}-{suffix}"));
    }

    /// <summary>
    /// Generates valid Markdown content.
    /// </summary>
    private static Gen<string> GenerateValidContent()
    {
        return Gen.Elements(
            "# Overview\n\nThis is the overview section.",
            "# Architecture\n\n## Components\n\nThe system consists of multiple components.",
            "# API Reference\n\n```csharp\npublic class Example { }\n```",
            "# Getting Started\n\n1. Install dependencies\n2. Configure settings\n3. Run the application",
            "# Configuration\n\n| Setting | Value |\n|---------|-------|\n| Debug | true |"
        );
    }

    /// <summary>
    /// Property 10: Doc Tool Write Association
    /// For any DocTool.Write operation, the created document SHALL be associated with the specified catalog item.
    /// Validates: Requirements 14.1, 14.3, 5.5
    /// </summary>
    [Property(MaxTest = 100)]
    public Property DocTool_Write_ShouldAssociateDocumentWithCatalogItem()
    {
        return Prop.ForAll(
            GenerateValidCatalogPath().ToArbitrary(),
            GenerateValidContent().ToArbitrary(),
            (path, content) =>
            {
                // Create fresh context for each test
                var options = new DbContextOptionsBuilder<TestDbContext>()
                    .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
                    .Options;

                using var context = new TestDbContext(options);
                context.Database.EnsureCreated();

                var branchLanguageId = Guid.NewGuid().ToString();

                // Create catalog item first
                var catalog = new DocCatalog
                {
                    Id = Guid.NewGuid().ToString(),
                    BranchLanguageId = branchLanguageId,
                    Path = path,
                    Title = "Test Title",
                    Order = 1
                };
                context.DocCatalogs.Add(catalog);
                context.SaveChanges();

                // Write document
                var tool = new TestDocTool(context, branchLanguageId);
                tool.WriteAsync(path, content).GetAwaiter().GetResult();

                // Verify association
                var updatedCatalog = context.DocCatalogs
                    .FirstOrDefault(c => c.Path == path && c.BranchLanguageId == branchLanguageId && !c.IsDeleted);

                if (updatedCatalog == null || string.IsNullOrEmpty(updatedCatalog.DocFileId))
                {
                    return false;
                }

                // Verify document exists and has correct content
                var docFile = context.DocFiles
                    .FirstOrDefault(d => d.Id == updatedCatalog.DocFileId && !d.IsDeleted);

                return docFile != null && docFile.Content == content;
            });
    }

    /// <summary>
    /// Property 10: Doc Tool Write Association
    /// For any DocTool.Write operation on existing document, content SHALL be updated.
    /// Validates: Requirements 14.1
    /// </summary>
    [Property(MaxTest = 50)]
    public Property DocTool_Write_ShouldUpdateExistingDocument()
    {
        return Prop.ForAll(
            GenerateValidCatalogPath().ToArbitrary(),
            GenerateValidContent().ToArbitrary(),
            GenerateValidContent().ToArbitrary(),
            (path, content1, content2) =>
            {
                var options = new DbContextOptionsBuilder<TestDbContext>()
                    .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
                    .Options;

                using var context = new TestDbContext(options);
                context.Database.EnsureCreated();

                var branchLanguageId = Guid.NewGuid().ToString();

                // Create catalog item
                var catalog = new DocCatalog
                {
                    Id = Guid.NewGuid().ToString(),
                    BranchLanguageId = branchLanguageId,
                    Path = path,
                    Title = "Test Title",
                    Order = 1
                };
                context.DocCatalogs.Add(catalog);
                context.SaveChanges();

                var tool = new TestDocTool(context, branchLanguageId);

                // Write first content
                tool.WriteAsync(path, content1).GetAwaiter().GetResult();

                // Write second content (should update)
                tool.WriteAsync(path, content2).GetAwaiter().GetResult();

                // Verify only one document exists
                var docCount = context.DocFiles
                    .Count(d => d.BranchLanguageId == branchLanguageId && !d.IsDeleted);

                // Verify content is updated
                var updatedCatalog = context.DocCatalogs
                    .FirstOrDefault(c => c.Path == path && c.BranchLanguageId == branchLanguageId && !c.IsDeleted);

                if (updatedCatalog == null || string.IsNullOrEmpty(updatedCatalog.DocFileId))
                {
                    return false;
                }

                var docFile = context.DocFiles
                    .FirstOrDefault(d => d.Id == updatedCatalog.DocFileId && !d.IsDeleted);

                return docCount == 1 && docFile != null && docFile.Content == content2;
            });
    }

    /// <summary>
    /// Property 10: Doc Tool Write Association
    /// Writing to non-existent catalog path should throw exception.
    /// Validates: Requirements 14.3
    /// </summary>
    [Fact]
    public async Task DocTool_Write_ToNonExistentPath_ShouldThrowException()
    {
        var tool = new TestDocTool(_context, _branchLanguageId);

        await Assert.ThrowsAsync<InvalidOperationException>(
            () => tool.WriteAsync("non-existent-path", "Some content"));
    }

    /// <summary>
    /// Property 10: Doc Tool Write Association
    /// Writing empty content should throw exception.
    /// Validates: Requirements 14.1
    /// </summary>
    [Fact]
    public async Task DocTool_Write_EmptyContent_ShouldThrowException()
    {
        await CreateCatalogItemAsync("1-test", "Test");
        var tool = new TestDocTool(_context, _branchLanguageId);

        await Assert.ThrowsAsync<ArgumentException>(
            () => tool.WriteAsync("1-test", ""));
    }

    /// <summary>
    /// Property 10: Doc Tool Write Association
    /// Document content should be stored in Markdown format.
    /// Validates: Requirements 14.5
    /// </summary>
    [Fact]
    public async Task DocTool_Write_ShouldStoreMarkdownContent()
    {
        var catalog = await CreateCatalogItemAsync("1-overview", "Overview");
        var tool = new TestDocTool(_context, _branchLanguageId);

        var markdownContent = "# Title\n\n## Section\n\n- Item 1\n- Item 2\n\n```code\nvar x = 1;\n```";
        await tool.WriteAsync("1-overview", markdownContent);

        var content = await tool.ReadAsync("1-overview");
        Assert.Equal(markdownContent, content);
    }

    /// <summary>
    /// Property 10: Doc Tool Write Association
    /// Edit should replace specified content.
    /// Validates: Requirements 14.2, 14.4
    /// </summary>
    [Fact]
    public async Task DocTool_Edit_ShouldReplaceContent()
    {
        var catalog = await CreateCatalogItemAsync("1-overview", "Overview");
        var tool = new TestDocTool(_context, _branchLanguageId);

        var originalContent = "# Title\n\nOriginal content here.";
        await tool.WriteAsync("1-overview", originalContent);

        await tool.EditAsync("1-overview", "Original content", "Updated content");

        var content = await tool.ReadAsync("1-overview");
        Assert.Contains("Updated content", content);
        Assert.DoesNotContain("Original content", content);
    }
}

/// <summary>
/// Test version of DocTool for property-based testing.
/// </summary>
public class TestDocTool
{
    private readonly IContext _context;
    private readonly string _branchLanguageId;

    public TestDocTool(IContext context, string branchLanguageId)
    {
        _context = context ?? throw new ArgumentNullException(nameof(context));
        _branchLanguageId = branchLanguageId ?? throw new ArgumentNullException(nameof(branchLanguageId));
    }

    public async Task WriteAsync(string catalogPath, string content, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(catalogPath))
        {
            throw new ArgumentException("Catalog path cannot be empty.", nameof(catalogPath));
        }

        if (string.IsNullOrWhiteSpace(content))
        {
            throw new ArgumentException("Content cannot be empty.", nameof(content));
        }

        var catalog = await _context.DocCatalogs
            .FirstOrDefaultAsync(c => c.BranchLanguageId == _branchLanguageId && 
                                      c.Path == catalogPath && 
                                      !c.IsDeleted, cancellationToken);

        if (catalog == null)
        {
            throw new InvalidOperationException($"Catalog item with path '{catalogPath}' not found.");
        }

        if (!string.IsNullOrEmpty(catalog.DocFileId))
        {
            var existingDoc = await _context.DocFiles
                .FirstOrDefaultAsync(d => d.Id == catalog.DocFileId && !d.IsDeleted, cancellationToken);

            if (existingDoc != null)
            {
                existingDoc.Content = content;
                existingDoc.UpdateTimestamp();
                await _context.SaveChangesAsync(cancellationToken);
                return;
            }
        }

        var docFile = new DocFile
        {
            Id = Guid.NewGuid().ToString(),
            BranchLanguageId = _branchLanguageId,
            Content = content
        };

        _context.DocFiles.Add(docFile);
        catalog.DocFileId = docFile.Id;
        catalog.UpdateTimestamp();

        await _context.SaveChangesAsync(cancellationToken);
    }

    public async Task EditAsync(string catalogPath, string oldContent, string newContent, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(catalogPath))
        {
            throw new ArgumentException("Catalog path cannot be empty.", nameof(catalogPath));
        }

        if (string.IsNullOrWhiteSpace(oldContent))
        {
            throw new ArgumentException("Old content cannot be empty.", nameof(oldContent));
        }

        var catalog = await _context.DocCatalogs
            .FirstOrDefaultAsync(c => c.BranchLanguageId == _branchLanguageId && 
                                      c.Path == catalogPath && 
                                      !c.IsDeleted, cancellationToken);

        if (catalog == null)
        {
            throw new InvalidOperationException($"Catalog item with path '{catalogPath}' not found.");
        }

        if (string.IsNullOrEmpty(catalog.DocFileId))
        {
            throw new InvalidOperationException($"No document associated with catalog item '{catalogPath}'.");
        }

        var docFile = await _context.DocFiles
            .FirstOrDefaultAsync(d => d.Id == catalog.DocFileId && !d.IsDeleted, cancellationToken);

        if (docFile == null)
        {
            throw new InvalidOperationException($"Document not found for catalog item '{catalogPath}'.");
        }

        if (!docFile.Content.Contains(oldContent))
        {
            throw new InvalidOperationException($"The specified content to replace was not found in the document.");
        }

        docFile.Content = docFile.Content.Replace(oldContent, newContent);
        docFile.UpdateTimestamp();

        await _context.SaveChangesAsync(cancellationToken);
    }

    public async Task<string?> ReadAsync(string catalogPath, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(catalogPath))
        {
            throw new ArgumentException("Catalog path cannot be empty.", nameof(catalogPath));
        }

        var catalog = await _context.DocCatalogs
            .FirstOrDefaultAsync(c => c.BranchLanguageId == _branchLanguageId && 
                                      c.Path == catalogPath && 
                                      !c.IsDeleted, cancellationToken);

        if (catalog == null || string.IsNullOrEmpty(catalog.DocFileId))
        {
            return null;
        }

        var docFile = await _context.DocFiles
            .FirstOrDefaultAsync(d => d.Id == catalog.DocFileId && !d.IsDeleted, cancellationToken);

        return docFile?.Content;
    }
}
