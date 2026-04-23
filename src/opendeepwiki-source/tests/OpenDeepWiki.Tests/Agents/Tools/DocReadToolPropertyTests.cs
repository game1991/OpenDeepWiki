using FsCheck;
using FsCheck.Fluent;
using FsCheck.Xunit;
using OpenDeepWiki.Agents.Tools;
using OpenDeepWiki.EFCore;
using OpenDeepWiki.Entities;
using Microsoft.EntityFrameworkCore;

namespace OpenDeepWiki.Tests.Agents.Tools;

/// <summary>
/// Property-based tests for DocReadTool access control validation.
/// Feature: doc-chat-assistant, Property 4: 文档读取权限控制
/// Validates: Requirements 6.2, 6.3
/// </summary>
public class DocReadToolPropertyTests
{
    /// <summary>
    /// Generates valid owner names (organization names).
    /// </summary>
    private static Gen<string> GenerateOwner()
    {
        return Gen.Elements("microsoft", "google", "amazon", "openai", "anthropic", "meta", "apple");
    }

    /// <summary>
    /// Generates valid repository names.
    /// </summary>
    private static Gen<string> GenerateRepo()
    {
        return Gen.Elements("docs", "wiki", "api", "sdk", "core", "framework", "tools");
    }

    /// <summary>
    /// Generates valid branch names.
    /// </summary>
    private static Gen<string> GenerateBranch()
    {
        return Gen.Elements("main", "master", "develop", "feature/test", "release/v1.0");
    }

    /// <summary>
    /// Generates valid language codes.
    /// </summary>
    private static Gen<string> GenerateLanguage()
    {
        return Gen.Elements("en", "zh", "ja", "ko", "es", "fr", "de");
    }

    /// <summary>
    /// Generates a tuple of (owner, repo, branch, language) for DocReadTool context.
    /// </summary>
    private static Gen<(string Owner, string Repo, string Branch, string Language)> GenerateContext()
    {
        return GenerateOwner().SelectMany(owner =>
            GenerateRepo().SelectMany(repo =>
                GenerateBranch().SelectMany(branch =>
                    GenerateLanguage().Select(language =>
                        (owner, repo, branch, language)))));
    }

    /// <summary>
    /// Property 4: 文档读取权限控制 - 同一仓库访问应该被允许
    /// For any DocReadTool context, accessing the same owner/repo/branch should be allowed.
    /// Validates: Requirements 6.2, 6.3
    /// </summary>
    [Property(MaxTest = 100)]
    public Property ValidateAccess_SameContext_ShouldReturnTrue()
    {
        return Prop.ForAll(
            GenerateContext().ToArbitrary(),
            context =>
            {
                // Create a mock context (we only test ValidateAccess which doesn't need DB)
                var tool = new DocReadTool(
                    null!, // Context not needed for ValidateAccess
                    context.Owner,
                    context.Repo,
                    context.Branch,
                    context.Language);

                // Same context should be allowed
                return tool.ValidateAccess(context.Owner, context.Repo, context.Branch);
            });
    }

    /// <summary>
    /// Property 4: 文档读取权限控制 - 不同Owner访问应该被拒绝
    /// For any DocReadTool context, accessing a different owner should be denied.
    /// Validates: Requirements 6.2, 6.3
    /// </summary>
    [Property(MaxTest = 100)]
    public Property ValidateAccess_DifferentOwner_ShouldReturnFalse()
    {
        var contextGen = GenerateContext();
        var differentOwnerGen = GenerateOwner();

        return Prop.ForAll(
            contextGen.ToArbitrary(),
            differentOwnerGen.ToArbitrary(),
            (context, differentOwner) =>
            {
                // Skip if the different owner happens to be the same
                if (string.Equals(context.Owner, differentOwner, StringComparison.OrdinalIgnoreCase))
                    return true; // Trivially true, skip this case

                var tool = new DocReadTool(
                    null!,
                    context.Owner,
                    context.Repo,
                    context.Branch,
                    context.Language);

                // Different owner should be denied
                return !tool.ValidateAccess(differentOwner, context.Repo, context.Branch);
            });
    }

    /// <summary>
    /// Property 4: 文档读取权限控制 - 不同Repo访问应该被拒绝
    /// For any DocReadTool context, accessing a different repository should be denied.
    /// Validates: Requirements 6.2, 6.3
    /// </summary>
    [Property(MaxTest = 100)]
    public Property ValidateAccess_DifferentRepo_ShouldReturnFalse()
    {
        var contextGen = GenerateContext();
        var differentRepoGen = GenerateRepo();

        return Prop.ForAll(
            contextGen.ToArbitrary(),
            differentRepoGen.ToArbitrary(),
            (context, differentRepo) =>
            {
                // Skip if the different repo happens to be the same
                if (string.Equals(context.Repo, differentRepo, StringComparison.OrdinalIgnoreCase))
                    return true; // Trivially true, skip this case

                var tool = new DocReadTool(
                    null!,
                    context.Owner,
                    context.Repo,
                    context.Branch,
                    context.Language);

                // Different repo should be denied
                return !tool.ValidateAccess(context.Owner, differentRepo, context.Branch);
            });
    }


