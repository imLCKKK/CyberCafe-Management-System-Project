namespace CyberManagement.Domain.Entities;

public class Game
{
    public int GameId { get; set; }

    public string GameName { get; set; } = null!;

    public int TeamSize { get; set; }

    public bool IsActive { get; set; } = true;

    public ICollection<GameRank> Ranks { get; set; } = new List<GameRank>();

    public ICollection<Tournament> Tournaments { get; set; } = new List<Tournament>();
}
