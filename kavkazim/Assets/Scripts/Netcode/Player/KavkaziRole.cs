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
            // Kavkazi: Slightly darker sprite to subtly indicate role, Red name for clarity
            _avatar.SetBodyColor(new Color(0.75f, 0.75f, 0.75f)); // Subtle dark tint
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
