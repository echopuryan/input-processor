using BusinessLayer.Channels;
using BusinessLayer.Infrastructure;
using BusinessLayer.Models;
using BusinessLayer.Services;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;
using Moq;
using System.Collections.Concurrent;
using System.Runtime.CompilerServices;
using System.Text;

namespace BusinessLayer.Tests.Services;

[TestClass]
public class InputProcessingServiceTests
{
    private Mock<ILogger<InputProcessingService>> _loggerMock = new()!;
    private Mock<IDataProcessingRequestChannel> _jobRequestChannelMock = new()!;
    private Mock<IDataProcessingChannel> _dataProcessingChannelMock = new()!;
    private Mock<IJobManager> _jobManagerMock = new()!;
    private IMemoryCache _memoryCache = null!;
    private InputProcessingService _sut = null!;

    [TestInitialize]
    public void Setup()
    {
        _memoryCache = new MemoryCache(new MemoryCacheOptions());

        _sut = new InputProcessingService(
            _loggerMock.Object,
            _jobRequestChannelMock.Object,
            _dataProcessingChannelMock.Object,
            _jobManagerMock.Object,
            _memoryCache);
    }

    [TestCleanup]
    public void Cleanup()
    {
        _memoryCache.Dispose();
    }

    #region StartProcessingAsync

