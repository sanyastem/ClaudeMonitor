using ClaudeMonitor.Services;

namespace ClaudeMonitor.Tests;

public class UpdateCheckerSecurityTests
{
    [Theory]
    [InlineData("https://api.github.com/repos/sanyastem/ClaudeMonitor/releases/assets/123")]
    [InlineData("https://API.GITHUB.COM/repos/x/y/releases/assets/1")]
    public void IsTrustedAssetUrl_AcceptsApiGithubHttps(string url)
    {
        Assert.True(UpdateChecker.IsTrustedAssetUrl(url));
    }

    [Theory]
    [InlineData("http://api.github.com/repos/x/y/releases/assets/1")]          // wrong scheme
    [InlineData("https://api.github.com.evil.com/x")]                          // host suffix attack
    [InlineData("https://github.com/sanyastem/ClaudeMonitor/releases/x.exe")]  // browser_download_url
    [InlineData("https://objects.githubusercontent.com/x")]                    // CDN, not the API
    [InlineData("https://evil.com/api.github.com/x")]                          // host substring
    [InlineData("ftp://api.github.com/x")]                                     // non-http(s)
    [InlineData("api.github.com/x")]                                           // not absolute
    [InlineData("")]                                                            // empty
    [InlineData(" ")]                                                           // whitespace
    public void IsTrustedAssetUrl_RejectsEverythingElse(string url)
    {
        Assert.False(UpdateChecker.IsTrustedAssetUrl(url));
    }

    [Fact]
    public void ExtractSha256_BareLine()
    {
        var body = "Release notes\n\nSHA256: 0123456789abcdef0123456789abcdef0123456789abcdef0123456789abcdef\n";
        Assert.Equal(
            "0123456789abcdef0123456789abcdef0123456789abcdef0123456789abcdef",
            UpdateChecker.ExtractSha256(body));
    }

    [Fact]
    public void ExtractSha256_Blockquote()
    {
        var body = "> SHA256: ABCDEF0123456789ABCDEF0123456789ABCDEF0123456789ABCDEF0123456789";
        Assert.Equal(
            "abcdef0123456789abcdef0123456789abcdef0123456789abcdef0123456789",
            UpdateChecker.ExtractSha256(body));
    }

    [Fact]
    public void ExtractSha256_LineWithBackticks()
    {
        // GitHub release notes commonly use a fenced code block with the hash on its own line.
        var body = "Integrity:\n\n`SHA256: fedcba9876543210fedcba9876543210fedcba9876543210fedcba9876543210`\n";
        Assert.Equal(
            "fedcba9876543210fedcba9876543210fedcba9876543210fedcba9876543210",
            UpdateChecker.ExtractSha256(body));
    }

    [Fact]
    public void ExtractSha256_BulletItem()
    {
        var body = "- SHA256: 2222222222222222222222222222222222222222222222222222222222222222";
        Assert.Equal(
            "2222222222222222222222222222222222222222222222222222222222222222",
            UpdateChecker.ExtractSha256(body));
    }

    [Fact]
    public void ExtractSha256_DashCase()
    {
        var body = "SHA-256 = 1111111111111111111111111111111111111111111111111111111111111111";
        Assert.Equal(
            "1111111111111111111111111111111111111111111111111111111111111111",
            UpdateChecker.ExtractSha256(body));
    }

    [Theory]
    [InlineData("no hash here")]
    [InlineData("SHA1: 0123456789abcdef0123456789abcdef0123456789abcdef0123456789abcdef")]
    [InlineData("SHA256: 0123456789abcdef0123456789abcdef0123456789abcdef0123456789abcde")]   // 63 chars
    [InlineData("SHA256: 0123456789abcdef0123456789abcdef0123456789abcdef0123456789abcdefz")] // 65 chars
    [InlineData("SHA256: xxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxx")]  // non-hex
    [InlineData("")]
    [InlineData(null)]
    public void ExtractSha256_RejectsInvalid(string? body)
    {
        Assert.Equal("", UpdateChecker.ExtractSha256(body ?? ""));
    }

    [Fact]
    public void ExtractSha256_FirstHashWins_WhenMultiple()
    {
        var body = @"
## v1.0.4
SHA256: aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa

## v1.0.3
SHA256: bbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbb
";
        var hash = UpdateChecker.ExtractSha256(body);
        Assert.Equal("aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa", hash);
    }
}
