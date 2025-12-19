using CopyTrading.Models.Models;
using CopyTrading.Models.Models.Enums;
using CopyTrading.Models.Values;
using CopyTrading.Services;
using CopyTrading.Test.Helpers;
using FluentAssertions;
using Microsoft.Extensions.Logging;
using Moq;

namespace CopyTrading.Test.Service;

public class PositionMappingServiceTests
{
    private readonly PositionMappingService _service;
    private readonly Wallet _traderWallet;
    private readonly Wallet _myWallet;

    public PositionMappingServiceTests()
    {
        var (redisMock, _) = MockRedisRepositoryFactory.Create();
        var logger = new Mock<ILogger<PositionMappingService>>();

        _service = new PositionMappingService(redisMock.Object, logger.Object);
        _traderWallet = new Wallet("0x7bde2b9240a2ee352108c6823a9fa20f225b83a0");
        _myWallet = new Wallet("0x1234567890abcdef1234567890abcdef12345678");
    }

    [Fact]
    public async Task SaveOrUpdateMapping_ShouldAddNewMapping()
    {
        // Arrange
        var mapping = new PositionMapping
        {
            TraderWallet = _traderWallet,
            MyWallet = _myWallet,
            Symbol = "BTC",
            Direction = Direction.Long,
            MyQuantity = 0.15M,
            PositionRatio = 0.1M,
            LastUpdate = DateTime.UtcNow
        };

        // Act
        await _service.SaveOrUpdateMapping(mapping);

        // Assert
        var retrieved = await _service.GetMapping(_traderWallet, _myWallet, "BTC", Direction.Long);
        retrieved.Should().NotBeNull();
        retrieved!.MyQuantity.Should().Be(0.15M);
        retrieved.PositionRatio.Should().Be(0.1M);
    }

    [Fact]
    public async Task SaveOrUpdateMapping_ShouldUpdateExistingMapping()
    {
        // Arrange
        var mapping = new PositionMapping
        {
            TraderWallet = _traderWallet,
            MyWallet = _myWallet,
            Symbol = "ETH",
            Direction = Direction.Short,
            MyQuantity = 1M,
            PositionRatio = 0.1M,
            LastUpdate = DateTime.UtcNow
        };

        await _service.SaveOrUpdateMapping(mapping);

        // Act - обновляем количества
        mapping.MyQuantity = 1.5M;
        await _service.SaveOrUpdateMapping(mapping);

        // Assert
        var retrieved = await _service.GetMapping(_traderWallet, _myWallet, "ETH", Direction.Short);
        retrieved.Should().NotBeNull();
        retrieved.MyQuantity.Should().Be(1.5M);
        retrieved.PositionRatio.Should().Be(0.1M);
    }

    [Fact]
    public async Task GetMapping_ShouldReturnNull_WhenMappingDoesNotExist()
    {
        // Act
        var retrieved = await _service.GetMapping(_traderWallet, _myWallet, "SOL", Direction.Long);

        // Assert
        retrieved.Should().BeNull();
    }

    [Fact]
    public async Task DeleteMapping_ShouldRemoveMapping()
    {
        // Arrange
        var mapping = new PositionMapping
        {
            TraderWallet = _traderWallet,
            MyWallet = _myWallet,
            Symbol = "AVAX",
            Direction = Direction.Long,
            MyQuantity = 10M,
            PositionRatio = 0.1M,
            LastUpdate = DateTime.UtcNow
        };

        await _service.SaveOrUpdateMapping(mapping);

        // Act
        var deleted = await _service.DeleteMapping(_traderWallet, _myWallet, "AVAX", Direction.Long);

        // Assert
        deleted.Should().BeTrue();
        var retrieved = await _service.GetMapping(_traderWallet, _myWallet, "AVAX", Direction.Long);
        retrieved.Should().BeNull();
    }

    [Fact]
    public async Task DeleteMapping_ShouldReturnFalse_WhenMappingDoesNotExist()
    {
        // Act
        var deleted = await _service.DeleteMapping(_traderWallet, _myWallet, "DOGE", Direction.Short);

        // Assert
        deleted.Should().BeFalse();
    }

