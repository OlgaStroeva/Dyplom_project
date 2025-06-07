using Microsoft.AspNetCore.Mvc;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Moq;
using System.Threading.Tasks;
using System.Collections.Generic;
using System.Security.Claims;
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

    [TestMethod]
    public async Task CreateEvent_ShouldReturnOk_WhenSuccessful()
    {
        var userId = 1;
        var context = new ControllerContext
        {
            HttpContext = new DefaultHttpContext()
        };
        context.HttpContext.User = new ClaimsPrincipal(new ClaimsIdentity(new[]
        {
            new Claim("userId", userId.ToString())
        }, "mock"));

        var request = new EventRequest { Name = "New Event" };

        _mockDbContext.Setup(x => x.CreateEventAsync(It.IsAny<Event>()))
            .ReturnsAsync(1);


        var controller = new EventController(_mockDbContext.Object) { ControllerContext = context };

        var result = await controller.CreateEvent(request);
        var okResult = result as OkObjectResult;

        Assert.IsNotNull(okResult);
        Assert.AreEqual(200, okResult.StatusCode);
    }

    [TestMethod]
    public async Task UpdateEvent_ShouldReturnOk_WhenUserIsOwner()
    {
        var userId = 1;
        var eventId = 1;
        var request = new UpdateEventRequest { Name = "Updated Event" };
        var context = new ControllerContext
        {
            HttpContext = new DefaultHttpContext()
        };
        context.HttpContext.User = new ClaimsPrincipal(new ClaimsIdentity(new[]
        {
            new Claim("userId", userId.ToString())
        }, "mock"));
        
        var controller = new EventController(_mockDbContext.Object) { ControllerContext = context };

        _mockDbContext.Setup(x => x.GetEventByIdAsync(eventId))
            .ReturnsAsync(new Event { Id = eventId, CreatedBy = userId });

        _mockDbContext.Setup(x => x.UpdateEventAsync(It.IsAny<int>(), request))
            .Returns(Task.CompletedTask);


        var result = await controller.UpdateEvent(eventId, request);
        var okResult = result as OkObjectResult;

        Assert.IsNotNull(okResult);
        Assert.AreEqual(200, okResult.StatusCode);
    }
    [TestMethod]
    public async Task GetEventById_ShouldReturnNotFound_WhenEventDoesNotExist()
    {
        _mockDbContext.Setup(x => x.GetEventByIdAsync(999))
            .ReturnsAsync((Event?)null);

        var controller = new EventController(_mockDbContext.Object);
        var result = await controller.GetEventById(999);

        Assert.IsInstanceOfType(result, typeof(NotFoundObjectResult));
    }
    
    [TestMethod]
    public async Task GetEventById_ShouldReturnOk_WhenEventExists()
    {
        _mockDbContext.Setup(x => x.GetEventByIdAsync(1))
            .ReturnsAsync(new Event { Id = 1, Name = "Sample Event" });

        var controller = new EventController(_mockDbContext.Object);
        var result = await controller.GetEventById(1);

        Assert.IsInstanceOfType(result, typeof(OkObjectResult));
    }
    [TestMethod]
    public async Task GetUserEvents_ShouldReturnList()
    {
        var userId = 1;
        var context = new ControllerContext
        {
            HttpContext = new DefaultHttpContext()
        };
        context.HttpContext.User = new ClaimsPrincipal(new ClaimsIdentity(new[]
        {
            new Claim("userId", userId.ToString())
        }, "mock"));
        
        var controller = new EventController(_mockDbContext.Object) { ControllerContext = context };


        _mockDbContext.Setup(x => x.GetUserEventsAsync(userId))
            .ReturnsAsync(new List<Event> {
                new Event { Id = 1, Name = "Event 1", CreatedBy = userId },
                new Event { Id = 2, Name = "Event 2", CreatedBy = userId }
            });

        var result = await controller.GetUserEvents();
        var okResult = result as OkObjectResult;

        Assert.IsNotNull(okResult);
        var events = okResult.Value as List<Event>;
        Assert.AreEqual(2, events?.Count);
    }
    [TestMethod]
    public async Task UpdateEvent_ShouldReturnForbid_WhenUserIsNotOwner()
    {
        var ownerId = 1;
        var editorId = 2;
        var eventId = 1;

        var context = new ControllerContext
        {
            HttpContext = new DefaultHttpContext()
        };
        context.HttpContext.User = new ClaimsPrincipal(new ClaimsIdentity(new[]
        {
            new Claim("userId", editorId.ToString())
        }, "mock"));
        
        var controller = new EventController(_mockDbContext.Object) { ControllerContext = context };

        _mockDbContext.Setup(x => x.GetEventByIdAsync(eventId))
            .ReturnsAsync(new Event { Id = eventId, CreatedBy = ownerId });

        var request = new UpdateEventRequest { Name = "Attempted Update" };

        var result = await controller.UpdateEvent(eventId, request);
        Assert.IsInstanceOfType(result, typeof(ForbidResult));
    }

}
