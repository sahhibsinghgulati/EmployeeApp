using System;
using System.Linq;
using System.Web.Mvc;
using EmployeeApp.Models; // Ensure this matches your namespace

namespace EmployeeAppMVC.Controllers
{
    [Authorize]
    public class DepartmentController : Controller
    {
        EmployeeEntities db = new EmployeeEntities();

        // GET: Index (Handles both "New Page" and "Edit Mode")
        public ActionResult Index(int? id)
        {
            if (Session["Role"] == null || Session["Role"].ToString() != "Admin")
            {
                return RedirectToAction("Index", "Employee");
            }
            // 1. Get the list for the Right Side Table
            ViewBag.DeptList = db.DeptMasters.OrderBy(x => x.DisplayOrder).ToList();

            // 2. Prepare the form for the Left Side
            DeptMaster model = new DeptMaster(); // Default: Empty (Create Mode)

            // If an ID was passed, we are in "Edit Mode" -> Fetch that data
            if (id.HasValue && id.Value > 0)
            {
                model = db.DeptMasters.Find(id.Value);
            }

            return View(model);
        }

        // POST: Save (Handles both Insert and Update)
        [HttpPost]
        public ActionResult Save(DeptMaster formDept)
        {
            if (ModelState.IsValid)
            {
                // CHECK: Is this a New Entry (ID=0) or Update (ID>0)?
                if (formDept.DeptID == 0)
                {
                    // === INSERT LOGIC ===
                    formDept.CreatedOn = DateTime.Now;

                    string username = User.Identity.Name;
                    var user = db.Users.FirstOrDefault(u => u.Username == username);
                    formDept.CreatedBy = (user != null) ? user.UserID : 0;

                    db.DeptMasters.Add(formDept);
                }
                else
                {
                    // === UPDATE LOGIC ===
                    var dbRecord = db.DeptMasters.Find(formDept.DeptID);
                    if (dbRecord != null)
                    {
                        dbRecord.DeptName = formDept.DeptName;
                        dbRecord.DisplayOrder = formDept.DisplayOrder;
                        // We DO NOT update CreatedBy or CreatedOn
                    }
                }

                db.SaveChanges();
                return RedirectToAction("Index"); // Clear the form by redirecting
            }

            // If validation failed, reload list and show form with errors
            ViewBag.DeptList = db.DeptMasters.OrderBy(x => x.DisplayOrder).ToList();
            return View("Index", formDept);
        }
    }
}