    [Fact]
    public async Task UpdateMyQuantity_ShouldUpdateQuantity_WhenMappingExists()
    {
        // Arrange
        var mapping = new PositionMapping
        {
            TraderWallet = _traderWallet,
            MyWallet = _myWallet,
            Symbol = "ADA",
            Direction = Direction.Short,
            MyQuantity = 100M,
            PositionRatio = 0.1M,
            LastUpdate = DateTime.UtcNow
        };

        await _service.SaveOrUpdateMapping(mapping);

        // Act
        var updated = await _service.UpdateMyQuantity(_traderWallet, _myWallet, "ADA", Direction.Short, 150M);

        // Assert
        updated.Should().BeTrue();
        var retrieved = await _service.GetMapping(_traderWallet, _myWallet, "ADA", Direction.Short);
        retrieved!.MyQuantity.Should().Be(150M);
    }

    [Fact]
    public async Task UpdateMyQuantity_ShouldReturnFalse_WhenMappingDoesNotExist()
    {
        // Act
        var updated = await _service.UpdateMyQuantity(_traderWallet, _myWallet, "DOT", Direction.Long, 50M);

        // Assert
        updated.Should().BeFalse();
    }

    [Fact]
    public async Task GetMappingsByTrader_ShouldReturnAllMappingsForTrader()
    {
        // Arrange
        var mapping1 = new PositionMapping
        {
            TraderWallet = _traderWallet,
            MyWallet = _myWallet,
            Symbol = "BTC",
            Direction = Direction.Long,
            MyQuantity = 0.1M,
            PositionRatio = 0.1M,
            LastUpdate = DateTime.UtcNow
        };

        var mapping2 = new PositionMapping
        {
            TraderWallet = _traderWallet,
            MyWallet = _myWallet,
            Symbol = "ETH",
            Direction = Direction.Short,
            MyQuantity = 1M,
            PositionRatio = 0.1M,
            LastUpdate = DateTime.UtcNow
        };

        var mapping3 = new PositionMapping
        {
            TraderWallet = _traderWallet,
            MyWallet = _myWallet,
            Symbol = "BTC",
            Direction = Direction.Short, // Другое направление
            MyQuantity = 0.05M,
            PositionRatio = 0.1M,
            LastUpdate = DateTime.UtcNow
        };

        await _service.SaveOrUpdateMapping(mapping1);
        await _service.SaveOrUpdateMapping(mapping2);
        await _service.SaveOrUpdateMapping(mapping3);

        // Act
        var mappings = await _service.GetMappingsByTrader(_traderWallet, _myWallet);

        // Assert
        mappings.Should().HaveCount(3);
        mappings.Should().Contain(m => m.Symbol == "BTC" && m.Direction == Direction.Long);
        mappings.Should().Contain(m => m.Symbol == "ETH" && m.Direction == Direction.Short);
        mappings.Should().Contain(m => m.Symbol == "BTC" && m.Direction == Direction.Short);
    }

    [Fact]
    public async Task GetMappingsByTrader_ShouldReturnEmpty_WhenNoMappingsExist()
    {
        // Act
        var mappings = await _service.GetMappingsByTrader(_traderWallet, _myWallet);

        // Assert
        mappings.Should().BeEmpty();
    }

    [Fact]
    public async Task GetAllMappings_ShouldReturnAllMappings()
    {
        // Arrange
        var anotherTrader = new Wallet("0xabcdef1234567890abcdef1234567890abcdef12");

        var mapping1 = new PositionMapping
        {
            TraderWallet = _traderWallet,
            MyWallet = _myWallet,
            Symbol = "BTC",
            Direction = Direction.Long,
            MyQuantity = 0.1M,
            PositionRatio = 0.1M,
            LastUpdate = DateTime.UtcNow
        };

        var mapping2 = new PositionMapping
        {
            TraderWallet = anotherTrader,
            MyWallet = _myWallet,
            Symbol = "ETH",
            Direction = Direction.Long,
            MyQuantity = 1M,
            PositionRatio = 0.1M,
            LastUpdate = DateTime.UtcNow
        };

        await _service.SaveOrUpdateMapping(mapping1);
        await _service.SaveOrUpdateMapping(mapping2);

        // Act
        var allMappings = await _service.GetAllMappings();

        // Assert
        allMappings.Should().HaveCount(2);
    }

