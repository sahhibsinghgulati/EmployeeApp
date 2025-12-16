using EmployeeAppMVC.Models;
using System;
using System.Collections.Generic;
using System.Configuration;
using System.Data;
using System.Data.SqlClient;
using System.IO;
using System.Web.Mvc; // Regex namespace removed
using Microsoft.Reporting.WebForms; // Required for RDLC

namespace EmployeeAppMVC.Controllers
{
    public class EmployeeController : Controller
    {
        string connStr = ConfigurationManager.ConnectionStrings["EmpDBConnection"].ConnectionString;

        // GET: Employee List
        public ActionResult Index()
        {
            List<Employee> employeeList = new List<Employee>();
            using (SqlConnection conn = new SqlConnection(connStr))
            {
                // 1. I added 'Address' explicitly to this query
                string sql = "SELECT EmpID, Name, fatherName, Gender, DOB, Address, Department, Mobile, Email, Aadhaar, JoiningDate, ImagePath FROM Employees";

                using (SqlCommand cmd = new SqlCommand(sql, conn))
                {
                    conn.Open();
                    SqlDataReader rdr = cmd.ExecuteReader();
                    while (rdr.Read())
                    {
                        var emp = new Employee();
                        emp.EmpID = Convert.ToInt32(rdr["EmpID"]);

                        emp.Name = rdr["Name"] != DBNull.Value ? rdr["Name"].ToString() : "";
                        emp.fatherName = rdr["fatherName"] != DBNull.Value ? rdr["fatherName"].ToString() : "";
                        emp.Gender = rdr["Gender"] != DBNull.Value ? rdr["Gender"].ToString() : "";

                        // 2. This line was likely missing or failing before:
                        emp.Address = rdr["Address"] != DBNull.Value ? rdr["Address"].ToString() : "";

                        emp.Department = rdr["Department"] != DBNull.Value ? rdr["Department"].ToString() : "";
                        emp.Mobile = rdr["Mobile"] != DBNull.Value ? rdr["Mobile"].ToString() : "";
                        emp.Email = rdr["Email"] != DBNull.Value ? rdr["Email"].ToString() : "";
                        emp.Aadhaar = rdr["Aadhaar"] != DBNull.Value ? rdr["Aadhaar"].ToString() : "";
                        emp.ImagePath = rdr["ImagePath"] != DBNull.Value ? rdr["ImagePath"].ToString() : "";

                        if (rdr["DOB"] != DBNull.Value) emp.DOB = Convert.ToDateTime(rdr["DOB"]);
                        if (rdr["JoiningDate"] != DBNull.Value) emp.JoiningDate = Convert.ToDateTime(rdr["JoiningDate"]);

                        employeeList.Add(emp);
                    }
                }
            }
            return View(employeeList);
        }

        // GET: Create
        public ActionResult Create()
        {
            return View(new Employee());
        }

