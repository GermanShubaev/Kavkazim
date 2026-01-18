using UnityEngine;
using Unity.Netcode;

namespace Kavkazim.Netcode
{
    public enum PlayerRoleType
    {
        Innocent = 0,
        Kavkazi = 1
    }

    public abstract class PlayerRole
    {
        protected PlayerAvatar _avatar;

        public PlayerRole(PlayerAvatar avatar)
        {
            _avatar = avatar;
        }

        public virtual void SetupVisuals() { }
    }
}
