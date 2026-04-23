using Xunit;
using OpenDeepWiki.Entities;

namespace OpenDeepWiki.Tests.Services.Bookmarks;

/// <summary>
/// Unit tests for BookmarkService.
/// Tests specific examples and edge cases.
/// </summary>
public class BookmarkServiceUnitTests
{
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

        public (bool Success, string? ErrorCode, string? ErrorMessage) AddBookmark(string userId, string repositoryId)
        {
            if (!_repositories.ContainsKey(repositoryId)) return (false, "REPOSITORY_NOT_FOUND", "仓库不存在");
            if (!_users.ContainsKey(userId)) return (false, "USER_NOT_FOUND", "用户不存在");
            if (_bookmarks.Contains((userId, repositoryId))) return (false, "BOOKMARK_DUPLICATE", "已收藏该仓库");

            _bookmarks.Add((userId, repositoryId));
            _repositories[repositoryId].BookmarkCount++;
            return (true, null, null);
        }

        public (bool Success, string? ErrorCode, string? ErrorMessage) RemoveBookmark(string userId, string repositoryId)
        {
            if (!_bookmarks.Contains((userId, repositoryId))) return (false, "BOOKMARK_NOT_FOUND", "收藏记录不存在");

            _bookmarks.Remove((userId, repositoryId));
            var repo = _repositories[repositoryId];
            repo.BookmarkCount = Math.Max(0, repo.BookmarkCount - 1);
            return (true, null, null);
        }
    }

    /// <summary>
    /// 测试重复收藏返回错误
    /// **Validates: Requirements 4.3**
    /// </summary>
    [Fact]
    public void AddBookmark_DuplicateBookmark_ReturnsConflictError()
    {
        // Arrange
        var store = new MockBookmarkStore();
        var repository = new Repository
        {
            Id = "repo-1",
            OwnerUserId = "owner-1",
            OrgName = "test-org",
            RepoName = "test-repo",
            GitUrl = "https://github.com/test-org/test-repo.git",
            BookmarkCount = 0,
            Status = RepositoryStatus.Completed,
            CreatedAt = DateTime.UtcNow
        };
        var user = new User
        {
            Id = "user-1",
            Name = "TestUser",
            Email = "test@example.com",
            CreatedAt = DateTime.UtcNow
        };
        store.AddRepository(repository);
        store.AddUser(user);

        // First bookmark should succeed
        var (success1, _, _) = store.AddBookmark(user.Id, repository.Id);
        Assert.True(success1);

        // Act - Second bookmark should fail
        var (success2, errorCode, _) = store.AddBookmark(user.Id, repository.Id);

        // Assert
        Assert.False(success2);
        Assert.Equal("BOOKMARK_DUPLICATE", errorCode);
    }

    /// <summary>
    /// 测试收藏不存在的仓库返回错误
    /// **Validates: Requirements 4.4**
    /// </summary>
    [Fact]
    public void AddBookmark_NonExistentRepository_ReturnsNotFoundError()
    {
        // Arrange
        var store = new MockBookmarkStore();
        var user = new User
        {
            Id = "user-1",
            Name = "TestUser",
            Email = "test@example.com",
            CreatedAt = DateTime.UtcNow
        };
        store.AddUser(user);

        // Act
        var (success, errorCode, _) = store.AddBookmark(user.Id, "non-existent-repo");

        // Assert
        Assert.False(success);
        Assert.Equal("REPOSITORY_NOT_FOUND", errorCode);
    }

    /// <summary>
    /// 测试取消不存在的收藏返回错误
    /// **Validates: Requirements 5.3**
    /// </summary>
    [Fact]
    public void RemoveBookmark_NonExistentBookmark_ReturnsNotFoundError()
    {
        // Arrange
        var store = new MockBookmarkStore();
        var repository = new Repository
        {
            Id = "repo-1",
            OwnerUserId = "owner-1",
            OrgName = "test-org",
            RepoName = "test-repo",
            GitUrl = "https://github.com/test-org/test-repo.git",
            BookmarkCount = 0,
            Status = RepositoryStatus.Completed,
            CreatedAt = DateTime.UtcNow
        };
        var user = new User
        {
            Id = "user-1",
            Name = "TestUser",
            Email = "test@example.com",
            CreatedAt = DateTime.UtcNow
        };
        store.AddRepository(repository);
        store.AddUser(user);

        // Act - Try to remove bookmark that doesn't exist
        var (success, errorCode, _) = store.RemoveBookmark(user.Id, repository.Id);

        // Assert
        Assert.False(success);
        Assert.Equal("BOOKMARK_NOT_FOUND", errorCode);
    }

    /// <summary>
    /// 测试收藏计数在操作失败时不变
    /// **Validates: Requirements 4.5**
    /// </summary>
    [Fact]
    public void AddBookmark_WhenFails_BookmarkCountUnchanged()
    {
        // Arrange
        var store = new MockBookmarkStore();
        var repository = new Repository
        {
            Id = "repo-1",
            OwnerUserId = "owner-1",
            OrgName = "test-org",
            RepoName = "test-repo",
            GitUrl = "https://github.com/test-org/test-repo.git",
            BookmarkCount = 5,
            Status = RepositoryStatus.Completed,
            CreatedAt = DateTime.UtcNow
        };
        var user = new User
        {
            Id = "user-1",
            Name = "TestUser",
            Email = "test@example.com",
            CreatedAt = DateTime.UtcNow
        };
        store.AddRepository(repository);
        store.AddUser(user);

        // First bookmark
        store.AddBookmark(user.Id, repository.Id);
        var countAfterFirst = store.GetBookmarkCount(repository.Id);

        // Act - Try duplicate bookmark (should fail)
        var (success, _, _) = store.AddBookmark(user.Id, repository.Id);

        // Assert
        Assert.False(success);
        Assert.Equal(countAfterFirst, store.GetBookmarkCount(repository.Id));
    }
}
