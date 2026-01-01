namespace Kavkazim.Netcode.Reporting
{
    /// <summary>
    /// Interface for objects that can be reported by players.
    /// Implemented by DeadBody, can be extended for emergency buttons etc.
    /// </summary>
    public interface IReportable
    {
        /// <summary>
        /// NetworkObjectId of the victim player.
        /// </summary>
        ulong VictimPlayerId { get; }
        
        /// <summary>
        /// Display name of the victim.
        /// </summary>
        string VictimName { get; }
        
        /// <summary>
        /// World position of the reportable object.
        /// </summary>
        UnityEngine.Vector3 Position { get; }
        
        /// <summary>
        /// Whether this object can currently be reported.
        /// False if already reported or otherwise unavailable.
        /// </summary>
        bool IsReportable { get; }
        
        /// <summary>
        /// Mark this object as reported.
        /// Called by ReportService after successful report.
        /// </summary>
        void MarkAsReported();
    }
}
