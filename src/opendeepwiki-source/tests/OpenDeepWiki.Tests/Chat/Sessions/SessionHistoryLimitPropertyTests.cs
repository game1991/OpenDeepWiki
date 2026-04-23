using FsCheck;
using FsCheck.Fluent;
using FsCheck.Xunit;
using OpenDeepWiki.Chat.Abstractions;
using OpenDeepWiki.Chat.Sessions;
using Xunit;

namespace OpenDeepWiki.Tests.Chat.Sessions;

/// <summary>
/// Property-based tests for session history limit.
/// Feature: multi-platform-agent-chat, Property 7: 会话历史限制
/// Validates: Requirements 6.3
/// </summary>
public class SessionHistoryLimitPropertyTests
{
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
    /// 生成有效的发送者ID
    /// </summary>
    private static Gen<string> GenerateValidSenderId()
    {
        return Gen.Elements(
            "user123",
            "sender_456",
            "platform_user_789"
        );
    }
    
    /// <summary>
    /// 生成 ChatMessage
    /// </summary>
    private static Gen<ChatMessage> GenerateChatMessage()
    {
        return from content in GenerateValidContent()
               from senderId in GenerateValidSenderId()
               select new ChatMessage
               {
                   MessageId = Guid.NewGuid().ToString(),
                   SenderId = senderId,
                   Content = content,
                   MessageType = ChatMessageType.Text,
                   Platform = "test",
                   Timestamp = DateTimeOffset.UtcNow
               };
    }
    
    /// <summary>
    /// 生成消息列表
    /// </summary>
    private static Gen<List<ChatMessage>> GenerateMessageList(int minCount, int maxCount)
    {
        return Gen.Choose(minCount, maxCount)
            .SelectMany(count => Gen.ListOf(GenerateChatMessage()).Select(l => l.Take(count).ToList()));
    }

    /// <summary>
    /// Property 7: 会话历史限制
    /// For any 配置了最大历史消息数量 N 的会话，添加消息后历史记录数量不应超过 N。
    /// Validates: Requirements 6.3
    /// </summary>
    [Property(MaxTest = 100)]
    public Property SessionHistory_ShouldNotExceedMaxLimit()
    {
        return Prop.ForAll(
            Gen.Choose(5, 20).ToArbitrary(),  // maxHistoryCount
            Gen.Choose(1, 50).ToArbitrary(),  // messageCount to add
            (maxHistoryCount, messageCount) =>
            {
                var session = new ChatSessionImpl(
                    Guid.NewGuid().ToString(),
                    "user123",
                    "test")
                {
                    MaxHistoryCount = maxHistoryCount
                };
                
                // 添加消息
                for (int i = 0; i < messageCount; i++)
                {
                    var message = new ChatMessage
                    {
                        MessageId = Guid.NewGuid().ToString(),
                        SenderId = "user123",
                        Content = $"Message {i}",
                        MessageType = ChatMessageType.Text,
                        Platform = "test",
                        Timestamp = DateTimeOffset.UtcNow
                    };
                    session.AddMessage(message);
                }
                
                // 历史记录数量不应超过最大限制
                return session.History.Count <= maxHistoryCount;
            });
    }
    
    /// <summary>
    /// Property 7: 会话历史限制
    /// For any 会话，当消息数量未超过限制时，所有消息都应保留。
    /// Validates: Requirements 6.3
    /// </summary>
    [Property(MaxTest = 100)]
    public Property SessionHistory_ShouldKeepAllMessages_WhenUnderLimit()
    {
        return Prop.ForAll(
            Gen.Choose(10, 50).ToArbitrary(),  // maxHistoryCount
            Gen.Choose(1, 9).ToArbitrary(),    // messageCount (less than min maxHistoryCount)
            (maxHistoryCount, messageCount) =>
            {
                var session = new ChatSessionImpl(
                    Guid.NewGuid().ToString(),
                    "user123",
                    "test")
                {
                    MaxHistoryCount = maxHistoryCount
                };
                
                // 添加消息
                for (int i = 0; i < messageCount; i++)
                {
                    var message = new ChatMessage
                    {
                        MessageId = Guid.NewGuid().ToString(),
                        SenderId = "user123",
                        Content = $"Message {i}",
                        MessageType = ChatMessageType.Text,
                        Platform = "test",
                        Timestamp = DateTimeOffset.UtcNow
                    };
                    session.AddMessage(message);
                }
                
                // 当消息数量未超过限制时，所有消息都应保留
                return session.History.Count == messageCount;
            });
    }

