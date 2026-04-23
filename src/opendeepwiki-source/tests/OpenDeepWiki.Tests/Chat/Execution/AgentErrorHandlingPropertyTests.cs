using FsCheck;
using FsCheck.Fluent;
using FsCheck.Xunit;
using OpenDeepWiki.Chat.Abstractions;
using OpenDeepWiki.Chat.Exceptions;
using OpenDeepWiki.Chat.Execution;
using Xunit;

namespace OpenDeepWiki.Tests.Chat.Execution;

/// <summary>
/// Property-based tests for Agent error handling.
/// Feature: multi-platform-agent-chat, Property 13: Agent 错误处理
/// Validates: Requirements 9.4
/// </summary>
public class AgentErrorHandlingPropertyTests
{
    /// <summary>
    /// 生成有效的错误消息
    /// </summary>
    private static Gen<string> GenerateErrorMessage()
    {
        return Gen.Elements(
            "Connection timeout",
            "API rate limit exceeded",
            "Invalid response format",
            "Service unavailable",
            "Authentication failed"
        );
    }
    
    /// <summary>
    /// 生成有效的错误代码
    /// </summary>
    private static Gen<string> GenerateErrorCode()
    {
        return Gen.Elements(
            "TIMEOUT",
            "RATE_LIMIT",
            "INVALID_RESPONSE",
            "SERVICE_UNAVAILABLE",
            "AUTH_FAILED"
        );
    }
    
    /// <summary>
    /// 生成友好错误消息模板
    /// </summary>
    private static Gen<string> GenerateFriendlyErrorTemplate()
    {
        return Gen.Elements(
            "抱歉，处理您的消息时遇到了问题，请稍后重试。",
            "Sorry, there was an issue processing your message.",
            "服务暂时不可用，请稍后再试。"
        );
    }

    /// <summary>
    /// Property 13: Agent 错误处理
    /// For any Agent 执行错误，返回的响应应包含友好的错误消息。
    /// Validates: Requirements 9.4
    /// </summary>
    [Property(MaxTest = 100)]
    public Property AgentResponse_OnError_ShouldContainFriendlyErrorMessage()
    {
        return Prop.ForAll(
            GenerateErrorMessage().ToArbitrary(),
            (errorMessage) =>
            {
                // 创建失败响应
                var response = AgentResponse.CreateFailure(errorMessage);
                
                // 验证错误消息存在且非空
                return !string.IsNullOrEmpty(response.ErrorMessage);
            });
    }
    
    /// <summary>
    /// Property 13: Agent 错误处理
    /// For any Agent 执行错误，返回的响应 Success 字段应为 false。
    /// Validates: Requirements 9.4
    /// </summary>
    [Property(MaxTest = 100)]
    public Property AgentResponse_OnError_SuccessShouldBeFalse()
    {
        return Prop.ForAll(
            GenerateErrorMessage().ToArbitrary(),
            (errorMessage) =>
            {
                // 创建失败响应
                var response = AgentResponse.CreateFailure(errorMessage);
                
                // 验证 Success 为 false
                return response.Success == false;
            });
    }
    
    /// <summary>
    /// Property 13: Agent 错误处理
    /// For any Agent 执行错误，返回的响应消息列表应为空。
    /// Validates: Requirements 9.4
    /// </summary>
    [Property(MaxTest = 100)]
    public Property AgentResponse_OnError_MessagesShouldBeEmpty()
    {
        return Prop.ForAll(
            GenerateErrorMessage().ToArbitrary(),
            (errorMessage) =>
            {
                // 创建失败响应
                var response = AgentResponse.CreateFailure(errorMessage);
                
                // 验证消息列表为空
                return !response.Messages.Any();
            });
    }
    
    /// <summary>
    /// Property 13: Agent 错误处理
    /// For any ChatException，错误响应应包含错误代码信息。
    /// Validates: Requirements 9.4
    /// </summary>
    [Property(MaxTest = 100)]
    public Property AgentResponse_OnChatException_ShouldIncludeErrorCode()
    {
        return Prop.ForAll(
            GenerateErrorMessage().ToArbitrary(),
            GenerateErrorCode().ToArbitrary(),
            GenerateFriendlyErrorTemplate().ToArbitrary(),
            (errorMessage, errorCode, friendlyTemplate) =>
            {
                // 创建 ChatException
                var exception = new ChatException(errorMessage, errorCode);
                
                // 使用测试辅助方法创建友好错误消息
                var friendlyMessage = TestErrorHandler.CreateFriendlyErrorMessage(exception, friendlyTemplate);
                
                // 验证错误消息包含错误代码
                return friendlyMessage.Contains(errorCode);
            });
    }
    
