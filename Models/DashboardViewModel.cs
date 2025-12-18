using System.Collections.Generic;

namespace EmployeeApp.Models
{
    public class DashboardViewModel
    {
        public List<string> DepartmentLabels { get; set; }
        public List<int> EmployeeCounts { get; set; }
    }
}