using FsCheck;
using FsCheck.Fluent;
using FsCheck.Xunit;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using OpenDeepWiki.Chat.Abstractions;
using OpenDeepWiki.Chat.Sessions;
using Xunit;

namespace OpenDeepWiki.Tests.Chat.Sessions;

/// <summary>
/// Property-based tests for session persistence round-trip consistency.
/// Feature: multi-platform-agent-chat, Property 8: 会话持久化往返一致性
/// Validates: Requirements 6.5
/// </summary>
public class SessionPersistencePropertyTests
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
    /// 生成有效的消息内容
    /// </summary>
    private static Gen<string> GenerateValidContent()
    {
        return Gen.Elements(
            "Hello, world!",
            "这是一条测试消息",
            "Test message",
            "Short",
            "Another message"
        );
    }
    
    /// <summary>
    /// 创建测试用的 SessionManager（禁用缓存以测试数据库持久化）
    /// </summary>
    private static SessionManager CreateSessionManager(TestDbContext context, bool enableCache = false)
    {
        var logger = new LoggerFactory().CreateLogger<SessionManager>();
        var options = Options.Create(new SessionManagerOptions
        {
            MaxHistoryCount = 100,
            SessionExpirationMinutes = 30,
            CacheExpirationMinutes = 10,
            EnableCache = enableCache
        });
        
        return new SessionManager(context, logger, options);
    }

    /// <summary>
    /// Property 8: 会话持久化往返一致性
    /// For any 有效的 ChatSession，保存到数据库后再加载，UserId 和 Platform 应保持一致。
    /// Validates: Requirements 6.5
    /// </summary>
    [Property(MaxTest = 100)]
    public Property SessionPersistence_UserIdAndPlatform_ShouldBeConsistent()
    {
        return Prop.ForAll(
            GenerateValidUserId().ToArbitrary(),
            GenerateValidPlatform().ToArbitrary(),
            (userId, platform) =>
            {
                using var context = TestDbContext.Create();
                var manager = CreateSessionManager(context, enableCache: false);
                
                // 创建会话
                var session1 = manager.GetOrCreateSessionAsync(userId, platform).Result;
                var sessionId = session1.SessionId;
                
                // 从数据库重新加载
                var session2 = manager.GetSessionAsync(sessionId).Result;
                
                return session2 != null &&
                       session2.UserId == userId &&
                       session2.Platform == platform;
            });
    }
    
    /// <summary>
    /// Property 8: 会话持久化往返一致性
    /// For any 有效的 ChatSession，保存到数据库后再加载，State 应保持一致。
    /// Validates: Requirements 6.5
    /// </summary>
    [Property(MaxTest = 100)]
    public Property SessionPersistence_State_ShouldBeConsistent()
    {
        return Prop.ForAll(
            GenerateValidUserId().ToArbitrary(),
            GenerateValidPlatform().ToArbitrary(),
            Gen.Elements(SessionState.Active, SessionState.Processing, SessionState.Waiting).ToArbitrary(),
            (userId, platform, state) =>
            {
                using var context = TestDbContext.Create();
                var manager = CreateSessionManager(context, enableCache: false);
                
                // 创建会话并更新状态
                var session1 = manager.GetOrCreateSessionAsync(userId, platform).Result;
                session1.UpdateState(state);
                manager.UpdateSessionAsync(session1).Wait();
                
                var sessionId = session1.SessionId;
                
                // 从数据库重新加载
                var session2 = manager.GetSessionAsync(sessionId).Result;
                
                return session2 != null && session2.State == state;
            });
    }

    /// <summary>
    /// Property 8: 会话持久化往返一致性
    /// For any 有效的 ChatSession，保存到数据库后再加载，History 消息内容应保持一致。
    /// Validates: Requirements 6.5
    /// </summary>
    [Property(MaxTest = 100)]
    public Property SessionPersistence_History_ShouldBeConsistent()
    {
        return Prop.ForAll(
            GenerateValidUserId().ToArbitrary(),
            GenerateValidPlatform().ToArbitrary(),
            Gen.Choose(1, 10).ToArbitrary(),  // messageCount
            (userId, platform, messageCount) =>
            {
                using var context = TestDbContext.Create();
                var manager = CreateSessionManager(context, enableCache: false);
                
                // 创建会话并添加消息
                var session1 = manager.GetOrCreateSessionAsync(userId, platform).Result;
                
                var messageContents = new List<string>();
                for (int i = 0; i < messageCount; i++)
                {
                    var content = $"Message {i}";
                    messageContents.Add(content);
                    
                    var message = new ChatMessage
                    {
                        MessageId = Guid.NewGuid().ToString(),
                        SenderId = userId,
                        Content = content,
                        MessageType = ChatMessageType.Text,
                        Platform = platform,
                        Timestamp = DateTimeOffset.UtcNow
                    };
                    session1.AddMessage(message);
                }
                
                manager.UpdateSessionAsync(session1).Wait();
                var sessionId = session1.SessionId;
                
                // 从数据库重新加载
                var session2 = manager.GetSessionAsync(sessionId).Result;
                
                if (session2 == null || session2.History.Count != messageCount)
                    return false;
                
                // 验证消息内容一致
                var loadedContents = session2.History.Select(m => m.Content).ToList();
                return messageContents.SequenceEqual(loadedContents);
            });
    }
    
    /// <summary>
    /// Property 8: 会话持久化往返一致性
    /// For any 关闭的会话，重新获取时不应返回该会话。
    /// Validates: Requirements 6.5
    /// </summary>
    [Property(MaxTest = 100)]
    public Property SessionPersistence_ClosedSession_ShouldNotBeReturned()
    {
        return Prop.ForAll(
            GenerateValidUserId().ToArbitrary(),
            GenerateValidPlatform().ToArbitrary(),
            (userId, platform) =>
            {
                using var context = TestDbContext.Create();
                var manager = CreateSessionManager(context, enableCache: false);
                
                // 创建会话
                var session1 = manager.GetOrCreateSessionAsync(userId, platform).Result;
                var sessionId = session1.SessionId;
                
                // 关闭会话
                manager.CloseSessionAsync(sessionId).Wait();
                
                // 重新获取会话（应创建新会话）
                var session2 = manager.GetOrCreateSessionAsync(userId, platform).Result;
                
                // 新会话应有不同的 SessionId
                return session2.SessionId != sessionId;
            });
    }

    /// <summary>
    /// Property 8: 会话持久化往返一致性
    /// For any 会话，通过 SessionId 获取应返回正确的会话。
    /// Validates: Requirements 6.5
    /// </summary>
    [Property(MaxTest = 100)]
    public Property SessionPersistence_GetBySessionId_ShouldReturnCorrectSession()
    {
        return Prop.ForAll(
            GenerateValidUserId().ToArbitrary(),
            GenerateValidPlatform().ToArbitrary(),
            (userId, platform) =>
            {
                using var context = TestDbContext.Create();
                var manager = CreateSessionManager(context, enableCache: false);
                
                // 创建会话
                var session1 = manager.GetOrCreateSessionAsync(userId, platform).Result;
                var sessionId = session1.SessionId;
                
                // 通过 SessionId 获取
                var session2 = manager.GetSessionAsync(sessionId).Result;
                
                return session2 != null &&
                       session2.SessionId == sessionId &&
                       session2.UserId == userId &&
                       session2.Platform == platform;
            });
    }
    
    /// <summary>
    /// Property 8: 会话持久化往返一致性
    /// For any 不存在的 SessionId，GetSessionAsync 应返回 null。
    /// Validates: Requirements 6.5
    /// </summary>
    [Fact]
    public async Task SessionPersistence_NonExistentSessionId_ShouldReturnNull()
    {
        using var context = TestDbContext.Create();
        var manager = CreateSessionManager(context, enableCache: false);
        
        var nonExistentId = Guid.NewGuid().ToString();
        var session = await manager.GetSessionAsync(nonExistentId);
        
        Assert.Null(session);
    }
    
    /// <summary>
    /// Property 8: 会话持久化往返一致性
    /// For any 无效的 SessionId 格式，GetSessionAsync 应返回 null。
    /// Validates: Requirements 6.5
    /// </summary>
    [Fact]
    public async Task SessionPersistence_InvalidSessionIdFormat_ShouldReturnNull()
    {
        using var context = TestDbContext.Create();
        var manager = CreateSessionManager(context, enableCache: false);
        
        var invalidId = "not-a-valid-guid";
        var session = await manager.GetSessionAsync(invalidId);
        
        Assert.Null(session);
    }
}
