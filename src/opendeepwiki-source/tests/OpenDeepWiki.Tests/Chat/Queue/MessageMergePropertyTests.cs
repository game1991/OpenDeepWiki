using FsCheck;
using FsCheck.Fluent;
using FsCheck.Xunit;
using Microsoft.Extensions.Options;
using OpenDeepWiki.Chat.Abstractions;
using OpenDeepWiki.Chat.Queue;
using Xunit;

namespace OpenDeepWiki.Tests.Chat.Queue;

/// <summary>
/// Property-based tests for message merge correctness.
/// Feature: multi-platform-agent-chat, Property 11: 消息合并正确性
/// Validates: Requirements 8.4
/// </summary>
public class MessageMergePropertyTests
{
    /// <summary>
    /// 生成短消息内容（长度在1-50之间）
    /// </summary>
    private static Gen<string> GenerateShortContent()
    {
        return Gen.Elements(
            "Hi",
            "Hello",
            "OK",
            "Yes",
            "No",
            "Thanks",
            "Got it",
            "Sure",
            "好的",
            "收到"
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
        return Gen.Elements("feishu", "qq", "wechat");
    }
    
    /// <summary>
    /// 生成消息数量（2-10）
    /// </summary>
    private static Gen<int> GenerateMessageCount()
    {
        return Gen.Choose(2, 10);
    }

    /// <summary>
    /// 创建测试用的 TextMessageMerger
    /// </summary>
    private static TextMessageMerger CreateMerger(int mergeThreshold = 500, int mergeWindowMs = 2000)
    {
        var options = Options.Create(new MessageQueueOptions
        {
            MergeThreshold = mergeThreshold,
            MergeWindowMs = mergeWindowMs
        });
        
        return new TextMessageMerger(options);
    }
    
    /// <summary>
    /// 创建测试消息
    /// </summary>
    private static IChatMessage CreateMessage(string content, string senderId, string platform, DateTimeOffset timestamp)
    {
        return new ChatMessage
        {
            MessageId = Guid.NewGuid().ToString(),
            SenderId = senderId,
            Content = content,
            MessageType = ChatMessageType.Text,
            Platform = platform,
            Timestamp = timestamp
        };
    }

    /// <summary>
    /// Property 11: 消息合并正确性
    /// For any 多条连续的短文本消息，合并后的消息内容应包含所有原始消息的内容。
    /// Validates: Requirements 8.4
    /// </summary>
    [Property(MaxTest = 100)]
    public Property MergedMessage_ShouldContainAllOriginalContent()
    {
        return Prop.ForAll(
            GenerateMessageCount().ToArbitrary(),
            GenerateValidUserId().ToArbitrary(),
            GenerateValidPlatform().ToArbitrary(),
            (count, userId, platform) =>
            {
                var merger = CreateMerger();
                var baseTime = DateTimeOffset.UtcNow;
                
                // 创建消息列表
                var messages = new List<IChatMessage>();
                var contents = new List<string>();
                for (var i = 0; i < count; i++)
                {
                    var content = $"Msg{i}";
                    contents.Add(content);
                    messages.Add(CreateMessage(content, userId, platform, baseTime.AddMilliseconds(i * 100)));
                }
                
                // 尝试合并
                var result = merger.TryMerge(messages);
                
                if (!result.WasMerged)
                    return true; // 如果没有合并，跳过验证
                
                // 验证合并后的内容包含所有原始内容
                var mergedContent = result.Messages[0].Content;
                return contents.All(c => mergedContent.Contains(c));
            });
    }

    /// <summary>
    /// Property 11: 消息合并正确性
    /// For any 多条连续的短文本消息，合并后的消息内容顺序应与原始消息顺序一致。
    /// Validates: Requirements 8.4
    /// </summary>
    [Property(MaxTest = 100)]
    public Property MergedMessage_ShouldPreserveContentOrder()
    {
        return Prop.ForAll(
            GenerateMessageCount().ToArbitrary(),
            GenerateValidUserId().ToArbitrary(),
            GenerateValidPlatform().ToArbitrary(),
            (count, userId, platform) =>
            {
                var merger = CreateMerger();
                var baseTime = DateTimeOffset.UtcNow;
                
                // 创建消息列表
                var messages = new List<IChatMessage>();
                for (var i = 0; i < count; i++)
                {
                    messages.Add(CreateMessage($"Msg{i}", userId, platform, baseTime.AddMilliseconds(i * 100)));
                }
                
                // 尝试合并
                var result = merger.TryMerge(messages);
                
                if (!result.WasMerged)
                    return true; // 如果没有合并，跳过验证
                
                // 验证顺序：每个消息内容在合并结果中的位置应该是递增的
                var mergedContent = result.Messages[0].Content;
                var lastIndex = -1;
                for (var i = 0; i < count; i++)
                {
                    var currentIndex = mergedContent.IndexOf($"Msg{i}", StringComparison.Ordinal);
                    if (currentIndex <= lastIndex)
                        return false;
                    lastIndex = currentIndex;
                }
                
                return true;
            });
    }
    
    /// <summary>
    /// Property 11: 消息合并正确性
    /// For any 来自不同发送者的消息，不应被合并。
    /// Validates: Requirements 8.4
    /// </summary>
    [Property(MaxTest = 100)]
    public Property MessagesFromDifferentSenders_ShouldNotMerge()
    {
        return Prop.ForAll(
            GenerateValidUserId().ToArbitrary(),
            GenerateValidUserId().ToArbitrary(),
            GenerateValidPlatform().ToArbitrary(),
            (userId1, userId2, platform) =>
            {
                // 跳过相同用户的情况
                if (userId1 == userId2) return true;
                
                var merger = CreateMerger();
                var baseTime = DateTimeOffset.UtcNow;
                
                var messages = new List<IChatMessage>
                {
                    CreateMessage("Msg1", userId1, platform, baseTime),
                    CreateMessage("Msg2", userId2, platform, baseTime.AddMilliseconds(100))
                };
                
                // 不应该能够合并
                return !merger.CanMerge(messages);
            });
    }

    /// <summary>
    /// Property 11: 消息合并正确性
    /// For any 来自不同平台的消息，不应被合并。
    /// Validates: Requirements 8.4
    /// </summary>
    [Property(MaxTest = 100)]
    public Property MessagesFromDifferentPlatforms_ShouldNotMerge()
    {
        return Prop.ForAll(
            GenerateValidUserId().ToArbitrary(),
            (userId) =>
            {
                var merger = CreateMerger();
                var baseTime = DateTimeOffset.UtcNow;
                
                var messages = new List<IChatMessage>
                {
                    CreateMessage("Msg1", userId, "feishu", baseTime),
                    CreateMessage("Msg2", userId, "qq", baseTime.AddMilliseconds(100))
                };
                
                // 不应该能够合并
                return !merger.CanMerge(messages);
            });
    }
    
    /// <summary>
    /// Property 11: 消息合并正确性
    /// For any 非文本类型的消息，不应被合并。
    /// Validates: Requirements 8.4
    /// </summary>
    [Property(MaxTest = 100)]
    public Property NonTextMessages_ShouldNotMerge()
    {
        return Prop.ForAll(
            GenerateValidUserId().ToArbitrary(),
            GenerateValidPlatform().ToArbitrary(),
            (userId, platform) =>
            {
                var merger = CreateMerger();
                var baseTime = DateTimeOffset.UtcNow;
                
                var messages = new List<IChatMessage>
                {
                    CreateMessage("Msg1", userId, platform, baseTime),
                    new ChatMessage
                    {
                        MessageId = Guid.NewGuid().ToString(),
                        SenderId = userId,
                        Content = "image.png",
                        MessageType = ChatMessageType.Image, // 非文本类型
                        Platform = platform,
                        Timestamp = baseTime.AddMilliseconds(100)
                    }
                };
                
                // 不应该能够合并
                return !merger.CanMerge(messages);
            });
    }
    
    /// <summary>
    /// Property 11: 消息合并正确性
    /// For any 超过合并阈值的消息，不应被合并。
    /// Validates: Requirements 8.4
    /// </summary>
    [Property(MaxTest = 100)]
    public Property MessagesExceedingThreshold_ShouldNotMerge()
    {
        return Prop.ForAll(
            GenerateValidUserId().ToArbitrary(),
            GenerateValidPlatform().ToArbitrary(),
            (userId, platform) =>
            {
                var merger = CreateMerger(mergeThreshold: 20); // 设置较小的阈值
                var baseTime = DateTimeOffset.UtcNow;
                
                // 创建总长度超过阈值的消息
                var messages = new List<IChatMessage>
                {
                    CreateMessage("This is a long message", userId, platform, baseTime),
                    CreateMessage("Another long message here", userId, platform, baseTime.AddMilliseconds(100))
                };
                
                // 不应该能够合并
                return !merger.CanMerge(messages);
            });
    }

    /// <summary>
    /// Property 11: 消息合并正确性
    /// For any 超过时间窗口的消息，不应被合并。
    /// Validates: Requirements 8.4
    /// </summary>
    [Property(MaxTest = 100)]
    public Property MessagesExceedingTimeWindow_ShouldNotMerge()
    {
        return Prop.ForAll(
            GenerateValidUserId().ToArbitrary(),
            GenerateValidPlatform().ToArbitrary(),
            (userId, platform) =>
            {
                var merger = CreateMerger(mergeWindowMs: 1000); // 1秒时间窗口
                var baseTime = DateTimeOffset.UtcNow;
                
                // 创建时间间隔超过窗口的消息
                var messages = new List<IChatMessage>
                {
                    CreateMessage("Msg1", userId, platform, baseTime),
                    CreateMessage("Msg2", userId, platform, baseTime.AddMilliseconds(2000)) // 超过1秒
                };
                
                // 不应该能够合并
                return !merger.CanMerge(messages);
            });
    }
    
    /// <summary>
    /// Property 11: 消息合并正确性
    /// For any 单条消息，不应被合并。
    /// Validates: Requirements 8.4
    /// </summary>
    [Property(MaxTest = 100)]
    public Property SingleMessage_ShouldNotMerge()
    {
        return Prop.ForAll(
            GenerateValidUserId().ToArbitrary(),
            GenerateValidPlatform().ToArbitrary(),
            (userId, platform) =>
            {
                var merger = CreateMerger();
                var baseTime = DateTimeOffset.UtcNow;
                
                var messages = new List<IChatMessage>
                {
                    CreateMessage("Single message", userId, platform, baseTime)
                };
                
                // 单条消息不应该能够合并
                return !merger.CanMerge(messages);
            });
    }
    
    /// <summary>
    /// Property 11: 消息合并正确性
    /// For any 合并后的消息，应保留发送者和平台信息。
    /// Validates: Requirements 8.4
    /// </summary>
    [Property(MaxTest = 100)]
    public Property MergedMessage_ShouldPreserveSenderAndPlatform()
    {
        return Prop.ForAll(
            GenerateMessageCount().ToArbitrary(),
            GenerateValidUserId().ToArbitrary(),
            GenerateValidPlatform().ToArbitrary(),
            (count, userId, platform) =>
            {
                var merger = CreateMerger();
                var baseTime = DateTimeOffset.UtcNow;
                
                // 创建消息列表
                var messages = new List<IChatMessage>();
                for (var i = 0; i < count; i++)
                {
                    messages.Add(CreateMessage($"Msg{i}", userId, platform, baseTime.AddMilliseconds(i * 100)));
                }
                
                // 尝试合并
                var result = merger.TryMerge(messages);
                
                if (!result.WasMerged)
                    return true; // 如果没有合并，跳过验证
                
                // 验证发送者和平台信息
                var merged = result.Messages[0];
                return merged.SenderId == userId && merged.Platform == platform;
            });
    }
    
    /// <summary>
    /// Property 11: 消息合并正确性
    /// For any 合并后的消息，类型应为 Text。
    /// Validates: Requirements 8.4
    /// </summary>
    [Property(MaxTest = 100)]
    public Property MergedMessage_ShouldBeTextType()
    {
        return Prop.ForAll(
            GenerateMessageCount().ToArbitrary(),
            GenerateValidUserId().ToArbitrary(),
            GenerateValidPlatform().ToArbitrary(),
            (count, userId, platform) =>
            {
                var merger = CreateMerger();
                var baseTime = DateTimeOffset.UtcNow;
                
                // 创建消息列表
                var messages = new List<IChatMessage>();
                for (var i = 0; i < count; i++)
                {
                    messages.Add(CreateMessage($"Msg{i}", userId, platform, baseTime.AddMilliseconds(i * 100)));
                }
                
                // 尝试合并
                var result = merger.TryMerge(messages);
                
                if (!result.WasMerged)
                    return true; // 如果没有合并，跳过验证
                
                // 验证消息类型
                return result.Messages[0].MessageType == ChatMessageType.Text;
            });
    }
}
