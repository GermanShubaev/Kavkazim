namespace Kavkazim.Netcode
{
    /// <summary>
    /// Static utility class for role visibility rules.
    /// Determines what role an observer should perceive for any target player.
    /// </summary>
    public static class RoleVisibilityService
    {
        /// <summary>
        /// Calculate what role the observer should see for the target.
        /// </summary>
        /// <param name="observerTrueRole">The observer's actual role</param>
        /// <param name="targetTrueRole">The target's actual role</param>
        /// <returns>The role the observer should perceive</returns>
        public static PlayerRoleType GetPerceivedRole(PlayerRoleType observerTrueRole, PlayerRoleType targetTrueRole)
        {
            // Kavkazi can see other Kavkazi as Kavkazi
            if (observerTrueRole == PlayerRoleType.Kavkazi && targetTrueRole == PlayerRoleType.Kavkazi)
            {
                return PlayerRoleType.Kavkazi;
            }
            
            // Everyone else sees everyone (including themselves if Innocent) as Innocent
            // Innocents see all players as Innocent
            // Kavkazi see non-Kavkazi as Innocent
            return PlayerRoleType.Innocent;
        }

        /// <summary>
        /// Check if the observer can see the target's true role.
        /// </summary>
        public static bool CanSeeTrue(PlayerRoleType observerTrueRole, PlayerRoleType targetTrueRole)
        {
            return GetPerceivedRole(observerTrueRole, targetTrueRole) == targetTrueRole;
        }
    }
}
