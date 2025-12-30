namespace Kavkazim.Netcode.Reporting
{
    /// <summary>
    /// Abstraction for report input sources.
    /// Allows swapping between keyboard, mobile button, etc.
    /// </summary>
    public interface IReportInput
    {
        /// <summary>
        /// Returns true on the frame the player wants to initiate a report.
        /// </summary>
        bool WantsToReport();
    }
}
