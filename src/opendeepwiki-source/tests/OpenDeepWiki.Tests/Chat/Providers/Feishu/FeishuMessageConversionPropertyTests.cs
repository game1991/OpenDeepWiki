using System.Text.Json;
using FsCheck;
using FsCheck.Fluent;
using FsCheck.Xunit;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using OpenDeepWiki.Chat.Abstractions;
using OpenDeepWiki.Chat.Providers.Feishu;

namespace OpenDeepWiki.Tests.Chat.Providers.Feishu;

/// <summary>
/// Property-based tests for Feishu message conversion round-trip consistency.
/// Feature: multi-platform-agent-chat, Property 2: 消息转换往返一致性（飞书）
/// Validates: Requirements 1.2, 1.3
/// </summary>
public class FeishuMessageConversionPropertyTests
{
    private readonly FeishuProvider _provider;
    
    public FeishuMessageConversionPropertyTests()
    {
        var logger = LoggerFactory.Create(builder => builder.AddConsole())
            .CreateLogger<FeishuProvider>();
        var options = Options.Create(new FeishuProviderOptions
        {
            AppId = "test_app_id",
            AppSecret = "test_app_secret",
            VerificationToken = "test_token"
        });
        var httpClient = new HttpClient();
        
        _provider = new FeishuProvider(logger, options, httpClient);
    }
    
    /// <summary>
    /// 生成有效的文本内容（避免特殊字符导致 JSON 解析问题）
    /// </summary>
    private static Gen<string> GenerateValidTextContent()
    {
        return Gen.Elements(
            "Hello, world!",
            "这是一条测试消息",
            "Test message 123",
            "多行消息\n第二行\n第三行",
            "Short",
            "A longer message that contains more text to test the conversion behavior",
            "消息包含数字 12345 和英文 ABC",
            "Special chars: @#$%^&*()"
        );
    }
    
    /// <summary>
    /// 生成有效的 SenderId
    /// </summary>
    private static Gen<string> GenerateSenderId()
    {
        return Gen.Elements(
            "ou_abc123def456",
            "ou_user001",
            "ou_test_user_id",
            "ou_12345678"
        );
    }
    
    /// <summary>
    /// 生成有效的 ChatId
    /// </summary>
    private static Gen<string> GenerateChatId()
    {
        return Gen.Elements(
            "oc_abc123def456",
            "oc_group001",
            "oc_test_chat_id",
            "oc_12345678"
        );
    }
    
    /// <summary>
    /// 生成文本类型的 ChatMessage
    /// </summary>
    private static Gen<ChatMessage> GenerateTextChatMessage()
    {
        return from content in GenerateValidTextContent()
               from senderId in GenerateSenderId()
               from chatId in GenerateChatId()
               select new ChatMessage
               {
                   MessageId = Guid.NewGuid().ToString(),
                   SenderId = senderId,
                   ReceiverId = chatId,
                   Content = content,
                   MessageType = ChatMessageType.Text,
                   Platform = "feishu",
                   Timestamp = DateTimeOffset.UtcNow
               };
    }
    
    /// <summary>
    /// 生成飞书原始消息事件 JSON
    /// </summary>
    private static string CreateFeishuWebhookEvent(string senderId, string chatId, string messageType, string content)
    {
        var timestamp = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds().ToString();
        var messageId = $"om_{Guid.NewGuid():N}";
        
        return JsonSerializer.Serialize(new
        {
            schema = "2.0",
            header = new
            {
                event_id = $"ev_{Guid.NewGuid():N}",
                event_type = "im.message.receive_v1",
                create_time = timestamp,
                token = "test_token",
                app_id = "test_app_id",
                tenant_key = "test_tenant"
            },
            @event = new
            {
                sender = new
                {
                    sender_id = new
                    {
                        union_id = $"on_{senderId}",
                        user_id = senderId,
                        open_id = senderId
                    },
                    sender_type = "user",
                    tenant_key = "test_tenant"
                },
                message = new
                {
                    message_id = messageId,
                    root_id = (string?)null,
                    parent_id = (string?)null,
                    create_time = timestamp,
                    chat_id = chatId,
                    chat_type = "p2p",
                    message_type = messageType,
                    content = content
                }
            }
        });
    }
    
