using EmployeeApp.Models;
using System;
using System.Collections.Generic;
using System.Data; // Needed for DataTable (RDLC)
using System.IO;
using System.Linq; // Needed for EF queries
using System.Web;
using System.Web.Mvc;
using Microsoft.Reporting.WebForms;

namespace EmployeeAppMVC.Controllers
{
    [Authorize]
    public class EmployeeController : Controller
    {
        EmployeeEntities db = new EmployeeEntities();

        // GET: Employee List
        public ActionResult Index()
        {
            // Simple 1-line fetch
            var list = db.Employees.ToList();
            return View(list);
        }

        // GET: Create
        public ActionResult Create()
        {
            return View(new Employee());
        }

        // POST
        [HttpPost]
        public ActionResult Create(Employee emp, HttpPostedFileBase ImageUpload)
        {
            // 1. Check if Aadhaar is Unique
            if (!string.IsNullOrEmpty(emp.Aadhaar))
            {
                // EF way to check existence
                bool exists = db.Employees.Any(x => x.Aadhaar == emp.Aadhaar);
                if (exists)
                {
                    ViewBag.AlertMessage = "Error: This Aadhaar Number is already registered!";
                    return View(emp);
                }
            }

            // 2. Save Basic Details first (to generate the EmpID)
            // Note: We handle nulls in the object automatically
            db.Employees.Add(emp);
            db.SaveChanges(); // This generates the EmpID in the database

            // 3. Handle Image Upload (Now that we have an EmpID)
            if (ImageUpload != null && ImageUpload.ContentLength > 0)
            {
                try
                {
                    string fileExtension = Path.GetExtension(ImageUpload.FileName);
                    string timeStamp = DateTime.Now.ToString("yyyyMMdd_HHmmss");
                    string fileName = $"{emp.EmpId}_{timeStamp}{fileExtension}";

                    string folderPath = Server.MapPath("~/EmployeeImages/");
                    if (!Directory.Exists(folderPath)) Directory.CreateDirectory(folderPath);

                    string savePath = Path.Combine(folderPath, fileName);
                    ImageUpload.SaveAs(savePath);

                    // Update the record with the path
                    emp.ImagePath = "~/EmployeeImages/" + fileName;
                    db.SaveChanges(); // Save the update
                }
                catch (Exception ex)
                {
                    ViewBag.AlertMessage = "Employee saved, but Image failed: " + ex.Message;
                }
            }

            ModelState.Clear();
            ViewBag.SuccessMessage = "Employee registered successfully!";
            return View(new Employee());
        }

        // GET: Delete Employee
        public ActionResult Delete(int id)
        {
            var emp = db.Employees.Find(id);
            if (emp != null)
            {
                // 1. Delete Physical File
                if (!string.IsNullOrEmpty(emp.ImagePath))
                {
                    string absolutePath = Server.MapPath(emp.ImagePath);
                    if (System.IO.File.Exists(absolutePath))
                    {
                        System.IO.File.Delete(absolutePath);
                    }
                }

                // 2. Delete from DB
                db.Employees.Remove(emp);
                db.SaveChanges();
            }
            return RedirectToAction("Index");
        }

        // GET: Edit Employee
        public ActionResult Edit(int id)
        {
            // Find employee by ID
            var emp = db.Employees.Find(id);
            if (emp == null) return HttpNotFound();

            return View("Create", emp);
        }

        // POST: Update Employee
        [HttpPost]
        public ActionResult Edit(Employee formEmp, HttpPostedFileBase ImageUpload)
        {
            // 1. Fetch the EXISTING record from DB
            var dbEmp = db.Employees.Find(formEmp.EmpId);

            if (dbEmp != null)
            {
                // 2. Update properties manually
                dbEmp.Name = formEmp.Name;
                dbEmp.fatherName = formEmp.fatherName;
                dbEmp.Gender = formEmp.Gender;
                dbEmp.DOB = formEmp.DOB;
                dbEmp.Address = formEmp.Address;
                dbEmp.Department = formEmp.Department;
                dbEmp.Mobile = formEmp.Mobile;
                dbEmp.Email = formEmp.Email;
                dbEmp.Aadhaar = formEmp.Aadhaar;
                dbEmp.JoiningDate = formEmp.JoiningDate;

                // 3. Handle Image Update
                if (ImageUpload != null && ImageUpload.ContentLength > 0)
                {
                    string fileExtension = Path.GetExtension(ImageUpload.FileName);
                    string timeStamp = DateTime.Now.ToString("yyyyMMdd_HHmmss");
                    string fileName = $"{formEmp.EmpId}_{timeStamp}{fileExtension}";

                    string folderPath = Server.MapPath("~/EmployeeImages/");
                    if (!Directory.Exists(folderPath)) Directory.CreateDirectory(folderPath);

                    string savePath = Path.Combine(folderPath, fileName);
                    ImageUpload.SaveAs(savePath);

                    // Update Path
                    dbEmp.ImagePath = "~/EmployeeImages/" + fileName;
                }

                // 4. Save Changes
                db.SaveChanges();
                ViewBag.SuccessMessage = "Employee details updated successfully!";
                return View("Create", dbEmp);
            }

            return HttpNotFound();
        }