        // POST
        [HttpPost]
        public ActionResult Create(Employee emp)
        {
            // 1. UNIQUE AADHAAR CHECK
            // We check the DB before doing anything else
            if (!string.IsNullOrEmpty(emp.Aadhaar))
            {
                using (SqlConnection conn = new SqlConnection(connStr))
                {
                    string checkSql = "SELECT COUNT(*) FROM Employees WHERE Aadhaar = @Aadhaar";
                    using (SqlCommand cmd = new SqlCommand(checkSql, conn))
                    {
                        cmd.Parameters.AddWithValue("@Aadhaar", emp.Aadhaar);
                        conn.Open();
                        int count = (int)cmd.ExecuteScalar();

                        if (count > 0)
                        {
                            // Aadhaar exists: Trigger the Error Alert
                            ViewBag.AlertMessage = "Error: This Aadhaar Number is already registered!";
                            return View(emp); // Return view with existing data so user doesn't lose input
                        }
                    }
                }
            }

            // 2. SAVE LOGIC (If Aadhaar is unique)
            int newEmpId = 0;
            using (SqlConnection conn = new SqlConnection(connStr))
            {
                string sqlInsert = @"INSERT INTO Employees 
            (Name, fatherName, Gender, DOB, Address, Department, Mobile, Email, Aadhaar, JoiningDate) 
            VALUES 
            (@Name, @fatherName, @Gender, @DOB, @Address, @Department, @Mobile, @Email, @Aadhaar, @JoiningDate);
            SELECT CAST(SCOPE_IDENTITY() AS INT);";

                using (SqlCommand cmd = new SqlCommand(sqlInsert, conn))
                {
                    cmd.Parameters.AddWithValue("@Name", emp.Name);
                    cmd.Parameters.AddWithValue("@fatherName", emp.fatherName ?? (object)DBNull.Value);
                    cmd.Parameters.AddWithValue("@Gender", emp.Gender ?? (object)DBNull.Value);
                    cmd.Parameters.AddWithValue("@DOB", emp.DOB ?? (object)DBNull.Value);
                    cmd.Parameters.AddWithValue("@Address", emp.Address ?? (object)DBNull.Value);
                    cmd.Parameters.AddWithValue("@Department", emp.Department ?? (object)DBNull.Value);
                    cmd.Parameters.AddWithValue("@Mobile", emp.Mobile ?? (object)DBNull.Value);
                    cmd.Parameters.AddWithValue("@Email", emp.Email ?? (object)DBNull.Value);
                    cmd.Parameters.AddWithValue("@Aadhaar", emp.Aadhaar ?? (object)DBNull.Value);
                    cmd.Parameters.AddWithValue("@JoiningDate", emp.JoiningDate ?? (object)DBNull.Value);

                    conn.Open();
                    newEmpId = (int)cmd.ExecuteScalar();
                }

                // Handle Image Upload
                if (emp.ImageUpload != null && emp.ImageUpload.ContentLength > 0 && newEmpId > 0)
                {
                    try
                    {
                        string fileExtension = Path.GetExtension(emp.ImageUpload.FileName);
                        string timeStamp = DateTime.Now.ToString("yyyyMMdd_HHmmss");
                        string fileName = $"{newEmpId}_{timeStamp}{fileExtension}";

                        // FIX: Ensure folder exists
                        string folderPath = Server.MapPath("~/EmployeeImages/");
                        if (!Directory.Exists(folderPath))
                        {
                            Directory.CreateDirectory(folderPath);
                        }

                        string savePath = Path.Combine(folderPath, fileName);
                        emp.ImageUpload.SaveAs(savePath);

                        // Update DB with the relative path
                        string sqlUpdate = "UPDATE Employees SET ImagePath = @Path WHERE EmpID = @ID";
                        using (SqlCommand cmdUpdate = new SqlCommand(sqlUpdate, conn))
                        {
                            // Ensure connection is open (it might have closed if we exited the previous using block)
                            if (conn.State == System.Data.ConnectionState.Closed) conn.Open();

                            cmdUpdate.Parameters.AddWithValue("@Path", "~/EmployeeImages/" + fileName);
                            cmdUpdate.Parameters.AddWithValue("@ID", newEmpId);
                            cmdUpdate.ExecuteNonQuery();
                        }
                    }
                    catch (Exception ex)
                    {
                        // If image save fails, we can log it, but the employee is already created
                        ViewBag.AlertMessage = "Employee saved, but Image failed: " + ex.Message;
                    }
                }
            }

            // 3. SUCCESS: Show Modal and Clear Form
            ModelState.Clear();
            ViewBag.SuccessMessage = "Employee registered successfully!";
            return View(new Employee()); // Return a fresh empty form
        }

        // GET: Delete Employee
        public ActionResult Delete(int id)
        {
            if (id > 0)
            {
                using (SqlConnection conn = new SqlConnection(connStr))
                {
                    // 1. (Optional) Delete the image file if it exists
                    string getFileSql = "SELECT ImagePath FROM Employees WHERE EmpID = @EmpID";
                    string filePath = "";

                    using (SqlCommand cmdFile = new SqlCommand(getFileSql, conn))
                    {
                        cmdFile.Parameters.AddWithValue("@EmpID", id);
                        conn.Open();
                        object result = cmdFile.ExecuteScalar();
                        if (result != null && result != DBNull.Value)
                        {
                            filePath = result.ToString();
                        }
                        conn.Close();
                    }

                    // Delete physical file
                    if (!string.IsNullOrEmpty(filePath))
                    {
                        string absolutePath = Server.MapPath(filePath);
                        if (System.IO.File.Exists(absolutePath))
                        {
                            System.IO.File.Delete(absolutePath);
                        }
                    }

                    // 2. Delete the Record from Database
                    string sql = "DELETE FROM Employees WHERE EmpID = @EmpID";
                    using (SqlCommand cmd = new SqlCommand(sql, conn))
                    {
                        cmd.Parameters.AddWithValue("@EmpID", id);
                        conn.Open();
                        cmd.ExecuteNonQuery();
                    }
                }
            }
            return RedirectToAction("Index");
        }

