using API.Controllers;
using API.Models;
using API.Settings;
using BusinessLayer.Services;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Moq;

namespace API.Tests.Controllers;

[TestClass]
public class InputProcessorControllerTests
{
    private InputProcessorController _controller;
    private DefaultHttpContext _httpContext;
    private MemoryStream _responseBody;

    private AppSettings _appSettings = new AppSettings { RandomDelayRange = new RandomDelayRangeSettings { Min = 100, Max = 200 } };

    private readonly Mock<ILogger<InputProcessorController>> _loggerMock = new();
    private readonly Mock<IInputProcessingService> _inputProcessorServiceMock = new();
    private readonly Mock<IOptions<AppSettings>> _appSettingsMock = new();


    [TestInitialize]
    public void Setup()
    {
        _appSettingsMock.SetupGet(s => s.Value).Returns(_appSettings);

        _controller = new InputProcessorController(_loggerMock.Object, _inputProcessorServiceMock.Object, _appSettingsMock.Object);

        _responseBody = new MemoryStream();
        _httpContext = new DefaultHttpContext();
        _httpContext.Response.Body = _responseBody;

        _controller.ControllerContext = new ControllerContext
        {
            HttpContext = _httpContext
        };
    }

    [TestMethod]
    public async Task GetEstimatedProcessingTime_ReturnsCorrectSize()
    {
        // Arrange
        var input = "test input";
        var processedInput = "someRandomProcessedText/base64";
        _inputProcessorServiceMock.Setup(s => s.ProcessInputAsync(input, It.IsAny<CancellationToken>()))
            .ReturnsAsync(processedInput);
        // Act
        var result = await _controller.GetEstimatedProcessingTime(input, CancellationToken.None) as JsonResult;
        // Assert
        Assert.IsNotNull(result);
        dynamic value = result.Value!;
        Assert.AreEqual(processedInput.Length, (int)value.Size);
    }

    [TestMethod]
    public async Task StreamProcessedInput_SetsCorrectResponseHeaders()
    {
        // Arrange
        var input = new UserInputProcessingModel { UserInput = "hi" };
        _inputProcessorServiceMock
            .Setup(s => s.ProcessInputAsync("hi", It.IsAny<CancellationToken>()))
            .ReturnsAsync("text/b64");

        // Act
        await _controller.StreamProcessedInput(input, CancellationToken.None);

        // Assert
        Assert.AreEqual("text/event-stream", _httpContext.Response.ContentType);
        Assert.AreEqual("no-cache", _httpContext.Response.Headers.CacheControl.ToString());
        Assert.AreEqual("keep-alive", _httpContext.Response.Headers.Connection.ToString());
    }

    [TestMethod]
    public async Task StreamProcessedInput_CallsServiceWithCorrectInput()
    {
        // Arrange
        var input = new UserInputProcessingModel { UserInput = "hello" };
        _inputProcessorServiceMock
            .Setup(s => s.ProcessInputAsync("hello", It.IsAny<CancellationToken>()))
            .ReturnsAsync("text/b64");

        // Act
        await _controller.StreamProcessedInput(input, CancellationToken.None);

        // Assert
        _inputProcessorServiceMock.Verify(
            s => s.ProcessInputAsync("hello", It.IsAny<CancellationToken>()),
            Times.Once
        );
    }

    [TestMethod]
    public async Task StreamProcessedInput_StreamsAllCharactersToResponse()
    {
        // Arrange
        var processedText = "text/b64";
        var input = new UserInputProcessingModel { UserInput = "ab" };
        _inputProcessorServiceMock
            .Setup(s => s.ProcessInputAsync("ab", It.IsAny<CancellationToken>()))
            .ReturnsAsync(processedText);

        // Act
        await _controller.StreamProcessedInput(input, CancellationToken.None);

        // Assert
        _responseBody.Seek(0, SeekOrigin.Begin);
        using var reader = new StreamReader(_responseBody);
        var written = await reader.ReadToEndAsync();
        Assert.AreEqual(processedText, written);
    }

    [TestMethod]
    public async Task StreamProcessedInput_StopsStreamingWhenCancelled()
    {
        // Arrange
        var input = new UserInputProcessingModel { UserInput = "test" };
        _inputProcessorServiceMock
            .Setup(s => s.ProcessInputAsync("test", It.IsAny<CancellationToken>()))
            .ReturnsAsync("tset");

        using var cts = new CancellationTokenSource();

        // Cancel immediately so the loop breaks on first check or Task.Delay throws
        cts.Cancel();

        // Act
        await _controller.StreamProcessedInput(input, cts.Token);

        // Assert - should have written nothing or at most partial output
        _responseBody.Seek(0, SeekOrigin.Begin);
        using var reader = new StreamReader(_responseBody);
        var written = await reader.ReadToEndAsync();

        // Entire string should NOT have been written
        Assert.AreNotEqual("tset", written);
    }

