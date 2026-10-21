using System.Text.Json;
using Shouldly;
using SoundboardLite.Api;
using SoundboardLite.Core.Services;

namespace SoundboardLite.Api.Tests;

public sealed class ApiSessionFileTests : IDisposable
{
    private readonly string _tempRoot;
    private readonly AppPaths _appPaths;

    public ApiSessionFileTests()
    {
        _tempRoot = Path.Combine(Path.GetTempPath(), "SoundboardLiteApiTests_" + Guid.NewGuid());
        _appPaths = new AppPaths(_tempRoot);
    }

    public void Dispose() => Directory.Delete(_tempRoot, recursive: true);

    private string SessionPath => Path.Combine(_appPaths.RuntimeDirectory, "api-session.json");

    [Fact]
    public void Write_CreatesAFileMatchingTheDocumentedJsonShape()
    {
        var session = new ApiSession(51823, "abc123token", 4242, DateTimeOffset.Parse("2026-01-01T00:00:00Z"));

        ApiSessionFile.Write(_appPaths, session);

        File.Exists(SessionPath).ShouldBeTrue();

        using var doc = JsonDocument.Parse(File.ReadAllText(SessionPath));
        doc.RootElement.GetProperty("port").GetInt32().ShouldBe(51823);
        doc.RootElement.GetProperty("token").GetString().ShouldBe("abc123token");
        doc.RootElement.GetProperty("pid").GetInt32().ShouldBe(4242);
        doc.RootElement.TryGetProperty("startedUtc", out _).ShouldBeTrue();
    }

    [Fact]
    public void Write_DoesNotLeaveATempFileBehind()
    {
        ApiSessionFile.Write(_appPaths, new ApiSession(1, "t", 1, DateTimeOffset.UtcNow));

        Directory.GetFiles(_appPaths.RuntimeDirectory).ShouldBe([SessionPath]);
    }

    [Fact]
    public void Write_CalledTwice_OverwritesRatherThanErroring()
    {
        ApiSessionFile.Write(_appPaths, new ApiSession(1, "first", 1, DateTimeOffset.UtcNow));
        ApiSessionFile.Write(_appPaths, new ApiSession(2, "second", 2, DateTimeOffset.UtcNow));

        using var doc = JsonDocument.Parse(File.ReadAllText(SessionPath));
        doc.RootElement.GetProperty("token").GetString().ShouldBe("second");
    }

    [Fact]
    public void Delete_RemovesTheFile()
    {
        ApiSessionFile.Write(_appPaths, new ApiSession(1, "t", 1, DateTimeOffset.UtcNow));

        ApiSessionFile.Delete(_appPaths);

        File.Exists(SessionPath).ShouldBeFalse();
    }

    [Fact]
    public void Delete_WhenFileDoesNotExist_DoesNotThrow()
    {
        Should.NotThrow(() => ApiSessionFile.Delete(_appPaths));
    }
}
