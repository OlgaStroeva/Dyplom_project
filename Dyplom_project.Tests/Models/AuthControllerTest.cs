using System.Collections.Generic;
using JetBrains.Annotations;
using Microsoft.Extensions.Configuration;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Dyplom_project.Tests.Models;

using Microsoft.AspNetCore.Mvc;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Moq;
using System.Threading.Tasks;

[TestClass]
[TestSubject(typeof(AuthController))]
public class AuthControllerTest
{
    private Mock<ApplicationDbContext> _mockDbContext;
    private Mock<JwtService> _mockJwtService;
    private AuthController _controller;

    [TestInitialize]
    public void Setup()
    {
        _mockDbContext = new Mock<ApplicationDbContext>();

        var configMock = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                { "Jwt:Key", "SuperSecretTestKey123456" },
                { "Jwt:Issuer", "TestIssuer" },
                { "Jwt:Audience", "TestAudience" },
                { "Jwt:ExpireMinutes", "60" }
            })
            .Build(); 

        var jwtService = new JwtService(configMock);

        _controller = new AuthController(_mockDbContext.Object, jwtService);
    }

    /// <summary>
    /// Тест успешной регистрации
    /// </summary>
    [TestMethod]
    public async Task Register_ShouldReturnOk_WhenRegistrationIsSuccessful()
    {
        var request = new RegisterRequest { Name = "Olga", Email = "olga@duck.com", Password = "mypassword" };

        _mockDbContext.Setup(db => db.GetUserByEmailAsync(request.Email))
            .ReturnsAsync((User)null);

        _mockDbContext.Setup(db => db.CreateUserAsync(It.IsAny<User>()))
            .Returns(Task.CompletedTask);

        var result = await _controller.Register(request);
        var okResult = result as OkObjectResult;

        Assert.IsNotNull(okResult);
        Assert.AreEqual(200, okResult.StatusCode);
    }

    /// <summary>
    /// Тест регистрации с уже существующим email
    /// </summary>
    [TestMethod]
    public async Task Register_ShouldReturnConflict_WhenEmailAlreadyExists()
    {
        var request = new RegisterRequest { Name = "Olga", Email = "olga@duck.com", Password = "mypassword" };

        _mockDbContext.Setup(db => db.GetUserByEmailAsync(request.Email))
            .ReturnsAsync(new User { Id = 1, Email = request.Email });

        var result = await _controller.Register(request);
        var conflictResult = result as ConflictObjectResult;

        Assert.IsNotNull(conflictResult);
        Assert.AreEqual(409, conflictResult.StatusCode);
    }

    /// <summary>
    /// Тест логина с недопустимым адресом электронной почты
    /// </summary>
    [TestMethod]
    public async Task Login_ShouldNotReturnToken_WhenCredentialsAreNotValid()
    {

    }

    /// <summary>
    /// Тест логина с неверным паролем
    /// </summary>
    [TestMethod]
    public async Task Login_ShouldReturnUnauthorized_WhenCredentialsAreInvalid()
    {
        var request = new LoginRequest { Email = "olga@example.com", Password = "wrongpassword" };

        _mockDbContext.Setup(db => db.GetUserByEmailAsync(request.Email))
            .ReturnsAsync((User)null);

        var result = await _controller.Login(request);
        var unauthorizedResult = result as UnauthorizedObjectResult;

        Assert.IsNotNull(unauthorizedResult);
        Assert.AreEqual(401, unauthorizedResult.StatusCode);
    }
}
