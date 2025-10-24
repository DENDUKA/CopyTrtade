using CopyTrading.Models.Models;
using CopyTrading.Models.Models.Enums;
using CopyTrading.Models.Values;
using CopyTrading.Services;
using FluentAssertions;
using Microsoft.Extensions.Logging;
using Moq;

namespace CopyTriding.Test.Service;

public class PositionMappingServiceTests
{
    private readonly PositionMappingService _service;
    private readonly Wallet _traderWallet;
    private readonly Wallet _myWallet;

    public PositionMappingServiceTests()
    {
        var logger = new Mock<ILogger<PositionMappingService>>();
        _service = new PositionMappingService(logger.Object);
        _traderWallet = new Wallet("0x7bde2b9240a2ee352108c6823a9fa20f225b83a0");
        _myWallet = new Wallet("0x1234567890abcdef1234567890abcdef12345678");
    }

    [Fact]
    public void SaveOrUpdateMapping_ShouldAddNewMapping()
    {
        // Arrange
        var mapping = new PositionMapping
        {
            TraderWallet = _traderWallet,
            MyWallet = _myWallet,
            Symbol = "BTC",
            Direction = Direction.Long,
            TraderQuantity = 1.5M,
            MyQuantity = 0.15M,
            PositionRatio = 0.1M,
            LastUpdate = DateTime.UtcNow
        };

        // Act
        _service.SaveOrUpdateMapping(mapping);

        // Assert
        var retrieved = _service.GetMapping(_traderWallet, _myWallet, "BTC", Direction.Long);
        retrieved.Should().NotBeNull();
        retrieved!.TraderQuantity.Should().Be(1.5M);
        retrieved.MyQuantity.Should().Be(0.15M);
        retrieved.PositionRatio.Should().Be(0.1M);
    }

    [Fact]
    public void SaveOrUpdateMapping_ShouldUpdateExistingMapping()
    {
        // Arrange
        var mapping = new PositionMapping
        {
            TraderWallet = _traderWallet,
            MyWallet = _myWallet,
            Symbol = "ETH",
            Direction = Direction.Short,
            TraderQuantity = 10M,
            MyQuantity = 1M,
            PositionRatio = 0.1M,
            LastUpdate = DateTime.UtcNow
        };

        _service.SaveOrUpdateMapping(mapping);

        // Act - обновляем количества
        mapping.TraderQuantity = 15M;
        mapping.MyQuantity = 1.5M;
        _service.SaveOrUpdateMapping(mapping);

        // Assert
        var retrieved = _service.GetMapping(_traderWallet, _myWallet, "ETH", Direction.Short);
        retrieved.Should().NotBeNull();
        retrieved!.TraderQuantity.Should().Be(15M);
        retrieved.MyQuantity.Should().Be(1.5M);
        retrieved.PositionRatio.Should().Be(0.1M);
    }

    [Fact]
    public void GetMapping_ShouldReturnNull_WhenMappingDoesNotExist()
    {
        // Act
        var retrieved = _service.GetMapping(_traderWallet, _myWallet, "SOL", Direction.Long);

        // Assert
        retrieved.Should().BeNull();
    }

    [Fact]
    public void DeleteMapping_ShouldRemoveMapping()
    {
        // Arrange
        var mapping = new PositionMapping
        {
            TraderWallet = _traderWallet,
            MyWallet = _myWallet,
            Symbol = "AVAX",
            Direction = Direction.Long,
            TraderQuantity = 100M,
            MyQuantity = 10M,
            PositionRatio = 0.1M,
            LastUpdate = DateTime.UtcNow
        };

        _service.SaveOrUpdateMapping(mapping);

        // Act
        var deleted = _service.DeleteMapping(_traderWallet, _myWallet, "AVAX", Direction.Long);

        // Assert
        deleted.Should().BeTrue();
        var retrieved = _service.GetMapping(_traderWallet, _myWallet, "AVAX", Direction.Long);
        retrieved.Should().BeNull();
    }

    [Fact]
    public void DeleteMapping_ShouldReturnFalse_WhenMappingDoesNotExist()
    {
        // Act
        var deleted = _service.DeleteMapping(_traderWallet, _myWallet, "DOGE", Direction.Short);

        // Assert
        deleted.Should().BeFalse();
    }

