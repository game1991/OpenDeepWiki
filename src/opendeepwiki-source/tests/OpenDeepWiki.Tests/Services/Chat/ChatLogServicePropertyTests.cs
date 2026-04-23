using FsCheck;
using FsCheck.Fluent;
using FsCheck.Xunit;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using OpenDeepWiki.Services.Chat;

namespace OpenDeepWiki.Tests.Services.Chat;

/// <summary>
/// Property-based tests for ChatLogService.
/// Feature: doc-chat-assistant, Property 10: 提问记录关联正确性
/// Validates: Requirements 16.1, 16.5
/// </summary>
public class ChatLogServicePropertyTests
{
    /// <summary>
    /// Creates an in-memory database context for testing.
    /// </summary>
    private static TestDbContext CreateTestContext()
    {
        var options = new DbContextOptionsBuilder<TestDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options;
        return new TestDbContext(options);
    }

    /// <summary>
    /// Generates valid AppIds.
    /// </summary>
    private static Gen<string> GenerateAppId()
    {
        return Gen.Elements(
            "app_log123456789012345678",
            "app_logabcdef123456789012",
            "app_logxyz987654321098765"
        );
    }

    /// <summary>
    /// Generates valid questions.
    /// </summary>
    private static Gen<string> GenerateQuestion()
    {
        return Gen.Elements(
            "How do I use this API?",
            "What is the best practice for authentication?",
            "Can you explain the architecture?",
            "How to configure the database?",
            "What are the system requirements?"
        );
    }

    /// <summary>
    /// Generates valid answer summaries.
    /// </summary>
    private static Gen<string?> GenerateAnswerSummary()
    {
        return Gen.OneOf(
            Gen.Constant<string?>(null),
            Gen.Elements<string?>(
                "The API can be used by...",
                "Best practice is to...",
                "The architecture consists of..."
            )
        );
    }


    /// <summary>
    /// Property 10: 提问记录关联正确性 - 记录应该关联到正确的AppId
    /// For any chat log recording, the log should be associated with the correct AppId.
    /// Validates: Requirements 16.1, 16.5
    /// </summary>
    [Property(MaxTest = 100)]
    public Property RecordChatLog_ShouldAssociateWithCorrectAppId()
    {
        return Prop.ForAll(
            GenerateAppId().ToArbitrary(),
            GenerateQuestion().ToArbitrary(),
            (appId, question) =>
            {
                using var context = CreateTestContext();
                var logger = NullLogger<ChatLogService>.Instance;
                var service = new ChatLogService(context, logger);

                // Record a chat log
                var result = service.RecordChatLogAsync(new RecordChatLogDto
                {
                    AppId = appId,
                    Question = question,
                    InputTokens = 100,
                    OutputTokens = 50
                }).GetAwaiter().GetResult();

                // Verify the log is associated with the correct AppId
                return result.AppId == appId;
            });
    }

    /// <summary>
    /// Property 10: 提问记录关联正确性 - 记录应该包含完整的提问内容
    /// For any chat log recording, the log should contain the complete question.
    /// Validates: Requirements 16.1
    /// </summary>
    [Property(MaxTest = 100)]
    public Property RecordChatLog_ShouldContainCompleteQuestion()
    {
        return Prop.ForAll(
            GenerateAppId().ToArbitrary(),
            GenerateQuestion().ToArbitrary(),
            (appId, question) =>
            {
                using var context = CreateTestContext();
                var logger = NullLogger<ChatLogService>.Instance;
                var service = new ChatLogService(context, logger);

                // Record a chat log
                var result = service.RecordChatLogAsync(new RecordChatLogDto
                {
                    AppId = appId,
                    Question = question,
                    InputTokens = 100,
                    OutputTokens = 50
                }).GetAwaiter().GetResult();

                // Verify the question is stored correctly
                return result.Question == question;
            });
    }


    /// <summary>
    /// Property 10: 提问记录关联正确性 - 查询应该只返回指定AppId的记录
    /// For any query, only logs for the specified AppId should be returned.
    /// Validates: Requirements 16.5
    /// </summary>
    [Property(MaxTest = 50)]
    public Property GetLogs_ShouldOnlyReturnLogsForSpecifiedAppId()
    {
        return Prop.ForAll(
            GenerateQuestion().ToArbitrary(),
            question =>
            {
                using var context = CreateTestContext();
                var logger = NullLogger<ChatLogService>.Instance;
                var service = new ChatLogService(context, logger);

                var appId1 = "app_query11111111111111111";
                var appId2 = "app_query22222222222222222";

                // Record logs for both apps
                service.RecordChatLogAsync(new RecordChatLogDto
                {
                    AppId = appId1,
                    Question = question,
                    InputTokens = 100,
                    OutputTokens = 50
                }).GetAwaiter().GetResult();

                service.RecordChatLogAsync(new RecordChatLogDto
                {
                    AppId = appId2,
                    Question = "Different question",
                    InputTokens = 200,
                    OutputTokens = 100
                }).GetAwaiter().GetResult();

                // Query logs for app1 only
                var result = service.GetLogsAsync(new ChatLogQueryDto
                {
                    AppId = appId1
                }).GetAwaiter().GetResult();

                // Verify only app1 logs are returned
                return result.Items.All(l => l.AppId == appId1) && result.TotalCount == 1;
            });
    }

