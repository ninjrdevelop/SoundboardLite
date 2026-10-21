namespace SoundboardLite.Core.Models;

public sealed class Board
{
    public required Guid Id { get; init; }
    public required string Name { get; set; }
    public required int SortOrder { get; set; }
    public required DateTimeOffset CreatedUtc { get; init; }
    public required DateTimeOffset UpdatedUtc { get; set; }
}
