using BusinessLayer.Services;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;
using Moq;
using System.Text;

namespace BusinessLayer.Tests.Services;

[TestClass]
public class InputProcessingServiceTests
{
    private InputProcessingService _service;
    private IMemoryCache _memoryCache;

    private readonly Mock<ILogger<InputProcessingService>> _loggerMock = new();

    [TestInitialize]
    public void Setup()
    {
        _memoryCache = new MemoryCache(new MemoryCacheOptions());
        _service = new InputProcessingService(_loggerMock.Object, _memoryCache);
    }

    [TestCleanup]
    public void Cleanup()
    {
        _memoryCache.Dispose();
    }

    [TestMethod]
    public async Task ProcessInputAsync_ReturnsCorrectlySortedOutput()
    {
        // Arrange
        var input = "hello";
        // h:1, e:1, l:2, o:1
        // Sorted by count asc, then char asc: e:1, h:1, o:1, l:2
        var expectedBase64 = Convert.ToBase64String(Encoding.UTF8.GetBytes(input));
        var expectedResult = $"e1h1o1l2/{expectedBase64}";

        // Act
        var result = await _service.ProcessInputAsync(input, CancellationToken.None);

        // Assert
        Assert.AreEqual(expectedResult, result);
    }

    [TestMethod]
    public async Task ProcessInputAsync_ReturnsCachedResult_OnSecondCall()
    {
        // Arrange
        var input = "test";

        // Act
        var firstResult = await _service.ProcessInputAsync(input, CancellationToken.None);
        var secondResult = await _service.ProcessInputAsync(input, CancellationToken.None);

        // Assert
        Assert.AreEqual(firstResult, secondResult);

        // Verify cache miss was logged only once (first call)
        _loggerMock.Verify(
            x => x.Log(
                LogLevel.Debug,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains("Cache miss")),
                null,
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()
            ),
            Times.Once
        );
    }

    [TestMethod]
    [DataRow(null)]
    [DataRow("")]
    public async Task ProcessInputAsync_ThrowsArgumentException_WhenInputIsNullOrEmpty(string? input)
    {
        // Act & Assert
        await Assert.ThrowsAsync<ArgumentException>(
            () => _service.ProcessInputAsync(input!, CancellationToken.None)
        );
    }

    [TestMethod]
    public async Task ProcessInputAsync_HandlesAllSameCharacters()
    {
        // Arrange
        var input = "aaa";
        // a:3
        var expectedBase64 = Convert.ToBase64String(Encoding.UTF8.GetBytes(input));
        var expectedResult = $"a3/{expectedBase64}";

        // Act
        var result = await _service.ProcessInputAsync(input, CancellationToken.None);

        // Assert
        Assert.AreEqual(expectedResult, result);
    }
}
