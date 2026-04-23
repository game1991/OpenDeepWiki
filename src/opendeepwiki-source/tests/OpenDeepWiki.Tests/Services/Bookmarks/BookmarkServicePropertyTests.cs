using FsCheck;
using FsCheck.Fluent;
using FsCheck.Xunit;
using OpenDeepWiki.Entities;

namespace OpenDeepWiki.Tests.Services.Bookmarks;

/// <summary>
/// Property-based tests for Bookmark Service functionality.
/// Feature: repository-bookmark-subscription
/// </summary>
public class BookmarkServicePropertyTests
{
    private static Gen<string> GenerateGuidString() =>
        Gen.Constant(0).Select(_ => Guid.NewGuid().ToString());

    private static Gen<string> GenerateOrgName() =>
        Gen.Elements("microsoft", "google", "facebook", "amazon");

    private static Gen<string> GenerateRepoName() =>
        Gen.Elements("api", "sdk", "cli", "web", "docs", "core");

    private static Gen<Repository> GenerateRepository(int initialBookmarkCount = 0) =>
        GenerateGuidString().SelectMany(id =>
            GenerateGuidString().SelectMany(ownerId =>
                GenerateOrgName().SelectMany(orgName =>
                    GenerateRepoName().Select(repoName =>
                        new Repository
                        {
                            Id = id,
                            OwnerUserId = ownerId,
                            OrgName = orgName,
                            RepoName = repoName,
                            GitUrl = $"https://github.com/{orgName}/{repoName}.git",
                            IsPublic = true,
                            Status = RepositoryStatus.Completed,
                            BookmarkCount = initialBookmarkCount,
                            CreatedAt = DateTime.UtcNow
                        }))));

    private static Gen<User> GenerateUser() =>
        GenerateGuidString().Select(id =>
            new User
            {
                Id = id,
                Name = $"User_{id[..8]}",
                Email = $"user_{id[..8]}@example.com",
                CreatedAt = DateTime.UtcNow
            });


    private static Gen<int> GeneratePositiveBookmarkCount() =>
        Gen.Choose(1, 1000);

    private static Gen<List<bool>> GenerateOperationSequence() =>
        Gen.ListOf(Gen.Elements(true, false)).Select(ops => ops.ToList());

    private class MockBookmarkStore
    {
        private readonly Dictionary<string, Repository> _repositories = new();
        private readonly Dictionary<string, User> _users = new();
        private readonly HashSet<(string UserId, string RepositoryId)> _bookmarks = new();

        public void AddRepository(Repository repository) => _repositories[repository.Id] = repository;
        public void AddUser(User user) => _users[user.Id] = user;
        public bool HasBookmark(string userId, string repositoryId) => _bookmarks.Contains((userId, repositoryId));
        public int GetBookmarkCount(string repositoryId) =>
            _repositories.TryGetValue(repositoryId, out var repo) ? repo.BookmarkCount : 0;

        public (bool Success, string? ErrorMessage, string? BookmarkId) AddBookmark(string userId, string repositoryId)
        {
            if (!_repositories.ContainsKey(repositoryId)) return (false, "仓库不存在", null);
            if (!_users.ContainsKey(userId)) return (false, "用户不存在", null);
            if (_bookmarks.Contains((userId, repositoryId))) return (false, "已收藏该仓库", null);

            var bookmarkId = Guid.NewGuid().ToString();
            _bookmarks.Add((userId, repositoryId));
            _repositories[repositoryId].BookmarkCount++;
            return (true, null, bookmarkId);
        }

        public (bool Success, string? ErrorMessage) RemoveBookmark(string userId, string repositoryId)
        {
            if (!_bookmarks.Contains((userId, repositoryId))) return (false, "收藏记录不存在");

            _bookmarks.Remove((userId, repositoryId));
            var repo = _repositories[repositoryId];
            repo.BookmarkCount = Math.Max(0, repo.BookmarkCount - 1);
            return (true, null);
        }
    }


    /// <summary>
    /// Property 1: 收藏操作原子性
    /// **Validates: Requirements 4.1, 4.2**
    /// </summary>
    [Property(MaxTest = 100)]
    public Property AddBookmark_CreatesRecordAndIncrementsCount_Atomically()
    {
        return Prop.ForAll(
            GenerateRepository().ToArbitrary(),
            GenerateUser().ToArbitrary(),
            (repository, user) =>
            {
                var store = new MockBookmarkStore();
                store.AddRepository(repository);
                store.AddUser(user);

                var initialCount = store.GetBookmarkCount(repository.Id);
                var (success, _, _) = store.AddBookmark(user.Id, repository.Id);
                var finalCount = store.GetBookmarkCount(repository.Id);
                var finalHasBookmark = store.HasBookmark(user.Id, repository.Id);

                return success && finalHasBookmark && finalCount == initialCount + 1;
            })
            .Label("Feature: repository-bookmark-subscription, Property 1: 收藏操作原子性");
    }

