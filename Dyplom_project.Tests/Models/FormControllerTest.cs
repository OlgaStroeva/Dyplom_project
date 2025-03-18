using Microsoft.AspNetCore.Mvc;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Moq;
using System.Threading.Tasks;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using JetBrains.Annotations;
using Microsoft.AspNetCore.Http;

[TestClass]
[TestSubject(typeof(FormController))]
public class FormControllerTest
{
    private Mock<ApplicationDbContext> _mockDbContext;
    private FormController _controller;

    private void SetupHttpContext(int userId)
    {
        var httpContext = new DefaultHttpContext();
        httpContext.User = new System.Security.Claims.ClaimsPrincipal(
            new System.Security.Claims.ClaimsIdentity(
                new List<System.Security.Claims.Claim>
                {
                    new System.Security.Claims.Claim("userId", userId.ToString())
                }, "mock"));

        _controller.ControllerContext = new ControllerContext { HttpContext = httpContext };
    }

    [TestInitialize]
    public void Setup()
    {
        _mockDbContext = new Mock<ApplicationDbContext>();
        _controller = new FormController(_mockDbContext.Object);
    }

    /// <summary>
    /// Тест успешного создания анкеты
    /// </summary>
    [TestMethod]
    public async Task CreateForm_ShouldReturnOk_WhenSuccessful()
    {
        var userId = 1;
        var eventId = 100;
        var mockEvent = new Event { Id = eventId, CreatedBy = userId };

        _mockDbContext.Setup(db => db.GetEventByIdAsync(eventId))
            .ReturnsAsync(mockEvent);

        _mockDbContext.Setup(db => db.CreateFormAsync(eventId))
            .Returns(Task.CompletedTask);

        var httpContext = new DefaultHttpContext();
        httpContext.User = new System.Security.Claims.ClaimsPrincipal(
            new System.Security.Claims.ClaimsIdentity(
                new List<System.Security.Claims.Claim> { new System.Security.Claims.Claim("userId", userId.ToString()) },
                "mock"));

        _controller.ControllerContext = new ControllerContext { HttpContext = httpContext };

        var result = await _controller.CreateForm(eventId);
        var okResult = result as OkObjectResult;

        Assert.IsNotNull(okResult);
        Assert.AreEqual(200, okResult.StatusCode);
    }

    /// <summary>
    /// Тест попытки создания анкеты, если мероприятие не найдено
    /// </summary>
    [TestMethod]
    public async Task CreateForm_ShouldReturnNotFound_WhenEventDoesNotExist()
    {
        var userId = 1;
        var eventId = 200;

        _mockDbContext.Setup(db => db.GetEventByIdAsync(eventId))
            .ReturnsAsync((Event)null);

        SetupHttpContext(userId); // ✅ Теперь HttpContext создаётся правильно

        var result = await _controller.CreateForm(eventId);
        var notFoundResult = result as NotFoundObjectResult;

        Assert.IsNotNull(notFoundResult);
        Assert.AreEqual(404, notFoundResult.StatusCode);
    }

    /// <summary>
    /// Тест успешного добавления участника
    /// </summary>
    [TestMethod]
    public async Task AddParticipant_ShouldReturnOk_WhenSuccessful()
    {
        var userId = 1;
        var formId = 300;
        var request = new AddParticipantRequest
        {
            Data = [new Dictionary<string, string>
            {
                { "Email", "test@example.com" },
                { "Name", "John Doe" }
            }]
        };

        var mockEvent = new Event { Id = 101, CreatedBy = userId };

        _mockDbContext.Setup(db => db.GetEventByFormIdAsync(formId))
            .ReturnsAsync(mockEvent);

        _mockDbContext.Setup(db => db.AddParticipantDataAsync(formId, It.IsAny<List<Dictionary<string, string>>>()))
            .ReturnsAsync(new List<string>()); // Нет ошибок

        var httpContext = new DefaultHttpContext();
        httpContext.User = new System.Security.Claims.ClaimsPrincipal(
            new System.Security.Claims.ClaimsIdentity(
                new List<System.Security.Claims.Claim> { new System.Security.Claims.Claim("userId", userId.ToString()) },
                "mock"));

        _controller.ControllerContext = new ControllerContext { HttpContext = httpContext };

        var result = await _controller.AddParticipant(formId, request);
        var okResult = result as OkObjectResult;

        Assert.IsNotNull(okResult);
        Assert.AreEqual(200, okResult.StatusCode);
    }

    /// <summary>
    /// Тест загрузки списка приглашённых из XLSX
    /// </summary>
    [TestMethod]
    public async Task UploadParticipants_ShouldReturnOk_WhenSuccessful()
    {
        var userId = 1;
        var formId = 400;
        var mockEvent = new Event { Id = 101, CreatedBy = userId };

        _mockDbContext.Setup(db => db.GetEventByFormIdAsync(formId))
            .ReturnsAsync(mockEvent);

        _mockDbContext.Setup(db => db.ParseXlsxParticipants(formId, It.IsAny<IFormFile>()))
            .ReturnsAsync(new List<string>()); // Нет ошибок

        var httpContext = new DefaultHttpContext();
        httpContext.User = new System.Security.Claims.ClaimsPrincipal(
            new System.Security.Claims.ClaimsIdentity(
                new List<System.Security.Claims.Claim> { new System.Security.Claims.Claim("userId", userId.ToString()) },
                "mock"));

        _controller.ControllerContext = new ControllerContext { HttpContext = httpContext };

        var mockFile = new Mock<IFormFile>();
        mockFile.Setup(f => f.Length).Returns(1024);
        mockFile.Setup(f => f.CopyToAsync(It.IsAny<Stream>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        var result = await _controller.UploadParticipants(formId, mockFile.Object);
        var okResult = result as OkObjectResult;

        Assert.IsNotNull(okResult);
        Assert.AreEqual(200, okResult.StatusCode);
    }
}
