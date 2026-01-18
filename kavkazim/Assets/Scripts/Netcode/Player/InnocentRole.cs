using UnityEngine;

namespace Kavkazim.Netcode
{
    public class InnocentRole : PlayerRole
    {
        public InnocentRole(PlayerAvatar avatar) : base(avatar) { }

        public override void SetupVisuals()
        {
            _avatar.SetBodyColor(Color.white);
            _avatar.SetNameColor(Color.white);
        }
    }
}
