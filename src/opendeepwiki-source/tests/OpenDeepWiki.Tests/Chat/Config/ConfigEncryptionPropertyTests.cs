using FsCheck;
using FsCheck.Fluent;
using FsCheck.Xunit;
using Microsoft.Extensions.Options;
using OpenDeepWiki.Chat.Config;
using Xunit;

namespace OpenDeepWiki.Tests.Chat.Config;

/// <summary>
/// Property-based tests for sensitive config encryption.
/// Feature: multi-platform-agent-chat, Property 17: 敏感配置加密
/// Validates: Requirements 11.4
/// </summary>
public class ConfigEncryptionPropertyTests
{
    /// <summary>
    /// 生成有效的配置数据
    /// </summary>
    private static Gen<string> GenerateValidConfigData()
    {
        return Gen.Elements(
            """{"ApiKey": "sk-test-key-123456789"}""",
            """{"AppId": "app123", "AppSecret": "secret-very-long-value-456"}""",
            """{"Token": "token789", "Password": "super-secret-password"}""",
            """{"ClientId": "client1", "ClientSecret": "secret1", "RefreshToken": "refresh123"}""",
            "Simple text config",
            "Another config value with special chars: !@#$%^&*()",
            """{"nested": {"key": "value", "secret": "hidden"}}"""
        );
    }
    
    /// <summary>
    /// 生成有效的加密密钥
    /// </summary>
    private static Gen<string> GenerateValidEncryptionKey()
    {
        return Gen.Elements(
            "test-encryption-key-32-bytes-long",
            "another-key-for-testing-purposes",
            "short-key",
            "very-long-encryption-key-that-exceeds-32-bytes-in-length"
        );
    }
    
    /// <summary>
    /// 创建加密服务
    /// </summary>
    private static AesConfigEncryption CreateEncryption(string key = "test-key")
    {
        var options = Options.Create(new ConfigEncryptionOptions
        {
            EncryptionKey = key
        });
        return new AesConfigEncryption(options);
    }

    /// <summary>
    /// Property 17: 敏感配置加密
    /// For any 包含敏感信息的配置，加密后的值应与原始值不同。
    /// Validates: Requirements 11.4
    /// </summary>
    [Property(MaxTest = 100)]
    public Property ConfigEncryption_EncryptedValue_ShouldDifferFromOriginal()
    {
        return Prop.ForAll(
            GenerateValidConfigData().ToArbitrary(),
            configData =>
            {
                var encryption = CreateEncryption();
                
                var encrypted = encryption.Encrypt(configData);
                
                // 加密后的值应与原始值不同
                return encrypted != configData;
            });
    }
    
    /// <summary>
    /// Property 17: 敏感配置加密
    /// For any 加密后的配置，解密后应与原始值一致。
    /// Validates: Requirements 11.4
    /// </summary>
    [Property(MaxTest = 100)]
    public Property ConfigEncryption_DecryptedValue_ShouldMatchOriginal()
    {
        return Prop.ForAll(
            GenerateValidConfigData().ToArbitrary(),
            configData =>
            {
                var encryption = CreateEncryption();
                
                var encrypted = encryption.Encrypt(configData);
                var decrypted = encryption.Decrypt(encrypted);
                
                // 解密后应与原始值一致
                return decrypted == configData;
            });
    }
    
    /// <summary>
    /// Property 17: 敏感配置加密
    /// For any 加密后的配置，IsEncrypted 应返回 true。
    /// Validates: Requirements 11.4
    /// </summary>
    [Property(MaxTest = 100)]
    public Property ConfigEncryption_EncryptedValue_ShouldBeDetectedAsEncrypted()
    {
        return Prop.ForAll(
            GenerateValidConfigData().ToArbitrary(),
            configData =>
            {
                var encryption = CreateEncryption();
                
                var encrypted = encryption.Encrypt(configData);
                
                // 加密后的值应被检测为已加密
                return encryption.IsEncrypted(encrypted);
            });
    }
    
    /// <summary>
    /// Property 17: 敏感配置加密
    /// For any 未加密的配置，IsEncrypted 应返回 false。
    /// Validates: Requirements 11.4
    /// </summary>
    [Property(MaxTest = 100)]
    public Property ConfigEncryption_PlainValue_ShouldNotBeDetectedAsEncrypted()
    {
        return Prop.ForAll(
            GenerateValidConfigData().ToArbitrary(),
            configData =>
            {
                var encryption = CreateEncryption();
                
                // 未加密的值不应被检测为已加密
                return !encryption.IsEncrypted(configData);
            });
    }
    
