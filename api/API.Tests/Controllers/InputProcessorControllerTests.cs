using API.Controllers;
using API.Models;
using BusinessLayer.Models;
using BusinessLayer.Services;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Primitives;
using Moq;
using System.Security.Claims;

namespace API.Tests.Controllers;

[TestClass]
public class InputProcessorControllerTests
{
    private Mock<ILogger<InputProcessorController>> _loggerMock = new();
    private Mock<IInputProcessingService> _serviceMock = new();
    private InputProcessorController _sut = null!;

    [TestInitialize]
    public void Setup()
    {
        _sut = new InputProcessorController(_loggerMock.Object, _serviceMock.Object);
        _sut.ControllerContext = CreateControllerContext("testuser");
    }

    #region ProcessInput

    [TestMethod]
    public async Task ProcessInput_ValidInput_ReturnsAcceptedWithJobId()
    {
        // Arrange
        var expectedJobId = Guid.NewGuid();
        var input = new UserInputProcessingModel { UserInput = "hello" };

        _serviceMock
            .Setup(x => x.StartProcessingAsync("hello", "testuser", It.IsAny<CancellationToken>()))
            .ReturnsAsync(expectedJobId);

        // Act
        var result = await _sut.ProcessInput(input, CancellationToken.None);

        // Assert
        var acceptedResult = result as ObjectResult;
        Assert.IsNotNull(acceptedResult);
        Assert.AreEqual(202, acceptedResult.StatusCode);

        dynamic value = acceptedResult.Value!;
        Assert.AreEqual(expectedJobId, (Guid)value.JobId);
    }

    [TestMethod]
    public async Task ProcessInput_PassesCurrentUserToService()
    {
        // Arrange
        _sut.ControllerContext = CreateControllerContext("specificuser");

        _serviceMock
            .Setup(x => x.StartProcessingAsync(It.IsAny<string>(), "specificuser", It.IsAny<CancellationToken>()))
            .ReturnsAsync(Guid.NewGuid());

        // Act
        await _sut.ProcessInput(new UserInputProcessingModel { UserInput = "test" }, CancellationToken.None);

        // Assert
        _serviceMock.Verify(
            x => x.StartProcessingAsync("test", "specificuser", It.IsAny<CancellationToken>()),
            Times.Once);
    }

    #endregion

    #region Cancel

    [TestMethod]
    public async Task Cancel_OwnerCancelsSuccessfully_ReturnsOk()
    {
        // Arrange
        var jobId = Guid.NewGuid();

        _serviceMock
            .Setup(x => x.CancelProcessingRequest(jobId, "testuser", It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        // Act
        var result = await _sut.Cancel(jobId, CancellationToken.None);

        // Assert
        var okResult = result as OkObjectResult;
        Assert.IsNotNull(okResult);
        Assert.AreEqual(200, okResult.StatusCode);
    }

    [TestMethod]
    public async Task Cancel_JobNotFoundOrNotOwner_ReturnsNotFound()
    {
        // Arrange
        var jobId = Guid.NewGuid();

        _serviceMock
            .Setup(x => x.CancelProcessingRequest(jobId, "testuser", It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);

        // Act
        var result = await _sut.Cancel(jobId, CancellationToken.None);

        // Assert
        var notFoundResult = result as NotFoundObjectResult;
        Assert.IsNotNull(notFoundResult);
        Assert.AreEqual(404, notFoundResult.StatusCode);
    }

    #endregion

    #region SubscribeToProcessedInputEvents

    [TestMethod]
    public async Task SubscribeToProcessedInputEvents_ReturnsServerSentEventsResult()
    {
        // Arrange
        var jobId = Guid.NewGuid();

        _serviceMock
            .Setup(x => x.GetProcessedInputEventsAsync(jobId, -1, "testuser", It.IsAny<CancellationToken>()))
            .Returns(ToAsyncEnumerable(new List<ProcessedInputEvent>()));

        // Act
        var result = await _sut.SubscribeToProcessedInputEvents(jobId, CancellationToken.None);

        // Assert
        Assert.IsNotNull(result);
    }

    #endregion

    #region Helpers

    private static ControllerContext CreateControllerContext(string username, string? lastEventId = null)
    {
        var claims = new[] { new Claim(ClaimTypes.Name, username) };
        var identity = new ClaimsIdentity(claims, "TestAuth");
        var principal = new ClaimsPrincipal(identity);

        var httpContext = new DefaultHttpContext
        {
            User = principal
        };

        if (lastEventId is not null)
        {
            httpContext.Request.Headers["Last-Event-ID"] = new StringValues(lastEventId);
        }

        return new ControllerContext
        {
            HttpContext = httpContext
        };
    }

    private static async IAsyncEnumerable<T> ToAsyncEnumerable<T>(IEnumerable<T> items)
    {
        foreach (var item in items)
        {
            yield return item;
            await Task.CompletedTask;
        }
    }

    #endregion
}