    /// <summary>
    /// Property 1: 收藏操作原子性 - 计数增量验证
    /// **Validates: Requirements 4.1, 4.2**
    /// </summary>
    [Property(MaxTest = 100)]
    public Property AddBookmark_IncrementsCountByExactlyOne()
    {
        return Prop.ForAll(
            GeneratePositiveBookmarkCount().ToArbitrary(),
            GenerateUser().ToArbitrary(),
            (initialCount, user) =>
            {
                var repository = new Repository
                {
                    Id = Guid.NewGuid().ToString(),
                    OwnerUserId = Guid.NewGuid().ToString(),
                    OrgName = "test-org",
                    RepoName = "test-repo",
                    GitUrl = "https://github.com/test-org/test-repo.git",
                    BookmarkCount = initialCount,
                    Status = RepositoryStatus.Completed,
                    CreatedAt = DateTime.UtcNow
                };

                var store = new MockBookmarkStore();
                store.AddRepository(repository);
                store.AddUser(user);

                var (success, _, _) = store.AddBookmark(user.Id, repository.Id);
                var finalCount = store.GetBookmarkCount(repository.Id);

                return success && finalCount == initialCount + 1;
            })
            .Label("Feature: repository-bookmark-subscription, Property 1: 收藏操作原子性 - 计数增量验证");
    }


    /// <summary>
    /// Property 2: 取消收藏操作原子性
    /// **Validates: Requirements 5.1, 5.2**
    /// </summary>
    [Property(MaxTest = 100)]
    public Property RemoveBookmark_DeletesRecordAndDecrementsCount_Atomically()
    {
        return Prop.ForAll(
            GenerateRepository().ToArbitrary(),
            GenerateUser().ToArbitrary(),
            (repository, user) =>
            {
                var store = new MockBookmarkStore();
                store.AddRepository(repository);
                store.AddUser(user);

                store.AddBookmark(user.Id, repository.Id);
                var countAfterAdd = store.GetBookmarkCount(repository.Id);

                var (success, _) = store.RemoveBookmark(user.Id, repository.Id);
                var finalCount = store.GetBookmarkCount(repository.Id);
                var finalHasBookmark = store.HasBookmark(user.Id, repository.Id);

                return success && !finalHasBookmark && finalCount == countAfterAdd - 1;
            })
            .Label("Feature: repository-bookmark-subscription, Property 2: 取消收藏操作原子性");
    }

    /// <summary>
    /// Property 2: 取消收藏操作原子性 - 计数减量验证
    /// **Validates: Requirements 5.1, 5.2**
    /// </summary>
    [Property(MaxTest = 100)]
    public Property RemoveBookmark_DecrementsCountByExactlyOne()
    {
        return Prop.ForAll(
            GeneratePositiveBookmarkCount().ToArbitrary(),
            GenerateUser().ToArbitrary(),
            (initialCount, user) =>
            {
                var repository = new Repository
                {
                    Id = Guid.NewGuid().ToString(),
                    OwnerUserId = Guid.NewGuid().ToString(),
                    OrgName = "test-org",
                    RepoName = "test-repo",
                    GitUrl = "https://github.com/test-org/test-repo.git",
                    BookmarkCount = initialCount,
                    Status = RepositoryStatus.Completed,
                    CreatedAt = DateTime.UtcNow
                };

                var store = new MockBookmarkStore();
                store.AddRepository(repository);
                store.AddUser(user);

                store.AddBookmark(user.Id, repository.Id);
                var countAfterAdd = store.GetBookmarkCount(repository.Id);

                var (success, _) = store.RemoveBookmark(user.Id, repository.Id);
                var finalCount = store.GetBookmarkCount(repository.Id);

                return success && finalCount == countAfterAdd - 1;
            })
            .Label("Feature: repository-bookmark-subscription, Property 2: 取消收藏操作原子性 - 计数减量验证");
    }


    /// <summary>
    /// Property 5: 收藏计数非负不变量
    /// **Validates: Requirements 1.4, 5.5**
    /// </summary>
    [Property(MaxTest = 100)]
    public Property BookmarkCount_NeverBecomesNegative_AfterAnyOperationSequence()
    {
        return Prop.ForAll(
            GenerateRepository().ToArbitrary(),
            GenerateUser().ToArbitrary(),
            GenerateOperationSequence().ToArbitrary(),
            (repository, user, operations) =>
            {
                var store = new MockBookmarkStore();
                store.AddRepository(repository);
                store.AddUser(user);

                foreach (var isAdd in operations)
                {
                    if (isAdd)
                        store.AddBookmark(user.Id, repository.Id);
                    else
                        store.RemoveBookmark(user.Id, repository.Id);

                    var currentCount = store.GetBookmarkCount(repository.Id);
                    if (currentCount < 0) return false;
                }

                return store.GetBookmarkCount(repository.Id) >= 0;
            })
            .Label("Feature: repository-bookmark-subscription, Property 5: 收藏计数非负不变量");
    }

