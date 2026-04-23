using FsCheck;
using FsCheck.Fluent;
using FsCheck.Xunit;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using OpenDeepWiki.Chat.Sessions;
using Xunit;

namespace OpenDeepWiki.Tests.Chat.Sessions;

/// <summary>
/// Property-based tests for session lookup/creation idempotency.
/// Feature: multi-platform-agent-chat, Property 6: 会话查找/创建幂等性
/// Validates: Requirements 6.2
/// </summary>
public class SessionIdempotencyPropertyTests
{
    /// <summary>
    /// 生成有效的用户ID
    /// </summary>
    private static Gen<string> GenerateValidUserId()
    {
        return Gen.Elements(
            "user123",
            "test_user_456",
            "platform_user_789",
            "U12345678",
            "user_abc_def"
        );
    }
    
    /// <summary>
    /// 生成有效的平台标识
    /// </summary>
    private static Gen<string> GenerateValidPlatform()
    {
        return Gen.Elements(
            "feishu",
            "qq",
            "wechat",
            "test",
            "custom"
        );
    }
    
    /// <summary>
    /// 创建测试用的 SessionManager
    /// </summary>
    private static SessionManager CreateSessionManager(TestDbContext context)
    {
        var logger = new LoggerFactory().CreateLogger<SessionManager>();
        var options = Options.Create(new SessionManagerOptions
        {
            MaxHistoryCount = 100,
            SessionExpirationMinutes = 30,
            CacheExpirationMinutes = 10,
            EnableCache = true
        });
        
        return new SessionManager(context, logger, options);
    }

    /// <summary>
    /// Property 6: 会话查找/创建幂等性
    /// For any 用户标识和平台组合，多次调用 GetOrCreateSession 应返回相同的会话实例（相同 SessionId）。
    /// Validates: Requirements 6.2
    /// </summary>
    [Property(MaxTest = 100)]
    public Property GetOrCreateSession_ShouldBeIdempotent()
    {
        return Prop.ForAll(
            GenerateValidUserId().ToArbitrary(),
            GenerateValidPlatform().ToArbitrary(),
            (userId, platform) =>
            {
                using var context = TestDbContext.Create();
                var manager = CreateSessionManager(context);
                
                // 第一次调用
                var session1 = manager.GetOrCreateSessionAsync(userId, platform).Result;
                
                // 第二次调用
                var session2 = manager.GetOrCreateSessionAsync(userId, platform).Result;
                
                // 第三次调用
                var session3 = manager.GetOrCreateSessionAsync(userId, platform).Result;
                
                // 所有调用应返回相同的 SessionId
                return session1.SessionId == session2.SessionId &&
                       session2.SessionId == session3.SessionId;
            });
    }
    
    /// <summary>
    /// Property 6: 会话查找/创建幂等性
    /// For any 用户标识和平台组合，GetOrCreateSession 返回的会话应包含正确的用户和平台信息。
    /// Validates: Requirements 6.2
    /// </summary>
    [Property(MaxTest = 100)]
    public Property GetOrCreateSession_ShouldReturnCorrectUserAndPlatform()
    {
        return Prop.ForAll(
            GenerateValidUserId().ToArbitrary(),
            GenerateValidPlatform().ToArbitrary(),
            (userId, platform) =>
            {
                using var context = TestDbContext.Create();
                var manager = CreateSessionManager(context);
                
                var session = manager.GetOrCreateSessionAsync(userId, platform).Result;
                
                return session.UserId == userId && session.Platform == platform;
            });
    }
    
    /// <summary>
    /// Property 6: 会话查找/创建幂等性
    /// For any 不同的用户标识或平台组合，GetOrCreateSession 应返回不同的会话。
    /// Validates: Requirements 6.2
    /// </summary>
    [Property(MaxTest = 100)]
    public Property GetOrCreateSession_DifferentUserOrPlatform_ShouldReturnDifferentSessions()
    {
        return Prop.ForAll(
            GenerateValidUserId().ToArbitrary(),
            GenerateValidUserId().ToArbitrary(),
            GenerateValidPlatform().ToArbitrary(),
            (userId1, userId2, platform) =>
            {
                // 跳过相同用户的情况
                if (userId1 == userId2) return true;
                
                using var context = TestDbContext.Create();
                var manager = CreateSessionManager(context);
                
                var session1 = manager.GetOrCreateSessionAsync(userId1, platform).Result;
                var session2 = manager.GetOrCreateSessionAsync(userId2, platform).Result;
                
                // 不同用户应返回不同的会话
                return session1.SessionId != session2.SessionId;
            });
    }

    /// <summary>
    /// Property 6: 会话查找/创建幂等性
    /// For any 新创建的会话，状态应为 Active。
    /// Validates: Requirements 6.2
    /// </summary>
    [Property(MaxTest = 100)]
    public Property NewSession_ShouldHaveActiveState()
    {
        return Prop.ForAll(
            GenerateValidUserId().ToArbitrary(),
            GenerateValidPlatform().ToArbitrary(),
            (userId, platform) =>
            {
                using var context = TestDbContext.Create();
                var manager = CreateSessionManager(context);
                
                var session = manager.GetOrCreateSessionAsync(userId, platform).Result;
                
                return session.State == SessionState.Active;
            });
    }
    
    /// <summary>
    /// Property 6: 会话查找/创建幂等性
    /// For any 新创建的会话，历史消息应为空。
    /// Validates: Requirements 6.2
    /// </summary>
    [Property(MaxTest = 100)]
    public Property NewSession_ShouldHaveEmptyHistory()
    {
        return Prop.ForAll(
            GenerateValidUserId().ToArbitrary(),
            GenerateValidPlatform().ToArbitrary(),
            (userId, platform) =>
            {
                using var context = TestDbContext.Create();
                var manager = CreateSessionManager(context);
                
                var session = manager.GetOrCreateSessionAsync(userId, platform).Result;
                
                return session.History.Count == 0;
            });
    }
    
    /// <summary>
    /// Property 6: 会话查找/创建幂等性
    /// For any 会话，通过 GetSessionAsync 获取应返回相同的会话。
    /// Validates: Requirements 6.2
    /// </summary>
    [Property(MaxTest = 100)]
    public Property GetSession_ShouldReturnSameSession()
    {
        return Prop.ForAll(
            GenerateValidUserId().ToArbitrary(),
            GenerateValidPlatform().ToArbitrary(),
            (userId, platform) =>
            {
                using var context = TestDbContext.Create();
                var manager = CreateSessionManager(context);
                
                var session1 = manager.GetOrCreateSessionAsync(userId, platform).Result;
                var session2 = manager.GetSessionAsync(session1.SessionId).Result;
                
                return session2 != null &&
                       session1.SessionId == session2.SessionId &&
                       session1.UserId == session2.UserId &&
                       session1.Platform == session2.Platform;
            });
    }
}
