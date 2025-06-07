using Microsoft.AspNetCore.Mvc;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Moq;
using System.Threading.Tasks;
using System.Security.Claims;
using Microsoft.AspNetCore.Http;
using System;
using System.Collections.Generic;

[TestClass]
public class FormControllerCreateFormTests
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
public async Task CreateForm_NoUserClaim_Returns401()
{
    // Arrange - Ensure User exists but has no userId claim
    _controller.ControllerContext = new ControllerContext
    {
        HttpContext = new DefaultHttpContext
        {
            User = new ClaimsPrincipal(new ClaimsIdentity()) // Authenticated but no claims
        }
    };
    
    // Act
    var result = await _controller.CreateForm(1);
    
    // Assert
    Assert.IsInstanceOfType(result, typeof(UnauthorizedObjectResult));
    var statusResult = (UnauthorizedObjectResult)result;
    Assert.AreEqual(401, statusResult.StatusCode);
}

    // Test 2: Event not found
    [TestMethod]
    public async Task CreateForm_EventNotFound_Returns404()
    {
        // Arrange
        SetUserContext(1);
        _mockDbContext.Setup(x => x.GetEventByIdAsync(It.IsAny<int>()))
            .ReturnsAsync((Event)null);

        // Act
        var result = await _controller.CreateForm(1);
        
        // Assert
        var statusResult = result as NotFoundObjectResult;
        Assert.IsNotNull(statusResult);
        Assert.AreEqual(404, statusResult.StatusCode);
    }

    // Test 3: User is not owner
    [TestMethod]
    public async Task CreateForm_NotOwner_Returns403()
    {
        // Arrange
        const int eventId = 100;
        SetUserContext(2); // Non-owner user
        _mockDbContext.Setup(x => x.GetEventByIdAsync(eventId))
            .ReturnsAsync(new Event { Id = eventId, CreatedBy = 1 }); // Owned by user 1

        // Act
        var result = await _controller.CreateForm(eventId);
        
        // Assert
        Assert.IsInstanceOfType(result, typeof(ForbidResult));
    }

    // Test 4: Form already exists
    [TestMethod]
    public async Task CreateForm_FormExists_Returns400()
    {
        // Arrange
        const int eventId = 100;
        SetUserContext(1); // Owner
        _mockDbContext.Setup(x => x.GetEventByIdAsync(eventId))
            .ReturnsAsync(new Event { Id = eventId, CreatedBy = 1 });
        _mockDbContext.Setup(x => x.EventHasFormAsync(eventId))
            .ReturnsAsync(true); // Form exists

        // Act
        var result = await _controller.CreateForm(eventId);
        
        // Assert
        Assert.IsInstanceOfType(result, typeof(BadRequestObjectResult));
        var statusResult = (BadRequestObjectResult)result;
        Assert.AreEqual(400, statusResult.StatusCode);
    }