        // GET: Edit Employee
        public ActionResult Edit(int id)
        {
            Employee emp = new Employee();
            using (SqlConnection conn = new SqlConnection(connStr))
            {
                string sql = "SELECT * FROM Employees WHERE EmpID = @EmpID";
                using (SqlCommand cmd = new SqlCommand(sql, conn))
                {
                    cmd.Parameters.AddWithValue("@EmpID", id);
                    conn.Open();
                    SqlDataReader rdr = cmd.ExecuteReader();
                    if (rdr.Read())
                    {
                        emp.EmpID = Convert.ToInt32(rdr["EmpID"]);
                        emp.Name = rdr["Name"] != DBNull.Value ? rdr["Name"].ToString() : "";
                        emp.fatherName = rdr["fatherName"] != DBNull.Value ? rdr["fatherName"].ToString() : "";
                        emp.Gender = rdr["Gender"] != DBNull.Value ? rdr["Gender"].ToString() : "";
                        emp.Address = rdr["Address"] != DBNull.Value ? rdr["Address"].ToString() : "";
                        emp.Department = rdr["Department"] != DBNull.Value ? rdr["Department"].ToString() : "";
                        emp.Mobile = rdr["Mobile"] != DBNull.Value ? rdr["Mobile"].ToString() : "";
                        emp.Email = rdr["Email"] != DBNull.Value ? rdr["Email"].ToString() : "";
                        emp.Aadhaar = rdr["Aadhaar"] != DBNull.Value ? rdr["Aadhaar"].ToString() : "";
                        emp.ImagePath = rdr["ImagePath"] != DBNull.Value ? rdr["ImagePath"].ToString() : "";

                        if (rdr["DOB"] != DBNull.Value) emp.DOB = Convert.ToDateTime(rdr["DOB"]);
                        if (rdr["JoiningDate"] != DBNull.Value) emp.JoiningDate = Convert.ToDateTime(rdr["JoiningDate"]);
                    }
                }
            }
            // IMPORTANT: This tells MVC to use the "Create.cshtml" file, but pass the filled 'emp' data
            return View("Create", emp);
        }

        // POST: Update Employee
        [HttpPost]
        public ActionResult Edit(Employee emp)
        {
            using (SqlConnection conn = new SqlConnection(connStr))
            {
                // 1. Handle Image Logic
                // If a new file is uploaded, use it. Otherwise, keep the old ImagePath (from hidden field)
                if (emp.ImageUpload != null && emp.ImageUpload.ContentLength > 0)
                {
                    string fileExtension = Path.GetExtension(emp.ImageUpload.FileName);
                    string timeStamp = DateTime.Now.ToString("yyyyMMdd_HHmmss");
                    string fileName = $"{emp.EmpID}_{timeStamp}{fileExtension}";

                    string folderPath = Server.MapPath("~/EmployeeImages/");
                    if (!Directory.Exists(folderPath)) Directory.CreateDirectory(folderPath);

                    string savePath = Path.Combine(folderPath, fileName);
                    emp.ImageUpload.SaveAs(savePath);

                    emp.ImagePath = "~/EmployeeImages/" + fileName;
                }

                // 2. Update Database
                string sqlUpdate = @"UPDATE Employees SET 
            Name = @Name, 
            fatherName = @fatherName, 
            Gender = @Gender, 
            DOB = @DOB, 
            Address = @Address, 
            Department = @Department, 
            Mobile = @Mobile, 
            Email = @Email, 
            Aadhaar = @Aadhaar, 
            JoiningDate = @JoiningDate,
            ImagePath = @ImagePath
            WHERE EmpID = @EmpID";

                using (SqlCommand cmd = new SqlCommand(sqlUpdate, conn))
                {
                    cmd.Parameters.AddWithValue("@EmpID", emp.EmpID);
                    cmd.Parameters.AddWithValue("@Name", emp.Name);
                    cmd.Parameters.AddWithValue("@fatherName", emp.fatherName ?? (object)DBNull.Value);
                    cmd.Parameters.AddWithValue("@Gender", emp.Gender ?? (object)DBNull.Value);
                    cmd.Parameters.AddWithValue("@DOB", emp.DOB ?? (object)DBNull.Value);
                    cmd.Parameters.AddWithValue("@Address", emp.Address ?? (object)DBNull.Value);
                    cmd.Parameters.AddWithValue("@Department", emp.Department ?? (object)DBNull.Value);
                    cmd.Parameters.AddWithValue("@Mobile", emp.Mobile ?? (object)DBNull.Value);
                    cmd.Parameters.AddWithValue("@Email", emp.Email ?? (object)DBNull.Value);
                    cmd.Parameters.AddWithValue("@Aadhaar", emp.Aadhaar ?? (object)DBNull.Value);
                    cmd.Parameters.AddWithValue("@JoiningDate", emp.JoiningDate ?? (object)DBNull.Value);
                    // Use existing path if no new file
                    cmd.Parameters.AddWithValue("@ImagePath", emp.ImagePath ?? (object)DBNull.Value);

                    conn.Open();
                    cmd.ExecuteNonQuery();
                }
            }

            ViewBag.SuccessMessage = "Employee details updated successfully!";
            return View("Create", emp);
        }

