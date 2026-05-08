using BusinessLayer.Channels;
using BusinessLayer.Infrastructure;
using BusinessLayer.Models;
using BusinessLayer.Services;
using BusinessLayer.Settings;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Moq;

namespace BusinessLayer.Tests.Services;

[TestClass]
public class DataProcessingJobTests
{
    private Mock<ILogger<DataProcessingJob>> _loggerMock = null!;
    private Mock<IDataProcessingChannel> _channelMock = null!;
    private Mock<IJobManager> _jobManagerMock = null!;
    private DataProcessingJob _sut = null!;

    [TestInitialize]
    public void Setup()
    {
        _loggerMock = new Mock<ILogger<DataProcessingJob>>();
        _channelMock = new Mock<IDataProcessingChannel>();
        _jobManagerMock = new Mock<IJobManager>();

        var appSettings = Options.Create(new AppSettings
        {
            RandomDelayRange = new RandomDelayRangeSettings { Min = 0, Max = 1 } // minimal delay for fast tests
        });

        _sut = new DataProcessingJob(
            _loggerMock.Object,
            appSettings,
            _channelMock.Object,
            _jobManagerMock.Object);
    }

    [TestMethod]
    public async Task ProcessAsync_ValidInput_PublishesEventForEachCharacter()
    {
        // Arrange
        var request = new DataProcessingRequest { Id = Guid.NewGuid(), UserInput = "abc" };
        var publishedEvents = new List<ProcessedInputEvent>();

        _jobManagerMock
            .Setup(x => x.Register(request.Id, It.IsAny<CancellationToken>()))
            .Returns(CancellationToken.None);

        _channelMock
            .Setup(x => x.PublishAsync(It.IsAny<ProcessedInputEvent>(), It.IsAny<CancellationToken>()))
            .Callback<ProcessedInputEvent, CancellationToken>((e, _) => publishedEvents.Add(e))
            .Returns(ValueTask.CompletedTask);

        // Act
        await _sut.ProcessAsync(request, CancellationToken.None);

        // Assert
        Assert.AreEqual(3, publishedEvents.Count);
        Assert.AreEqual('a', publishedEvents[0].Data);
        Assert.AreEqual('b', publishedEvents[1].Data);
        Assert.AreEqual('c', publishedEvents[2].Data);
    }

    [TestMethod]
    public async Task ProcessAsync_LastEvent_IsMarkedCompleted()
    {
        // Arrange
        var request = new DataProcessingRequest { Id = Guid.NewGuid(), UserInput = "ab" };
        var publishedEvents = new List<ProcessedInputEvent>();

        _jobManagerMock
            .Setup(x => x.Register(request.Id, It.IsAny<CancellationToken>()))
            .Returns(CancellationToken.None);

        _channelMock
            .Setup(x => x.PublishAsync(It.IsAny<ProcessedInputEvent>(), It.IsAny<CancellationToken>()))
            .Callback<ProcessedInputEvent, CancellationToken>((e, _) => publishedEvents.Add(e))
            .Returns(ValueTask.CompletedTask);

        // Act
        await _sut.ProcessAsync(request, CancellationToken.None);

        // Assert
        Assert.IsFalse(publishedEvents[0].IsCompleted);
        Assert.IsTrue(publishedEvents[1].IsCompleted);
    }

    [TestMethod]
    public async Task ProcessAsync_CalculatesProgressCorrectly()
    {
        // Arrange
        var request = new DataProcessingRequest { Id = Guid.NewGuid(), UserInput = "abcd" };
        var publishedEvents = new List<ProcessedInputEvent>();

        _jobManagerMock
            .Setup(x => x.Register(request.Id, It.IsAny<CancellationToken>()))
            .Returns(CancellationToken.None);

        _channelMock
            .Setup(x => x.PublishAsync(It.IsAny<ProcessedInputEvent>(), It.IsAny<CancellationToken>()))
            .Callback<ProcessedInputEvent, CancellationToken>((e, _) => publishedEvents.Add(e))
            .Returns(ValueTask.CompletedTask);

        // Act
        await _sut.ProcessAsync(request, CancellationToken.None);

        // Assert
        Assert.AreEqual(25, publishedEvents[0].Progress);  // 1/4 * 100
        Assert.AreEqual(50, publishedEvents[1].Progress);  // 2/4 * 100
        Assert.AreEqual(75, publishedEvents[2].Progress);  // 3/4 * 100
        Assert.AreEqual(100, publishedEvents[3].Progress); // 4/4 * 100
    }

    [TestMethod]
    public async Task ProcessAsync_UserCancelsJob_PublishesCancelledEvent()
    {
        // Arrange
        var request = new DataProcessingRequest { Id = Guid.NewGuid(), UserInput = "abcdef" };
        var publishedEvents = new List<ProcessedInputEvent>();

        // Simulate user cancellation: jobToken is cancelled, stoppingToken is NOT
        var jobCts = new CancellationTokenSource();
        jobCts.Cancel(); // pre-cancel the job token

        _jobManagerMock
            .Setup(x => x.Register(request.Id, It.IsAny<CancellationToken>()))
            .Returns(jobCts.Token); // returns already-cancelled token

        _channelMock
            .Setup(x => x.PublishAsync(It.IsAny<ProcessedInputEvent>(), It.IsAny<CancellationToken>()))
            .Callback<ProcessedInputEvent, CancellationToken>((e, _) => publishedEvents.Add(e))
            .Returns(ValueTask.CompletedTask);

        // Act - stoppingToken is NOT cancelled
        await _sut.ProcessAsync(request, CancellationToken.None);

        // Assert - should publish a cancellation event
        Assert.AreEqual(1, publishedEvents.Count);
        Assert.IsTrue(publishedEvents[0].IsCancelled);
        Assert.IsTrue(publishedEvents[0].IsCompleted);
        Assert.AreEqual(-1, publishedEvents[0].Progress);
    }

    [TestMethod]
    public async Task ProcessAsync_AppShutdown_DoesNotPublishCancelledEvent()
    {
        // Arrange
        var request = new DataProcessingRequest { Id = Guid.NewGuid(), UserInput = "abcdef" };
        var stoppingCts = new CancellationTokenSource();
        stoppingCts.Cancel(); // simulate app shutdown

        _jobManagerMock
            .Setup(x => x.Register(request.Id, It.IsAny<CancellationToken>()))
            .Returns(stoppingCts.Token); // linked token is also cancelled

        // Act
        await _sut.ProcessAsync(request, stoppingCts.Token);

        // Assert - no events published at all (graceful shutdown)
        _channelMock.Verify(
            x => x.PublishAsync(It.IsAny<ProcessedInputEvent>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [TestMethod]
    public async Task ProcessAsync_Always_RemovesJobFromManagerInFinally()
    {
        // Arrange
        var request = new DataProcessingRequest { Id = Guid.NewGuid(), UserInput = "a" };

        _jobManagerMock
            .Setup(x => x.Register(request.Id, It.IsAny<CancellationToken>()))
            .Returns(CancellationToken.None);

        _channelMock
            .Setup(x => x.PublishAsync(It.IsAny<ProcessedInputEvent>(), It.IsAny<CancellationToken>()))
            .Returns(ValueTask.CompletedTask);

        // Act
        await _sut.ProcessAsync(request, CancellationToken.None);

        // Assert
        _jobManagerMock.Verify(x => x.Remove(request.Id), Times.Once);
    }
}
