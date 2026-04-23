using FsCheck;
using FsCheck.Fluent;
using FsCheck.Xunit;
using OpenDeepWiki.Chat.Abstractions;
using Xunit;

namespace OpenDeepWiki.Tests.Chat.Abstractions;

/// <summary>
/// Property-based tests for ChatMessage field completeness.
/// Feature: multi-platform-agent-chat, Property 1: 消息字段完整性
/// Validates: Requirements 1.1
/// </summary>
public class ChatMessagePropertyTests
{
    /// <summary>
    /// 生成有效的发送者ID
    /// </summary>
    private static Gen<string> GenerateValidSenderId()
    {
        return Gen.Elements(
            "user123",
            "sender_456",
            "platform_user_789",
            "test-user",
            "U12345678"
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
            "Multi\nline\nmessage",
            "Short"
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
            "custom_platform"
        );
    }
    
    /// <summary>
    /// 生成有效的消息类型
    /// </summary>
    private static Gen<ChatMessageType> GenerateMessageType()
    {
        return Gen.Elements(
            ChatMessageType.Text,
            ChatMessageType.Image,
            ChatMessageType.File,
            ChatMessageType.Audio,
            ChatMessageType.Video,
            ChatMessageType.RichText,
            ChatMessageType.Card,
            ChatMessageType.Unknown
        );
    }
    
    /// <summary>
    /// 生成完整的 ChatMessage
    /// </summary>
    private static Gen<ChatMessage> GenerateCompleteChatMessage()
    {
        return from senderId in GenerateValidSenderId()
               from content in GenerateValidContent()
               from platform in GenerateValidPlatform()
               from messageType in GenerateMessageType()
               select new ChatMessage
               {
                   MessageId = Guid.NewGuid().ToString(),
                   SenderId = senderId,
                   Content = content,
                   MessageType = messageType,
                   Platform = platform,
                   Timestamp = DateTimeOffset.UtcNow
               };
    }
    
    /// <summary>
    /// Property 1: 消息字段完整性
    /// For any ChatMessage 对象，MessageId 必须存在且非空。
    /// Validates: Requirements 1.1
    /// </summary>
    [Property(MaxTest = 100)]
    public Property ChatMessage_MessageId_ShouldBeNonEmpty()
    {
        return Prop.ForAll(
            GenerateCompleteChatMessage().ToArbitrary(),
            message => !string.IsNullOrEmpty(message.MessageId));
    }
    
    /// <summary>
    /// Property 1: 消息字段完整性
    /// For any ChatMessage 对象，SenderId 必须存在且非空。
    /// Validates: Requirements 1.1
    /// </summary>
    [Property(MaxTest = 100)]
    public Property ChatMessage_SenderId_ShouldBeNonEmpty()
    {
        return Prop.ForAll(
            GenerateCompleteChatMessage().ToArbitrary(),
            message => !string.IsNullOrEmpty(message.SenderId));
    }
    
    /// <summary>
    /// Property 1: 消息字段完整性
    /// For any ChatMessage 对象，Content 必须存在（可以为空字符串但不能为 null）。
    /// Validates: Requirements 1.1
    /// </summary>
    [Property(MaxTest = 100)]
    public Property ChatMessage_Content_ShouldNotBeNull()
    {
        return Prop.ForAll(
            GenerateCompleteChatMessage().ToArbitrary(),
            message => message.Content != null);
    }
    
    /// <summary>
    /// Property 1: 消息字段完整性
    /// For any ChatMessage 对象，MessageType 必须是有效的枚举值。
    /// Validates: Requirements 1.1
    /// </summary>
    [Property(MaxTest = 100)]
    public Property ChatMessage_MessageType_ShouldBeValidEnum()
    {
        return Prop.ForAll(
            GenerateCompleteChatMessage().ToArbitrary(),
            message => Enum.IsDefined(typeof(ChatMessageType), message.MessageType));
    }
    
    /// <summary>
    /// Property 1: 消息字段完整性
    /// For any ChatMessage 对象，Platform 必须存在且非空。
    /// Validates: Requirements 1.1
    /// </summary>
    [Property(MaxTest = 100)]
    public Property ChatMessage_Platform_ShouldBeNonEmpty()
    {
        return Prop.ForAll(
            GenerateCompleteChatMessage().ToArbitrary(),
            message => !string.IsNullOrEmpty(message.Platform));
    }
    
    /// <summary>
    /// Property 1: 消息字段完整性
    /// For any ChatMessage 对象，Timestamp 必须是有效的时间戳。
    /// Validates: Requirements 1.1
    /// </summary>
    [Property(MaxTest = 100)]
    public Property ChatMessage_Timestamp_ShouldBeValid()
    {
        return Prop.ForAll(
            GenerateCompleteChatMessage().ToArbitrary(),
            message => message.Timestamp != default(DateTimeOffset));
    }
    
    /// <summary>
    /// Property 1: 消息字段完整性
    /// For any 默认构造的 ChatMessage，必需字段应有合理的默认值。
    /// Validates: Requirements 1.1
    /// </summary>
    [Fact]
    public void DefaultChatMessage_ShouldHaveValidDefaults()
    {
        var message = new ChatMessage();
        
        // MessageId 应自动生成
        Assert.False(string.IsNullOrEmpty(message.MessageId));
        
        // SenderId 默认为空字符串（非 null）
        Assert.NotNull(message.SenderId);
        
        // Content 默认为空字符串（非 null）
        Assert.NotNull(message.Content);
        
        // MessageType 默认为 Text
        Assert.Equal(ChatMessageType.Text, message.MessageType);
        
        // Platform 默认为空字符串（非 null）
        Assert.NotNull(message.Platform);
        
        // Timestamp 应自动设置为当前时间
        Assert.NotEqual(default(DateTimeOffset), message.Timestamp);
    }
    
    /// <summary>
    /// Property 1: 消息字段完整性
    /// For any ChatMessage，实现 IChatMessage 接口的所有属性。
    /// Validates: Requirements 1.1
    /// </summary>
    [Property(MaxTest = 100)]
    public Property ChatMessage_ShouldImplementIChatMessage()
    {
        return Prop.ForAll(
            GenerateCompleteChatMessage().ToArbitrary(),
            message =>
            {
                IChatMessage iMessage = message;
                return iMessage.MessageId == message.MessageId &&
                       iMessage.SenderId == message.SenderId &&
                       iMessage.Content == message.Content &&
                       iMessage.MessageType == message.MessageType &&
                       iMessage.Platform == message.Platform &&
                       iMessage.Timestamp == message.Timestamp;
            });
    }
}