    /// <summary>
    /// Property 10: 提问记录关联正确性 - 不同AppId的记录应该独立
    /// For different AppIds, chat logs should be independent.
    /// Validates: Requirements 16.5
    /// </summary>
    [Property(MaxTest = 50)]
    public Property RecordChatLog_DifferentApps_ShouldBeIndependent()
    {
        return Prop.ForAll(
            GenerateQuestion().ToArbitrary(),
            GenerateQuestion().ToArbitrary(),
            (question1, question2) =>
            {
                using var context = CreateTestContext();
                var logger = NullLogger<ChatLogService>.Instance;
                var service = new ChatLogService(context, logger);

                var appId1 = "app_indep11111111111111111";
                var appId2 = "app_indep22222222222222222";

                // Record for app1
                service.RecordChatLogAsync(new RecordChatLogDto
                {
                    AppId = appId1,
                    Question = question1,
                    InputTokens = 100,
                    OutputTokens = 50
                }).GetAwaiter().GetResult();

                // Record for app2
                service.RecordChatLogAsync(new RecordChatLogDto
                {
                    AppId = appId2,
                    Question = question2,
                    InputTokens = 200,
                    OutputTokens = 100
                }).GetAwaiter().GetResult();

                // Query each app
                var result1 = service.GetLogsAsync(new ChatLogQueryDto { AppId = appId1 }).GetAwaiter().GetResult();
                var result2 = service.GetLogsAsync(new ChatLogQueryDto { AppId = appId2 }).GetAwaiter().GetResult();

                // Verify independence
                return result1.TotalCount == 1 &&
                       result2.TotalCount == 1 &&
                       result1.Items[0].Question == question1 &&
                       result2.Items[0].Question == question2;
            });
    }


    /// <summary>
    /// Property 10: 提问记录关联正确性 - 记录应该包含时间戳
    /// For any chat log recording, the log should have a valid timestamp.
    /// Validates: Requirements 16.1
    /// </summary>
    [Property(MaxTest = 100)]
    public Property RecordChatLog_ShouldHaveValidTimestamp()
    {
        return Prop.ForAll(
            GenerateAppId().ToArbitrary(),
            GenerateQuestion().ToArbitrary(),
            (appId, question) =>
            {
                using var context = CreateTestContext();
                var logger = NullLogger<ChatLogService>.Instance;
                var service = new ChatLogService(context, logger);

                var beforeRecord = DateTime.UtcNow;

                // Record a chat log
                var result = service.RecordChatLogAsync(new RecordChatLogDto
                {
                    AppId = appId,
                    Question = question,
                    InputTokens = 100,
                    OutputTokens = 50
                }).GetAwaiter().GetResult();

                var afterRecord = DateTime.UtcNow;

                // Verify the timestamp is within the expected range
                return result.CreatedAt >= beforeRecord && result.CreatedAt <= afterRecord;
            });
    }

    /// <summary>
    /// Property 10: 提问记录关联正确性 - 记录应该包含Token信息
    /// For any chat log recording, the log should contain token information.
    /// Validates: Requirements 16.1
    /// </summary>
    [Property(MaxTest = 100)]
    public Property RecordChatLog_ShouldContainTokenInfo()
    {
        var gen = GenerateAppId().SelectMany(appId =>
            GenerateQuestion().SelectMany(question =>
                Gen.Choose(1, 10000).SelectMany(inputTokens =>
                    Gen.Choose(1, 10000).Select(outputTokens =>
                        (appId, question, inputTokens, outputTokens)))));

        return Prop.ForAll(
            gen.ToArbitrary(),
            tuple =>
            {
                var (appId, question, inputTokens, outputTokens) = tuple;

                using var context = CreateTestContext();
                var logger = NullLogger<ChatLogService>.Instance;
                var service = new ChatLogService(context, logger);

                // Record a chat log
                var result = service.RecordChatLogAsync(new RecordChatLogDto
                {
                    AppId = appId,
                    Question = question,
                    InputTokens = inputTokens,
                    OutputTokens = outputTokens
                }).GetAwaiter().GetResult();

                // Verify token information is stored correctly
                return result.InputTokens == inputTokens && result.OutputTokens == outputTokens;
            });
    }
}