    [Fact]
    public void UpdateTraderQuantity_ShouldUpdateQuantity_WhenMappingExists()
    {
        // Arrange
        var mapping = new PositionMapping
        {
            TraderWallet = _traderWallet,
            MyWallet = _myWallet,
            Symbol = "BNB",
            Direction = Direction.Long,
            TraderQuantity = 5M,
            MyQuantity = 0.5M,
            PositionRatio = 0.1M,
            LastUpdate = DateTime.UtcNow
        };

        _service.SaveOrUpdateMapping(mapping);

        // Act
        var updated = _service.UpdateTraderQuantity(_traderWallet, _myWallet, "BNB", Direction.Long, 7.5M);

        // Assert
        updated.Should().BeTrue();
        var retrieved = _service.GetMapping(_traderWallet, _myWallet, "BNB", Direction.Long);
        retrieved!.TraderQuantity.Should().Be(7.5M);
        retrieved.MyQuantity.Should().Be(0.5M); // не изменилось
    }

    [Fact]
    public void UpdateTraderQuantity_ShouldReturnFalse_WhenMappingDoesNotExist()
    {
        // Act
        var updated = _service.UpdateTraderQuantity(_traderWallet, _myWallet, "XRP", Direction.Short, 100M);

        // Assert
        updated.Should().BeFalse();
    }

    [Fact]
    public void UpdateMyQuantity_ShouldUpdateQuantity_WhenMappingExists()
    {
        // Arrange
        var mapping = new PositionMapping
        {
            TraderWallet = _traderWallet,
            MyWallet = _myWallet,
            Symbol = "ADA",
            Direction = Direction.Short,
            TraderQuantity = 1000M,
            MyQuantity = 100M,
            PositionRatio = 0.1M,
            LastUpdate = DateTime.UtcNow
        };

        _service.SaveOrUpdateMapping(mapping);

        // Act
        var updated = _service.UpdateMyQuantity(_traderWallet, _myWallet, "ADA", Direction.Short, 150M);

        // Assert
        updated.Should().BeTrue();
        var retrieved = _service.GetMapping(_traderWallet, _myWallet, "ADA", Direction.Short);
        retrieved!.MyQuantity.Should().Be(150M);
        retrieved.TraderQuantity.Should().Be(1000M); // не изменилось
    }

    [Fact]
    public void UpdateMyQuantity_ShouldReturnFalse_WhenMappingDoesNotExist()
    {
        // Act
        var updated = _service.UpdateMyQuantity(_traderWallet, _myWallet, "DOT", Direction.Long, 50M);

        // Assert
        updated.Should().BeFalse();
    }

    [Fact]
    public void GetMappingsByTrader_ShouldReturnAllMappingsForTrader()
    {
        // Arrange
        var mapping1 = new PositionMapping
        {
            TraderWallet = _traderWallet,
            MyWallet = _myWallet,
            Symbol = "BTC",
            Direction = Direction.Long,
            TraderQuantity = 1M,
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
            TraderQuantity = 10M,
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
            TraderQuantity = 0.5M,
            MyQuantity = 0.05M,
            PositionRatio = 0.1M,
            LastUpdate = DateTime.UtcNow
        };

        _service.SaveOrUpdateMapping(mapping1);
        _service.SaveOrUpdateMapping(mapping2);
        _service.SaveOrUpdateMapping(mapping3);

        // Act
        var mappings = _service.GetMappingsByTrader(_traderWallet, _myWallet);

        // Assert
        mappings.Should().HaveCount(3);
        mappings.Should().Contain(m => m.Symbol == "BTC" && m.Direction == Direction.Long);
        mappings.Should().Contain(m => m.Symbol == "ETH" && m.Direction == Direction.Short);
        mappings.Should().Contain(m => m.Symbol == "BTC" && m.Direction == Direction.Short);
    }

    [Fact]
    public void GetMappingsByTrader_ShouldReturnEmpty_WhenNoMappingsExist()
    {
        // Act
        var mappings = _service.GetMappingsByTrader(_traderWallet, _myWallet);

        // Assert
        mappings.Should().BeEmpty();
    }

    [Fact]
    public void GetAllMappings_ShouldReturnAllMappings()
    {
        // Arrange
        var anotherTrader = new Wallet("0xabcdef1234567890abcdef1234567890abcdef12");

        var mapping1 = new PositionMapping
        {
            TraderWallet = _traderWallet,
            MyWallet = _myWallet,
            Symbol = "BTC",
            Direction = Direction.Long,
            TraderQuantity = 1M,
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
            TraderQuantity = 10M,
            MyQuantity = 1M,
            PositionRatio = 0.1M,
            LastUpdate = DateTime.UtcNow
        };

        _service.SaveOrUpdateMapping(mapping1);
        _service.SaveOrUpdateMapping(mapping2);

        // Act
        var allMappings = _service.GetAllMappings();

        // Assert
        allMappings.Should().HaveCount(2);
    }