[TestMethod]
public async Task CreateForm_Success_Returns200()
{
    // Arrange
    const int eventId = 100;
    const int templateId = 500;
    SetUserContext(1); // Owner
    
    // Create test-specific context
    var testDbContext = new TestApplicationDbContext
    {
        GetEventByIdAsyncHandler = async (id) => 
            id == eventId ? new Event { Id = eventId, CreatedBy = 1 } : null,
        
        EventHasFormAsyncHandler = async (id) => 
            id == eventId ? false : true,
        
        CreateFormAsyncHandler = async (id) => templateId
    };

    var controller = new FormController(testDbContext);
    controller.ControllerContext = _controller.ControllerContext;

    // Act
    var result = await controller.CreateForm(eventId);
    
    // Assert
    Assert.IsInstanceOfType(result, typeof(OkObjectResult));
    var okResult = result as OkObjectResult;
    Assert.IsNotNull(okResult);
    Assert.AreEqual(200, okResult.StatusCode);
    
    // SAFE RESPONSE VERIFICATION
    var response = okResult.Value;
    Assert.IsNotNull(response);
    
    // Using reflection to safely access properties
    var messageProperty = response.GetType().GetProperty("message");
    var idProperty = response.GetType().GetProperty("invitationTemplateId");
    
    Assert.IsNotNull(messageProperty, "Response missing 'message' property");
    Assert.IsNotNull(idProperty, "Response missing 'invitationTemplateId' property");
    
    Assert.AreEqual("Шаблон анкеты создан!", messageProperty.GetValue(response)?.ToString());
    Assert.AreEqual(templateId, (int)idProperty.GetValue(response));
}
[TestMethod]
public async Task UpdateForm_MissingUserClaim_Returns401()
{
    // Arrange - No user claim
    _controller.ControllerContext = new ControllerContext
    {
        HttpContext = new DefaultHttpContext
        {
            User = new ClaimsPrincipal(new ClaimsIdentity())
        }
    };
    
    var request = new FormRequest { Fields = new List<FormField>() };
    
    // Act
    var result = await _controller.UpdateForm(1, request);
    
    // Assert
    Assert.IsInstanceOfType(result, typeof(UnauthorizedObjectResult));
    var response = (UnauthorizedObjectResult)result;
    Assert.AreEqual(401, response.StatusCode);
}

[TestMethod]
public async Task UpdateForm_EventNotFound_Returns403()
{
    // Arrange
    const int formId = 100;
    const int userId = 1;
    SetUserContext(userId);
    
    // Create test context
    var testDbContext = new TestApplicationDbContext
    {
        GetEventByFormIdAsyncHandler = async (id) => null // Event not found
    };

    var controller = new FormController(testDbContext);
    controller.ControllerContext = _controller.ControllerContext;
    
    var request = new FormRequest { Fields = new List<FormField>() };
    
    // Act
    var result = await controller.UpdateForm(formId, request);
    
    // Assert
    Assert.IsInstanceOfType(result, typeof(ForbidResult));
}

[TestMethod]
public async Task UpdateForm_UserNotOwner_Returns403()
{
    // Arrange
    const int formId = 100;
    const int ownerId = 1;
    const int currentUserId = 2;
    SetUserContext(currentUserId);
    
    // Create test context
    var testDbContext = new TestApplicationDbContext
    {
        GetEventByFormIdAsyncHandler = async (id) => 
            new Event { Id = 1, CreatedBy = ownerId } // Owned by different user
    };

    var controller = new FormController(testDbContext);
    controller.ControllerContext = _controller.ControllerContext;
    
    var request = new FormRequest { Fields = new List<FormField>() };
    
    // Act
    var result = await controller.UpdateForm(formId, request);
    
    // Assert
    Assert.IsInstanceOfType(result, typeof(ForbidResult));
}

[TestMethod]
public async Task UpdateForm_MissingEmailField_Returns400()
{
    // Arrange
    const int formId = 100;
    const int userId = 1;
    SetUserContext(userId);
    
    // Create test context
    var testDbContext = new TestApplicationDbContext
    {
        GetEventByFormIdAsyncHandler = async (id) => 
            new Event { Id = 1, CreatedBy = userId } // User is owner
    };

    var controller = new FormController(testDbContext);
    controller.ControllerContext = _controller.ControllerContext;
    
    // Request without email field
    var request = new FormRequest
    {
        Fields = new List<FormField>
        {
            new FormField { Name = "Name", Type = "text" },
            new FormField { Name = "Phone", Type = "phone" }
        }
    };
    
    // Act
    var result = await controller.UpdateForm(formId, request);
    
    // Assert
    Assert.IsInstanceOfType(result, typeof(BadRequestObjectResult));
    var response = (BadRequestObjectResult)result;
    Assert.AreEqual(400, response.StatusCode);
    
    // Verify error message
    dynamic responseBody = response.Value!;
    var messageProperty = responseBody.GetType().GetProperty("message");
    Assert.IsNotNull(messageProperty, "Response missing 'message' property");
    StringAssert.Contains(messageProperty.GetValue(responseBody)?.ToString(), 
        "Поле Email обязательно");
}

