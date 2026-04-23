using FsCheck;
using FsCheck.Fluent;
using FsCheck.Xunit;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using OpenDeepWiki.Chat.Abstractions;
using OpenDeepWiki.Chat.Providers;

namespace OpenDeepWiki.Tests.Chat.Providers;

/// <summary>
/// Property-based tests for message type degradation.
/// Feature: multi-platform-agent-chat, Property 3: 消息类型降级正确性
/// Validates: Requirements 1.5
/// </summary>
public class MessageDegradationPropertyTests
{
    private readonly TestMessageProvider _provider;
    
    public MessageDegradationPropertyTests()
    {
        var logger = LoggerFactory.Create(builder => builder.AddConsole())
            .CreateLogger<TestMessageProvider>();
        var options = Options.Create(new ProviderOptions());
        
        // 创建只支持 Text 类型的 Provider
        _provider = new TestMessageProvider(logger, options, new HashSet<ChatMessageType> { ChatMessageType.Text });
    }
    
    /// <summary>
    /// 生成非文本类型的消息类型
    /// </summary>
    private static Gen<ChatMessageType> GenerateNonTextMessageType()
    {
        return Gen.Elements(
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
    /// 生成有效的消息内容
    /// </summary>
    private static Gen<string> GenerateValidContent()
    {
        return Gen.Elements(
            "Hello, world!",
            "这是一条测试消息",
            "Test message with special chars: @#$%",
            "Multi\nline\nmessage",
            "Short",
            "A longer message that contains more text to test the degradation behavior"
        );
    }
    
    /// <summary>
    /// 生成有效的 ChatMessage
    /// </summary>
    private static Gen<ChatMessage> GenerateChatMessage(ChatMessageType messageType)
    {
        return GenerateValidContent().Select(content => new ChatMessage
        {
            MessageId = Guid.NewGuid().ToString(),
            SenderId = "user123",
            ReceiverId = "bot456",
            Content = content,
            MessageType = messageType,
            Platform = "test",
            Timestamp = DateTimeOffset.UtcNow,
            Metadata = new Dictionary<string, object> { { "key", "value" } }
        });
    }
    
    /// <summary>
    /// Property 3: 消息类型降级正确性
    /// For any 不被目标平台支持的消息类型，降级后的消息类型必须为 Text。
    /// Validates: Requirements 1.5
    /// </summary>
    [Property(MaxTest = 100)]
    public Property DegradedMessage_ShouldHaveTextType()
    {
        return Prop.ForAll(
            GenerateNonTextMessageType().SelectMany(GenerateChatMessage).ToArbitrary(),
            message =>
            {
                var degraded = _provider.TestDegradeMessage(message);
                return degraded.MessageType == ChatMessageType.Text;
            });
    }
    
    /// <summary>
    /// Property 3: 消息类型降级正确性
    /// For any 不被目标平台支持的消息类型，原始内容信息应被保留在降级后的 Content 中。
    /// Validates: Requirements 1.5
    /// </summary>
    [Property(MaxTest = 100)]
    public Property DegradedMessage_ShouldPreserveOriginalContent()
    {
        return Prop.ForAll(
            GenerateNonTextMessageType().SelectMany(GenerateChatMessage).ToArbitrary(),
            message =>
            {
                var degraded = _provider.TestDegradeMessage(message);
                // 降级后的内容应包含原始内容
                return degraded.Content.Contains(message.Content);
            });
    }
    
    /// <summary>
    /// Property 3: 消息类型降级正确性
    /// For any 不被目标平台支持的消息类型，降级后的 Content 应包含原始消息类型标识。
    /// Validates: Requirements 1.5
    /// </summary>
    [Property(MaxTest = 100)]
    public Property DegradedMessage_ShouldContainOriginalTypeIndicator()
    {
        return Prop.ForAll(
            GenerateNonTextMessageType().SelectMany(GenerateChatMessage).ToArbitrary(),
            message =>
            {
                var degraded = _provider.TestDegradeMessage(message);
                // 降级后的内容应包含原始类型标识
                var typeIndicator = $"[{message.MessageType}]";
                return degraded.Content.Contains(typeIndicator);
            });
    }
    
    /// <summary>
    /// Property 3: 消息类型降级正确性
    /// For any 文本类型消息，不应进行降级处理。
    /// Validates: Requirements 1.5
    /// </summary>
    [Property(MaxTest = 100)]
    public Property TextMessage_ShouldNotBeModified()
    {
        return Prop.ForAll(
            GenerateChatMessage(ChatMessageType.Text).ToArbitrary(),
            message =>
            {
                var result = _provider.TestDegradeMessage(message);
                // 文本消息应保持不变
                return result.MessageId == message.MessageId &&
                       result.Content == message.Content &&
                       result.MessageType == ChatMessageType.Text;
            });
    }
    
    /// <summary>
    /// Property 3: 消息类型降级正确性
    /// For any 降级后的消息，关键字段（MessageId、SenderId、Platform、Timestamp）应保持不变。
    /// Validates: Requirements 1.5
    /// </summary>
    [Property(MaxTest = 100)]
    public Property DegradedMessage_ShouldPreserveKeyFields()
    {
        return Prop.ForAll(
            GenerateNonTextMessageType().SelectMany(GenerateChatMessage).ToArbitrary(),
            message =>
            {
                var degraded = _provider.TestDegradeMessage(message);
                return degraded.MessageId == message.MessageId &&
                       degraded.SenderId == message.SenderId &&
                       degraded.ReceiverId == message.ReceiverId &&
                       degraded.Platform == message.Platform &&
                       degraded.Timestamp == message.Timestamp;
            });
    }
}
