using System;
using System.Collections.Generic;
using System.Security.Claims;
using System.Threading.Tasks;
using JetBrains.Annotations;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Moq;

namespace Dyplom_project.Tests.Models;
[TestClass]
public class ParticipantsControllerTest
{
    private readonly Mock<ApplicationDbContext> _mockDbContext = new Mock<ApplicationDbContext>();
    private FormController _controller;

    [TestInitialize]
    public void Setup()
    {
        _controller = new FormController(_mockDbContext.Object);
    }

    // Helper to safely set user context
    private void SetUserContext(int userId)
    {
        _controller.ControllerContext = new ControllerContext
        {
            HttpContext = new DefaultHttpContext
            {
                User = new ClaimsPrincipal(new ClaimsIdentity(new[]
                {
                    new Claim("userId", userId.ToString())
                }))
            }
        };
    }

[TestMethod]
public async Task AddParticipant_FormNotFound_Returns404()
{
    // Arrange
    const int formId = 100;
    SetUserContext(1);
    
    var testDbContext = new TestApplicationDbContext
    {
        GetEventByFormIdAsyncHandler = async (id) => null
    };

    var controller = new FormController(testDbContext);
    controller.ControllerContext = _controller.ControllerContext;
    
    var request = new AddParticipantRequest { Data = new List<Dictionary<string, string>>() };
    
    // Act
    var result = await controller.AddParticipant(formId, request);
    
    // Assert
    Assert.IsInstanceOfType(result, typeof(NotFoundObjectResult));
    var response = (NotFoundObjectResult)result;
    Assert.AreEqual(404, response.StatusCode);
    
    // Verify error message
    dynamic responseBody = response.Value!;
    var messageProperty = responseBody.GetType().GetProperty("message");
    Assert.IsNotNull(messageProperty, "Response missing 'message' property");
    Assert.AreEqual("Анкета не найдена.", messageProperty.GetValue(responseBody)?.ToString());
}

[TestMethod]
public async Task AddParticipant_ValidationErrors_Returns400()
{
    // Arrange
    const int formId = 100;
    SetUserContext(1);
    
    var validationErrors = new List<string>
    {
        "Email is required",
        "Phone format is invalid"
    };
    
    var testDbContext = new TestApplicationDbContext
    {
        GetEventByFormIdAsyncHandler = async (id) => new Event { Id = 1, Name = "Test Event" },
        AddParticipantDataAsyncHandler = async (id, data) => validationErrors
    };

    var controller = new FormController(testDbContext);
    controller.ControllerContext = _controller.ControllerContext;
    
    var request = new AddParticipantRequest
    {
        Data = new List<Dictionary<string, string>>
        {
            new Dictionary<string, string>
            {
                ["Name"] = "John Doe",
                ["Phone"] = "invalid"
            }
        }
    };
    
    // Act
    var result = await controller.AddParticipant(formId, request);
    
    // Assert
    Assert.IsInstanceOfType(result, typeof(BadRequestObjectResult));
    var response = (BadRequestObjectResult)result;
    Assert.AreEqual(400, response.StatusCode);
    
    // Verify response structure
    dynamic responseBody = response.Value!;
    var messageProperty = responseBody.GetType().GetProperty("message");
    var errorsProperty = responseBody.GetType().GetProperty("errors");
    
    Assert.IsNotNull(messageProperty, "Response missing 'message' property");
    Assert.IsNotNull(errorsProperty, "Response missing 'errors' property");
    
    Assert.AreEqual("Ошибка в данных.", messageProperty.GetValue(responseBody)?.ToString());
    
    var errors = errorsProperty.GetValue(responseBody) as List<string>;
    CollectionAssert.AreEqual(validationErrors, errors);
}

[TestMethod]
public async Task AddParticipant_Success_Returns200()
{
    // Arrange
    const int formId = 100;
    SetUserContext(1);
    
    var testDbContext = new TestApplicationDbContext
    {
        GetEventByFormIdAsyncHandler = async (id) => new Event { Id = 1, Name = "Test Event" },
        AddParticipantDataAsyncHandler = async (id, data) => new List<string>() // No errors
    };

    var controller = new FormController(testDbContext);
    controller.ControllerContext = _controller.ControllerContext;
    
    var request = new AddParticipantRequest
    {
        Data = new List<Dictionary<string, string>>
        {
            new Dictionary<string, string>
            {
                ["Name"] = "John Doe",
                ["Email"] = "john@example.com",
                ["Phone"] = "+1234567890"
            }
        }
    };
    
    // Act
    var result = await controller.AddParticipant(formId, request);
    
    // Assert
    Assert.IsInstanceOfType(result, typeof(OkObjectResult));
    var response = (OkObjectResult)result;
    Assert.AreEqual(200, response.StatusCode);
    
    // Verify success message
    dynamic responseBody = response.Value!;
    var messageProperty = responseBody.GetType().GetProperty("message");
    Assert.IsNotNull(messageProperty, "Response missing 'message' property");
    Assert.AreEqual("Участник добавлен!", messageProperty.GetValue(responseBody)?.ToString());
}

// Test 1: Fixed to expect UnauthorizedObjectResult
[TestMethod]
public async Task AddParticipant_MissingUserClaim_Returns401()
{
    // Arrange - No user claim
    _controller.ControllerContext = new ControllerContext
    {
        HttpContext = new DefaultHttpContext
        {
            User = new ClaimsPrincipal(new ClaimsIdentity())
        }
    };
    
    var request = new AddParticipantRequest { Data = new List<Dictionary<string, string>>() };
    
    // Act
    var result = await _controller.AddParticipant(1, request);
    
    // Assert - Changed to expect UnauthorizedObjectResult
    Assert.IsInstanceOfType(result, typeof(UnauthorizedObjectResult));
    var response = (UnauthorizedObjectResult)result;
    Assert.AreEqual(401, response.StatusCode);
}
// Tests for GetParticipants
[TestMethod]
public async Task GetParticipants_ValidRequest_ReturnsParticipants()
{
    // Arrange
    const int eventId = 100;
    
    var expectedParticipants = new List<ParticipantData>
    {
        new ParticipantData { Id = 1,  Data = new Dictionary<string, string> { ["Name"] = "John" } },
        new ParticipantData { Id = 2,  Data = new Dictionary<string, string> { ["Name"] = "Alice" } }
    };
    
    _mockDbContext.Setup(x => x.GetParticipantsByEventIdAsync(eventId))
        .ReturnsAsync(expectedParticipants);
    
    // Act
    var result = await _controller.GetParticipants(eventId);
    
    // Assert
    Assert.IsInstanceOfType(result, typeof(OkObjectResult));
    var response = (OkObjectResult)result;
    Assert.AreEqual(200, response.StatusCode);
    
    var participants = response.Value as List<ParticipantData>;
    CollectionAssert.AreEqual(expectedParticipants, participants);
}

[TestMethod]
public async Task GetParticipants_NoParticipants_ReturnsEmptyList()
{
    // Arrange
    const int eventId = 100;
    
    _mockDbContext.Setup(x => x.GetParticipantsByEventIdAsync(eventId))
        .ReturnsAsync(new List<ParticipantData>());
    
    // Act
    var result = await _controller.GetParticipants(eventId);
    
    // Assert
    Assert.IsInstanceOfType(result, typeof(OkObjectResult));
    var response = (OkObjectResult)result;
    Assert.AreEqual(200, response.StatusCode);
    
    var participants = response.Value as List<ParticipantData>;
    Assert.AreEqual(0, participants?.Count);
}

[TestMethod]
public async Task CancelParticipant_Success_ReturnsOk()
{
    // Arrange
    const int participantId = 100;
    
    // Create test context with safe implementation
    var testDbContext = new TestApplicationDbContext
    {
        DeleteParticipantAsyncHandler = async (id) => { /* Simulate deletion */ }
    };

    var controller = new FormController(testDbContext);
    SetUserContext(controller, 1);  // Use helper to set user context

    // Act
    var result = await controller.CancelParticipant(participantId);
    
    // Assert
    Assert.IsInstanceOfType(result, typeof(OkObjectResult));
    var response = (OkObjectResult)result;
    Assert.AreEqual(200, response.StatusCode);
    
    // Verify success message
    dynamic responseBody = response.Value!;
    var messageProperty = responseBody.GetType().GetProperty("message");
    Assert.IsNotNull(messageProperty, "Response missing 'message' property");
    Assert.AreEqual("Приглашённый удалён.", messageProperty.GetValue(responseBody)?.ToString());
}

// Helper method to set user context
private void SetUserContext(FormController controller, int userId)
{
    controller.ControllerContext = new ControllerContext
    {
        HttpContext = new DefaultHttpContext
        {
            User = new ClaimsPrincipal(new ClaimsIdentity(new[]
            {
                new Claim("userId", userId.ToString())
            }))
        }
    };
}

public class TestApplicationDbContext : ApplicationDbContext
{
    // Existing handlers
    public Func<int, Task<Event>> GetEventByIdAsyncHandler { get; set; }
    public Func<int, Task<bool>> EventHasFormAsyncHandler { get; set; }
    public Func<int, Task<int>> CreateFormAsyncHandler { get; set; }
    public Func<int, Task<Event>> GetEventByFormIdAsyncHandler { get; set; }
    public Func<int, List<FormField>, Task> UpdateFormAsyncHandler { get; set; }
    public Func<int, int, Task> DeleteFormAsyncHandler { get; set; }
    public Func<int, Task<byte[]>> GenerateFormTemplateXlsxHandler { get; set; }
    
