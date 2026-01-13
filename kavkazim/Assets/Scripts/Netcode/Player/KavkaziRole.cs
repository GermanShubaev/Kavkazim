using UnityEngine;

namespace Kavkazim.Netcode
{
    /// <summary>
    /// Kavkazi (impostor) role implementation.
    /// Kill functionality is delegated to the KillerAbility component.
    /// </summary>
    public class KavkaziRole : PlayerRole
    {

        private KillerAbility _killerAbility;

        public KavkaziRole(PlayerAvatar avatar) : base(avatar) 
        {
            // Cache reference to KillerAbility component
            _killerAbility = avatar.GetComponent<KillerAbility>();
        }

        public override void SetupVisuals()
        {
            // Kavkazi: Keep original sprite colors, only Red name to indicate role
            _avatar.SetBodyColor(Color.white); // No tint - preserve original sprite
            _avatar.SetNameColor(Color.red);
        }

        /// <summary>
        /// Attempts to kill the nearest target.
        /// Delegates to KillerAbility for server-validated kill.
        /// </summary>
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
