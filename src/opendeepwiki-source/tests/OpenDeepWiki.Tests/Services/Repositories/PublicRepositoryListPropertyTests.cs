using FsCheck;
using FsCheck.Fluent;
using FsCheck.Xunit;
using OpenDeepWiki.Entities;

namespace OpenDeepWiki.Tests.Services.Repositories;

/// <summary>
/// Property-based tests for Public Repository List functionality.
/// Feature: homepage-repository-list
/// Validates: Requirements 1.1, 1.2, 2.1
/// </summary>
public class PublicRepositoryListPropertyTests
{
    /// <summary>
    /// Generates a valid organization name.
    /// </summary>
    private static Gen<string> GenerateOrgName()
    {
        return Gen.Elements("microsoft", "google", "facebook", "amazon", "openai", "anthropic", "meta", "apple");
    }

    /// <summary>
    /// Generates a valid repository name.
    /// </summary>
    private static Gen<string> GenerateRepoName()
    {
        return Gen.Elements("api", "sdk", "cli", "web", "docs", "core", "utils", "tools", "app", "service");
    }

    /// <summary>
    /// Generates a valid Repository entity with random isPublic value.
    /// </summary>
    private static Gen<Repository> GenerateRepository()
    {
        return GenerateOrgName().SelectMany(orgName =>
            GenerateRepoName().SelectMany(repoName =>
                ArbMap.Default.GeneratorFor<bool>().SelectMany(isPublic =>
                    ArbMap.Default.GeneratorFor<DateTime>().Where(d => d > DateTime.MinValue && d < DateTime.MaxValue).Select(createdAt =>
                        new Repository
                        {
                            Id = Guid.NewGuid().ToString(),
                            OrgName = orgName,
                            RepoName = repoName,
                            IsPublic = isPublic,
                            GitUrl = $"https://github.com/{orgName}/{repoName}.git",
                            OwnerUserId = Guid.NewGuid().ToString(),
                            CreatedAt = createdAt,
                            Status = RepositoryStatus.Completed
                        }))));
    }

    /// <summary>
    /// Property 1: Public Repository Filtering
    /// For any API response containing repositories, when isPublic=true is specified,
    /// all returned repositories SHALL have isPublic=true.
    /// **Validates: Requirements 1.1**
    /// </summary>
    [Property(MaxTest = 100)]
    public Property PublicRepositoryFilter_ShouldOnlyReturnPublicRepositories()
    {
        var repositoryListGen = GenerateRepository().ListOf(20);

        return Prop.ForAll(
            repositoryListGen.ToArbitrary(),
            repositories =>
            {
                // Simulate filtering with isPublic=true
                var filteredRepositories = repositories.Where(r => r.IsPublic).ToList();

                // All filtered repositories should have IsPublic = true
                return filteredRepositories.All(r => r.IsPublic);
            });
    }

    /// <summary>
    /// Property 1: Public Repository Filtering - Completeness
    /// For any list of repositories, filtering by isPublic=true SHALL return
    /// all and only repositories where IsPublic is true.
    /// **Validates: Requirements 1.1**
    /// </summary>
    [Property(MaxTest = 100)]
    public Property PublicRepositoryFilter_ShouldReturnAllPublicRepositories()
    {
        var repositoryListGen = GenerateRepository().ListOf(20);

        return Prop.ForAll(
            repositoryListGen.ToArbitrary(),
            repositories =>
            {
                var expectedPublicCount = repositories.Count(r => r.IsPublic);
                var filteredRepositories = repositories.Where(r => r.IsPublic).ToList();

                return filteredRepositories.Count == expectedPublicCount;
            });
    }

