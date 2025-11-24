using UnityEngine;
using Unity.Netcode;
using System.Collections;

namespace Kavkazim.Netcode
{
    public class KavkaziRole : PlayerRole
    {
        public override PlayerRoleType RoleType => PlayerRoleType.Kavkazi;

        private float _killCooldown = 10f;
        private float _lastKillTime;
        private float _killRange = 2.0f;

        public KavkaziRole(PlayerAvatar avatar) : base(avatar) 
        {
            _lastKillTime = -_killCooldown; // Ready immediately
        }

        public override void SetupVisuals()
        {
            // Kavkazi: Red body, Red name
            _avatar.SetBodyColor(Color.red);
            _avatar.SetNameColor(Color.red);
        }

        public void TryKill()
        {
            if (Time.time - _lastKillTime < _killCooldown)
            {
                Debug.Log("Kill on cooldown.");
                return;
            }

            // Find closest target
            PlayerAvatar target = FindClosestTarget();
            if (target != null)
            {
                _lastKillTime = Time.time;
                _avatar.PerformSlashAnimation();
                
                // In a real game, we would call a ServerRpc here to kill the target.
                // For now, we'll just log it or disable the target locally if we are the server, 
                // but since this logic runs on the owner client, we need to request the kill.
                _avatar.RequestKillServerRpc(target.NetworkObjectId);
            }
        }

        private PlayerAvatar FindClosestTarget()
        {
            var allPlayers = Object.FindObjectsByType<PlayerAvatar>(FindObjectsSortMode.None);
            PlayerAvatar closest = null;
            float minDst = _killRange;

            foreach (var p in allPlayers)
            {
                if (p == _avatar) continue; // Don't kill self
                // In a real game, check if p is also Kavkazi (teammate)

                float dst = Vector3.Distance(_avatar.transform.position, p.transform.position);
                if (dst < minDst)
                {
                    minDst = dst;
                    closest = p;
                }
            }
            return closest;
        }
    }
}
