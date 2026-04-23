using System.Diagnostics;
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
/// Property-based tests for async message acknowledgment.
/// Feature: multi-platform-agent-chat, Property 14: 异步消息确认
/// Validates: Requirements 10.1
/// </summary>
public class AsyncMessageAcknowledgmentPropertyTests
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
            QueuedMessageType.Incoming
        );
    }

    /// <summary>
    /// Property 14: 异步消息确认
    /// For any 收到的平台消息，系统应在将消息入队后立即返回确认，入队操作不应阻塞。
    /// Validates: Requirements 10.1
    /// </summary>
    [Property(MaxTest = 100)]
    public Property EnqueueAsync_ShouldCompleteQuickly()
    {
        return Prop.ForAll(
            GenerateValidContent().ToArbitrary(),
            GenerateValidPlatform().ToArbitrary(),
            GenerateValidUserId().ToArbitrary(),
            (content, platform, userId) =>
            {
                using var context = TestDbContext.Create();
                var queue = CreateMessageQueue(context);
                var message = CreateQueuedMessage(content, platform, userId);

                var stopwatch = Stopwatch.StartNew();
                queue.EnqueueAsync(message).Wait();
                stopwatch.Stop();

                // 入队操作应在 100ms 内完成（非阻塞）
                return stopwatch.ElapsedMilliseconds < 100;
            });
    }

    /// <summary>
    /// Property 14: 异步消息确认
    /// For any 入队的消息，入队后应立即可查询到。
    /// Validates: Requirements 10.1
    /// </summary>
    [Property(MaxTest = 100)]
    public Property EnqueueAsync_MessageShouldBeImmediatelyQueryable()
    {
        return Prop.ForAll(
            GenerateValidContent().ToArbitrary(),
            GenerateValidPlatform().ToArbitrary(),
            GenerateValidUserId().ToArbitrary(),
            (content, platform, userId) =>
            {
                using var context = TestDbContext.Create();
                var queue = CreateMessageQueue(context);
                var message = CreateQueuedMessage(content, platform, userId);

                // 入队前队列长度
                var lengthBefore = queue.GetQueueLengthAsync().Result;

                // 入队
                queue.EnqueueAsync(message).Wait();

                // 入队后队列长度应立即增加
                var lengthAfter = queue.GetQueueLengthAsync().Result;

                return lengthAfter == lengthBefore + 1;
            });
    }

    /// <summary>
    /// Property 14: 异步消息确认
    /// For any 多条并发入队的消息，所有入队操作都应成功完成。
    /// Validates: Requirements 10.1
    /// </summary>
    [Property(MaxTest = 50)]
    public Property EnqueueAsync_ConcurrentEnqueuesShouldAllSucceed()
    {
        return Prop.ForAll(
            Gen.Choose(2, 10).ToArbitrary(),
            GenerateValidPlatform().ToArbitrary(),
            GenerateValidUserId().ToArbitrary(),
            (count, platform, userId) =>
            {
                using var context = TestDbContext.Create();
                var queue = CreateMessageQueue(context);

                // 创建多条消息
                var messages = Enumerable.Range(0, count)
                    .Select(i => CreateQueuedMessage($"Message_{i}", platform, userId))
                    .ToList();

                // 并发入队
                var tasks = messages.Select(m => queue.EnqueueAsync(m)).ToArray();
                Task.WaitAll(tasks);

                // 验证所有消息都已入队
                var queueLength = queue.GetQueueLengthAsync().Result;
                return queueLength == count;
            });
    }

    /// <summary>
    /// Property 14: 异步消息确认
    /// For any 入队的消息，消息内容应保持完整。
    /// Validates: Requirements 10.1
    /// </summary>
    [Property(MaxTest = 100)]
    public Property EnqueueAsync_MessageContentShouldBePreserved()
    {
        return Prop.ForAll(
            GenerateValidContent().ToArbitrary(),
            GenerateValidPlatform().ToArbitrary(),
            GenerateValidUserId().ToArbitrary(),
            (content, platform, userId) =>
            {
                using var context = TestDbContext.Create();
                var queue = CreateMessageQueue(context);
                var message = CreateQueuedMessage(content, platform, userId);

                // 入队
                queue.EnqueueAsync(message).Wait();

                // 出队并验证内容
                var dequeued = queue.DequeueAsync().Result;

                return dequeued != null &&
                       dequeued.Message.Content == content &&
                       dequeued.Message.Platform == platform &&
                       dequeued.Message.SenderId == userId;
            });
    }

    /// <summary>
    /// Property 14: 异步消息确认
    /// For any 入队的消息，入队操作应是非阻塞的（不等待处理完成）。
    /// Validates: Requirements 10.1
    /// </summary>
    [Fact]
    public async Task EnqueueAsync_ShouldBeNonBlocking()
    {
        using var context = TestDbContext.Create();
        var queue = CreateMessageQueue(context);

        // 入队多条消息
        var tasks = new List<Task>();
        for (var i = 0; i < 10; i++)
        {
            var message = CreateQueuedMessage($"Message_{i}", "feishu", "user123");
            tasks.Add(queue.EnqueueAsync(message));
        }

        var stopwatch = Stopwatch.StartNew();
        await Task.WhenAll(tasks);
        stopwatch.Stop();

        // 所有入队操作应在 500ms 内完成
        Assert.True(stopwatch.ElapsedMilliseconds < 500);

        // 验证所有消息都已入队
        var queueLength = await queue.GetQueueLengthAsync();
        Assert.Equal(10, queueLength);
    }

    /// <summary>
    /// Property 14: 异步消息确认
    /// For any 入队的消息，状态应为 Pending。
    /// Validates: Requirements 10.1
    /// </summary>
    [Fact]
    public async Task EnqueueAsync_MessageStatusShouldBePending()
    {
        using var context = TestDbContext.Create();
        var queue = CreateMessageQueue(context);
        var message = CreateQueuedMessage("Test", "feishu", "user123");

        await queue.EnqueueAsync(message);

        // 直接查询数据库验证状态
        var entity = context.ChatMessageQueues.FirstOrDefault();
        Assert.NotNull(entity);
        Assert.Equal("Pending", entity.Status);
    }
}