    [Fact]
    public async Task GetMappingsCount_ShouldReturnCorrectCount()
    {
        // Arrange
        var mapping1 = new PositionMapping
        {
            TraderWallet = _traderWallet,
            MyWallet = _myWallet,
            Symbol = "BTC",
            Direction = Direction.Long,
            MyQuantity = 0.1M,
            PositionRatio = 0.1M,
            LastUpdate = DateTime.UtcNow
        };

        var mapping2 = new PositionMapping
        {
            TraderWallet = _traderWallet,
            MyWallet = _myWallet,
            Symbol = "ETH",
            Direction = Direction.Long,
            MyQuantity = 1M,
            PositionRatio = 0.1M,
            LastUpdate = DateTime.UtcNow
        };

        // Act & Assert
        _service.GetMappingsCount.Should().Be(0);

        await _service.SaveOrUpdateMapping(mapping1);
        _service.GetMappingsCount.Should().Be(1);

        await _service.SaveOrUpdateMapping(mapping2);
        _service.GetMappingsCount.Should().Be(2);

        await _service.DeleteMapping(_traderWallet, _myWallet, "BTC", Direction.Long);
        _service.GetMappingsCount.Should().Be(1);
    }

    [Fact]
    public async Task HedgePositions_ShouldBeMaintainedSeparately()
    {
        // Arrange - создаем Long и Short позиции по одному символу
        var longMapping = new PositionMapping
        {
            TraderWallet = _traderWallet,
            MyWallet = _myWallet,
            Symbol = "BTC",
            Direction = Direction.Long,
            MyQuantity = 0.1M,
            PositionRatio = 0.1M,
            LastUpdate = DateTime.UtcNow
        };

        var shortMapping = new PositionMapping
        {
            TraderWallet = _traderWallet,
            MyWallet = _myWallet,
            Symbol = "BTC",
            Direction = Direction.Short,
            MyQuantity = 0.05M,
            PositionRatio = 0.1M,
            LastUpdate = DateTime.UtcNow
        };

        // Act
        await _service.SaveOrUpdateMapping(longMapping);
        await _service.SaveOrUpdateMapping(shortMapping);

        // Assert - обе позиции должны существовать независимо
        var longRetrieved = await _service.GetMapping(_traderWallet, _myWallet, "BTC", Direction.Long);
        var shortRetrieved = await _service.GetMapping(_traderWallet, _myWallet, "BTC", Direction.Short);

        longRetrieved.Should().NotBeNull();
        shortRetrieved.Should().NotBeNull();

        longRetrieved!.MyQuantity.Should().Be(0.1M);
        shortRetrieved!.MyQuantity.Should().Be(0.05M);

        // Удаление Long не должно влиять на Short
        await _service.DeleteMapping(_traderWallet, _myWallet, "BTC", Direction.Long);

        longRetrieved = await _service.GetMapping(_traderWallet, _myWallet, "BTC", Direction.Long);
        shortRetrieved = await _service.GetMapping(_traderWallet, _myWallet, "BTC", Direction.Short);

        longRetrieved.Should().BeNull();
        shortRetrieved.Should().NotBeNull();
    }

    [Fact]
    public async Task SaveOrUpdateMapping_ShouldUpdateLastUpdateTime()
    {
        // Arrange
        var mapping = new PositionMapping
        {
            TraderWallet = _traderWallet,
            MyWallet = _myWallet,
            Symbol = "LINK",
            Direction = Direction.Long,
            MyQuantity = 10M,
            PositionRatio = 0.1M,
            LastUpdate = DateTime.UtcNow.AddHours(-1) // Старая дата
        };

        await _service.SaveOrUpdateMapping(mapping);
        var firstSave = await _service.GetMapping(_traderWallet, _myWallet, "LINK", Direction.Long);
        var firstTimestamp = firstSave!.LastUpdate;

        // Act - небольшая задержка чтобы время точно изменилось
        Thread.Sleep(10);
        mapping.MyQuantity = 15M;
        await _service.SaveOrUpdateMapping(mapping);

        // Assert
        var secondSave = await _service.GetMapping(_traderWallet, _myWallet, "LINK", Direction.Long);
        secondSave!.LastUpdate.Should().BeAfter(firstTimestamp);
    }
}
