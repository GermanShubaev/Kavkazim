namespace Kavkazim.Netcode.Reporting
{
    public interface IReportable
    {
        ulong VictimPlayerId { get; }
        
        string VictimName { get; }
        
        UnityEngine.Vector3 Position { get; }
        
        bool IsReportable { get; }
        
        void MarkAsReported();
    }
}
