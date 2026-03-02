using AwesomeAssertions;
using Moq;
using ToggleServer.Api.Services;
using ToggleServer.Core.Interfaces;
using ToggleServer.Core.Models;

namespace ToggleServer.UnitTests;

public class ToggleServiceTests
{
    private readonly Mock<IFeatureToggleRepository> _repoMock;
    private readonly ToggleService _service;

    public ToggleServiceTests()
    {
        _repoMock = new Mock<IFeatureToggleRepository>();
        _service = new ToggleService(_repoMock.Object);
    }

    [Fact]
    public async Task CreateToggleAsync_WhenToggleDoesNotExist_ShouldCreateSuccessfullyWithVersion1()
    {
        // Arrange
        var newToggle = new FeatureToggle { Key = "test_toggle" };
        _repoMock.Setup(x => x.GetByKeyAsync(newToggle.Key, It.IsAny<CancellationToken>()))
            .ReturnsAsync((FeatureToggle)null!);

        // Act
        var result = await _service.CreateToggleAsync(newToggle, "op1", "Operator", CancellationToken.None);

        // Assert
        result.Should().NotBeNull();
        result.Version.Should().Be(1);
        result.Enabled.Should().BeTrue();
        result.LastUpdatedBy.Should().Be("Operator");

        _repoMock.Verify(x => x.CreateAsync(It.IsAny<FeatureToggle>(), It.IsAny<CancellationToken>()), Times.Once);
        _repoMock.Verify(x => x.InsertAuditLogAsync(It.Is<ToggleAuditLog>(l => l.Action == AuditAction.CREATE && l.Version == 1), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task CreateToggleAsync_WhenToggleExists_ShouldThrowInvalidOperationException()
    {
        // Arrange
        var newToggle = new FeatureToggle { Key = "test_toggle" };
        _repoMock.Setup(x => x.GetByKeyAsync(newToggle.Key, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new FeatureToggle { Key = "test_toggle" });

        // Act & Assert
        var act = async () => await _service.CreateToggleAsync(newToggle, "op1", "Operator", CancellationToken.None);
        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*already exists*");
    }

    [Fact]
    public async Task UpdateToggleAsync_ShouldIncrementVersionAndInsertUpdateAuditLog()
    {
        // Arrange
        var existingToggle = new FeatureToggle { Key = "test_toggle", Version = 5 };
        var incomingToggle = new FeatureToggle { Key = "test_toggle", Version = 5 }; // Version simulated from client
        
        _repoMock.Setup(x => x.GetByKeyAsync("test_toggle", It.IsAny<CancellationToken>()))
            .ReturnsAsync(existingToggle);
        
        _repoMock.Setup(x => x.UpdateAsync(It.IsAny<FeatureToggle>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(true); // Simulate success

        // Act
        var result = await _service.UpdateToggleAsync(incomingToggle, "op1", "Operator", CancellationToken.None);

        // Assert
        result.Version.Should().Be(6); // Version MUST be incremented
        _repoMock.Verify(x => x.UpdateAsync(It.Is<FeatureToggle>(t => t.Version == 6), It.IsAny<CancellationToken>()), Times.Once);
        _repoMock.Verify(x => x.InsertAuditLogAsync(It.Is<ToggleAuditLog>(l => l.Action == AuditAction.UPDATE && l.Version == 6), It.IsAny<CancellationToken>()), Times.Once);
    }
    
    [Fact]
    public async Task UpdateToggleAsync_WhenConcurrencyFails_ShouldThrowInvalidOperationException()
    {
        // Arrange
        var existingToggle = new FeatureToggle { Key = "test_toggle", Version = 5 };
        var incomingToggle = new FeatureToggle { Key = "test_toggle", Version = 5 };
        
        _repoMock.Setup(x => x.GetByKeyAsync("test_toggle", It.IsAny<CancellationToken>()))
            .ReturnsAsync(existingToggle);
        
        _repoMock.Setup(x => x.UpdateAsync(It.IsAny<FeatureToggle>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(false); // Simulate Optimistic Locking failure!

        // Act & Assert
        var act = async () => await _service.UpdateToggleAsync(incomingToggle, "op1", "Operator", CancellationToken.None);
        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*Optimistic concurrency violation*");
            
        // Log shouldn't be inserted
        _repoMock.Verify(x => x.InsertAuditLogAsync(It.IsAny<ToggleAuditLog>(), It.IsAny<CancellationToken>()), Times.Never);
    }
}
