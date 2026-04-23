using FsCheck;
using FsCheck.Fluent;
using FsCheck.Xunit;
using OpenDeepWiki.Services.Chat;

namespace OpenDeepWiki.Tests.Services.Chat;

/// <summary>
/// Property-based tests for EmbedService domain validation.
/// Feature: doc-chat-assistant, Property 8: 域名校验正确性
/// Validates: Requirements 14.7, 17.2
/// </summary>
public class EmbedServiceDomainValidationPropertyTests
{
    /// <summary>
    /// Property 8: 域名校验正确性 - 精确匹配的域名应该被允许
    /// For any domain that exactly matches an allowed domain, it should be allowed.
    /// Validates: Requirements 14.7, 17.2
    /// </summary>
    [Property(MaxTest = 100)]
    public Property ExactDomainMatch_ShouldBeAllowed()
    {
        return Prop.ForAll(
            DomainGenerators.ValidDomainArb(),
            domain =>
            {
                var result = EmbedService.IsDomainMatch(domain, domain);
                return result.Label($"Domain '{domain}' should match itself");
            });
    }

    /// <summary>
    /// Property 8: 域名校验正确性 - 通配符模式应该匹配子域名
    /// For any subdomain of a base domain, a wildcard pattern should match.
    /// Validates: Requirements 14.7, 17.2
    /// </summary>
    [Property(MaxTest = 100)]
    public Property WildcardPattern_ShouldMatchSubdomains()
    {
        return Prop.ForAll(
            DomainGenerators.SubdomainArb(),
            DomainGenerators.BaseDomainArb(),
            (subdomain, baseDomain) =>
            {
                var fullDomain = $"{subdomain}.{baseDomain}";
                var pattern = $"*.{baseDomain}";
                
                var result = EmbedService.IsDomainMatch(fullDomain, pattern);
                return result.Label($"'{fullDomain}' should match pattern '{pattern}'");
            });
    }

    /// <summary>
    /// Property 8: 域名校验正确性 - 通配符模式也应该匹配基础域名本身
    /// For a wildcard pattern *.example.com, example.com should also match.
    /// Validates: Requirements 14.7, 17.2
    /// </summary>
    [Property(MaxTest = 100)]
    public Property WildcardPattern_ShouldMatchBaseDomain()
    {
        return Prop.ForAll(
            DomainGenerators.BaseDomainArb(),
            baseDomain =>
            {
                var pattern = $"*.{baseDomain}";
                
                var result = EmbedService.IsDomainMatch(baseDomain, pattern);
                return result.Label($"'{baseDomain}' should match pattern '{pattern}'");
            });
    }

    /// <summary>
    /// Property 8: 域名校验正确性 - 不匹配的域名应该被拒绝
    /// For any domain that doesn't match the pattern, it should be rejected.
    /// Validates: Requirements 14.7, 17.2
    /// </summary>
    [Property(MaxTest = 100)]
    public Property NonMatchingDomain_ShouldBeRejected()
    {
        return Prop.ForAll(
            DomainGenerators.BaseDomainArb(),
            DomainGenerators.DifferentBaseDomainArb(),
            (domain1, domain2) =>
            {
                // Ensure domains are actually different
                if (domain1 == domain2)
                {
                    return true.Label("Skipped - same domain");
                }

                var result = !EmbedService.IsDomainMatch(domain1, domain2);
                return result.Label($"'{domain1}' should NOT match '{domain2}'");
            });
    }

    /// <summary>
    /// Property 8: 域名校验正确性 - 带协议的域名应该被正确处理
    /// For any domain with protocol prefix, it should still match correctly.
    /// Validates: Requirements 14.7, 17.2
    /// </summary>
    [Property(MaxTest = 100)]
    public Property DomainWithProtocol_ShouldBeNormalized()
    {
        return Prop.ForAll(
            DomainGenerators.ValidDomainArb(),
            Gen.Elements("https://", "http://").ToArbitrary(),
            (domain, protocol) =>
            {
                var domainWithProtocol = $"{protocol}{domain}";
                
                // The pattern without protocol should match domain with protocol
                // Note: IsDomainMatch normalizes both inputs
                var result = EmbedService.IsDomainMatch(domainWithProtocol, domain);
                return result.Label($"'{domainWithProtocol}' should match '{domain}'");
            });
    }

