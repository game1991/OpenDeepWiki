using FsCheck;
using FsCheck.Fluent;
using FsCheck.Xunit;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using OpenDeepWiki.Chat.Abstractions;
using OpenDeepWiki.Chat.Queue;
using OpenDeepWiki.Tests.Chat.Sessions;
using Xunit;

namespace OpenDeepWiki.Tests.Chat.Queue;

/// <summary>
/// Property-based tests for message retry mechanism.
/// Feature: multi-platform-agent-chat, Property 10: 消息重试机制
/// Validates: Requirements 7.4, 8.5
/// </summary>
public class MessageRetryPropertyTests
{
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
    /// 生成重试次数（1-5）
    /// </summary>
    private static Gen<int> GenerateRetryCount()
    {
        return Gen.Choose(1, 5);
    }
    
    /// <summary>
    /// 生成延迟秒数（1-60）
    /// </summary>
    private static Gen<int> GenerateDelaySeconds()
    {
        return Gen.Choose(1, 60);
    }

    /// <summary>
    /// 创建测试用的 DatabaseMessageQueue
    /// </summary>
    private static DatabaseMessageQueue CreateMessageQueue(TestDbContext context, int maxRetryCount = 3)
    {
        var logger = new LoggerFactory().CreateLogger<DatabaseMessageQueue>();
        var options = Options.Create(new MessageQueueOptions
        {
            MaxRetryCount = maxRetryCount,
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
    /// Property 10: 消息重试机制
    /// For any 发送失败的消息，调用 RetryAsync 后 RetryCount 应递增。
    /// Validates: Requirements 7.4, 8.5
    /// </summary>
    [Property(MaxTest = 100)]
    public Property RetryAsync_ShouldIncrementRetryCount()
    {
        return Prop.ForAll(
            GenerateValidPlatform().ToArbitrary(),
            GenerateValidUserId().ToArbitrary(),
            GenerateDelaySeconds().ToArbitrary(),
            (platform, userId, delaySeconds) =>
            {
                using var context = TestDbContext.Create();
                var queue = CreateMessageQueue(context);
                
                // 入队消息
                var message = CreateQueuedMessage("Test message", platform, userId);
                queue.EnqueueAsync(message).Wait();
                
                // 出队消息
                var dequeued = queue.DequeueAsync().Result;
                if (dequeued == null) return false;
                
                // 获取初始重试次数
                var entityBefore = context.ChatMessageQueues.Find(Guid.Parse(dequeued.Id));
                var initialRetryCount = entityBefore?.RetryCount ?? 0;
                
                // 调用重试
                queue.RetryAsync(dequeued.Id, delaySeconds).Wait();
                
                // 验证重试次数递增
                context.Entry(entityBefore!).Reload();
                var entityAfter = context.ChatMessageQueues.Find(Guid.Parse(dequeued.Id));
                
                return entityAfter != null && entityAfter.RetryCount == initialRetryCount + 1;
            });
    }

    /// <summary>
    /// Property 10: 消息重试机制
    /// For any 重试的消息，状态应变为 Pending 且类型应变为 Retry。
    /// Validates: Requirements 7.4, 8.5
    /// </summary>
    [Property(MaxTest = 100)]
    public Property RetryAsync_ShouldSetStatusToPendingAndTypeToRetry()
    {
        return Prop.ForAll(
            GenerateValidPlatform().ToArbitrary(),
            GenerateValidUserId().ToArbitrary(),
            (platform, userId) =>
            {
                using var context = TestDbContext.Create();
                var queue = CreateMessageQueue(context, maxRetryCount: 5); // 设置较高的重试次数
                
                // 入队消息
                var message = CreateQueuedMessage("Test message", platform, userId);
                queue.EnqueueAsync(message).Wait();
                
                // 出队消息
                var dequeued = queue.DequeueAsync().Result;
                if (dequeued == null) return false;
                
                // 调用重试
                queue.RetryAsync(dequeued.Id, 30).Wait();
                
                // 验证状态和类型
                var entity = context.ChatMessageQueues.Find(Guid.Parse(dequeued.Id));
                
                return entity != null && 
                       entity.Status == "Pending" && 
                       entity.QueueType == "Retry";
            });
    }
    
    /// <summary>
    /// Property 10: 消息重试机制
    /// For any 超过最大重试次数的消息，应被移入死信队列。
    /// Validates: Requirements 7.4, 8.5
    /// </summary>
    [Property(MaxTest = 100)]
    public Property RetryAsync_ExceedingMaxRetry_ShouldMoveToDeadLetter()
    {
        return Prop.ForAll(
            GenerateValidPlatform().ToArbitrary(),
            GenerateValidUserId().ToArbitrary(),
            (platform, userId) =>
            {
                using var context = TestDbContext.Create();
                const int maxRetryCount = 3;
                var queue = CreateMessageQueue(context, maxRetryCount);
                
                // 入队消息
                var message = CreateQueuedMessage("Test message", platform, userId);
                queue.EnqueueAsync(message).Wait();
                
                // 出队消息
                var dequeued = queue.DequeueAsync().Result;
                if (dequeued == null) return false;
                
                // 重试直到超过最大次数
                for (var i = 0; i < maxRetryCount; i++)
                {
                    queue.RetryAsync(dequeued.Id, 0).Wait();
                }
                
                // 验证消息已移入死信队列
                var entity = context.ChatMessageQueues.Find(Guid.Parse(dequeued.Id));
                
                return entity != null && entity.Status == "DeadLetter";
            });
    }

    /// <summary>
    /// Property 10: 消息重试机制
    /// For any 调用 FailAsync 的消息，RetryCount 应递增。
    /// Validates: Requirements 7.4, 8.5
    /// </summary>
    [Property(MaxTest = 100)]
    public Property FailAsync_ShouldIncrementRetryCount()
    {
        return Prop.ForAll(
            GenerateValidPlatform().ToArbitrary(),
            GenerateValidUserId().ToArbitrary(),
            (platform, userId) =>
            {
                using var context = TestDbContext.Create();
                var queue = CreateMessageQueue(context);
                
                // 入队消息
                var message = CreateQueuedMessage("Test message", platform, userId);
                queue.EnqueueAsync(message).Wait();
                
                // 出队消息
                var dequeued = queue.DequeueAsync().Result;
                if (dequeued == null) return false;
                
                // 获取初始重试次数
                var entityBefore = context.ChatMessageQueues.Find(Guid.Parse(dequeued.Id));
                var initialRetryCount = entityBefore?.RetryCount ?? 0;
                
                // 调用失败
                queue.FailAsync(dequeued.Id, "Test failure").Wait();
                
                // 验证重试次数递增
                context.Entry(entityBefore!).Reload();
                var entityAfter = context.ChatMessageQueues.Find(Guid.Parse(dequeued.Id));
                
                return entityAfter != null && entityAfter.RetryCount == initialRetryCount + 1;
            });
    }
    
    /// <summary>
    /// Property 10: 消息重试机制
    /// For any 调用 FailAsync 超过最大重试次数的消息，应被移入死信队列。
    /// Validates: Requirements 7.4, 8.5
    /// </summary>
    [Property(MaxTest = 100)]
    public Property FailAsync_ExceedingMaxRetry_ShouldMoveToDeadLetter()
    {
        return Prop.ForAll(
            GenerateValidPlatform().ToArbitrary(),
            GenerateValidUserId().ToArbitrary(),
            (platform, userId) =>
            {
                using var context = TestDbContext.Create();
                const int maxRetryCount = 3;
                var queue = CreateMessageQueue(context, maxRetryCount);
                
                // 入队消息
                var message = CreateQueuedMessage("Test message", platform, userId);
                queue.EnqueueAsync(message).Wait();
                
                // 出队消息
                var dequeued = queue.DequeueAsync().Result;
                if (dequeued == null) return false;
                
                // 失败直到超过最大次数
                for (var i = 0; i < maxRetryCount; i++)
                {
                    queue.FailAsync(dequeued.Id, $"Failure {i + 1}").Wait();
                }
                
                // 验证消息已移入死信队列
                var entity = context.ChatMessageQueues.Find(Guid.Parse(dequeued.Id));
                
                return entity != null && entity.Status == "DeadLetter";
            });
    }
    
    /// <summary>
    /// Property 10: 消息重试机制
    /// For any 死信队列中的消息，GetDeadLetterCountAsync 应正确计数。
    /// Validates: Requirements 7.4, 8.5
    /// </summary>
    [Property(MaxTest = 100)]
    public Property DeadLetterCount_ShouldReflectDeadLetterMessages()
    {
        return Prop.ForAll(
            Gen.Choose(1, 5).ToArbitrary(),
            GenerateValidPlatform().ToArbitrary(),
            GenerateValidUserId().ToArbitrary(),
            (count, platform, userId) =>
            {
                using var context = TestDbContext.Create();
                const int maxRetryCount = 1; // 设置为1，这样第一次失败就会进入死信队列
                var queue = CreateMessageQueue(context, maxRetryCount);
                
                // 入队并使消息进入死信队列
                for (var i = 0; i < count; i++)
                {
                    var message = CreateQueuedMessage($"Message_{i}", platform, userId);
                    queue.EnqueueAsync(message).Wait();
                    
                    var dequeued = queue.DequeueAsync().Result;
                    if (dequeued != null)
                    {
                        queue.FailAsync(dequeued.Id, "Test failure").Wait();
                    }
                }
                
                // 验证死信队列计数
                var deadLetterCount = queue.GetDeadLetterCountAsync().Result;
                
                return deadLetterCount == count;
            });
    }
}