    [TestMethod]
    public async Task StreamProcessedInput_CancellationDoesNotThrow()
    {
        // Arrange
        var input = new UserInputProcessingModel { UserInput = "test" };
        _inputProcessorServiceMock
            .Setup(s => s.ProcessInputAsync("test", It.IsAny<CancellationToken>()))
            .ReturnsAsync("tset");

        using var cts = new CancellationTokenSource();
        cts.Cancel();

        // Act & Assert - should not throw
        await _controller.StreamProcessedInput(input, cts.Token);
    }

    [TestMethod]
    public async Task StreamProcessedInput_CancellationMidStream_WritesPartialOutput()
    {
        // Arrange
        var input = new UserInputProcessingModel { UserInput = "abcd" };
        _inputProcessorServiceMock
            .Setup(s => s.ProcessInputAsync("abcd", It.IsAny<CancellationToken>()))
            .ReturnsAsync("dcba");

        _appSettings.RandomDelayRange.Min = 500; // Increase delay to ensure we can cancel mid-stream
        _appSettings.RandomDelayRange.Max = 3000;

        using var cts = new CancellationTokenSource();

        // Cancel after a short delay - should interrupt mid-stream
        cts.CancelAfter(TimeSpan.FromMilliseconds(1500));

        // Act
        await _controller.StreamProcessedInput(input, cts.Token);

        // Assert
        _responseBody.Seek(0, SeekOrigin.Begin);
        using var reader = new StreamReader(_responseBody);
        var written = await reader.ReadToEndAsync();

        // Should have at least one character but not all
        Assert.IsTrue(written.Length > 0, "Should have written at least one character");
        Assert.IsTrue(written.Length < 4, "Should not have written all characters");

        _appSettings.RandomDelayRange.Min = 100;
        _appSettings.RandomDelayRange.Max = 200;
    }

    [TestMethod]
    public async Task StreamProcessedInput_EmptyProcessedInput_WritesNothing()
    {
        // Arrange
        var input = new UserInputProcessingModel { UserInput = "test" };
        _inputProcessorServiceMock
            .Setup(s => s.ProcessInputAsync("test", It.IsAny<CancellationToken>()))
            .ReturnsAsync(string.Empty);

        // Act
        await _controller.StreamProcessedInput(input, CancellationToken.None);

        // Assert
        Assert.AreEqual(0, _responseBody.Length);
    }

    [TestMethod]
    public async Task StreamProcessedInput_LogsProcessedInput()
    {
        // Arrange
        var input = new UserInputProcessingModel { UserInput = "hi" };
        _inputProcessorServiceMock
            .Setup(s => s.ProcessInputAsync("hi", It.IsAny<CancellationToken>()))
            .ReturnsAsync("ih");

        // Act
        await _controller.StreamProcessedInput(input, CancellationToken.None);

        // Assert
        _loggerMock.Verify(
            x => x.Log(
                LogLevel.Information,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains("ih")),
                null,
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()
            ),
            Times.AtLeastOnce
        );
    }

    [TestMethod]
    public async Task StreamProcessedInput_LogsWhenCancelled()
    {
        // Arrange
        var input = new UserInputProcessingModel { UserInput = "test" };
        _inputProcessorServiceMock
            .Setup(s => s.ProcessInputAsync("test", It.IsAny<CancellationToken>()))
            .ReturnsAsync("tset");

        using var cts = new CancellationTokenSource();
        cts.Cancel();

        // Act
        await _controller.StreamProcessedInput(input, cts.Token);

        // Assert
        _loggerMock.Verify(
            x => x.Log(
                LogLevel.Information,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains("cancelled")),
                null,
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()
            ),
            Times.AtLeastOnce
        );
    }

    [TestMethod]
    public async Task StreamProcessedInput_ServiceThrows_PropagatesException()
    {
        // Arrange
        var input = new UserInputProcessingModel { UserInput = "test" };
        _inputProcessorServiceMock
            .Setup(s => s.ProcessInputAsync("test", It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("Service failure"));

        // Act & Assert
        await Assert.ThrowsAsync<InvalidOperationException>(
            () => _controller.StreamProcessedInput(input, CancellationToken.None)
        );
    }

    [TestCleanup]
    public void Cleanup()
    {
        _responseBody.Dispose();
    }
}