        // "int id = 0" makes the parameter optional. Default is 0.
        public ActionResult Print(int id = 0, string companyName = "", string companyAddr = "", string companyMobile = "")
        {
            // 1. Setup DataTable
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

            using (SqlConnection conn = new SqlConnection(connStr))
            {
                string sql;
                // SMART LOGIC: Choose Query based on ID
                if (id > 0)
                {
                    // Fetch Single Employee
                    sql = "SELECT * FROM Employees WHERE EmpID = @EmpID";
                }
                else
                {
                    // Fetch ALL Employees (Ordered by Name)
                    sql = "SELECT * FROM Employees ORDER BY Name ASC";
                }

                using (SqlCommand cmd = new SqlCommand(sql, conn))
                {
                    // Only add parameter if we are filtering by ID
                    if (id > 0)
                    {
                        cmd.Parameters.AddWithValue("@EmpID", id);
                    }

                    conn.Open();
                    SqlDataReader rdr = cmd.ExecuteReader();

                    // Loop handles both 1 record or 100 records automatically
                    while (rdr.Read())
                    {
                        DataRow row = dt.NewRow();
                        row["EmpID"] = rdr["EmpID"].ToString();
                        row["Name"] = rdr["Name"].ToString();
                        row["FatherName"] = rdr["fatherName"].ToString();
                        row["Gender"] = rdr["Gender"].ToString();
                        row["DOB"] = rdr["DOB"] != DBNull.Value ? Convert.ToDateTime(rdr["DOB"]).ToString("dd/MM/yyyy") : "";
                        row["Mobile"] = rdr["Mobile"].ToString();
                        row["Email"] = rdr["Email"].ToString();
                        string rawAddress = rdr["Address"] != DBNull.Value ? rdr["Address"].ToString() : "";
                        row["Address"] = rawAddress.Replace("\r\n", ", ").Replace("\n", ", ");
                        row["Department"] = rdr["Department"].ToString();
                        row["Aadhaar"] = rdr["Aadhaar"].ToString();
                        row["JoiningDate"] = rdr["JoiningDate"] != DBNull.Value ? Convert.ToDateTime(rdr["JoiningDate"]).ToString("dd/MM/yyyy") : "";

                        // Image Path Logic
                        string dbPath = rdr["ImagePath"] != DBNull.Value ? rdr["ImagePath"].ToString() : "";
                        if (!string.IsNullOrEmpty(dbPath) && dbPath.StartsWith("~"))
                        {
                            row["ImagePath"] = new Uri(Server.MapPath(dbPath)).AbsoluteUri;
                        }
                        else
                        {
                            string defPath = Server.MapPath("~/EmployeeImages/no-image.png");
                            if (System.IO.File.Exists(defPath))
                                row["ImagePath"] = new Uri(defPath).AbsoluteUri;
                            else
                                row["ImagePath"] = "";
                        }

                        dt.Rows.Add(row);
                    }
                }
            }

            // 2. Render Report
            LocalReport report = new LocalReport();
            report.ReportPath = Server.MapPath("~/Report.rdlc");
            report.EnableExternalImages = true; 

            // 1. Get Absolute Path for the Logo
            string logoPath = new Uri(Server.MapPath("~/Assets/logo.jpg")).AbsoluteUri;

            // 2. Create Parameter
            ReportParameter paramLogo = new ReportParameter("LogoPath", logoPath);

            // 3. Pass Parameter to Report
            ReportParameter p1 = new ReportParameter("CompanyName", string.IsNullOrEmpty(companyName) ? "Default Company" : companyName);
            ReportParameter p2 = new ReportParameter("CompanyAddress", companyAddr);
            ReportParameter p3 = new ReportParameter("MobileNumber", companyMobile);

            // 3. Pass ALL parameters to the report
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

            renderedBytes = report.Render(
                reportType, null, out mimeType, out encoding,
                out fileNameExtension, out streams, out warnings
            );

            return File(renderedBytes, mimeType);
        }
    }
}