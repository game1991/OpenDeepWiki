using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using OpenDeepWiki.Chat.Abstractions;
using OpenDeepWiki.Chat.Providers;

namespace OpenDeepWiki.Tests.Chat.Providers;

/// <summary>
/// 测试用的 Provider 实现
/// </summary>
public class TestMessageProvider : BaseMessageProvider
{
    private readonly ISet<ChatMessageType> _supportedTypes;
    
    public override string PlatformId => "test";
    public override string DisplayName => "Test Provider";
    
    public TestMessageProvider(
        ILogger<TestMessageProvider> logger, 
        IOptions<ProviderOptions> options,
        ISet<ChatMessageType>? supportedTypes = null) 
        : base(logger, options)
    {
        _supportedTypes = supportedTypes ?? new HashSet<ChatMessageType> { ChatMessageType.Text };
    }
    
    public override Task<IChatMessage?> ParseMessageAsync(string rawMessage, CancellationToken cancellationToken = default)
    {
        return Task.FromResult<IChatMessage?>(new ChatMessage
        {
            Content = rawMessage,
            MessageType = ChatMessageType.Text,
            Platform = PlatformId
        });
    }
    
    public override Task<SendResult> SendMessageAsync(IChatMessage message, string targetUserId, CancellationToken cancellationToken = default)
    {
        return Task.FromResult(new SendResult(true, message.MessageId));
    }
    
    /// <summary>
    /// 公开 DegradeMessage 方法用于测试
    /// </summary>
    public IChatMessage TestDegradeMessage(IChatMessage message)
    {
        return DegradeMessage(message, _supportedTypes);
    }
}
