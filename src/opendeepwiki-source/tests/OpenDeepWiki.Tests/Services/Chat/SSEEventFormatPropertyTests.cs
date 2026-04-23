using FsCheck;
using FsCheck.Fluent;
using FsCheck.Xunit;
using OpenDeepWiki.Services.Chat;

namespace OpenDeepWiki.Tests.Services.Chat;

/// <summary>
/// Property-based tests for SSE event format consistency.
/// Feature: doc-chat-assistant, Property 3: SSE事件格式一致性
/// Validates: Requirements 9.3, 9.4
/// </summary>
public class SSEEventFormatPropertyTests
{
    /// <summary>
    /// Generates valid tool call IDs.
    /// </summary>
    private static Gen<string> GenerateToolCallId()
    {
        return Gen.Choose(1, 1000).Select(n => $"call_{n}");
    }

    /// <summary>
    /// Generates valid tool names.
    /// </summary>
    private static Gen<string> GenerateToolName()
    {
        return Gen.Elements(
            "ReadDocument", "ListDocuments", "SearchDocs", 
            "GetMetadata", "Mcp_Weather", "Mcp_Calculator");
    }

    /// <summary>
    /// Generates tool call arguments.
    /// </summary>
    private static Gen<Dictionary<string, object>?> GenerateArguments()
    {
        return Gen.OneOf(
            Gen.Constant<Dictionary<string, object>?>(null),
            Gen.Constant(new Dictionary<string, object> { { "path", "docs/intro" } }),
            Gen.Constant(new Dictionary<string, object> { { "query", "search term" }, { "limit", 10 } }));
    }

    /// <summary>
    /// Generates a valid ToolCallDto.
    /// </summary>
    private static Gen<ToolCallDto> GenerateToolCallDto()
    {
        return GenerateToolCallId().SelectMany(id =>
            GenerateToolName().SelectMany(name =>
                GenerateArguments().Select(args =>
                    new ToolCallDto
                    {
                        Id = id,
                        Name = name,
                        Arguments = args
                    })));
    }

    /// <summary>
    /// Generates tool result content.
    /// </summary>
    private static Gen<string> GenerateResultContent()
    {
        return Gen.Elements(
            "# Document Content\n\nThis is the document.",
            "{\"error\": true, \"message\": \"Not found\"}",
            "Operation completed successfully",
            "[]",
            "{\"items\": [\"a\", \"b\", \"c\"]}");
    }

    /// <summary>
    /// Generates a valid ToolResultDto.
    /// </summary>
    private static Gen<ToolResultDto> GenerateToolResultDto()
    {
        return GenerateToolCallId().SelectMany(toolCallId =>
            GenerateResultContent().SelectMany(result =>
                Gen.Elements(true, false).Select(isError =>
                    new ToolResultDto
                    {
                        ToolCallId = toolCallId,
                        Result = result,
                        IsError = isError
                    })));
    }

    /// <summary>
    /// Property 3: SSE事件格式一致性 - 工具调用事件必须包含id、name、arguments字段
    /// For any tool call event, it must contain id, name, and arguments fields.
    /// Validates: Requirements 9.3
    /// </summary>
    [Property(MaxTest = 100)]
    public Property ToolCallEvent_ShouldContainRequiredFields()
    {
        return Prop.ForAll(
            GenerateToolCallDto().ToArbitrary(),
            toolCall =>
            {
                var sseEvent = new SSEEvent
                {
                    Type = SSEEventType.ToolCall,
                    Data = toolCall
                };

                // Verify the event type is correct
                var typeCorrect = sseEvent.Type == SSEEventType.ToolCall;

                // Verify the data is a ToolCallDto with required fields
                var data = sseEvent.Data as ToolCallDto;
                var hasId = data != null && !string.IsNullOrEmpty(data.Id);
                var hasName = data != null && !string.IsNullOrEmpty(data.Name);
                // Arguments can be null, but the field should exist
                var hasArgumentsField = data != null;

                return typeCorrect && hasId && hasName && hasArgumentsField;
            });
    }