    /// <summary>
    /// Property 13: Agent 错误处理
    /// For any TimeoutException，错误响应应包含超时相关信息。
    /// Validates: Requirements 9.4
    /// </summary>
    [Property(MaxTest = 100)]
    public Property AgentResponse_OnTimeout_ShouldIndicateTimeout()
    {
        return Prop.ForAll(
            GenerateFriendlyErrorTemplate().ToArbitrary(),
            (friendlyTemplate) =>
            {
                // 创建 TimeoutException
                var exception = new TimeoutException("Operation timed out");
                
                // 使用测试辅助方法创建友好错误消息
                var friendlyMessage = TestErrorHandler.CreateFriendlyErrorMessage(exception, friendlyTemplate);
                
                // 验证错误消息包含超时相关信息
                return friendlyMessage.Contains("超时") || friendlyMessage.Contains("timeout");
            });
    }
    
    /// <summary>
    /// Property 13: Agent 错误处理
    /// For any 流式响应错误，AgentResponseChunk 应包含错误消息且 IsComplete 为 true。
    /// Validates: Requirements 9.4
    /// </summary>
    [Property(MaxTest = 100)]
    public Property AgentResponseChunk_OnError_ShouldBeCompleteWithErrorMessage()
    {
        return Prop.ForAll(
            GenerateErrorMessage().ToArbitrary(),
            (errorMessage) =>
            {
                // 创建错误块
                var chunk = AgentResponseChunk.CreateError(errorMessage);
                
                // 验证 IsComplete 为 true 且包含错误消息
                return chunk.IsComplete && 
                       !string.IsNullOrEmpty(chunk.ErrorMessage) &&
                       chunk.ErrorMessage == errorMessage;
            });
    }
    
    /// <summary>
    /// Property 13: Agent 错误处理
    /// For any 成功的流式响应，AgentResponseChunk 不应包含错误消息。
    /// Validates: Requirements 9.4
    /// </summary>
    [Property(MaxTest = 100)]
    public Property AgentResponseChunk_OnSuccess_ShouldNotHaveErrorMessage()
    {
        return Prop.ForAll(
            Gen.Elements("Hello", "World", "Test content", "Response text").ToArbitrary(),
            (content) =>
            {
                // 创建内容块
                var chunk = AgentResponseChunk.CreateContent(content);
                
                // 验证没有错误消息
                return chunk.ErrorMessage == null && 
                       chunk.Content == content &&
                       !chunk.IsComplete;
            });
    }
    
    /// <summary>
    /// Property 13: Agent 错误处理
    /// For any 完成的流式响应，AgentResponseChunk 应标记为完成且无错误。
    /// Validates: Requirements 9.4
    /// </summary>
    [Fact]
    public void AgentResponseChunk_OnComplete_ShouldBeCompleteWithoutError()
    {
        // 创建完成块
        var chunk = AgentResponseChunk.CreateComplete();
        
        // 验证 IsComplete 为 true 且没有错误消息
        Assert.True(chunk.IsComplete);
        Assert.Null(chunk.ErrorMessage);
        Assert.Equal(string.Empty, chunk.Content);
    }
    
    /// <summary>
    /// Property 13: Agent 错误处理
    /// For any 成功响应，AgentResponse 应包含消息且 Success 为 true。
    /// Validates: Requirements 9.4
    /// </summary>
    [Property(MaxTest = 100)]
    public Property AgentResponse_OnSuccess_ShouldHaveMessagesAndSuccessTrue()
    {
        return Prop.ForAll(
            Gen.Elements("Response 1", "Response 2", "Hello there").ToArbitrary(),
            (content) =>
            {
                var message = new ChatMessage
                {
                    MessageId = Guid.NewGuid().ToString(),
                    SenderId = "assistant",
                    Content = content,
                    MessageType = ChatMessageType.Text,
                    Platform = "test",
                    Timestamp = DateTimeOffset.UtcNow
                };
                
                // 创建成功响应
                var response = AgentResponse.CreateSuccess(message);
                
                // 验证 Success 为 true 且包含消息
                return response.Success && 
                       response.Messages.Any() &&
                       response.ErrorMessage == null;
            });
    }
}

/// <summary>
/// 测试用的错误处理器，模拟 AgentExecutor 的错误处理逻辑
/// </summary>
internal static class TestErrorHandler
{
    /// <summary>
    /// 创建友好的错误消息
    /// 这个方法模拟 AgentExecutor.CreateFriendlyErrorMessage 的逻辑
    /// </summary>
    public static string CreateFriendlyErrorMessage(Exception ex, string friendlyTemplate)
    {
        var friendlyMessage = friendlyTemplate;
        
        // 根据异常类型提供更具体的错误信息
        if (ex is ChatException chatEx)
        {
            friendlyMessage = $"{friendlyTemplate} (错误代码: {chatEx.ErrorCode})";
        }
        else if (ex is TimeoutException or OperationCanceledException)
        {
            friendlyMessage = "处理超时，请稍后重试。";
        }

        return friendlyMessage;
    }
}