    /// <summary>
    /// Property 7: 会话历史限制
    /// For any 会话，当超过限制时，应保留最新的消息。
    /// Validates: Requirements 6.3
    /// </summary>
    [Property(MaxTest = 100)]
    public Property SessionHistory_ShouldKeepNewestMessages_WhenOverLimit()
    {
        return Prop.ForAll(
            Gen.Choose(5, 15).ToArbitrary(),   // maxHistoryCount
            Gen.Choose(20, 40).ToArbitrary(),  // messageCount (more than maxHistoryCount)
            (maxHistoryCount, messageCount) =>
            {
                var session = new ChatSessionImpl(
                    Guid.NewGuid().ToString(),
                    "user123",
                    "test")
                {
                    MaxHistoryCount = maxHistoryCount
                };
                
                var messageIds = new List<string>();
                
                // 添加消息
                for (int i = 0; i < messageCount; i++)
                {
                    var messageId = Guid.NewGuid().ToString();
                    messageIds.Add(messageId);
                    
                    var message = new ChatMessage
                    {
                        MessageId = messageId,
                        SenderId = "user123",
                        Content = $"Message {i}",
                        MessageType = ChatMessageType.Text,
                        Platform = "test",
                        Timestamp = DateTimeOffset.UtcNow
                    };
                    session.AddMessage(message);
                }
                
                // 应保留最新的消息
                var expectedMessageIds = messageIds.Skip(messageCount - maxHistoryCount).ToList();
                var actualMessageIds = session.History.Select(m => m.MessageId).ToList();
                
                return expectedMessageIds.SequenceEqual(actualMessageIds);
            });
    }
    
    /// <summary>
    /// Property 7: 会话历史限制
    /// For any 会话，清空历史后历史记录应为空。
    /// Validates: Requirements 6.3
    /// </summary>
    [Property(MaxTest = 100)]
    public Property SessionHistory_ShouldBeEmpty_AfterClear()
    {
        return Prop.ForAll(
            Gen.Choose(1, 20).ToArbitrary(),  // messageCount
            messageCount =>
            {
                var session = new ChatSessionImpl(
                    Guid.NewGuid().ToString(),
                    "user123",
                    "test");
                
                // 添加消息
                for (int i = 0; i < messageCount; i++)
                {
                    var message = new ChatMessage
                    {
                        MessageId = Guid.NewGuid().ToString(),
                        SenderId = "user123",
                        Content = $"Message {i}",
                        MessageType = ChatMessageType.Text,
                        Platform = "test",
                        Timestamp = DateTimeOffset.UtcNow
                    };
                    session.AddMessage(message);
                }
                
                // 清空历史
                session.ClearHistory();
                
                // 历史记录应为空
                return session.History.Count == 0;
            });
    }
    
    /// <summary>
    /// Property 7: 会话历史限制
    /// For any 会话，添加消息后最后活动时间应更新。
    /// Validates: Requirements 6.3
    /// </summary>
    [Fact]
    public void SessionHistory_AddMessage_ShouldUpdateLastActivityAt()
    {
        var session = new ChatSessionImpl(
            Guid.NewGuid().ToString(),
            "user123",
            "test");
        
        var initialLastActivityAt = session.LastActivityAt;
        
        // 等待一小段时间
        Thread.Sleep(10);
        
        var message = new ChatMessage
        {
            MessageId = Guid.NewGuid().ToString(),
            SenderId = "user123",
            Content = "Test message",
            MessageType = ChatMessageType.Text,
            Platform = "test",
            Timestamp = DateTimeOffset.UtcNow
        };
        session.AddMessage(message);
        
        // 最后活动时间应更新
        Assert.True(session.LastActivityAt > initialLastActivityAt);
    }
}