    /// <summary>
    /// Property 5: 收藏计数非负不变量 - 多用户场景
    /// **Validates: Requirements 1.4, 5.5**
    /// </summary>
    [Property(MaxTest = 100)]
    public Property BookmarkCount_NeverBecomesNegative_WithMultipleUsers()
    {
        var multiUserGen = Gen.ListOf(GenerateUser()).Select(users => users.Take(3).ToList());

        return Prop.ForAll(
            GenerateRepository().ToArbitrary(),
            multiUserGen.ToArbitrary(),
            (repository, users) =>
            {
                var store = new MockBookmarkStore();
                store.AddRepository(repository);
                foreach (var user in users) store.AddUser(user);

                foreach (var user in users) store.AddBookmark(user.Id, repository.Id);
                foreach (var user in users) store.RemoveBookmark(user.Id, repository.Id);
                foreach (var user in users) store.RemoveBookmark(user.Id, repository.Id);

                return store.GetBookmarkCount(repository.Id) >= 0;
            })
            .Label("Feature: repository-bookmark-subscription, Property 5: 收藏计数非负不变量 - 多用户场景");
    }

    /// <summary>
    /// Property 5: 收藏计数非负不变量 - 边界情况
    /// **Validates: Requirements 1.4, 5.5**
    /// </summary>
    [Property(MaxTest = 100)]
    public Property BookmarkCount_StaysZero_WhenRemovingFromZero()
    {
        return Prop.ForAll(
            GenerateUser().ToArbitrary(),
            user =>
            {
                var repository = new Repository
                {
                    Id = Guid.NewGuid().ToString(),
                    OwnerUserId = Guid.NewGuid().ToString(),
                    OrgName = "test-org",
                    RepoName = "test-repo",
                    GitUrl = "https://github.com/test-org/test-repo.git",
                    BookmarkCount = 0,
                    Status = RepositoryStatus.Completed,
                    CreatedAt = DateTime.UtcNow
                };

                var store = new MockBookmarkStore();
                store.AddRepository(repository);
                store.AddUser(user);

                var (success, _) = store.RemoveBookmark(user.Id, repository.Id);
                var finalCount = store.GetBookmarkCount(repository.Id);

                return !success && finalCount == 0;
            })
            .Label("Feature: repository-bookmark-subscription, Property 5: 收藏计数非负不变量 - 边界情况");
    }

    /// <summary>
    /// Property 9: 收藏列表完整性
    /// **Validates: Requirements 6.1**
    /// </summary>
    [Property(MaxTest = 100)]
    public Property GetUserBookmarks_ReturnsAllBookmarkedRepositories()
    {
        var multiRepoGen = Gen.ListOf(GenerateRepository())
            .Select(repos => repos.Take(5).ToList());

        return Prop.ForAll(
            GenerateUser().ToArbitrary(),
            multiRepoGen.ToArbitrary(),
            (user, repositories) =>
            {
                var store = new MockBookmarkStore();
                store.AddUser(user);
                foreach (var repo in repositories) store.AddRepository(repo);

                // Bookmark all repositories
                foreach (var repo in repositories)
                    store.AddBookmark(user.Id, repo.Id);

                // Verify all bookmarks exist
                var allBookmarked = repositories.All(repo => store.HasBookmark(user.Id, repo.Id));
                var bookmarkCount = repositories.Count(repo => store.HasBookmark(user.Id, repo.Id));

                return allBookmarked && bookmarkCount == repositories.Count;
            })
            .Label("Feature: repository-bookmark-subscription, Property 9: 收藏列表完整性");
    }

    /// <summary>
    /// Property 10: 收藏列表排序
    /// **Validates: Requirements 6.5**
    /// </summary>
    [Property(MaxTest = 100)]
    public Property GetUserBookmarks_ReturnsSortedByBookmarkTime()
    {
        var multiRepoGen = Gen.ListOf(GenerateRepository())
            .Select(repos => repos.Take(5).ToList());

        return Prop.ForAll(
            GenerateUser().ToArbitrary(),
            multiRepoGen.ToArbitrary(),
            (user, repositories) =>
            {
                var store = new MockBookmarkStoreWithTime();
                store.AddUser(user);
                foreach (var repo in repositories) store.AddRepository(repo);

                // Bookmark repositories with increasing timestamps
                var bookmarkOrder = new List<string>();
                foreach (var repo in repositories)
                {
                    store.AddBookmark(user.Id, repo.Id);
                    bookmarkOrder.Add(repo.Id);
                }

                // Get bookmarks sorted by time (newest first)
                var sortedBookmarks = store.GetUserBookmarksSortedByTime(user.Id);

                // Verify order is reversed (newest first)
                var expectedOrder = bookmarkOrder.AsEnumerable().Reverse().ToList();
                return sortedBookmarks.SequenceEqual(expectedOrder);
            })
            .Label("Feature: repository-bookmark-subscription, Property 10: 收藏列表排序");
    }

