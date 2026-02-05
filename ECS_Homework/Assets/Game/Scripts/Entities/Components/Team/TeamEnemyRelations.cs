using System.Collections.Generic;

namespace Game.Scripts.Targeting
{
    /// <summary>
    /// Утилита для определения враждебных отношений между командами
    /// Использует Dictionary для гибкой настройки вражды
    /// </summary>
    public static class TeamRelations
    {
        /// <summary>
        /// Словарь, где ключ - тип команды, значение - массив враждебных команд
        /// </summary>
        private static readonly Dictionary<TeamType, TeamType[]> EnemyRelations = new()
        {
            { TeamType.Player, new[] { TeamType.Enemy, TeamType.Boss } },
            { TeamType.Enemy, new[] { TeamType.Player, TeamType.Ally } },
            { TeamType.Neutral, new TeamType[] { } },
            { TeamType.Ally, new[] { TeamType.Enemy, TeamType.Boss } },
            { TeamType.Boss, new[] { TeamType.Player, TeamType.Ally } }
        };

        /// <summary>
        /// Проверяет, являются ли две команды врагами
        /// </summary>
        public static bool IsEnemy(TeamType currentTeam, TeamType targetTeam)
        {
            // Одна команда не враждебна сама себе
            if (currentTeam == targetTeam)
                return false;

            // Проверяем в словаре враждебных отношений
            if (EnemyRelations.ContainsKey(currentTeam))
            {
                foreach (TeamType enemy in EnemyRelations[currentTeam])
                {
                    if (enemy == targetTeam)
                        return true;
                }
            }

            return false;
        }

        /// <summary>
        /// Возвращает тип вражеского замка для указанной команды
        /// </summary>
        public static CastleTargetType GetEnemyCastleType(TeamType myTeam)
        {
            switch (myTeam)
            {
                case TeamType.Player:
                case TeamType.Ally:
                    return CastleTargetType.EnemyCastle;

                case TeamType.Enemy:
                case TeamType.Boss:
                    return CastleTargetType.PlayerCastle;

                case TeamType.Neutral:
                default:
                    return CastleTargetType.None;
            }
        }
    }

    /// <summary>
    /// Тип целевого замка
    /// </summary>
    public enum CastleTargetType
    {
        None,
        PlayerCastle,
        EnemyCastle
    }
}