namespace Services.Common.Mapping;

/// <summary>
/// Mapping helpers for game catalog entities.
/// </summary>
public static class GameMappers
{
    public static GameDto ToGameDto(this Game game)
    {
        ArgumentNullException.ThrowIfNull(game);
        return new GameDto(game.Id, game.Name);
    }

    public static UserGameDto ToUserGameDto(this UserGame userGame, string gameName)
    {
        ArgumentNullException.ThrowIfNull(userGame);

        return new UserGameDto(
            GameId: userGame.GameId,
            GameName: gameName,
            InGameName: userGame.InGameName,
            Skill: userGame.Skill,
            AddedAt: userGame.AddedAt
        );
    }
}
