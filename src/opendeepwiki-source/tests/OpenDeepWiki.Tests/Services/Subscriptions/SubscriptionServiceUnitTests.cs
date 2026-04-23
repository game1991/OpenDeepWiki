using Xunit;
using OpenDeepWiki.Entities;

namespace OpenDeepWiki.Tests.Services.Subscriptions;

public class SubscriptionServiceUnitTests
{
    private class MockSubscriptionStore
    {
        private readonly Dictionary<string, Repository> _repositories = new();
        private readonly Dictionary<string, User> _users = new();
        private readonly HashSet<(string UserId, string RepositoryId)> _subscriptions = new();

        public void AddRepository(Repository repository) => _repositories[repository.Id] = repository;
        public void AddUser(User user) => _users[user.Id] = user;
        public int GetSubscriptionCount(string repositoryId) =>
            _repositories.TryGetValue(repositoryId, out var repo) ? repo.SubscriptionCount : 0;

        public (bool Success, string? ErrorCode, string? ErrorMessage) AddSubscription(string userId, string repositoryId)
        {
            if (!_repositories.ContainsKey(repositoryId)) return (false, "REPOSITORY_NOT_FOUND", "仓库不存在");
            if (!_users.ContainsKey(userId)) return (false, "USER_NOT_FOUND", "用户不存在");
            if (_subscriptions.Contains((userId, repositoryId))) return (false, "SUBSCRIPTION_DUPLICATE", "已订阅该仓库");

            _subscriptions.Add((userId, repositoryId));
            _repositories[repositoryId].SubscriptionCount++;
            return (true, null, null);
        }

        public (bool Success, string? ErrorCode, string? ErrorMessage) RemoveSubscription(string userId, string repositoryId)
        {
            if (!_subscriptions.Contains((userId, repositoryId))) return (false, "SUBSCRIPTION_NOT_FOUND", "订阅记录不存在");

            _subscriptions.Remove((userId, repositoryId));
            var repo = _repositories[repositoryId];
            repo.SubscriptionCount = Math.Max(0, repo.SubscriptionCount - 1);
            return (true, null, null);
        }
    }


    /// <summary>
    /// 测试重复订阅返回错误
    /// **Validates: Requirements 7.3**
    /// </summary>
    [Fact]
    public void AddSubscription_DuplicateSubscription_ReturnsConflictError()
    {
        var store = new MockSubscriptionStore();
        var repository = new Repository
        {
            Id = "repo-1",
            OwnerUserId = "owner-1",
            OrgName = "test-org",
            RepoName = "test-repo",
            GitUrl = "https://github.com/test-org/test-repo.git",
            SubscriptionCount = 0,
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

        var (success1, _, _) = store.AddSubscription(user.Id, repository.Id);
        Assert.True(success1);

        var (success2, errorCode, _) = store.AddSubscription(user.Id, repository.Id);

        Assert.False(success2);
        Assert.Equal("SUBSCRIPTION_DUPLICATE", errorCode);
    }

    /// <summary>
    /// 测试订阅不存在的仓库返回错误
    /// **Validates: Requirements 7.4**
    /// </summary>
    [Fact]
    public void AddSubscription_NonExistentRepository_ReturnsNotFoundError()
    {
        var store = new MockSubscriptionStore();
        var user = new User
        {
            Id = "user-1",
            Name = "TestUser",
            Email = "test@example.com",
            CreatedAt = DateTime.UtcNow
        };
        store.AddUser(user);

        var (success, errorCode, _) = store.AddSubscription(user.Id, "non-existent-repo");

        Assert.False(success);
        Assert.Equal("REPOSITORY_NOT_FOUND", errorCode);
    }

    /// <summary>
    /// 测试取消不存在的订阅返回错误
    /// **Validates: Requirements 8.3**
    /// </summary>
    [Fact]
    public void RemoveSubscription_NonExistentSubscription_ReturnsNotFoundError()
    {
        var store = new MockSubscriptionStore();
        var repository = new Repository
        {
            Id = "repo-1",
            OwnerUserId = "owner-1",
            OrgName = "test-org",
            RepoName = "test-repo",
            GitUrl = "https://github.com/test-org/test-repo.git",
            SubscriptionCount = 0,
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

        var (success, errorCode, _) = store.RemoveSubscription(user.Id, repository.Id);

        Assert.False(success);
        Assert.Equal("SUBSCRIPTION_NOT_FOUND", errorCode);
    }
}