    /// <summary>
    /// Property 17: 敏感配置加密
    /// For any 已加密的配置，重复加密应返回相同的值（幂等性）。
    /// Validates: Requirements 11.4
    /// </summary>
    [Property(MaxTest = 100)]
    public Property ConfigEncryption_DoubleEncryption_ShouldBeIdempotent()
    {
        return Prop.ForAll(
            GenerateValidConfigData().ToArbitrary(),
            configData =>
            {
                var encryption = CreateEncryption();
                
                var encrypted1 = encryption.Encrypt(configData);
                var encrypted2 = encryption.Encrypt(encrypted1);
                
                // 重复加密应返回相同的值
                return encrypted1 == encrypted2;
            });
    }
    
    /// <summary>
    /// Property 17: 敏感配置加密
    /// For any 不同的加密密钥，加密结果应不同。
    /// Validates: Requirements 11.4
    /// </summary>
    [Property(MaxTest = 100)]
    public Property ConfigEncryption_DifferentKeys_ShouldProduceDifferentResults()
    {
        return Prop.ForAll(
            GenerateValidConfigData().ToArbitrary(),
            configData =>
            {
                var encryption1 = CreateEncryption("key-one-for-testing");
                var encryption2 = CreateEncryption("key-two-for-testing");
                
                var encrypted1 = encryption1.Encrypt(configData);
                var encrypted2 = encryption2.Encrypt(configData);
                
                // 不同密钥加密的结果应不同
                return encrypted1 != encrypted2;
            });
    }
    
    /// <summary>
    /// Property 17: 敏感配置加密
    /// For any 使用相同密钥加密的配置，解密应成功。
    /// Validates: Requirements 11.4
    /// </summary>
    [Property(MaxTest = 100)]
    public Property ConfigEncryption_SameKey_ShouldDecryptSuccessfully()
    {
        return Prop.ForAll(
            GenerateValidConfigData().ToArbitrary(),
            GenerateValidEncryptionKey().ToArbitrary(),
            (configData, key) =>
            {
                var encryption = CreateEncryption(key);
                
                var encrypted = encryption.Encrypt(configData);
                var decrypted = encryption.Decrypt(encrypted);
                
                return decrypted == configData;
            });
    }
    
    /// <summary>
    /// Property 17: 敏感配置加密
    /// For any 空字符串，加密和解密应返回空字符串。
    /// Validates: Requirements 11.4
    /// </summary>
    [Fact]
    public void ConfigEncryption_EmptyString_ShouldReturnEmptyString()
    {
        var encryption = CreateEncryption();
        
        var encrypted = encryption.Encrypt(string.Empty);
        var decrypted = encryption.Decrypt(string.Empty);
        
        Assert.Equal(string.Empty, encrypted);
        Assert.Equal(string.Empty, decrypted);
    }
    
    /// <summary>
    /// Property 17: 敏感配置加密
    /// For any null 值，加密和解密应返回 null。
    /// Validates: Requirements 11.4
    /// </summary>
    [Fact]
    public void ConfigEncryption_NullValue_ShouldReturnNull()
    {
        var encryption = CreateEncryption();
        
        var encrypted = encryption.Encrypt(null!);
        var decrypted = encryption.Decrypt(null!);
        
        Assert.Null(encrypted);
        Assert.Null(decrypted);
    }
    
    /// <summary>
    /// Property 17: 敏感配置加密
    /// 验证加密后的值以 "ENC:" 前缀开头。
    /// Validates: Requirements 11.4
    /// </summary>
    [Property(MaxTest = 100)]
    public Property ConfigEncryption_EncryptedValue_ShouldHavePrefix()
    {
        return Prop.ForAll(
            GenerateValidConfigData().ToArbitrary(),
            configData =>
            {
                var encryption = CreateEncryption();
                
                var encrypted = encryption.Encrypt(configData);
                
                // 加密后的值应以 "ENC:" 前缀开头
                return encrypted.StartsWith("ENC:");
            });
    }
}