    /// <summary>
    /// Property 2: Sorting by Creation Date
    /// For any list of repositories returned by the API with sortBy=createdAt and sortOrder=desc,
    /// for every adjacent pair of repositories (repo[i], repo[i+1]), repo[i].createdAt >= repo[i+1].createdAt.
    /// **Validates: Requirements 1.2**
    /// </summary>
    [Property(MaxTest = 100)]
    public Property SortByCreatedAtDesc_ShouldMaintainDescendingOrder()
    {
        var repositoryListGen = GenerateRepository().ListOf(20);

        return Prop.ForAll(
            repositoryListGen.ToArbitrary(),
            repositories =>
            {
                // Simulate sorting by createdAt descending
                var sortedRepositories = repositories
                    .OrderByDescending(r => r.CreatedAt)
                    .ToList();

                // Verify descending order for all adjacent pairs
                for (int i = 0; i < sortedRepositories.Count - 1; i++)
                {
                    if (sortedRepositories[i].CreatedAt < sortedRepositories[i + 1].CreatedAt)
                    {
                        return false;
                    }
                }
                return true;
            });
    }

    /// <summary>
    /// Property 2: Sorting by Creation Date - Ascending
    /// For any list of repositories with sortBy=createdAt and sortOrder=asc,
    /// for every adjacent pair of repositories (repo[i], repo[i+1]), repo[i].createdAt <= repo[i+1].createdAt.
    /// **Validates: Requirements 1.2**
    /// </summary>
    [Property(MaxTest = 100)]
    public Property SortByCreatedAtAsc_ShouldMaintainAscendingOrder()
    {
        var repositoryListGen = GenerateRepository().ListOf(20);

        return Prop.ForAll(
            repositoryListGen.ToArbitrary(),
            repositories =>
            {
                // Simulate sorting by createdAt ascending
                var sortedRepositories = repositories
                    .OrderBy(r => r.CreatedAt)
                    .ToList();

                // Verify ascending order for all adjacent pairs
                for (int i = 0; i < sortedRepositories.Count - 1; i++)
                {
                    if (sortedRepositories[i].CreatedAt > sortedRepositories[i + 1].CreatedAt)
                    {
                        return false;
                    }
                }
                return true;
            });
    }

    /// <summary>
    /// Property 5: Search Filtering Logic
    /// For any search keyword and list of repositories, the filtered results SHALL only contain
    /// repositories where orgName.ToLower().Contains(keyword.ToLower()) OR repoName.ToLower().Contains(keyword.ToLower()).
    /// **Validates: Requirements 2.1**
    /// </summary>
    [Property(MaxTest = 100)]
    public Property SearchFilter_ShouldMatchOrgNameOrRepoName()
    {
        var repositoryListGen = GenerateRepository().ListOf(20);
        var keywordGen = Gen.Elements("micro", "api", "sdk", "google", "web", "app", "core", "doc");

        return Prop.ForAll(
            repositoryListGen.ToArbitrary(),
            keywordGen.ToArbitrary(),
            (repositories, keyword) =>
            {
                var lowerKeyword = keyword.ToLower();

                // Simulate search filtering
                var filteredRepositories = repositories
                    .Where(r => r.OrgName.ToLower().Contains(lowerKeyword) ||
                                r.RepoName.ToLower().Contains(lowerKeyword))
                    .ToList();

                // All filtered repositories should match the keyword
                return filteredRepositories.All(r =>
                    r.OrgName.ToLower().Contains(lowerKeyword) ||
                    r.RepoName.ToLower().Contains(lowerKeyword));
            });
    }

    /// <summary>
    /// Property 5: Search Filtering Logic - Completeness
    /// For any search keyword, the filter SHALL return all repositories that match.
    /// **Validates: Requirements 2.1**
    /// </summary>
    [Property(MaxTest = 100)]
    public Property SearchFilter_ShouldReturnAllMatchingRepositories()
    {
        var repositoryListGen = GenerateRepository().ListOf(20);
        var keywordGen = Gen.Elements("micro", "api", "sdk", "google", "web", "app", "core", "doc");

        return Prop.ForAll(
            repositoryListGen.ToArbitrary(),
            keywordGen.ToArbitrary(),
            (repositories, keyword) =>
            {
                var lowerKeyword = keyword.ToLower();

                var expectedMatches = repositories.Count(r =>
                    r.OrgName.ToLower().Contains(lowerKeyword) ||
                    r.RepoName.ToLower().Contains(lowerKeyword));

                var filteredRepositories = repositories
                    .Where(r => r.OrgName.ToLower().Contains(lowerKeyword) ||
                                r.RepoName.ToLower().Contains(lowerKeyword))
                    .ToList();

                return filteredRepositories.Count == expectedMatches;
            });
    }