    /// <summary>
    /// Property 4: 文档读取权限控制 - 不同Branch访问应该被拒绝
    /// For any DocReadTool context, accessing a different branch should be denied.
    /// Validates: Requirements 6.2, 6.3
    /// </summary>
    [Property(MaxTest = 100)]
    public Property ValidateAccess_DifferentBranch_ShouldReturnFalse()
    {
        var contextGen = GenerateContext();
        var differentBranchGen = GenerateBranch();

        return Prop.ForAll(
            contextGen.ToArbitrary(),
            differentBranchGen.ToArbitrary(),
            (context, differentBranch) =>
            {
                // Skip if the different branch happens to be the same
                if (string.Equals(context.Branch, differentBranch, StringComparison.OrdinalIgnoreCase))
                    return true; // Trivially true, skip this case

                var tool = new DocReadTool(
                    null!,
                    context.Owner,
                    context.Repo,
                    context.Branch,
                    context.Language);

                // Different branch should be denied
                return !tool.ValidateAccess(context.Owner, context.Repo, differentBranch);
            });
    }

    /// <summary>
    /// Property 4: 文档读取权限控制 - 大小写不敏感比较
    /// For any DocReadTool context, access validation should be case-insensitive.
    /// Validates: Requirements 6.2, 6.3
    /// </summary>
    [Property(MaxTest = 100)]
    public Property ValidateAccess_CaseInsensitive_ShouldReturnTrue()
    {
        return Prop.ForAll(
            GenerateContext().ToArbitrary(),
            context =>
            {
                var tool = new DocReadTool(
                    null!,
                    context.Owner,
                    context.Repo,
                    context.Branch,
                    context.Language);

                // Case variations should still be allowed
                var upperOwner = context.Owner.ToUpperInvariant();
                var upperRepo = context.Repo.ToUpperInvariant();
                var upperBranch = context.Branch.ToUpperInvariant();

                return tool.ValidateAccess(upperOwner, upperRepo, upperBranch);
            });
    }

    /// <summary>
    /// Property 4: 文档读取权限控制 - 完全不同的上下文应该被拒绝
    /// For any DocReadTool context, accessing a completely different context should be denied.
    /// Validates: Requirements 6.2, 6.3
    /// </summary>
    [Property(MaxTest = 100)]
    public Property ValidateAccess_CompletelyDifferentContext_ShouldReturnFalse()
    {
        var contextGen = GenerateContext();

        return Prop.ForAll(
            contextGen.ToArbitrary(),
            contextGen.ToArbitrary(),
            (originalContext, requestedContext) =>
            {
                // Skip if contexts happen to be the same
                if (string.Equals(originalContext.Owner, requestedContext.Owner, StringComparison.OrdinalIgnoreCase) &&
                    string.Equals(originalContext.Repo, requestedContext.Repo, StringComparison.OrdinalIgnoreCase) &&
                    string.Equals(originalContext.Branch, requestedContext.Branch, StringComparison.OrdinalIgnoreCase))
                    return true; // Trivially true, skip this case

                var tool = new DocReadTool(
                    null!,
                    originalContext.Owner,
                    originalContext.Repo,
                    originalContext.Branch,
                    originalContext.Language);

                // Different context should be denied (at least one component is different)
                var isAllowed = tool.ValidateAccess(
                    requestedContext.Owner,
                    requestedContext.Repo,
                    requestedContext.Branch);

                // If any component is different, access should be denied
                var ownerSame = string.Equals(originalContext.Owner, requestedContext.Owner, StringComparison.OrdinalIgnoreCase);
                var repoSame = string.Equals(originalContext.Repo, requestedContext.Repo, StringComparison.OrdinalIgnoreCase);
                var branchSame = string.Equals(originalContext.Branch, requestedContext.Branch, StringComparison.OrdinalIgnoreCase);

                // Access should only be allowed if all components match
                return isAllowed == (ownerSame && repoSame && branchSame);
            });
    }

    /// <summary>
    /// Property 4: 文档读取权限控制 - 工具属性应该正确存储
    /// For any DocReadTool, the context properties should be correctly stored.
    /// Validates: Requirements 6.2, 6.3
    /// </summary>
    [Property(MaxTest = 100)]
    public Property DocReadTool_PropertiesShouldBeCorrectlyStored()
    {
        return Prop.ForAll(
            GenerateContext().ToArbitrary(),
            context =>
            {
                var tool = new DocReadTool(
                    null!,
                    context.Owner,
                    context.Repo,
                    context.Branch,
                    context.Language);

                return tool.Owner == context.Owner &&
                       tool.Repo == context.Repo &&
                       tool.Branch == context.Branch &&
                       tool.Language == context.Language;
            });
    }
}