    [TestMethod]
    public async Task StartProcessingAsync_ValidInput_ReturnsJobIdAndWritesToChannel()
    {
        // Arrange
        var input = "hello";
        var username = "user1";
        DataProcessingRequest? capturedRequest = null;

        _jobRequestChannelMock
            .Setup(x => x.WriteAsync(It.IsAny<DataProcessingRequest>(), It.IsAny<CancellationToken>()))
            .Callback<DataProcessingRequest, CancellationToken>((req, _) => capturedRequest = req)
            .Returns(ValueTask.CompletedTask);

        // Act
        var jobId = await _sut.StartProcessingAsync(input, username, CancellationToken.None);

        // Assert
        Assert.AreNotEqual(Guid.Empty, jobId);
        Assert.IsNotNull(capturedRequest);
        Assert.AreEqual(jobId, capturedRequest.Id);
        Assert.IsTrue(capturedRequest.UserInput.Contains("/")); // contains base64 separator
        _jobRequestChannelMock.Verify(
            x => x.WriteAsync(It.IsAny<DataProcessingRequest>(), It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [TestMethod]
    public async Task StartProcessingAsync_EmptyInput_ThrowsArgumentException()
    {
        // Act & Assert
        await Assert.ThrowsAsync<ArgumentException>(
            () => _sut.StartProcessingAsync("", "user1", CancellationToken.None));
    }

    #endregion

    #region CancelProcessingRequest

    [TestMethod]
    public async Task CancelProcessingRequest_Owner_CancelsSuccessfully()
    {
        // Arrange
        var username = "user1";

        _jobRequestChannelMock
            .Setup(x => x.WriteAsync(It.IsAny<DataProcessingRequest>(), It.IsAny<CancellationToken>()))
            .Returns(ValueTask.CompletedTask);

        _jobManagerMock.Setup(x => x.Cancel(It.IsAny<Guid>())).Returns(true);

        var jobId = await _sut.StartProcessingAsync("test", username, CancellationToken.None);

        // Act
        var result = await _sut.CancelProcessingRequest(jobId, username, CancellationToken.None);

        // Assert
        Assert.IsTrue(result);
        _jobManagerMock.Verify(x => x.Cancel(jobId), Times.Once);
    }

    [TestMethod]
    public async Task CancelProcessingRequest_NonOwner_ReturnsFalse()
    {
        // Arrange
        _jobRequestChannelMock
            .Setup(x => x.WriteAsync(It.IsAny<DataProcessingRequest>(), It.IsAny<CancellationToken>()))
            .Returns(ValueTask.CompletedTask);

        var jobId = await _sut.StartProcessingAsync("test", "user1", CancellationToken.None);

        // Act
        var result = await _sut.CancelProcessingRequest(jobId, "attacker", CancellationToken.None);

        // Assert
        Assert.IsFalse(result);
        _jobManagerMock.Verify(x => x.Cancel(It.IsAny<Guid>()), Times.Never);
    }

    #endregion

    #region GetProcessedInputEventsAsync

    [TestMethod]
    public async Task GetProcessedInputEventsAsync_NonOwner_YieldsNoEvents()
    {
        // Arrange
        _jobRequestChannelMock
            .Setup(x => x.WriteAsync(It.IsAny<DataProcessingRequest>(), It.IsAny<CancellationToken>()))
            .Returns(ValueTask.CompletedTask);

        var jobId = await _sut.StartProcessingAsync("test", "user1", CancellationToken.None);

        // Act
        var events = new List<ProcessedInputEvent>();
        await foreach (var e in _sut.GetProcessedInputEventsAsync(jobId, -1, "attacker", CancellationToken.None))
        {
            events.Add(e);
        }

        // Assert
        Assert.AreEqual(0, events.Count);
    }

    [TestMethod]
    public async Task GetProcessedInputEventsAsync_Owner_ReceivesEventsFromChannel()
    {
        // Arrange
        var username = "user1";

        _jobRequestChannelMock
            .Setup(x => x.WriteAsync(It.IsAny<DataProcessingRequest>(), It.IsAny<CancellationToken>()))
            .Returns(ValueTask.CompletedTask);

        var jobId = await _sut.StartProcessingAsync("ab", username, CancellationToken.None);

        var channelEvents = new List<ProcessedInputEvent>
        {
            new() { Id = 0, RequestId = jobId, Progress = 50, Data = 'a', IsCompleted = false },
            new() { Id = 1, RequestId = jobId, Progress = 100, Data = 'b', IsCompleted = true },
        };

        _dataProcessingChannelMock
            .Setup(x => x.ReadAllAsync(jobId, It.IsAny<CancellationToken>()))
            .Returns(ToAsyncEnumerable(channelEvents));

        // Act
        var events = new List<ProcessedInputEvent>();
        await foreach (var e in _sut.GetProcessedInputEventsAsync(jobId, -1, username, CancellationToken.None))
        {
            events.Add(e);
        }

        // Assert
        Assert.AreEqual(2, events.Count);
        Assert.AreEqual('a', events[0].Data);
        Assert.AreEqual('b', events[1].Data);
        Assert.IsTrue(events[1].IsCompleted);
    }

    [TestMethod]
    public async Task GetProcessedInputEventsAsync_WithCachedEvents_ReplaysMissedEvents()
    {
        // Arrange
        var username = "user1";
        var jobId = Guid.NewGuid();

        _jobRequestChannelMock
            .Setup(x => x.WriteAsync(It.IsAny<DataProcessingRequest>(), It.IsAny<CancellationToken>()))
            .Returns(ValueTask.CompletedTask);

        // Start a job to register ownership
        // We need to use reflection or start a real job - easier to just start one
        var actualJobId = await _sut.StartProcessingAsync("test", username, CancellationToken.None);

        // Pre-populate cache with events
        var cachedQueue = new ConcurrentQueue<ProcessedInputEvent>();
        cachedQueue.Enqueue(new ProcessedInputEvent { Id = 0, RequestId = actualJobId, Data = 'x' });
        cachedQueue.Enqueue(new ProcessedInputEvent { Id = 1, RequestId = actualJobId, Data = 'y' });
        cachedQueue.Enqueue(new ProcessedInputEvent { Id = 2, RequestId = actualJobId, Data = 'z' });
        _memoryCache.Set(actualJobId, cachedQueue);

        // Channel returns no new events
        _dataProcessingChannelMock
            .Setup(x => x.ReadAllAsync(actualJobId, It.IsAny<CancellationToken>()))
            .Returns(ToAsyncEnumerable(new List<ProcessedInputEvent>()));

        // Act - request events after Id 1 (should get only Id 2)
        var events = new List<ProcessedInputEvent>();
        await foreach (var e in _sut.GetProcessedInputEventsAsync(actualJobId, 1, username, CancellationToken.None))
        {
            events.Add(e);
        }

        // Assert
        Assert.AreEqual(1, events.Count);
        Assert.AreEqual('z', events[0].Data);
        Assert.AreEqual(2, events[0].Id);
    }

    #endregion

    #region Helpers

    private static async IAsyncEnumerable<T> ToAsyncEnumerable<T>(
        IEnumerable<T> items,
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        foreach (var item in items)
        {
            cancellationToken.ThrowIfCancellationRequested();
            yield return item;
            await Task.CompletedTask;
        }
    }

    #endregion
}
