using FsCheck;
using FsCheck.Fluent;
using FsCheck.Xunit;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using OpenDeepWiki.Chat.Abstractions;
using OpenDeepWiki.Chat.Queue;
using OpenDeepWiki.Tests.Chat.Sessions;
using Xunit;

namespace OpenDeepWiki.Tests.Chat.Processing;

/// <summary>
/// Property-based tests for dead letter queue processing.
/// Feature: multi-platform-agent-chat, Property 15: 死信队列处理
/// Validates: Requirements 10.4
/// </summary>
public class DeadLetterQueuePropertyTests
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
    /// 创建测试用的 DatabaseMessageQueue（最大重试次数为2）
    /// </summary>
    private static DatabaseMessageQueue CreateMessageQueue(TestDbContext context, int maxRetryCount = 2)
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
    /// Property 15: 死信队列处理
    /// For any 处理失败且超过最大重试次数的消息，应被移入死信队列。
    /// Validates: Requirements 10.4
    /// </summary>
    [Property(MaxTest = 100)]
    public Property FailedMessage_ExceedingMaxRetries_ShouldMoveToDeadLetter()
    {
        return Prop.ForAll(
            GenerateValidContent().ToArbitrary(),
            GenerateValidPlatform().ToArbitrary(),
            GenerateValidUserId().ToArbitrary(),
            (content, platform, userId) =>
            {
                var reason = "Test failure reason";
                using var context = TestDbContext.Create();
                var queue = CreateMessageQueue(context, maxRetryCount: 2);
                var message = CreateQueuedMessage(content, platform, userId);

                // 入队
                queue.EnqueueAsync(message).Wait();

                // 出队
                var dequeued = queue.DequeueAsync().Result;
                Assert.NotNull(dequeued);

                // 第一次失败（重试次数变为1）
                queue.FailAsync(dequeued.Id, reason).Wait();
                
                // 验证还未进入死信队列
                var deadLetterCount1 = queue.GetDeadLetterCountAsync().Result;
                
                // 重新入队（模拟重试）
                var entity1 = context.ChatMessageQueues.First();
                entity1.Status = "Pending";
                context.SaveChanges();
                
                // 再次出队
                var dequeued2 = queue.DequeueAsync().Result;
                Assert.NotNull(dequeued2);
                
                // 第二次失败（重试次数变为2，达到最大值）
                queue.FailAsync(dequeued2.Id, reason).Wait();

                // 验证已进入死信队列
                var deadLetterCount2 = queue.GetDeadLetterCountAsync().Result;
                var queueLength = queue.GetQueueLengthAsync().Result;

                return deadLetterCount1 == 0 && deadLetterCount2 == 1 && queueLength == 0;
            });
    }

    /// <summary>
    /// Property 15: 死信队列处理
    /// For any 移入死信队列的消息，原队列中不再包含该消息。
    /// Validates: Requirements 10.4
    /// </summary>
    [Property(MaxTest = 100)]
    public Property DeadLetterMessage_ShouldNotBeInOriginalQueue()
    {
        return Prop.ForAll(
            GenerateValidContent().ToArbitrary(),
            GenerateValidPlatform().ToArbitrary(),
            GenerateValidUserId().ToArbitrary(),
            (content, platform, userId) =>
            {
                var reason = "Test failure reason";
                using var context = TestDbContext.Create();
                var queue = CreateMessageQueue(context, maxRetryCount: 1);
                var message = CreateQueuedMessage(content, platform, userId);

                // 入队
                queue.EnqueueAsync(message).Wait();

                // 出队
                var dequeued = queue.DequeueAsync().Result;
                Assert.NotNull(dequeued);

                // 失败（直接进入死信队列，因为 maxRetryCount=1）
                queue.FailAsync(dequeued.Id, reason).Wait();

                // 验证原队列为空
                var queueLength = queue.GetQueueLengthAsync().Result;
                
                // 验证死信队列有消息
                var deadLetterCount = queue.GetDeadLetterCountAsync().Result;

                // 尝试再次出队，应该返回 null
                var nextDequeued = queue.DequeueAsync().Result;

                return queueLength == 0 && deadLetterCount == 1 && nextDequeued == null;
            });
    }

    /// <summary>
    /// Property 15: 死信队列处理
    /// For any 死信队列中的消息，应保留原始消息内容和错误信息。
    /// Validates: Requirements 10.4
    /// </summary>
    [Property(MaxTest = 100)]
    public Property DeadLetterMessage_ShouldPreserveContentAndError()
    {
        return Prop.ForAll(
            GenerateValidContent().ToArbitrary(),
            GenerateValidPlatform().ToArbitrary(),
            GenerateValidUserId().ToArbitrary(),
            (content, platform, userId) =>
            {
                var reason = "Specific error reason";
                using var context = TestDbContext.Create();
                var queue = CreateMessageQueue(context, maxRetryCount: 1);
                var message = CreateQueuedMessage(content, platform, userId);

                // 入队
                queue.EnqueueAsync(message).Wait();

                // 出队并失败
                var dequeued = queue.DequeueAsync().Result;
                Assert.NotNull(dequeued);
                queue.FailAsync(dequeued.Id, reason).Wait();

                // 获取死信消息
                var deadLetters = queue.GetDeadLetterMessagesAsync().Result;

                return deadLetters.Count == 1 &&
                       deadLetters[0].Message.Content == content &&
                       deadLetters[0].Message.Platform == platform &&
                       deadLetters[0].ErrorMessage == reason;
            });
    }

    /// <summary>
    /// Property 15: 死信队列处理
    /// For any 死信队列中的消息，可以被重新处理。
    /// Validates: Requirements 10.4
    /// </summary>
    [Property(MaxTest = 100)]
    public Property DeadLetterMessage_CanBeReprocessed()
    {
        return Prop.ForAll(
            GenerateValidContent().ToArbitrary(),
            GenerateValidPlatform().ToArbitrary(),
            GenerateValidUserId().ToArbitrary(),
            (content, platform, userId) =>
            {
                var reason = "Test failure reason";
                using var context = TestDbContext.Create();
                var queue = CreateMessageQueue(context, maxRetryCount: 1);
                var message = CreateQueuedMessage(content, platform, userId);

                // 入队
                queue.EnqueueAsync(message).Wait();

                // 出队并失败
                var dequeued = queue.DequeueAsync().Result;
                Assert.NotNull(dequeued);
                queue.FailAsync(dequeued.Id, reason).Wait();

                // 验证在死信队列
                var deadLetterCountBefore = queue.GetDeadLetterCountAsync().Result;

                // 重新处理
                var reprocessed = queue.ReprocessDeadLetterAsync(dequeued.Id).Result;

                // 验证已从死信队列移出
                var deadLetterCountAfter = queue.GetDeadLetterCountAsync().Result;
                var queueLength = queue.GetQueueLengthAsync().Result;

                return reprocessed &&
                       deadLetterCountBefore == 1 &&
                       deadLetterCountAfter == 0 &&
                       queueLength == 1;
            });
    }

    /// <summary>
    /// Property 15: 死信队列处理
    /// For any 死信队列中的消息，可以被删除。
    /// Validates: Requirements 10.4
    /// </summary>
    [Property(MaxTest = 100)]
    public Property DeadLetterMessage_CanBeDeleted()
    {
        return Prop.ForAll(
            GenerateValidContent().ToArbitrary(),
            GenerateValidPlatform().ToArbitrary(),
            GenerateValidUserId().ToArbitrary(),
            (content, platform, userId) =>
            {
                var reason = "Test failure reason";
                using var context = TestDbContext.Create();
                var queue = CreateMessageQueue(context, maxRetryCount: 1);
                var message = CreateQueuedMessage(content, platform, userId);

                // 入队
                queue.EnqueueAsync(message).Wait();

                // 出队并失败
                var dequeued = queue.DequeueAsync().Result;
                Assert.NotNull(dequeued);
                queue.FailAsync(dequeued.Id, reason).Wait();

                // 验证在死信队列
                var deadLetterCountBefore = queue.GetDeadLetterCountAsync().Result;

                // 删除
                var deleted = queue.DeleteDeadLetterAsync(dequeued.Id).Result;

                // 验证已删除
                var deadLetterCountAfter = queue.GetDeadLetterCountAsync().Result;

                return deleted &&
                       deadLetterCountBefore == 1 &&
                       deadLetterCountAfter == 0;
            });
    }

    /// <summary>
    /// Property 15: 死信队列处理
    /// For any 多条死信消息，清空操作应删除所有消息。
    /// Validates: Requirements 10.4
    /// </summary>
    [Property(MaxTest = 50)]
    public Property ClearDeadLetterQueue_ShouldRemoveAllMessages()
    {
        return Prop.ForAll(
            Gen.Choose(2, 5).ToArbitrary(),
            GenerateValidPlatform().ToArbitrary(),
            GenerateValidUserId().ToArbitrary(),
            (count, platform, userId) =>
            {
                using var context = TestDbContext.Create();
                var queue = CreateMessageQueue(context, maxRetryCount: 1);

                // 创建多条消息并使其进入死信队列
                for (var i = 0; i < count; i++)
                {
                    var message = CreateQueuedMessage($"Message_{i}", platform, userId);
                    queue.EnqueueAsync(message).Wait();
                    var dequeued = queue.DequeueAsync().Result;
                    Assert.NotNull(dequeued);
                    queue.FailAsync(dequeued.Id, "Test failure").Wait();
                }

                // 验证死信队列有消息
                var deadLetterCountBefore = queue.GetDeadLetterCountAsync().Result;

                // 清空
                var clearedCount = queue.ClearDeadLetterQueueAsync().Result;

                // 验证已清空
                var deadLetterCountAfter = queue.GetDeadLetterCountAsync().Result;

                return deadLetterCountBefore == count &&
                       clearedCount == count &&
                       deadLetterCountAfter == 0;
            });
    }

    /// <summary>
    /// Property 15: 死信队列处理
    /// For any 使用 MoveToDeadLetter 直接移入的消息，应立即进入死信队列。
    /// Validates: Requirements 10.4
    /// </summary>
    [Property(MaxTest = 100)]
    public Property MoveToDeadLetter_ShouldImmediatelyMoveMessage()
    {
        return Prop.ForAll(
            GenerateValidContent().ToArbitrary(),
            GenerateValidPlatform().ToArbitrary(),
            GenerateValidUserId().ToArbitrary(),
            (content, platform, userId) =>
            {
                var reason = "Manual move to dead letter";
                using var context = TestDbContext.Create();
                var queue = CreateMessageQueue(context);
                var message = CreateQueuedMessage(content, platform, userId);

                // 入队
                queue.EnqueueAsync(message).Wait();

                // 直接移入死信队列
                queue.MoveToDeadLetterAsync(message.Id, reason).Wait();

                // 验证
                var deadLetterCount = queue.GetDeadLetterCountAsync().Result;
                var queueLength = queue.GetQueueLengthAsync().Result;

                return deadLetterCount == 1 && queueLength == 0;
            });
    }

    /// <summary>
    /// 空死信队列的清空操作应返回0
    /// </summary>
    [Fact]
    public async Task ClearEmptyDeadLetterQueue_ShouldReturnZero()
    {
        using var context = TestDbContext.Create();
        var queue = CreateMessageQueue(context);

        var clearedCount = await queue.ClearDeadLetterQueueAsync();

        Assert.Equal(0, clearedCount);
    }

    /// <summary>
    /// 重新处理不存在的消息应返回 false
    /// </summary>
    [Fact]
    public async Task ReprocessNonExistentMessage_ShouldReturnFalse()
    {
        using var context = TestDbContext.Create();
        var queue = CreateMessageQueue(context);

        var result = await queue.ReprocessDeadLetterAsync(Guid.NewGuid().ToString());

        Assert.False(result);
    }

    /// <summary>
    /// 删除不存在的消息应返回 false
    /// </summary>
    [Fact]
    public async Task DeleteNonExistentMessage_ShouldReturnFalse()
    {
        using var context = TestDbContext.Create();
        var queue = CreateMessageQueue(context);

        var result = await queue.DeleteDeadLetterAsync(Guid.NewGuid().ToString());

        Assert.False(result);
    }
}
