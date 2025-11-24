using UnityEngine;

namespace Kavkazim.Netcode
{
    public class InnocentRole : PlayerRole
    {
        public override PlayerRoleType RoleType => PlayerRoleType.Innocent;

        public InnocentRole(PlayerAvatar avatar) : base(avatar) { }

        public override void SetupVisuals()
        {
            // Innocent: Green body, White name
            _avatar.SetBodyColor(new Color(0.0f, 1f, 0.0f)); // Green
            _avatar.SetNameColor(Color.white);
        }
    }
}
