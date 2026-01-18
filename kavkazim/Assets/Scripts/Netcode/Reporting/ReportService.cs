using System;
using System.Collections.Generic;
using Kavkazim.Config;
using Netcode.Player;
using Kavkazim.Netcode;
using UnityEngine;

namespace Kavkazim.Netcode.Reporting
{
    public enum ReportType
    {
        DeadBody,
        EmergencyMeeting
    }

    public static class ReportService
    {
        private static float _reportRange = 2.5f;
        
        private static HashSet<ulong> _playersWhoCalledEmergency = new HashSet<ulong>();
        
        public static event Action<string, string> OnBodyReported;

        public static event Action<string> OnEmergencyMeetingCalled;

        public static void SetReportRange(float range)
        {
            _reportRange = range;
            Debug.Log($"[ReportService] Report range set to {range}");
        }

        public static void ResetEmergencyTracking()
        {
            _playersWhoCalledEmergency.Clear();
            Debug.Log("[ReportService] Emergency meeting tracking reset for new game.");
        }

        public static bool HasCalledEmergency(ulong clientId)
        {
            return _playersWhoCalledEmergency.Contains(clientId);
        }

        public static void MarkEmergencyCalled(ulong clientId)
        {
            _playersWhoCalledEmergency.Add(clientId);
            Debug.Log($"[ReportService] Player {clientId} has used their emergency meeting.");
        }

        public static void TryReport(PlayerState reporter)
        {
            if (reporter == null)
            {
                Debug.LogWarning("[ReportService] TryReport called with null reporter.");
                return;
            }
            
            if (!reporter.IsAlive.Value)
            {
                Debug.Log("[ReportService] Dead players cannot report.");
                return;
            }

            string reporterName = GetPlayerName(reporter);
            
            DeadBody nearestBody = FindNearestReportableBody(reporter.transform.position);
            if (nearestBody != null)
            {
                nearestBody.RequestReportServerRpc();
                return;
            }
            
            if (EmergencyButton.Instance != null && 
                EmergencyButton.Instance.IsPlayerInRange(reporter.transform.position))
            {
                EmergencyButton.Instance.TryCallEmergencyMeeting(reporter);
                return;
            }
            
            Debug.Log("[ReportService] Nothing to report nearby.");
        }

        private static string GetPlayerName(PlayerState player)
        {
            string name = $"Player {player.OwnerClientId}";
            PlayerAvatar avatar = player.GetComponent<PlayerAvatar>();
            if (avatar != null && !string.IsNullOrEmpty(avatar.PlayerName.Value.ToString()))
            {
                name = avatar.PlayerName.Value.ToString();
            }
            return name;
        }

        public static DeadBody FindNearestReportableBody(Vector3 position)
        {
            var allBodies = DeadBody.ActiveBodies;
            DeadBody nearest = null;
            float minDistance = _reportRange;

            foreach (var body in allBodies)
            {
                if (!body.IsReportable)
                    continue;
                
                float distance = Vector3.Distance(position, body.Position);
                if (distance < minDistance)
                {
                    minDistance = distance;
                    nearest = body;
                }
            }

            return nearest;
        }

        public static bool HasReportableInRange(Vector3 position)
        {
            if (FindNearestReportableBody(position) != null)
                return true;
            
            if (EmergencyButton.Instance != null && 
                EmergencyButton.Instance.IsPlayerInRange(position) &&
                !EmergencyButton.Instance.IsOnCooldown)
                return true;
            
            return false;
        }

        internal static void NotifyBodyReported(string reporterName, string victimName, ulong reporterId, ulong victimId)
        {
            Debug.Log($"REPORT (Dead Body), Found Body by \"{reporterName}\"");
            OnBodyReported?.Invoke(reporterName, victimName);

            if (GameSessionManager.Instance != null && GameSessionManager.Instance.IsServer)
            {
                var meetingData = new Kavkazim.Netcode.Meeting.MeetingStartData
                {
                    Type = Kavkazim.Netcode.Meeting.MeetingType.BodyReport,
                    CallerId = reporterId,
                    CallerName = reporterName,
                    VictimId = victimId,
                    VictimName = victimName,
                    Timestamp = UnityEngine.Time.time
                };

                GameSessionManager.Instance.LoadMeetingScene(meetingData);
            }
            else
            {
                Debug.LogError("[ReportService] GameSessionManager not found, cannot load meeting scene!");
            }
        }

        internal static void NotifyEmergencyMeeting(string callerName, ulong callerId)
        {
            Debug.Log($"REPORT (Emergency Meeting) BY \"{callerName}\"");
            OnEmergencyMeetingCalled?.Invoke(callerName);

            if (GameSessionManager.Instance != null && GameSessionManager.Instance.IsServer)
            {
                var meetingData = new Kavkazim.Netcode.Meeting.MeetingStartData
                {
                    Type = Kavkazim.Netcode.Meeting.MeetingType.Emergency,
                    CallerId = callerId,
                    CallerName = callerName,
                    VictimId = ulong.MaxValue,
                    VictimName = "",
                    Timestamp = UnityEngine.Time.time
                };

                GameSessionManager.Instance.LoadMeetingScene(meetingData);
            }
            else
            {
                Debug.LogError("[ReportService] GameSessionManager not found, cannot load meeting scene!");
            }
        }
    }
}