    /// <summary>
    /// Property 8: 域名校验正确性 - 空域名或空模式应该返回false
    /// For any empty domain or pattern, the match should return false.
    /// Validates: Requirements 14.7, 17.2
    /// </summary>
    [Property(MaxTest = 100)]
    public Property EmptyDomainOrPattern_ShouldReturnFalse()
    {
        return Prop.ForAll(
            DomainGenerators.ValidDomainArb(),
            Gen.Elements("", null, "   ").ToArbitrary(),
            (validDomain, emptyValue) =>
            {
                var result1 = !EmbedService.IsDomainMatch(emptyValue!, validDomain);
                var result2 = !EmbedService.IsDomainMatch(validDomain, emptyValue!);
                
                return (result1 && result2).Label("Empty domain or pattern should not match");
            });
    }

    /// <summary>
    /// Property 8: 域名校验正确性 - 大小写不敏感匹配
    /// For any domain, matching should be case-insensitive.
    /// Validates: Requirements 14.7, 17.2
    /// </summary>
    [Property(MaxTest = 100)]
    public Property DomainMatching_ShouldBeCaseInsensitive()
    {
        return Prop.ForAll(
            DomainGenerators.ValidDomainArb(),
            domain =>
            {
                var upperDomain = domain.ToUpperInvariant();
                var lowerDomain = domain.ToLowerInvariant();
                
                var result = EmbedService.IsDomainMatch(upperDomain, lowerDomain);
                return result.Label($"'{upperDomain}' should match '{lowerDomain}' (case-insensitive)");
            });
    }

    /// <summary>
    /// Property 8: 域名校验正确性 - 带端口的域名应该被正确处理
    /// For any domain with port, it should still match correctly.
    /// Validates: Requirements 14.7, 17.2
    /// </summary>
    [Property(MaxTest = 100)]
    public Property DomainWithPort_ShouldBeNormalized()
    {
        return Prop.ForAll(
            DomainGenerators.ValidDomainArb(),
            Gen.Choose(1, 65535).ToArbitrary(),
            (domain, port) =>
            {
                var domainWithPort = $"{domain}:{port}";
                
                var result = EmbedService.IsDomainMatch(domainWithPort, domain);
                return result.Label($"'{domainWithPort}' should match '{domain}'");
            });
    }
}


/// <summary>
/// Generators for domain-related test data.
/// </summary>
public static class DomainGenerators
{
    private static readonly string[] TopLevelDomains = { "com", "org", "net", "io", "dev", "app", "co" };
    private static readonly string[] DomainNames = { "example", "test", "mysite", "webapp", "api", "docs", "app" };
    private static readonly string[] Subdomains = { "www", "api", "docs", "app", "admin", "dev", "staging" };

    /// <summary>
    /// Generates a valid base domain (e.g., example.com).
    /// </summary>
    public static Arbitrary<string> BaseDomainArb()
    {
        return Gen.Elements(DomainNames)
            .SelectMany(name => Gen.Elements(TopLevelDomains).Select(tld => $"{name}.{tld}"))
            .ToArbitrary();
    }

    /// <summary>
    /// Generates a different base domain for non-matching tests.
    /// </summary>
    public static Arbitrary<string> DifferentBaseDomainArb()
    {
        return Gen.Elements(DomainNames)
            .SelectMany(name => Gen.Elements(TopLevelDomains).Select(tld => $"other{name}.{tld}"))
            .ToArbitrary();
    }

    /// <summary>
    /// Generates a subdomain prefix.
    /// </summary>
    public static Arbitrary<string> SubdomainArb()
    {
        return Gen.Elements(Subdomains).ToArbitrary();
    }

    /// <summary>
    /// Generates a valid domain (base domain or with subdomain).
    /// </summary>
    public static Arbitrary<string> ValidDomainArb()
    {
        var baseDomainGen = Gen.Elements(DomainNames)
            .SelectMany(name => Gen.Elements(TopLevelDomains).Select(tld => $"{name}.{tld}"));

        var subdomainGen = Gen.Elements(Subdomains)
            .SelectMany(sub => baseDomainGen.Select(baseDomain => $"{sub}.{baseDomain}"));

        return Gen.OneOf(baseDomainGen, subdomainGen).ToArbitrary();
    }
}