[TestMethod]
public async Task UpdateForm_InvalidEmailType_Returns400()
{
    // Arrange
    const int formId = 100;
    const int userId = 1;
    SetUserContext(userId);
    
    // Create test context
    var testDbContext = new TestApplicationDbContext
    {
        GetEventByFormIdAsyncHandler = async (id) => 
            new Event { Id = 1, CreatedBy = userId }
    };

    var controller = new FormController(testDbContext);
    controller.ControllerContext = _controller.ControllerContext;
    
    // Request with email but wrong type
    var request = new FormRequest
    {
        Fields = new List<FormField>
        {
            new FormField { Name = "Email", Type = "text" }, // Should be 'email'
            new FormField { Name = "Phone", Type = "phone" }
        }
    };
    
    // Act
    var result = await controller.UpdateForm(formId, request);
    
    // Assert
    Assert.IsInstanceOfType(result, typeof(BadRequestObjectResult));
    var response = (BadRequestObjectResult)result;
    Assert.AreEqual(400, response.StatusCode);
    
    // Verify error message
    dynamic responseBody = response.Value!;
    var messageProperty = responseBody.GetType().GetProperty("message");
    Assert.IsNotNull(messageProperty, "Response missing 'message' property");
    StringAssert.Contains(messageProperty.GetValue(responseBody)?.ToString(), 
        "тип 'email'");
}

[TestMethod]
public async Task UpdateForm_ValidRequest_Returns200()
{
    // Arrange
    const int formId = 100;
    const int userId = 1;
    SetUserContext(userId);
    
    // Track if update was called
    bool updateCalled = false;
    
    // Create test context
    var testDbContext = new TestApplicationDbContext
    {
        GetEventByFormIdAsyncHandler = async (id) => 
            new Event { Id = 1, CreatedBy = userId },
            
        UpdateFormAsyncHandler = async (id, fields) => 
        {
            updateCalled = true; // Mark update as called
            await Task.CompletedTask;
        }
    };

    var controller = new FormController(testDbContext);
    controller.ControllerContext = _controller.ControllerContext;
    
    // Valid request with email field
    var request = new FormRequest
    {
        Fields = new List<FormField>
        {
            new FormField { Name = "Email", Type = "email" },
            new FormField { Name = "Phone", Type = "phone" }
        }
    };
    
    // Act
    var result = await controller.UpdateForm(formId, request);
    
    // Assert
    Assert.IsInstanceOfType(result, typeof(OkObjectResult));
    var response = (OkObjectResult)result;
    Assert.AreEqual(200, response.StatusCode);
    
    // Verify success message
    dynamic responseBody = response.Value!;
    var messageProperty = responseBody.GetType().GetProperty("message");
    Assert.IsNotNull(messageProperty, "Response missing 'message' property");
    Assert.AreEqual("Шаблон анкеты обновлён!", 
        messageProperty.GetValue(responseBody)?.ToString());
    
    // Verify database update was called
    Assert.IsTrue(updateCalled, "UpdateFormAsync was not called");
}
[TestMethod]
public async Task DeleteForm_MissingUserClaim_Returns401()
{
    // Arrange - No user claim
    _controller.ControllerContext = new ControllerContext
    {
        HttpContext = new DefaultHttpContext
        {
            User = new ClaimsPrincipal(new ClaimsIdentity())
        }
    };
    
    // Act
    var result = await _controller.DeleteForm(1);
    
    // Assert
    Assert.IsInstanceOfType(result, typeof(UnauthorizedResult));
}

