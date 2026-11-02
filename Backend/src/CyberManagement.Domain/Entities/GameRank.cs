namespace CyberManagement.Domain.Entities;

public class GameRank
{
    public int RankId { get; set; }

    public int GameId { get; set; }

    public string RankName { get; set; } = null!;

    public int RankOrder { get; set; }

    public Game Game { get; set; } = null!;
}