        // "int id = 0" makes the parameter optional. Default is 0.
        public ActionResult Print(int id = 0, string companyName = "", string companyAddr = "", string companyMobile = "")
        {
            // 1. Get Data using EF
            List<Employee> dataList;

            if (id > 0)
            {
                // Fetch Single
                dataList = db.Employees.Where(x => x.EmpId == id).ToList();
            }
            else
            {
                // Fetch All
                dataList = db.Employees.OrderBy(x => x.Name).ToList();
            }

            // 2. Convert List to DataTable (Required for RDLC)
            DataTable dt = new DataTable();
            dt.Columns.Add("EmpID");
            dt.Columns.Add("Name");
            dt.Columns.Add("FatherName");
            dt.Columns.Add("Gender");
            dt.Columns.Add("DOB");
            dt.Columns.Add("Mobile");
            dt.Columns.Add("Email");
            dt.Columns.Add("Address");
            dt.Columns.Add("Department");
            dt.Columns.Add("Aadhaar");
            dt.Columns.Add("JoiningDate");
            dt.Columns.Add("ImagePath");

            foreach (var item in dataList)
            {
                DataRow row = dt.NewRow();
                row["EmpID"] = item.EmpId;
                row["Name"] = item.Name;
                row["FatherName"] = item.fatherName;
                row["Gender"] = item.Gender;
                row["DOB"] = item.DOB.HasValue ? item.DOB.Value.ToString("dd/MM/yyyy") : "";
                row["Mobile"] = item.Mobile;
                row["Email"] = item.Email;

                // Address Logic (New lines to Commas)
                string rawAddress = item.Address ?? "";
                row["Address"] = rawAddress.Replace("\r\n", ", ").Replace("\n", ", ");

                row["Department"] = item.Department;
                row["Aadhaar"] = item.Aadhaar;
                row["JoiningDate"] = item.JoiningDate.HasValue ? item.JoiningDate.Value.ToString("dd/MM/yyyy") : "";

                // Image Logic
                string dbPath = item.ImagePath ?? "";
                if (!string.IsNullOrEmpty(dbPath) && dbPath.StartsWith("~"))
                {
                    row["ImagePath"] = new Uri(Server.MapPath(dbPath)).AbsoluteUri;
                }
                else
                {
                    string defPath = Server.MapPath("~/EmployeeImages/no-image.png");
                    row["ImagePath"] = System.IO.File.Exists(defPath) ? new Uri(defPath).AbsoluteUri : "";
                }

                dt.Rows.Add(row);
            }

            // 3. Render Report (Same as before)
            LocalReport report = new LocalReport();
            report.ReportPath = Server.MapPath("~/Report.rdlc");
            report.EnableExternalImages = true;

            string logoPath = new Uri(Server.MapPath("~/Assets/logo.jpg")).AbsoluteUri;
            ReportParameter paramLogo = new ReportParameter("LogoPath", logoPath);
            ReportParameter p1 = new ReportParameter("CompanyName", string.IsNullOrEmpty(companyName) ? "Default Company" : companyName);
            ReportParameter p2 = new ReportParameter("CompanyAddress", companyAddr);
            ReportParameter p3 = new ReportParameter("MobileNumber", companyMobile);

            report.SetParameters(new ReportParameter[] { paramLogo, p1, p2, p3 });

            ReportDataSource rds = new ReportDataSource("EmployeeDataSet", dt);
            report.DataSources.Add(rds);

            string reportType = "PDF";
            string mimeType;
            string encoding;
            string fileNameExtension;
            Warning[] warnings;
            string[] streams;
            byte[] renderedBytes;

            renderedBytes = report.Render(reportType, null, out mimeType, out encoding, out fileNameExtension, out streams, out warnings);
            return File(renderedBytes, mimeType);
        }
    }
}