using System;
using System.Collections.Generic;
using Kavkazim.Config;
using Netcode.Player;
using Kavkazim.Netcode;
using UnityEngine;

namespace Kavkazim.Netcode.Reporting
{
    /// <summary>
    /// Enum to distinguish report types.
    /// </summary>
    public enum ReportType
    {
        DeadBody,
        EmergencyMeeting
    }

    /// <summary>
    /// Service for handling reports (dead bodies and emergency meetings).
    /// Uses static methods. Same L key triggers both - priority: body first, then emergency.
    /// RULES:
    /// - Body reports: UNLIMITED (players can report multiple bodies)
    /// - Emergency meetings: ONE per player per game (tracked below)
    /// </summary>
    public static class ReportService
    {
        private static float _reportRange = 2.5f;
        
        // Track which players have already called an EMERGENCY meeting (server-side)
        // NOTE: This does NOT apply to body reports (body reports are unlimited)
        private static HashSet<ulong> _playersWhoCalledEmergency = new HashSet<ulong>();
        
        /// <summary>
        /// Event fired when a body is successfully reported.
        /// Parameters: (reporterName, victimName)
        /// </summary>
        public static event Action<string, string> OnBodyReported;

        /// <summary>
        /// Event fired when emergency meeting is called.
        /// Parameters: (callerName)
        /// </summary>
        public static event Action<string> OnEmergencyMeetingCalled;

        /// <summary>
        /// Set the report range (call from ReportingSetup with config value).
        /// </summary>
        public static void SetReportRange(float range)
        {
            _reportRange = range;
            Debug.Log($"[ReportService] Report range set to {range}");
        }

        /// <summary>
        /// Reset emergency meeting tracking (call at start of new game/round).
        /// </summary>
        public static void ResetEmergencyTracking()
        {
            _playersWhoCalledEmergency.Clear();
            Debug.Log("[ReportService] Emergency meeting tracking reset for new game.");
        }

        /// <summary>
        /// Check if a player has already called an emergency meeting this game.
        /// </summary>
        public static bool HasCalledEmergency(ulong clientId)
        {
            return _playersWhoCalledEmergency.Contains(clientId);
        }

        /// <summary>
        /// Mark a player as having called an emergency meeting (server-side).
        /// </summary>
        public static void MarkEmergencyCalled(ulong clientId)
        {
            _playersWhoCalledEmergency.Add(clientId);
            Debug.Log($"[ReportService] Player {clientId} has used their emergency meeting.");
        }

        /// <summary>
        /// Client-side: Attempt to report (body OR emergency meeting).
        /// Priority: Dead body first, then emergency button.
        /// </summary>
        public static void TryReport(PlayerState reporter)
        {
            if (reporter == null)
            {
                Debug.LogWarning("[ReportService] TryReport called with null reporter.");
                return;
            }
            
            // Only alive players can report
            if (!reporter.IsAlive.Value)
            {
                Debug.Log("[ReportService] Dead players cannot report.");
                return;
            }

            // Get reporter info
            string reporterName = GetPlayerName(reporter);
            
            // Priority 1: Check for dead body nearby
            DeadBody nearestBody = FindNearestReportableBody(reporter.transform.position);
            if (nearestBody != null)
            {
                // Report the dead body
                nearestBody.RequestReportServerRpc();
                return;
            }
            
            // Priority 2: Check for emergency button nearby
            if (EmergencyButton.Instance != null && 
                EmergencyButton.Instance.IsPlayerInRange(reporter.transform.position))
            {
                // Call emergency meeting
                EmergencyButton.Instance.TryCallEmergencyMeeting(reporter);
                return;
            }
            
            Debug.Log("[ReportService] Nothing to report nearby.");
        }

        /// <summary>
        /// Get player display name.
        /// </summary>
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

        /// <summary>
        /// Find the nearest reportable body within range.
        /// </summary>
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

        /// <summary>
        /// Check if there's something reportable near the given position.
        /// Returns true if body or emergency button is in range.
        /// </summary>
        public static bool HasReportableInRange(Vector3 position)
        {
            // Check for body
            if (FindNearestReportableBody(position) != null)
                return true;
            
            // Check for emergency button
            if (EmergencyButton.Instance != null && 
                EmergencyButton.Instance.IsPlayerInRange(position) &&
                !EmergencyButton.Instance.IsOnCooldown)
                return true;
            
            return false;
        }

        /// <summary>
        /// Legacy method - checks for body only.
        /// </summary>
        public static bool HasReportableBodyInRange(Vector3 position)
        {
            return FindNearestReportableBody(position) != null;
        }

        /// <summary>
        /// Get what type of report is available at position.
        /// </summary>
        public static ReportType? GetAvailableReportType(Vector3 position)
        {
            if (FindNearestReportableBody(position) != null)
                return ReportType.DeadBody;
            
            if (EmergencyButton.Instance != null && 
                EmergencyButton.Instance.IsPlayerInRange(position) &&
                !EmergencyButton.Instance.IsOnCooldown)
                return ReportType.EmergencyMeeting;
            
            return null;
        }

        /// <summary>
        /// Called by DeadBody when a report is validated on server.
        /// Triggers meeting scene load.
        /// </summary>
        internal static void NotifyBodyReported(string reporterName, string victimName, ulong reporterId, ulong victimId)
        {
            Debug.Log($"REPORT (Dead Body), Found Body by \"{reporterName}\"");
            OnBodyReported?.Invoke(reporterName, victimName);

            // Trigger meeting scene load (SERVER ONLY)
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

        /// <summary>
        /// Called by EmergencyButton when meeting is validated on server.
        /// Triggers meeting scene load.
        /// </summary>
        internal static void NotifyEmergencyMeeting(string callerName, ulong callerId)
        {
            Debug.Log($"REPORT (Emergency Meeting) BY \"{callerName}\"");
            OnEmergencyMeetingCalled?.Invoke(callerName);

            // Trigger meeting scene load (SERVER ONLY)
            if (GameSessionManager.Instance != null && GameSessionManager.Instance.IsServer)
            {
                var meetingData = new Kavkazim.Netcode.Meeting.MeetingStartData
                {
                    Type = Kavkazim.Netcode.Meeting.MeetingType.Emergency,
                    CallerId = callerId,
                    CallerName = callerName,
                    VictimId = ulong.MaxValue, // No victim for emergency
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
