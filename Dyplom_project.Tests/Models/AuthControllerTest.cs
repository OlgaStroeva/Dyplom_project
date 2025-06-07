using System.Collections.Generic;
using Dyplom_project.Models;
using JetBrains.Annotations;
using Microsoft.Extensions.Configuration;

namespace Dyplom_project.Tests.Models;

using Microsoft.AspNetCore.Mvc;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Moq;
using System.Threading.Tasks;

[TestClass]
[TestSubject(typeof(AuthController))]
public class AuthControllerTest
{
    private Mock<ApplicationDbContext> _dbContextMock = null!;
    private AuthController _controller = null!;
    private Mock<IEmailService> _iEmaiService = null!;

    [TestInitialize]
    public void Setup()
    {
        _dbContextMock = new Mock<ApplicationDbContext>();
        _iEmaiService = new Mock<IEmailService>();

        var configMock = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                { "Jwt:Key", "SuperSecretTestKey1234567890123456" },
                { "Jwt:Issuer", "TestIssuer" },
                { "Jwt:Audience", "TestAudience" },
                { "Jwt:ExpireMinutes", "60" }
            })
            .Build();

        var jwtService = new JwtService(configMock);

        _controller = new AuthController(_dbContextMock.Object, jwtService, _iEmaiService.Object);
    }


    [TestMethod]
    public async Task Register_ShouldReturnOk_WhenSuccessful()
    {
        var request = new RegisterRequest
        {
            Name = "Test User",
            Email = "test@gmail.com",
            Password = "password123"
        };

        _dbContextMock.Setup(x => x.GetUserByEmailAsync(request.Email))
            .ReturnsAsync((User?)null);

        _dbContextMock.Setup(x => x.CreateUserAsync(It.IsAny<User>()))
            .Returns(Task.CompletedTask);
        
        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                { "Jwt:Key", "SuperSecretTestKey1234567890123456" },
                { "Jwt:Issuer", "TestIssuer" },
                { "Jwt:Audience", "TestAudience" },
                { "Jwt:ExpireMinutes", "60" }
            })
            .Build();
        
        var jwtService = new JwtService(config);
        var controller = new AuthController(_dbContextMock.Object, jwtService, _iEmaiService.Object);

        var result = await controller.Register(request);
        var okResult = result as OkObjectResult;
        
        Assert.IsNotNull(okResult);
        Assert.AreEqual(200, okResult.StatusCode);
    }


    [TestMethod]
    public async Task Register_ShouldReturnConflict_WhenEmailExists()
    {
        var request = new RegisterRequest
        {
            Name = "Existing User",
            Email = "existing@gmail.com",
            Password = "password123"
        };

        _dbContextMock.Setup(x => x.GetUserByEmailAsync(request.Email))
            .ReturnsAsync(new User());

        var result = await _controller.Register(request);
        var conflict = result as ConflictObjectResult;

        Assert.IsNotNull(conflict);
        Assert.AreEqual(409, conflict.StatusCode);
    }


    [TestMethod]
    public async Task Login_ShouldReturnOk_WhenSuccessful()
    {
        var request = new LoginRequest
        {
            Email = "test@example.com",
            Password = "password123"
        };

        var user = new User
        {
            Id = 1,
            Email = request.Email,
            Name = "Test User",
            PasswordHash = BCrypt.Net.BCrypt.HashPassword("password123"),
            IsEmailConfirmed = true
        };

        _dbContextMock.Setup(x => x.GetUserByEmailAsync(request.Email))
            .ReturnsAsync(user);

        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                { "Jwt:Key", "SuperSecretTestKey1234567890123456" },
                { "Jwt:Issuer", "TestIssuer" },
                { "Jwt:Audience", "TestAudience" },
                { "Jwt:ExpireMinutes", "60" }
            })
            .Build();
        
        var jwtService = new JwtService(config);
        var controller = new AuthController(_dbContextMock.Object, jwtService, _iEmaiService.Object);

        var result = await controller.Login(request);
        var okResult = result as OkObjectResult;

        Assert.IsNotNull(okResult);
        Assert.AreEqual(200, okResult.StatusCode);

    }


    [TestMethod]
    public async Task Login_ShouldReturnUnauthorized_WhenPasswordIncorrect()
    {
        var request = new LoginRequest
        {
            Email = "test@example.com",
            Password = "wrongpass"
        };

        var user = new User
        {
            Email = request.Email,
            PasswordHash = BCrypt.Net.BCrypt.HashPassword("password123"),
            IsEmailConfirmed = true
        };

        _dbContextMock.Setup(x => x.GetUserByEmailAsync(It.IsAny<string>()))
                      .ReturnsAsync(user);

        var result = await _controller.Login(request);
        var unauthorized = result as UnauthorizedObjectResult;

        Assert.IsNotNull(unauthorized);
        Assert.AreEqual(401, unauthorized.StatusCode);
    }
}