    /// <summary>
    /// Property 2: 消息转换往返一致性（飞书）
    /// For any 有效的文本 ChatMessage，转换为飞书格式后再解析回来，Content 应保持一致。
    /// Validates: Requirements 1.2, 1.3
    /// </summary>
    [Property(MaxTest = 100)]
    public Property TextMessage_RoundTrip_ShouldPreserveContent()
    {
        return Prop.ForAll(
            GenerateTextChatMessage().ToArbitrary(),
            message =>
            {
                // 1. 将 ChatMessage 转换为飞书格式
                var (msgType, feishuContent) = _provider.ConvertToFeishuFormat(message);
                
                // 2. 创建模拟的飞书 Webhook 事件
                var webhookJson = CreateFeishuWebhookEvent(
                    message.SenderId,
                    message.ReceiverId ?? "oc_default",
                    msgType,
                    feishuContent);
                
                // 3. 解析回 ChatMessage
                var parsedMessage = _provider.ParseMessageAsync(webhookJson).Result;
                
                // 4. 验证内容一致性
                return parsedMessage != null && parsedMessage.Content == message.Content;
            });
    }
    
    /// <summary>
    /// Property 2: 消息转换往返一致性（飞书）
    /// For any 有效的文本 ChatMessage，转换为飞书格式后再解析回来，MessageType 应保持一致。
    /// Validates: Requirements 1.2, 1.3
    /// </summary>
    [Property(MaxTest = 100)]
    public Property TextMessage_RoundTrip_ShouldPreserveMessageType()
    {
        return Prop.ForAll(
            GenerateTextChatMessage().ToArbitrary(),
            message =>
            {
                var (msgType, feishuContent) = _provider.ConvertToFeishuFormat(message);
                var webhookJson = CreateFeishuWebhookEvent(
                    message.SenderId,
                    message.ReceiverId ?? "oc_default",
                    msgType,
                    feishuContent);
                
                var parsedMessage = _provider.ParseMessageAsync(webhookJson).Result;
                
                return parsedMessage != null && parsedMessage.MessageType == message.MessageType;
            });
    }
    
    /// <summary>
    /// Property 2: 消息转换往返一致性（飞书）
    /// For any 有效的文本 ChatMessage，转换为飞书格式后再解析回来，SenderId 应保持一致。
    /// Validates: Requirements 1.2, 1.3
    /// </summary>
    [Property(MaxTest = 100)]
    public Property TextMessage_RoundTrip_ShouldPreserveSenderId()
    {
        return Prop.ForAll(
            GenerateTextChatMessage().ToArbitrary(),
            message =>
            {
                var (msgType, feishuContent) = _provider.ConvertToFeishuFormat(message);
                var webhookJson = CreateFeishuWebhookEvent(
                    message.SenderId,
                    message.ReceiverId ?? "oc_default",
                    msgType,
                    feishuContent);
                
                var parsedMessage = _provider.ParseMessageAsync(webhookJson).Result;
                
                return parsedMessage != null && parsedMessage.SenderId == message.SenderId;
            });
    }
    
    /// <summary>
    /// Property 2: 消息转换往返一致性（飞书）
    /// For any 文本内容，转换为飞书文本格式应生成有效的 JSON。
    /// Validates: Requirements 1.2
    /// </summary>
    [Property(MaxTest = 100)]
    public Property ConvertToFeishuFormat_TextMessage_ShouldProduceValidJson()
    {
        return Prop.ForAll(
            GenerateTextChatMessage().ToArbitrary(),
            message =>
            {
                var (msgType, content) = _provider.ConvertToFeishuFormat(message);
                
                // 验证消息类型
                if (msgType != "text")
                    return false;
                
                // 验证 JSON 格式有效
                try
                {
                    var parsed = JsonSerializer.Deserialize<FeishuTextContent>(content);
                    return parsed != null && parsed.Text == message.Content;
                }
                catch
                {
                    return false;
                }
            });
    }
    
    /// <summary>
    /// Property 2: 消息转换往返一致性（飞书）
    /// For any 有效的飞书文本消息 JSON，解析后应生成有效的 ChatMessage。
    /// Validates: Requirements 1.2
    /// </summary>
    [Property(MaxTest = 100)]
    public Property ParseMessage_ValidTextEvent_ShouldProduceValidChatMessage()
    {
        return Prop.ForAll(
            GenerateValidTextContent().ToArbitrary(),
            GenerateSenderId().ToArbitrary(),
            GenerateChatId().ToArbitrary(),
            (content, senderId, chatId) =>
            {
                var textJson = JsonSerializer.Serialize(new { text = content });
                var webhookJson = CreateFeishuWebhookEvent(senderId, chatId, "text", textJson);
                
                var parsedMessage = _provider.ParseMessageAsync(webhookJson).Result;
                
                return parsedMessage != null &&
                       !string.IsNullOrEmpty(parsedMessage.MessageId) &&
                       parsedMessage.SenderId == senderId &&
                       parsedMessage.Content == content &&
                       parsedMessage.MessageType == ChatMessageType.Text &&
                       parsedMessage.Platform == "feishu";
            });
    }
}