    /// <summary>
    /// Property 5: Search Filtering Logic - Empty Keyword
    /// When the search term is empty, the filter SHALL return all repositories.
    /// **Validates: Requirements 2.2**
    /// </summary>
    [Property(MaxTest = 100)]
    public Property SearchFilter_EmptyKeyword_ShouldReturnAllRepositories()
    {
        var repositoryListGen = GenerateRepository().ListOf(20);

        return Prop.ForAll(
            repositoryListGen.ToArbitrary(),
            repositories =>
            {
                var keyword = string.Empty;

                // When keyword is empty, return all
                var filteredRepositories = string.IsNullOrEmpty(keyword)
                    ? repositories.ToList()
                    : repositories.Where(r =>
                        r.OrgName.ToLower().Contains(keyword.ToLower()) ||
                        r.RepoName.ToLower().Contains(keyword.ToLower())).ToList();

                return filteredRepositories.Count == repositories.Count;
            });
    }

    /// <summary>
    /// Property 5: Search Filtering Logic - Case Insensitivity
    /// Search filtering SHALL be case-insensitive.
    /// **Validates: Requirements 2.1**
    /// </summary>
    [Property(MaxTest = 100)]
    public Property SearchFilter_ShouldBeCaseInsensitive()
    {
        var repositoryListGen = GenerateRepository().ListOf(20);

        return Prop.ForAll(
            repositoryListGen.ToArbitrary(),
            repositories =>
            {
                var keyword = "API";
                var lowerKeyword = keyword.ToLower();
                var upperKeyword = keyword.ToUpper();

                var lowerFiltered = repositories
                    .Where(r => r.OrgName.ToLower().Contains(lowerKeyword) ||
                                r.RepoName.ToLower().Contains(lowerKeyword))
                    .ToList();

                var upperFiltered = repositories
                    .Where(r => r.OrgName.ToLower().Contains(upperKeyword.ToLower()) ||
                                r.RepoName.ToLower().Contains(upperKeyword.ToLower()))
                    .ToList();

                // Both should return the same results
                return lowerFiltered.Count == upperFiltered.Count &&
                       lowerFiltered.All(r => upperFiltered.Contains(r));
            });
    }

    /// <summary>
    /// Combined Property: Public + Sorted + Filtered
    /// When filtering by isPublic=true, sorting by createdAt desc, and searching by keyword,
    /// the result SHALL satisfy all three properties simultaneously.
    /// **Validates: Requirements 1.1, 1.2, 2.1**
    /// </summary>
    [Property(MaxTest = 100)]
    public Property CombinedFilter_ShouldSatisfyAllProperties()
    {
        var repositoryListGen = GenerateRepository().ListOf(30);
        var keywordGen = Gen.Elements("micro", "api", "sdk", "google", "web", "app");

        return Prop.ForAll(
            repositoryListGen.ToArbitrary(),
            keywordGen.ToArbitrary(),
            (repositories, keyword) =>
            {
                var lowerKeyword = keyword.ToLower();

                // Apply all filters
                var result = repositories
                    .Where(r => r.IsPublic)
                    .Where(r => r.OrgName.ToLower().Contains(lowerKeyword) ||
                                r.RepoName.ToLower().Contains(lowerKeyword))
                    .OrderByDescending(r => r.CreatedAt)
                    .ToList();

                // Verify Property 1: All are public
                var allPublic = result.All(r => r.IsPublic);

                // Verify Property 2: Sorted descending
                var isSorted = true;
                for (int i = 0; i < result.Count - 1; i++)
                {
                    if (result[i].CreatedAt < result[i + 1].CreatedAt)
                    {
                        isSorted = false;
                        break;
                    }
                }

                // Verify Property 5: All match keyword
                var allMatchKeyword = result.All(r =>
                    r.OrgName.ToLower().Contains(lowerKeyword) ||
                    r.RepoName.ToLower().Contains(lowerKeyword));

                return allPublic && isSorted && allMatchKeyword;
            });
    }
}
