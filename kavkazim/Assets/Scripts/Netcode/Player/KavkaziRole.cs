using UnityEngine;

namespace Kavkazim.Netcode
{
    /// <summary>
    /// Kavkazi (impostor) role implementation.
    /// Kill functionality is delegated to the KillerAbility component.
    /// </summary>
    public class KavkaziRole : PlayerRole
    {
        public override PlayerRoleType RoleType => PlayerRoleType.Kavkazi;

        private KillerAbility _killerAbility;

        public KavkaziRole(PlayerAvatar avatar) : base(avatar) 
        {
            // Cache reference to KillerAbility component
            _killerAbility = avatar.GetComponent<KillerAbility>();
        }

        public override void SetupVisuals()
        {
            // Kavkazi: Red body, Red name
            _avatar.SetBodyColor(Color.red);
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
                Debug.LogWarning("[KavkaziRole] No KillerAbility component found on player.");
                return;
            }

            _killerAbility.TryKill();
        }

        /// <summary>
        /// Check if kill ability is ready (off cooldown).
        /// </summary>
        public bool IsKillReady => _killerAbility != null && _killerAbility.IsKillReady;

        /// <summary>
        /// Get remaining cooldown time for UI display.
        /// </summary>
        public float RemainingCooldown => _killerAbility?.RemainingCooldown ?? 0f;
    }
}
