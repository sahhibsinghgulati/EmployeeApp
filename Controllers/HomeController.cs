using System.Linq;
using System.Web.Mvc;
using EmployeeApp.Models;

namespace EmployeeApp.Controllers
{
    [Authorize]
    public class HomeController : Controller
    {
        private EmployeeEntities db = new EmployeeEntities();

        public ActionResult Index()
        {
            // 1. Fetch and Group Data
            var groupedData = db.Employees.Include("DeptMaster")
                .GroupBy(e => e.DeptMaster.DeptName)
                .ToList() // Bring to memory to handle null keys safely
                .Select(g => new 
                { 
                    Name = string.IsNullOrEmpty(g.Key) ? "Unassigned" : g.Key, 
                    Count = g.Count() 
                })
                .ToList();

            // 2. Map to ViewModel
            var model = new DashboardViewModel
            {
                DepartmentLabels = groupedData.Select(x => x.Name).ToList(),
                EmployeeCounts = groupedData.Select(x => x.Count).ToList()
            };

            return View(model);
        }
    }
}