using System;
using System.Collections.Generic;

namespace EmployeeApp.Models
{
    public class TallyDashboardViewModel
    {
        // For the Dropdown
        public int? SelectedEmpId { get; set; }
        public string SelectedEmpName { get; set; }

        // LEFT PANEL: Master Status (from vw_TallyData)
        public TallyStatusDetails TallyStatus { get; set; }

        // RIGHT PANEL: Unsynced Remarks (from vw_EmployeeRemark)
        public List<vw_EmployeeRemark> UnsyncedRemarks { get; set; }
    }

    public class TallyStatusDetails
    {
        public bool IsMapped { get; set; }
        public string LedgerNameInTally { get; set; }
        public DateTime? LastSyncOn { get; set; }
        public bool NameMismatch { get; set; } // To warn if Rename is needed
    }
}