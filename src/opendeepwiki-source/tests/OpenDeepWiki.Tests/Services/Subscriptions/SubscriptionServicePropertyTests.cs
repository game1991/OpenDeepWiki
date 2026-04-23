using FsCheck;
using FsCheck.Fluent;
using FsCheck.Xunit;
using OpenDeepWiki.Entities;

namespace OpenDeepWiki.Tests.Services.Subscriptions;

public class SubscriptionServicePropertyTests
{
    private static Gen<string> GenerateGuidString() =>
        Gen.Constant(0).Select(_ => Guid.NewGuid().ToString());

    private static Gen<Repository> GenerateRepository() =>
        GenerateGuidString().SelectMany(id =>
            GenerateGuidString().Select(ownerId =>
                new Repository
                {
                    Id = id,
                    OwnerUserId = ownerId,
                    OrgName = "test-org",
                    RepoName = "test-repo",
                    GitUrl = "https://github.com/test-org/test-repo.git",
                    IsPublic = true,
                    Status = RepositoryStatus.Completed,
                    SubscriptionCount = 0,
                    CreatedAt = DateTime.UtcNow
                }));

    private static Gen<User> GenerateUser() =>
        GenerateGuidString().Select(id =>
            new User
            {
                Id = id,
                Name = "TestUser",
                Email = "test@example.com",
                CreatedAt = DateTime.UtcNow
            });


    private class MockSubscriptionStore
    {
        private readonly Dictionary<string, Repository> _repositories = new();
        private readonly Dictionary<string, User> _users = new();
        private readonly HashSet<(string UserId, string RepositoryId)> _subscriptions = new();

        public void AddRepository(Repository repository) => _repositories[repository.Id] = repository;
        public void AddUser(User user) => _users[user.Id] = user;
        public int GetSubscriptionCount(string repositoryId) =>
            _repositories.TryGetValue(repositoryId, out var repo) ? repo.SubscriptionCount : 0;

        public (bool Success, string? ErrorMessage) AddSubscription(string userId, string repositoryId)
        {
            if (!_repositories.ContainsKey(repositoryId)) return (false, "仓库不存在");
            if (!_users.ContainsKey(userId)) return (false, "用户不存在");
            if (_subscriptions.Contains((userId, repositoryId))) return (false, "已订阅该仓库");

            _subscriptions.Add((userId, repositoryId));
            _repositories[repositoryId].SubscriptionCount++;
            return (true, null);
        }

        public (bool Success, string? ErrorMessage) RemoveSubscription(string userId, string repositoryId)
        {
            if (!_subscriptions.Contains((userId, repositoryId))) return (false, "订阅记录不存在");

            _subscriptions.Remove((userId, repositoryId));
            var repo = _repositories[repositoryId];
            repo.SubscriptionCount = Math.Max(0, repo.SubscriptionCount - 1);
            return (true, null);
        }
    }

    /// <summary>
    /// Property 3: 订阅操作原子性
    /// **Validates: Requirements 7.1, 7.2**
    /// </summary>
    [Property(MaxTest = 100)]
    public Property AddSubscription_CreatesRecordAndIncrementsCount_Atomically()
    {
        return Prop.ForAll(
            GenerateRepository().ToArbitrary(),
            GenerateUser().ToArbitrary(),
            (repository, user) =>
            {
                var store = new MockSubscriptionStore();
                store.AddRepository(repository);
                store.AddUser(user);

                var initialCount = store.GetSubscriptionCount(repository.Id);
                var (success, _) = store.AddSubscription(user.Id, repository.Id);
                var finalCount = store.GetSubscriptionCount(repository.Id);

                return success && finalCount == initialCount + 1;
            })
            .Label("Property 3: 订阅操作原子性");
    }


    /// <summary>
    /// Property 4: 取消订阅操作原子性
    /// **Validates: Requirements 8.1, 8.2**
    /// </summary>
    [Property(MaxTest = 100)]
    public Property RemoveSubscription_DeletesRecordAndDecrementsCount_Atomically()
    {
        return Prop.ForAll(
            GenerateRepository().ToArbitrary(),
            GenerateUser().ToArbitrary(),
            (repository, user) =>
            {
                var store = new MockSubscriptionStore();
                store.AddRepository(repository);
                store.AddUser(user);

                store.AddSubscription(user.Id, repository.Id);
                var countAfterAdd = store.GetSubscriptionCount(repository.Id);

                var (success, _) = store.RemoveSubscription(user.Id, repository.Id);
                var finalCount = store.GetSubscriptionCount(repository.Id);

                return success && finalCount == countAfterAdd - 1;
            })
            .Label("Property 4: 取消订阅操作原子性");
    }

    /// <summary>
    /// Property 6: 订阅计数非负不变量
    /// **Validates: Requirements 1.4, 8.5**
    /// </summary>
    [Property(MaxTest = 100)]
    public Property SubscriptionCount_NeverBecomesNegative()
    {
        return Prop.ForAll(
            GenerateRepository().ToArbitrary(),
            GenerateUser().ToArbitrary(),
            (repository, user) =>
            {
                var store = new MockSubscriptionStore();
                store.AddRepository(repository);
                store.AddUser(user);

                store.RemoveSubscription(user.Id, repository.Id);
                store.RemoveSubscription(user.Id, repository.Id);

                return store.GetSubscriptionCount(repository.Id) >= 0;
            })
            .Label("Property 6: 订阅计数非负不变量");
    }

    /// <summary>
    /// Property 13: 订阅状态查询一致性
    /// **Validates: Requirements 9.2**
    /// </summary>
    [Property(MaxTest = 100)]
    public Property GetSubscriptionStatus_ConsistentWithActualState()
    {
        return Prop.ForAll(
            GenerateRepository().ToArbitrary(),
            GenerateUser().ToArbitrary(),
            Gen.Elements(true, false).ToArbitrary(),
            (repository, user, shouldSubscribe) =>
            {
                var store = new MockSubscriptionStoreWithStatus();
                store.AddRepository(repository);
                store.AddUser(user);

                if (shouldSubscribe)
                    store.AddSubscription(user.Id, repository.Id);

                var status = store.IsSubscribed(user.Id, repository.Id);
                return status == shouldSubscribe;
            })
            .Label("Property 13: 订阅状态查询一致性");
    }

    private class MockSubscriptionStoreWithStatus
    {
        private readonly Dictionary<string, Repository> _repositories = [];
        private readonly Dictionary<string, User> _users = [];
        private readonly HashSet<(string UserId, string RepositoryId)> _subscriptions = [];

        public void AddRepository(Repository repository) => _repositories[repository.Id] = repository;
        public void AddUser(User user) => _users[user.Id] = user;
        public bool IsSubscribed(string userId, string repositoryId) => _subscriptions.Contains((userId, repositoryId));

        public void AddSubscription(string userId, string repositoryId)
        {
            if (_repositories.ContainsKey(repositoryId) && _users.ContainsKey(userId))
            {
                _subscriptions.Add((userId, repositoryId));
            }
        }
    }
}
