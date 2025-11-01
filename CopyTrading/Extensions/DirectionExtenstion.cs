using CopyTrading.Models.Models.Enums;

namespace CopyTrading.Extensions;

public static class DirectionExtenstion
{
	public static Direction Opposite(this Direction direction)
	{
		if(direction == Direction.Long) return Direction.Short;
		if(direction == Direction.Short) return Direction.Long;
		return Direction.None;
	}
}