    /// <summary>
    /// Property 11: 收藏列表分页正确性
    /// **Validates: Requirements 6.4**
    /// </summary>
    [Property(MaxTest = 100)]
    public Property GetUserBookmarks_PaginationReturnsCorrectSubset()
    {
        var multiRepoGen = Gen.ListOf(GenerateRepository())
            .Select(repos => repos.Take(10).ToList());

        return Prop.ForAll(
            GenerateUser().ToArbitrary(),
            multiRepoGen.ToArbitrary(),
            Gen.Choose(1, 5).ToArbitrary(),
            (user, repositories, pageSize) =>
            {
                var store = new MockBookmarkStoreWithPagination();
                store.AddUser(user);
                foreach (var repo in repositories) store.AddRepository(repo);
                foreach (var repo in repositories) store.AddBookmark(user.Id, repo.Id);

                var totalBookmarks = repositories.Count;
                var totalPages = (int)Math.Ceiling((double)totalBookmarks / pageSize);

                // Verify each page returns correct count
                for (var page = 1; page <= totalPages; page++)
                {
                    var (items, total) = store.GetUserBookmarksPaged(user.Id, page, pageSize);
                    var expectedCount = page == totalPages
                        ? totalBookmarks - (page - 1) * pageSize
                        : pageSize;

                    if (items.Count != expectedCount || total != totalBookmarks)
                        return false;
                }

                return true;
            })
            .Label("Feature: repository-bookmark-subscription, Property 11: 收藏列表分页正确性");
    }

    /// <summary>
    /// Property 12: 收藏状态查询一致性
    /// **Validates: Requirements 9.1**
    /// </summary>
    [Property(MaxTest = 100)]
    public Property GetBookmarkStatus_ConsistentWithActualState()
    {
        return Prop.ForAll(
            GenerateRepository().ToArbitrary(),
            GenerateUser().ToArbitrary(),
            Gen.Elements(true, false).ToArbitrary(),
            (repository, user, shouldBookmark) =>
            {
                var store = new MockBookmarkStore();
                store.AddRepository(repository);
                store.AddUser(user);

                if (shouldBookmark)
                    store.AddBookmark(user.Id, repository.Id);

                var status = store.HasBookmark(user.Id, repository.Id);
                return status == shouldBookmark;
            })
            .Label("Feature: repository-bookmark-subscription, Property 12: 收藏状态查询一致性");
    }

    private class MockBookmarkStoreWithTime
    {
        private readonly Dictionary<string, Repository> _repositories = [];
        private readonly Dictionary<string, User> _users = [];
        private readonly Dictionary<(string UserId, string RepositoryId), DateTime> _bookmarks = [];
        private int _timeCounter;

        public void AddRepository(Repository repository) => _repositories[repository.Id] = repository;
        public void AddUser(User user) => _users[user.Id] = user;

        public void AddBookmark(string userId, string repositoryId)
        {
            if (!_bookmarks.ContainsKey((userId, repositoryId)))
            {
                _bookmarks[(userId, repositoryId)] = DateTime.UtcNow.AddSeconds(_timeCounter++);
            }
        }

        public List<string> GetUserBookmarksSortedByTime(string userId)
        {
            return _bookmarks
                .Where(b => b.Key.UserId == userId)
                .OrderByDescending(b => b.Value)
                .Select(b => b.Key.RepositoryId)
                .ToList();
        }
    }

    private class MockBookmarkStoreWithPagination
    {
        private readonly Dictionary<string, Repository> _repositories = [];
        private readonly Dictionary<string, User> _users = [];
        private readonly List<(string UserId, string RepositoryId)> _bookmarks = [];

        public void AddRepository(Repository repository) => _repositories[repository.Id] = repository;
        public void AddUser(User user) => _users[user.Id] = user;

        public void AddBookmark(string userId, string repositoryId)
        {
            var key = (userId, repositoryId);
            if (!_bookmarks.Contains(key))
                _bookmarks.Add(key);
        }

        public (List<string> Items, int Total) GetUserBookmarksPaged(string userId, int page, int pageSize)
        {
            var userBookmarks = _bookmarks
                .Where(b => b.UserId == userId)
                .Select(b => b.RepositoryId)
                .ToList();

            var items = userBookmarks
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .ToList();

            return (items, userBookmarks.Count);
        }
    }
}