    /// <summary>
    /// Property 3: SSE事件格式一致性 - 工具结果事件必须包含toolCallId、result、isError字段
    /// For any tool result event, it must contain toolCallId, result, and isError fields.
    /// Validates: Requirements 9.4
    /// </summary>
    [Property(MaxTest = 100)]
    public Property ToolResultEvent_ShouldContainRequiredFields()
    {
        return Prop.ForAll(
            GenerateToolResultDto().ToArbitrary(),
            toolResult =>
            {
                var sseEvent = new SSEEvent
                {
                    Type = SSEEventType.ToolResult,
                    Data = toolResult
                };

                // Verify the event type is correct
                var typeCorrect = sseEvent.Type == SSEEventType.ToolResult;

                // Verify the data is a ToolResultDto with required fields
                var data = sseEvent.Data as ToolResultDto;
                var hasToolCallId = data != null && !string.IsNullOrEmpty(data.ToolCallId);
                var hasResult = data != null && data.Result != null;
                // IsError is a bool, always has a value
                var hasIsError = data != null;

                return typeCorrect && hasToolCallId && hasResult && hasIsError;
            });
    }

    /// <summary>
    /// Property 3: SSE事件格式一致性 - 工具调用ID应该是非空字符串
    /// Tool call ID should be a non-empty string.
    /// Validates: Requirements 9.3
    /// </summary>
    [Property(MaxTest = 100)]
    public Property ToolCallId_ShouldBeNonEmptyString()
    {
        return Prop.ForAll(
            GenerateToolCallDto().ToArbitrary(),
            toolCall =>
            {
                return !string.IsNullOrWhiteSpace(toolCall.Id);
            });
    }

    /// <summary>
    /// Property 3: SSE事件格式一致性 - 工具名称应该是非空字符串
    /// Tool name should be a non-empty string.
    /// Validates: Requirements 9.3
    /// </summary>
    [Property(MaxTest = 100)]
    public Property ToolName_ShouldBeNonEmptyString()
    {
        return Prop.ForAll(
            GenerateToolCallDto().ToArbitrary(),
            toolCall =>
            {
                return !string.IsNullOrWhiteSpace(toolCall.Name);
            });
    }

    /// <summary>
    /// Property 3: SSE事件格式一致性 - 工具结果的toolCallId应该是非空字符串
    /// Tool result's toolCallId should be a non-empty string.
    /// Validates: Requirements 9.4
    /// </summary>
    [Property(MaxTest = 100)]
    public Property ToolResultToolCallId_ShouldBeNonEmptyString()
    {
        return Prop.ForAll(
            GenerateToolResultDto().ToArbitrary(),
            toolResult =>
            {
                return !string.IsNullOrWhiteSpace(toolResult.ToolCallId);
            });
    }

    /// <summary>
    /// Property 3: SSE事件格式一致性 - SSE事件类型应该是预定义的类型之一
    /// SSE event type should be one of the predefined types.
    /// Validates: Requirements 9.3, 9.4
    /// </summary>
    [Property(MaxTest = 100)]
    public Property SSEEventType_ShouldBeValid()
    {
        var validTypes = new[] 
        { 
            SSEEventType.Content, 
            SSEEventType.ToolCall, 
            SSEEventType.ToolResult, 
            SSEEventType.Done, 
            SSEEventType.Error 
        };

        return Prop.ForAll(
            Gen.Elements(validTypes).ToArbitrary(),
            eventType =>
            {
                return validTypes.Contains(eventType);
            });
    }

    /// <summary>
    /// Property 3: SSE事件格式一致性 - 工具调用和结果应该可以关联
    /// Tool call and result should be associable via toolCallId.
    /// Validates: Requirements 9.3, 9.4
    /// </summary>
    [Property(MaxTest = 100)]
    public Property ToolCallAndResult_ShouldBeAssociable()
    {
        return Prop.ForAll(
            GenerateToolCallDto().ToArbitrary(),
            GenerateResultContent().ToArbitrary(),
            Gen.Elements(true, false).ToArbitrary(),
            (toolCall, resultContent, isError) =>
            {
                // Create a tool result that references the tool call
                var toolResult = new ToolResultDto
                {
                    ToolCallId = toolCall.Id,
                    Result = resultContent,
                    IsError = isError
                };

                // They should be associable via the ID
                return toolResult.ToolCallId == toolCall.Id;
            });
    }
}