    // Corrected handler for participant addition
    public Func<int, List<Dictionary<string, string>>, Task<List<string>>> AddParticipantDataAsyncHandler { get; set; }

    public override Task<Event> GetEventByIdAsync(int id)
        => GetEventByIdAsyncHandler?.Invoke(id) ?? base.GetEventByIdAsync(id);
    
    public override Task<bool> EventHasFormAsync(int eventId)
        => EventHasFormAsyncHandler?.Invoke(eventId) ?? base.EventHasFormAsync(eventId);
    
    public override Task<int> CreateFormAsync(int eventId)
        => CreateFormAsyncHandler?.Invoke(eventId) ?? base.CreateFormAsync(eventId);
    
    public override Task<Event> GetEventByFormIdAsync(int formId)
        => GetEventByFormIdAsyncHandler?.Invoke(formId) ?? base.GetEventByFormIdAsync(formId);
    
    public override Task UpdateFormAsync(int formId, List<FormField> fields)
        => UpdateFormAsyncHandler?.Invoke(formId, fields) ?? base.UpdateFormAsync(formId, fields);
    
    public override Task DeleteFormAsync(int formId, int eventId)
        => DeleteFormAsyncHandler?.Invoke(formId, eventId) ?? base.DeleteFormAsync(formId, eventId);
    
    public override Task<byte[]> GenerateFormTemplateXlsx(int formId)
        => GenerateFormTemplateXlsxHandler?.Invoke(formId) 
            ?? base.GenerateFormTemplateXlsx(formId);
    
    // Corrected method signature
    public override Task<List<string>> AddParticipantDataAsync(int formId, List<Dictionary<string, string>> data)
        => AddParticipantDataAsyncHandler?.Invoke(formId, data) 
            ?? base.AddParticipantDataAsync(formId, data);
    public Func<int, Task> DeleteParticipantAsyncHandler { get; set; } = 
        async (id) => await Task.CompletedTask;
    
    public new Task DeleteParticipantAsync(int participantId)
        => DeleteParticipantAsyncHandler?.Invoke(participantId) 
           ?? Task.CompletedTask;
}
}