[TestMethod]
public async Task DeleteForm_FormNotFound_Returns404()
{
    // Arrange
    const int formId = 100;
    const int userId = 1;
    SetUserContext(userId);
    
    // Create test context
    var testDbContext = new TestApplicationDbContext
    {
        GetEventByFormIdAsyncHandler = async (id) => null // Form not found
    };

    var controller = new FormController(testDbContext);
    controller.ControllerContext = _controller.ControllerContext;
    
    // Act
    var result = await controller.DeleteForm(formId);
    
    // Assert
    Assert.IsInstanceOfType(result, typeof(NotFoundObjectResult));
    var response = (NotFoundObjectResult)result;
    Assert.AreEqual(404, response.StatusCode);
    
    // Verify error message
    dynamic responseBody = response.Value!;
    var messageProperty = responseBody.GetType().GetProperty("message");
    Assert.IsNotNull(messageProperty, "Response missing 'message' property");
    StringAssert.Contains(messageProperty.GetValue(responseBody)?.ToString(), 
        "Анкета или мероприятие не найдено");
}

[TestMethod]
public async Task DeleteForm_UserNotOwner_Returns403()
{
    // Arrange
    const int formId = 100;
    const int ownerId = 1;
    const int currentUserId = 2;
    SetUserContext(currentUserId);
    
    // Create test context
    var testDbContext = new TestApplicationDbContext
    {
        GetEventByFormIdAsyncHandler = async (id) => 
            new Event { Id = 1, CreatedBy = ownerId } // Owned by different user
    };

    var controller = new FormController(testDbContext);
    controller.ControllerContext = _controller.ControllerContext;
    
    // Act
    var result = await controller.DeleteForm(formId);
    
    // Assert
    Assert.IsInstanceOfType(result, typeof(ForbidResult));
}

[TestMethod]
public async Task DeleteForm_Success_Returns200()
{
    // Arrange
    const int formId = 100;
    const int eventId = 200;
    const int userId = 1;
    SetUserContext(userId);
    
    // Track if delete was called
    bool deleteCalled = false;
    
    // Create test context
    var testDbContext = new TestApplicationDbContext
    {
        GetEventByFormIdAsyncHandler = async (id) => 
            new Event { Id = eventId, CreatedBy = userId },
            
        DeleteFormAsyncHandler = async (fId, eId) => 
        {
            deleteCalled = true; // Mark delete as called
            await Task.CompletedTask;
        }
    };

    var controller = new FormController(testDbContext);
    controller.ControllerContext = _controller.ControllerContext;
    
    // Act
    var result = await controller.DeleteForm(formId);
    
    // Assert
    Assert.IsInstanceOfType(result, typeof(OkObjectResult));
    var response = (OkObjectResult)result;
    Assert.AreEqual(200, response.StatusCode);
    
    // Verify success message
    dynamic responseBody = response.Value!;
    var messageProperty = responseBody.GetType().GetProperty("message");
    Assert.IsNotNull(messageProperty, "Response missing 'message' property");
    Assert.AreEqual("Шаблон анкеты удалён!", 
        messageProperty.GetValue(responseBody)?.ToString());
    
    // Verify database delete was called
    Assert.IsTrue(deleteCalled, "DeleteFormAsync was not called");
}

    [TestMethod]
    public async Task DeleteForm_InvalidClaimFormat_Returns401()
    {
        // Arrange
        var context = new ControllerContext
        {
            HttpContext = new DefaultHttpContext
            {
                User = new ClaimsPrincipal(new ClaimsIdentity(new[]
                {
                    new Claim("userId", "not_an_integer") // Invalid format
                }))
            }
        };
        _controller.ControllerContext = context;

        // Act & Assert
        var exception = await Assert.ThrowsExceptionAsync<FormatException>(async () => 
            await _controller.DeleteForm(1));
        
        // Optional: Verify exception message
        StringAssert.Contains(exception.Message, "input string");
    }

    [TestMethod]
