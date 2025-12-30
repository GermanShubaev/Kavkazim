using UnityEngine;

namespace Kavkazim.Netcode.Reporting
{
    /// <summary>
    /// Keyboard implementation of IReportInput.
    /// Triggers report on L key press.
    /// </summary>
    public class KeyboardReportInput : IReportInput
    {
        private readonly KeyCode _reportKey;
        
        public KeyboardReportInput(KeyCode reportKey = KeyCode.L)
        {
            _reportKey = reportKey;
        }
        
        public bool WantsToReport()
        {
            return Input.GetKeyDown(_reportKey);
        }
    }
}
