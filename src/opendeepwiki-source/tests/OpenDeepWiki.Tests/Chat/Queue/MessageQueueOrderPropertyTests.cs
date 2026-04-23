using FsCheck;
using FsCheck.Fluent;
using FsCheck.Xunit;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using OpenDeepWiki.Chat.Abstractions;
using OpenDeepWiki.Chat.Queue;
using OpenDeepWiki.Tests.Chat.Sessions;
using Xunit;

namespace OpenDeepWiki.Tests.Chat.Queue;

/// <summary>
/// Property-based tests for message queue order preservation.
/// Feature: multi-platform-agent-chat, Property 9: 消息队列顺序保持
/// Validates: Requirements 8.1
/// </summary>
public class MessageQueueOrderPropertyTests
{
    /// <summary>
    /// 生成有效的消息内容
    /// </summary>
    private static Gen<string> GenerateValidContent()
    {
        return Gen.Elements(
            "Hello",
            "Test message",
            "你好世界",
            "Message content 123",
            "Short"
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
    /// 生成消息数量（1-20）
    /// </summary>
    private static Gen<int> GenerateMessageCount()
    {
        return Gen.Choose(1, 20);
    }

    /// <summary>
    /// 创建测试用的 DatabaseMessageQueue
    /// </summary>
    private static DatabaseMessageQueue CreateMessageQueue(TestDbContext context)
    {
        var logger = new LoggerFactory().CreateLogger<DatabaseMessageQueue>();
        var options = Options.Create(new MessageQueueOptions
        {
            MaxRetryCount = 3,
            DefaultRetryDelaySeconds = 30,
            MessageIntervalMs = 500
        });
        
        return new DatabaseMessageQueue(context, logger, options);
    }
    
    /// <summary>
    /// 创建测试消息
    /// </summary>
    private static QueuedMessage CreateQueuedMessage(string content, string platform, string userId)
    {
        var message = new ChatMessage
        {
            MessageId = Guid.NewGuid().ToString(),
            SenderId = userId,
            Content = content,
            MessageType = ChatMessageType.Text,
            Platform = platform,
            Timestamp = DateTimeOffset.UtcNow
        };
        
        return new QueuedMessage(
            Guid.NewGuid().ToString(),
            message,
            Guid.NewGuid().ToString(),
            userId,
            QueuedMessageType.Outgoing
        );
    }

    /// <summary>
    /// Property 9: 消息队列顺序保持
    /// For any 按顺序入队的消息序列，出队顺序应与入队顺序一致（FIFO）。
    /// Validates: Requirements 8.1
    /// </summary>
    [Property(MaxTest = 100)]
    public Property MessageQueue_ShouldMaintainFIFOOrder()
    {
        return Prop.ForAll(
            GenerateMessageCount().ToArbitrary(),
            GenerateValidPlatform().ToArbitrary(),
            GenerateValidUserId().ToArbitrary(),
            (count, platform, userId) =>
            {
                using var context = TestDbContext.Create();
                var queue = CreateMessageQueue(context);
                
                // 生成并入队消息
                var enqueuedContents = new List<string>();
                for (var i = 0; i < count; i++)
                {
                    var content = $"Message_{i}";
                    enqueuedContents.Add(content);
                    var message = CreateQueuedMessage(content, platform, userId);
                    queue.EnqueueAsync(message).Wait();
                    // 添加小延迟确保时间戳不同
                    Thread.Sleep(1);
                }
                
                // 出队并验证顺序
                var dequeuedContents = new List<string>();
                while (queue.GetQueueLengthAsync().Result > 0)
                {
                    var item = queue.DequeueAsync().Result;
                    if (item != null)
                    {
                        dequeuedContents.Add(item.Message.Content);
                        queue.CompleteAsync(item.Id).Wait();
                    }
                }
                
                return enqueuedContents.SequenceEqual(dequeuedContents);
            });
    }

    /// <summary>
    /// Property 9: 消息队列顺序保持
    /// For any 入队的消息，队列长度应正确反映待处理消息数量。
    /// Validates: Requirements 8.1
    /// </summary>
    [Property(MaxTest = 100)]
    public Property MessageQueue_LengthShouldReflectPendingMessages()
    {
        return Prop.ForAll(
            GenerateMessageCount().ToArbitrary(),
            GenerateValidPlatform().ToArbitrary(),
            GenerateValidUserId().ToArbitrary(),
            (count, platform, userId) =>
            {
                using var context = TestDbContext.Create();
                var queue = CreateMessageQueue(context);
                
                // 入队消息
                for (var i = 0; i < count; i++)
                {
                    var message = CreateQueuedMessage($"Message_{i}", platform, userId);
                    queue.EnqueueAsync(message).Wait();
                }
                
                // 验证队列长度
                var length = queue.GetQueueLengthAsync().Result;
                return length == count;
            });
    }
    
    /// <summary>
    /// Property 9: 消息队列顺序保持
    /// For any 完成的消息，不应再次出队。
    /// Validates: Requirements 8.1
    /// </summary>
    [Property(MaxTest = 100)]
    public Property MessageQueue_CompletedMessagesShouldNotDequeue()
    {
        return Prop.ForAll(
            GenerateMessageCount().ToArbitrary(),
            GenerateValidPlatform().ToArbitrary(),
            GenerateValidUserId().ToArbitrary(),
            (count, platform, userId) =>
            {
                using var context = TestDbContext.Create();
                var queue = CreateMessageQueue(context);
                
                // 入队消息
                var messageIds = new List<string>();
                for (var i = 0; i < count; i++)
                {
                    var message = CreateQueuedMessage($"Message_{i}", platform, userId);
                    queue.EnqueueAsync(message).Wait();
                    messageIds.Add(message.Id);
                }
                
                // 出队并完成所有消息
                var dequeuedIds = new HashSet<string>();
                while (queue.GetQueueLengthAsync().Result > 0)
                {
                    var item = queue.DequeueAsync().Result;
                    if (item != null)
                    {
                        dequeuedIds.Add(item.Id);
                        queue.CompleteAsync(item.Id).Wait();
                    }
                }
                
                // 再次尝试出队，应该返回 null
                var extraItem = queue.DequeueAsync().Result;
                
                return extraItem == null && dequeuedIds.Count == count;
            });
    }
    
    /// <summary>
    /// Property 9: 消息队列顺序保持
    /// For any 空队列，出队应返回 null。
    /// Validates: Requirements 8.1
    /// </summary>
    [Fact]
    public async Task EmptyQueue_DequeueShouldReturnNull()
    {
        using var context = TestDbContext.Create();
        var queue = CreateMessageQueue(context);
        
        var result = await queue.DequeueAsync();
        
        Assert.Null(result);
    }
    
    /// <summary>
    /// Property 9: 消息队列顺序保持
    /// For any 空队列，长度应为 0。
    /// Validates: Requirements 8.1
    /// </summary>
    [Fact]
    public async Task EmptyQueue_LengthShouldBeZero()
    {
        using var context = TestDbContext.Create();
        var queue = CreateMessageQueue(context);
        
        var length = await queue.GetQueueLengthAsync();
        
        Assert.Equal(0, length);
    }
}
