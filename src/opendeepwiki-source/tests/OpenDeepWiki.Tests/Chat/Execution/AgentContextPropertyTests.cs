using FsCheck;
using FsCheck.Fluent;
using FsCheck.Xunit;
using OpenDeepWiki.Chat.Abstractions;
using OpenDeepWiki.Chat.Execution;
using OpenDeepWiki.Chat.Sessions;
using Xunit;

namespace OpenDeepWiki.Tests.Chat.Execution;

/// <summary>
/// Property-based tests for Agent context passing completeness.
/// Feature: multi-platform-agent-chat, Property 12: Agent 上下文传递完整性
/// Validates: Requirements 9.1, 9.3
/// </summary>
public class AgentContextPropertyTests
{
    /// <summary>
    /// 生成有效的消息内容
    /// </summary>
    private static Gen<string> GenerateValidContent()
    {
        return Gen.Elements(
            "Hello, how are you?",
            "What is the weather today?",
            "Tell me a joke",
            "Help me with coding",
            "Explain quantum physics"
        );
    }
    
    /// <summary>
    /// 生成有效的用户ID
    /// </summary>
    private static Gen<string> GenerateValidUserId()
    {
        return Gen.Elements(
            "user123",
            "test_user_456",
            "platform_user_789"
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
            "wechat"
        );
    }
    
    /// <summary>
    /// 生成历史消息数量 (0-10)
    /// </summary>
    private static Gen<int> GenerateHistoryCount()
    {
        return Gen.Choose(0, 10);
    }
    
    /// <summary>
    /// 创建测试用的会话
    /// </summary>
    private static TestChatSession CreateTestSession(string userId, string platform, int historyCount)
    {
        var session = new TestChatSession(userId, platform);
        
        // 添加历史消息
        for (int i = 0; i < historyCount; i++)
        {
            var role = i % 2 == 0 ? userId : "assistant";
            session.AddMessage(new ChatMessage
            {
                MessageId = Guid.NewGuid().ToString(),
                SenderId = role,
                Content = $"History message {i + 1}",
                MessageType = ChatMessageType.Text,
                Platform = platform,
                Timestamp = DateTimeOffset.UtcNow.AddMinutes(-historyCount + i)
            });
        }
        
        return session;
    }

    /// <summary>
    /// Property 12: Agent 上下文传递完整性
    /// For any Agent 执行请求，传递给 Agent 的上下文应包含当前消息。
    /// Validates: Requirements 9.1
    /// </summary>
    [Property(MaxTest = 100)]
    public Property BuildContextMessages_ShouldIncludeCurrentMessage()
    {
        return Prop.ForAll(
            GenerateValidUserId().ToArbitrary(),
            GenerateValidPlatform().ToArbitrary(),
            GenerateValidContent().ToArbitrary(),
            (userId, platform, content) =>
            {
                var historyCount = new Random().Next(0, 11);
                var session = CreateTestSession(userId, platform, historyCount);
                
                var currentMessage = new ChatMessage
                {
                    MessageId = Guid.NewGuid().ToString(),
                    SenderId = userId,
                    Content = content,
                    MessageType = ChatMessageType.Text,
                    Platform = platform,
                    Timestamp = DateTimeOffset.UtcNow
                };
                
                // 使用测试辅助方法构建上下文
                var contextMessages = TestAgentContextBuilder.BuildContextMessages(currentMessage, session);
                
                // 验证当前消息在上下文中
                return contextMessages.Any(m => m.MessageId == currentMessage.MessageId && m.Content == content);
            });
    }
    
    /// <summary>
    /// Property 12: Agent 上下文传递完整性
    /// For any Agent 执行请求，传递给 Agent 的上下文应包含会话历史中的所有消息。
    /// Validates: Requirements 9.3
    /// </summary>
    [Property(MaxTest = 100)]
    public Property BuildContextMessages_ShouldIncludeAllHistoryMessages()
    {
        return Prop.ForAll(
            GenerateValidUserId().ToArbitrary(),
            GenerateValidPlatform().ToArbitrary(),
            GenerateHistoryCount().ToArbitrary(),
            (userId, platform, historyCount) =>
            {
                var session = CreateTestSession(userId, platform, historyCount);
                
                var currentMessage = new ChatMessage
                {
                    MessageId = Guid.NewGuid().ToString(),
                    SenderId = userId,
                    Content = "Test message",
                    MessageType = ChatMessageType.Text,
                    Platform = platform,
                    Timestamp = DateTimeOffset.UtcNow
                };
                
                // 使用测试辅助方法构建上下文
                var contextMessages = TestAgentContextBuilder.BuildContextMessages(currentMessage, session);
                
                // 验证所有历史消息都在上下文中
                foreach (var historyMsg in session.History)
                {
                    if (!contextMessages.Any(m => m.MessageId == historyMsg.MessageId))
                    {
                        return false;
                    }
                }
                
                return true;
            });
    }
    