    [Fact]
    public void ClearAllMappings_ShouldRemoveAllMappings()
    {
        // Arrange
        var mapping1 = new PositionMapping
        {
            TraderWallet = _traderWallet,
            MyWallet = _myWallet,
            Symbol = "BTC",
            Direction = Direction.Long,
            TraderQuantity = 1M,
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
            TraderQuantity = 10M,
            MyQuantity = 1M,
            PositionRatio = 0.1M,
            LastUpdate = DateTime.UtcNow
        };

        _service.SaveOrUpdateMapping(mapping1);
        _service.SaveOrUpdateMapping(mapping2);

        // Act
        _service.ClearAllMappings();

        // Assert
        var count = _service.GetMappingsCount;
        count.Should().Be(0);
        _service.GetAllMappings().Should().BeEmpty();
    }

    [Fact]
    public void GetMappingsCount_ShouldReturnCorrectCount()
    {
        // Arrange
        var mapping1 = new PositionMapping
        {
            TraderWallet = _traderWallet,
            MyWallet = _myWallet,
            Symbol = "BTC",
            Direction = Direction.Long,
            TraderQuantity = 1M,
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
            TraderQuantity = 10M,
            MyQuantity = 1M,
            PositionRatio = 0.1M,
            LastUpdate = DateTime.UtcNow
        };

        // Act & Assert
        _service.GetMappingsCount.Should().Be(0);

        _service.SaveOrUpdateMapping(mapping1);
        _service.GetMappingsCount.Should().Be(1);

        _service.SaveOrUpdateMapping(mapping2);
        _service.GetMappingsCount.Should().Be(2);

        _service.DeleteMapping(_traderWallet, _myWallet, "BTC", Direction.Long);
        _service.GetMappingsCount.Should().Be(1);
    }

    [Fact]
    public void HedgePositions_ShouldBeMaintainedSeparately()
    {
        // Arrange - создаем Long и Short позиции по одному символу
        var longMapping = new PositionMapping
        {
            TraderWallet = _traderWallet,
            MyWallet = _myWallet,
            Symbol = "BTC",
            Direction = Direction.Long,
            TraderQuantity = 1M,
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
            TraderQuantity = 0.5M,
            MyQuantity = 0.05M,
            PositionRatio = 0.1M,
            LastUpdate = DateTime.UtcNow
        };

        // Act
        _service.SaveOrUpdateMapping(longMapping);
        _service.SaveOrUpdateMapping(shortMapping);

        // Assert - обе позиции должны существовать независимо
        var longRetrieved = _service.GetMapping(_traderWallet, _myWallet, "BTC", Direction.Long);
        var shortRetrieved = _service.GetMapping(_traderWallet, _myWallet, "BTC", Direction.Short);

        longRetrieved.Should().NotBeNull();
        shortRetrieved.Should().NotBeNull();

        longRetrieved!.TraderQuantity.Should().Be(1M);
        shortRetrieved!.TraderQuantity.Should().Be(0.5M);

        // Удаление Long не должно влиять на Short
        _service.DeleteMapping(_traderWallet, _myWallet, "BTC", Direction.Long);

        longRetrieved = _service.GetMapping(_traderWallet, _myWallet, "BTC", Direction.Long);
        shortRetrieved = _service.GetMapping(_traderWallet, _myWallet, "BTC", Direction.Short);

        longRetrieved.Should().BeNull();
        shortRetrieved.Should().NotBeNull();
    }

    [Fact]
    public void SaveOrUpdateMapping_ShouldUpdateLastUpdateTime()
    {
        // Arrange
        var mapping = new PositionMapping
        {
            TraderWallet = _traderWallet,
            MyWallet = _myWallet,
            Symbol = "LINK",
            Direction = Direction.Long,
            TraderQuantity = 100M,
            MyQuantity = 10M,
            PositionRatio = 0.1M,
            LastUpdate = DateTime.UtcNow.AddHours(-1) // Старая дата
        };

        _service.SaveOrUpdateMapping(mapping);
        var firstSave = _service.GetMapping(_traderWallet, _myWallet, "LINK", Direction.Long);
        var firstTimestamp = firstSave!.LastUpdate;

        // Act - небольшая задержка чтобы время точно изменилось
        System.Threading.Thread.Sleep(10);
        mapping.TraderQuantity = 150M;
        _service.SaveOrUpdateMapping(mapping);

        // Assert
        var secondSave = _service.GetMapping(_traderWallet, _myWallet, "LINK", Direction.Long);
        secondSave!.LastUpdate.Should().BeAfter(firstTimestamp);
    }
}
