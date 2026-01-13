using UnityEngine;
using Unity.Netcode;

namespace Kavkazim.Netcode
{
    public enum PlayerRoleType
    {
        Innocent = 0,
        Kavkazi = 1
    }

    /// <summary>
    /// Abstract base class for all player roles.
    /// </summary>
    public abstract class PlayerRole
    {
        protected PlayerAvatar _avatar;

        public PlayerRole(PlayerAvatar avatar)
        {
            _avatar = avatar;
        }

        /// <summary>
        /// Called when this role is assigned to the player.
        /// Use this to set up visuals.
        /// </summary>
        public virtual void SetupVisuals() { }
    }
}