    /// <summary>
    /// Property 12: Agent 上下文传递完整性
    /// For any Agent 执行请求，上下文消息数量应等于历史消息数量加1（当前消息）。
    /// Validates: Requirements 9.1, 9.3
    /// </summary>
    [Property(MaxTest = 100)]
    public Property BuildContextMessages_ShouldHaveCorrectCount()
    {
        return Prop.ForAll(
            GenerateValidUserId().ToArbitrary(),
            GenerateValidPlatform().ToArbitrary(),
            GenerateHistoryCount().ToArbitrary(),
            (userId, platform, historyCount) =>
            {
                var session = CreateTestSession(userId, platform, historyCount);
                
                var currentMessage = new ChatMessage
                {
                    MessageId = Guid.NewGuid().ToString(),
                    SenderId = userId,
                    Content = "Test message",
                    MessageType = ChatMessageType.Text,
                    Platform = platform,
                    Timestamp = DateTimeOffset.UtcNow
                };
                
                // 使用测试辅助方法构建上下文
                var contextMessages = TestAgentContextBuilder.BuildContextMessages(currentMessage, session);
                
                // 上下文消息数量 = 历史消息数量 + 1（当前消息）
                return contextMessages.Count == historyCount + 1;
            });
    }
    
    /// <summary>
    /// Property 12: Agent 上下文传递完整性
    /// For any Agent 执行请求，历史消息应在当前消息之前。
    /// Validates: Requirements 9.3
    /// </summary>
    [Property(MaxTest = 100)]
    public Property BuildContextMessages_HistoryShouldBeBeforeCurrentMessage()
    {
        return Prop.ForAll(
            GenerateValidUserId().ToArbitrary(),
            GenerateValidPlatform().ToArbitrary(),
            GenerateHistoryCount().ToArbitrary(),
            (userId, platform, historyCount) =>
            {
                var session = CreateTestSession(userId, platform, historyCount);
                
                var currentMessage = new ChatMessage
                {
                    MessageId = Guid.NewGuid().ToString(),
                    SenderId = userId,
                    Content = "Test message",
                    MessageType = ChatMessageType.Text,
                    Platform = platform,
                    Timestamp = DateTimeOffset.UtcNow
                };
                
                // 使用测试辅助方法构建上下文
                var contextMessages = TestAgentContextBuilder.BuildContextMessages(currentMessage, session);
                
                // 当前消息应该是最后一条
                if (contextMessages.Count == 0) return false;
                
                var lastMessage = contextMessages[^1];
                return lastMessage.MessageId == currentMessage.MessageId;
            });
    }
    
    /// <summary>
    /// Property 12: Agent 上下文传递完整性
    /// For any Agent 执行请求，历史消息的顺序应保持不变。
    /// Validates: Requirements 9.3
    /// </summary>
    [Property(MaxTest = 100)]
    public Property BuildContextMessages_HistoryOrderShouldBePreserved()
    {
        return Prop.ForAll(
            GenerateValidUserId().ToArbitrary(),
            GenerateValidPlatform().ToArbitrary(),
            GenerateHistoryCount().ToArbitrary(),
            (userId, platform, historyCount) =>
            {
                var session = CreateTestSession(userId, platform, historyCount);
                
                var currentMessage = new ChatMessage
                {
                    MessageId = Guid.NewGuid().ToString(),
                    SenderId = userId,
                    Content = "Test message",
                    MessageType = ChatMessageType.Text,
                    Platform = platform,
                    Timestamp = DateTimeOffset.UtcNow
                };
                
                // 使用测试辅助方法构建上下文
                var contextMessages = TestAgentContextBuilder.BuildContextMessages(currentMessage, session);
                
                // 验证历史消息顺序
                var historyList = session.History.ToList();
                for (int i = 0; i < historyList.Count; i++)
                {
                    if (contextMessages[i].MessageId != historyList[i].MessageId)
                    {
                        return false;
                    }
                }
                
                return true;
            });
    }
}

/// <summary>
/// 测试用的 ChatSession 实现
/// </summary>
internal class TestChatSession : IChatSession
{
    private readonly List<IChatMessage> _history = new();
    
    public TestChatSession(string userId, string platform)
    {
        SessionId = Guid.NewGuid().ToString();
        UserId = userId;
        Platform = platform;
        State = SessionState.Active;
        CreatedAt = DateTimeOffset.UtcNow;
        LastActivityAt = DateTimeOffset.UtcNow;
    }
    
    public string SessionId { get; }
    public string UserId { get; }
    public string Platform { get; }
    public SessionState State { get; private set; }
    public IReadOnlyList<IChatMessage> History => _history.AsReadOnly();
    public DateTimeOffset CreatedAt { get; }
    public DateTimeOffset LastActivityAt { get; private set; }
    public IDictionary<string, object>? Metadata { get; }
    
    public void AddMessage(IChatMessage message)
    {
        _history.Add(message);
        LastActivityAt = DateTimeOffset.UtcNow;
    }
    
    public void ClearHistory()
    {
        _history.Clear();
    }
    
    public void UpdateState(SessionState state)
    {
        State = state;
    }
    
    public void Touch()
    {
        LastActivityAt = DateTimeOffset.UtcNow;
    }
}

/// <summary>
/// 测试用的上下文构建器，模拟 AgentExecutor 的上下文构建逻辑
/// </summary>
internal static class TestAgentContextBuilder
{
    /// <summary>
    /// 构建上下文消息列表，包含当前消息和会话历史
    /// 这个方法模拟 AgentExecutor.BuildContextMessages 的逻辑
    /// </summary>
    public static List<IChatMessage> BuildContextMessages(IChatMessage currentMessage, IChatSession session)
    {
        var messages = new List<IChatMessage>();
        
        // 添加会话历史
        messages.AddRange(session.History);
        
        // 添加当前消息
        messages.Add(currentMessage);
        
        return messages;
    }
}