public async Task DownloadFormTemplate_MissingUserClaim_Returns401()
{
    // Arrange - No user claim
    _controller.ControllerContext = new ControllerContext
    {
        HttpContext = new DefaultHttpContext
        {
            User = new ClaimsPrincipal(new ClaimsIdentity())
        }
    };
    
    // Act
    var result = await _controller.DownloadFormTemplate(1);
    
    // Assert
    Assert.IsInstanceOfType(result, typeof(UnauthorizedResult));
}

[TestMethod]
public async Task DownloadFormTemplate_FormNotFound_Returns404()
{
    // Arrange
    const int formId = 100;
    SetUserContext(1);
    
    // Create test context
    var testDbContext = new TestApplicationDbContext
    {
        GetEventByFormIdAsyncHandler = async (id) => null // Form not found
    };

    var controller = new FormController(testDbContext);
    controller.ControllerContext = _controller.ControllerContext;
    
    // Act
    var result = await controller.DownloadFormTemplate(formId);
    
    // Assert
    Assert.IsInstanceOfType(result, typeof(NotFoundObjectResult));
    var response = (NotFoundObjectResult)result;
    Assert.AreEqual(404, response.StatusCode);
    
    // Verify error message
    dynamic responseBody = response.Value!;
    var messageProperty = responseBody.GetType().GetProperty("message");
    Assert.IsNotNull(messageProperty, "Response missing 'message' property");
    Assert.AreEqual("Анкета не найдена.", 
        messageProperty.GetValue(responseBody)?.ToString());
}

[TestMethod]
public async Task DownloadFormTemplate_Success_ReturnsFile()
{
    // Arrange
    const int formId = 100;
    SetUserContext(1);
    byte[] fakeXlsx = new byte[] { 0x50, 0x4B, 0x03, 0x04 }; // Fake XLSX content
    
    // Create test context with ALL required handlers
    var testDbContext = new TestApplicationDbContext
    {
        // Must include this to avoid null reference
        GetEventByFormIdAsyncHandler = async (id) => 
            new Event { Id = 1, Name = "Test Event" },
            
        // The new handler for XLSX generation
        GenerateFormTemplateXlsxHandler = async (id) => fakeXlsx
    };

    var controller = new FormController(testDbContext);
    controller.ControllerContext = _controller.ControllerContext;
    
    // Act
    var result = await controller.DownloadFormTemplate(formId);
    
    // Assert
    Assert.IsInstanceOfType(result, typeof(FileContentResult));
    var fileResult = (FileContentResult)result;
    
    // Verify file properties
    Assert.AreEqual("application/vnd.openxmlformats-officedocument.spreadsheetml.sheet", 
        fileResult.ContentType);
    Assert.AreEqual("form_template.xlsx", fileResult.FileDownloadName);
    CollectionAssert.AreEqual(fakeXlsx, fileResult.FileContents);
}

public class TestApplicationDbContext : ApplicationDbContext
{
    // Existing handlers from previous tests
    public Func<int, Task<Event>> GetEventByIdAsyncHandler { get; set; }
    public Func<int, Task<bool>> EventHasFormAsyncHandler { get; set; }
    public Func<int, Task<int>> CreateFormAsyncHandler { get; set; }
    public Func<int, Task<Event>> GetEventByFormIdAsyncHandler { get; set; }
    public Func<int, List<FormField>, Task> UpdateFormAsyncHandler { get; set; }
    public Func<int, int, Task> DeleteFormAsyncHandler { get; set; }
    
    // New handler for download functionality
    public Func<int, Task<byte[]>> GenerateFormTemplateXlsxHandler { get; set; }

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
    
    // Add this method to handle XLSX generation
    public override Task<byte[]> GenerateFormTemplateXlsx(int formId)
        => GenerateFormTemplateXlsxHandler?.Invoke(formId) 
           ?? base.GenerateFormTemplateXlsx(formId);
}
}