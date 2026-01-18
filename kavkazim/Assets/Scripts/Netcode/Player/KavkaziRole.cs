using UnityEngine;

namespace Kavkazim.Netcode
{
    public class KavkaziRole : PlayerRole
    {

        private KillerAbility _killerAbility;

        public KavkaziRole(PlayerAvatar avatar) : base(avatar) 
        {
            _killerAbility = avatar.GetComponent<KillerAbility>();
        }

        public override void SetupVisuals()
        {
            _avatar.SetBodyColor(new Color(0.75f, 0.75f, 0.75f));
            _avatar.SetNameColor(Color.red);
        }

        public void TryKill()
        {
            if (_killerAbility == null)
            {
                return;
            }

            _killerAbility.TryKill();
        }
    }
}
