using Microsoft.AspNetCore.Mvc;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Moq;
using System.Threading.Tasks;
using System.Collections.Generic;
using JetBrains.Annotations;
using Microsoft.AspNetCore.Http;

[TestClass]
[TestSubject(typeof(EventController))]
public class EventControllerTest
{
    private Mock<ApplicationDbContext> _mockDbContext;
    private EventController _controller;

    [TestInitialize]
    public void Setup()
    {
        _mockDbContext = new Mock<ApplicationDbContext>();
        _controller = new EventController(_mockDbContext.Object);
    }

    /// <summary>
    /// Тест успешного получения мероприятия по ID
    /// </summary>
    [TestMethod]
    public async Task GetEventById_ShouldReturnEvent_WhenEventExists()
    {
        var eventId = 123;
        var mockEvent = new Event
        {
            Id = eventId,
            Name = "Тестовое мероприятие",
            Description = "Описание тестового мероприятия",
            ImageUrl = "https://www.meme-arsenal.com/memes/8b0bb788781f6917098b8bfccc45f5a2.jpg"
        };

        _mockDbContext.Setup(db => db.GetEventByIdAsync(eventId))
            .ReturnsAsync(mockEvent);

        var result = await _controller.GetEventById(eventId);
        var okResult = result as OkObjectResult;
        var returnedEvent = okResult?.Value as Event;

        Assert.IsNotNull(okResult);
        Assert.AreEqual(200, okResult.StatusCode);
        Assert.IsNotNull(returnedEvent);
        Assert.AreEqual(mockEvent.Id, returnedEvent.Id);
        Assert.AreEqual(mockEvent.Name, returnedEvent.Name);
        Assert.AreEqual(mockEvent.Description, returnedEvent.Description);
        Assert.AreEqual(mockEvent.ImageUrl, returnedEvent.ImageUrl);
    }

    /// <summary>
    /// Тест получения мероприятия, если оно не найдено
    /// </summary>
    [TestMethod]
    public async Task GetEventById_ShouldReturnNotFound_WhenEventDoesNotExist()
    {
        var eventId = 999;
        _mockDbContext.Setup(db => db.GetEventByIdAsync(eventId))
            .ReturnsAsync((Event)null);

        var result = await _controller.GetEventById(eventId);
        var notFoundResult = result as NotFoundObjectResult;

        Assert.IsNotNull(notFoundResult);
        Assert.AreEqual(404, notFoundResult.StatusCode);
    }

    /// <summary>
    /// Тест получения мероприятий пользователя
    /// </summary>
    [TestMethod]
    public async Task GetUserEvents_ShouldReturnEvents_WhenUserHasEvents()
    {
        var userId = 1;
        var mockEvents = new List<Event>
        {
            new Event { Id = 101, Name = "Событие 1", Description = "Описание 1", ImageUrl = "https://www.meme-arsenal.com/memes/1032dd1b48a30455116b43332f27b862.jpg" },
            new Event { Id = 102, Name = "Событие 2", Description = "Описание 2", ImageUrl = "https://www.meme-arsenal.com/memes/88c1679749eec9f4fe8c7b452b476574.jpg" }
        };

        _mockDbContext.Setup(db => db.GetUserEventsAsync(userId))
            .ReturnsAsync(mockEvents);

        var httpContext = new DefaultHttpContext();
        httpContext.User = new System.Security.Claims.ClaimsPrincipal(
            new System.Security.Claims.ClaimsIdentity(
                new List<System.Security.Claims.Claim> { new System.Security.Claims.Claim("userId", userId.ToString()) },
                "mock"));

        _controller.ControllerContext = new ControllerContext { HttpContext = httpContext };

        var result = await _controller.GetUserEvents();
        var okResult = result as OkObjectResult;
        var returnedEvents = okResult?.Value as List<Event>;

        Assert.IsNotNull(okResult);
        Assert.AreEqual(200, okResult.StatusCode);
        Assert.IsNotNull(returnedEvents);
        Assert.AreEqual(2, returnedEvents.Count);
    }

    /// <summary>
    /// Тест получения мероприятий пользователя, если у него нет мероприятий
    /// </summary>
    [TestMethod]
    public async Task GetUserEvents_ShouldReturnEmptyList_WhenUserHasNoEvents()
    {
        var userId = 1;

        _mockDbContext.Setup(db => db.GetUserEventsAsync(userId))
            .ReturnsAsync(new List<Event>());

        var httpContext = new DefaultHttpContext();
        httpContext.User = new System.Security.Claims.ClaimsPrincipal(
            new System.Security.Claims.ClaimsIdentity(
                new List<System.Security.Claims.Claim> { new System.Security.Claims.Claim("userId", userId.ToString()) },
                "mock"));

        _controller.ControllerContext = new ControllerContext { HttpContext = httpContext };

        var result = await _controller.GetUserEvents();
        var okResult = result as OkObjectResult;
        var returnedEvents = okResult?.Value as List<Event>;

        Assert.IsNotNull(okResult);
        Assert.AreEqual(200, okResult.StatusCode);
        Assert.IsNotNull(returnedEvents);
        Assert.AreEqual(0, returnedEvents.Count);
    }
}
