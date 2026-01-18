namespace Kavkazim.Netcode
{
    public static class RoleVisibilityService
    {
        public static PlayerRoleType GetPerceivedRole(PlayerRoleType observerTrueRole, PlayerRoleType targetTrueRole)
        {
            if (observerTrueRole == PlayerRoleType.Kavkazi && targetTrueRole == PlayerRoleType.Kavkazi)
            {
                return PlayerRoleType.Kavkazi;
            }
            
            return PlayerRoleType.Innocent;
        }
    }
}
