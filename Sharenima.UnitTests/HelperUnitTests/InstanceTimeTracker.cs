using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Sharenima.UnitTests.Mocks;
using StackExchange.Redis;

namespace Sharenima.UnitTests.HelperUnitTests;

public class InstanceTimeTracker {
    private Mock<IConnectionMultiplexer> _connectionMultiplexerMock { get; set; }
    private NullLoggerFactory _LoggerFactory { get; set; }

    [SetUp]
    public void Setup() {
        var mockMultiplexer = new Mock<IConnectionMultiplexer>();
        mockMultiplexer.Setup(item => item.IsConnected).Returns(false);
        mockMultiplexer.Setup(connectionMultiplexer => connectionMultiplexer.GetDatabase(It.IsAny<int>(), It.IsAny<object>())).Returns(new MockDatabase());
        _connectionMultiplexerMock = mockMultiplexer;
        _LoggerFactory = new NullLoggerFactory();
    }

    [Test]
    public void Test() {
        Server.Services.InstanceTimeTracker instanceTimeTracker = new Server.Services.InstanceTimeTracker(_LoggerFactory, _connectionMultiplexerMock.Object);
        Guid instanceId = new Guid();
        instanceTimeTracker.Upsert(instanceId, TimeSpan.FromHours(1));
        Assert.IsTrue(instanceTimeTracker.GetInstanceTime(instanceId).HasValue);
    }
}