using UnityEngine;

namespace Kavkazim.Netcode
{
    public class InnocentRole : PlayerRole
    {
        public InnocentRole(PlayerAvatar avatar) : base(avatar) { }

        public override void SetupVisuals()
        {
            // Innocent: Keep original sprite colors (no tint), White name
            _avatar.SetBodyColor(Color.white); // No tint - preserve original sprite
            _avatar.SetNameColor(Color.white);
        }
    }
}
