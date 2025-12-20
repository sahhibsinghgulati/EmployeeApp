using System;
using System.IO;
using System.Linq;
using System.Security.Cryptography.Xml;
using System.Web;
using System.Web.Mvc;
using EmployeeApp.Models;

namespace EmployeeApp.Controllers
{
    [Authorize] // Ensure user is logged in
    public class RemarkController : Controller
    {
        EmployeeEntities db = new EmployeeEntities();

        // GET
        public ActionResult Index(int? id)
        {
            if (Session["Role"] == null || Session["Role"].ToString() != "Admin")
            {
                return RedirectToAction("Index", "Employee");
            }
            // 1. SORT BY EMPID: Order the list by EmpId instead of Name
            // We maintain the selection using the 'id' parameter in the SelectList constructor
            var empList = db.Employees.OrderBy(e => e.EmpId).ToList();
            ViewBag.EmpList = new SelectList(empList, "EmpId", "Name", id);

            // 2. FILTER SYSTEM:
            var RemarksQuery = db.vw_EmployeeRemark.AsQueryable();

            // If an Employee is selected (id is not null), filter the list
            if (id.HasValue)
            {
                RemarksQuery = RemarksQuery.Where(x => x.EmpId == id.Value);
            }

            // Execute query
            var rems = RemarksQuery.OrderByDescending(x => x.CreatedOn).ToList();

            // Pass the current selection to the view (for the Upload form to know who is selected)
            ViewBag.CurrentEmpId = id;

            return View(rems);
        }

        //POST: Upload Document
        [HttpPost]
        public ActionResult Upload(int EmpId, string Type, string Remark, HttpPostedFileBase fileUpload)
        {
            try
            {
                string filePath = null;

                if (fileUpload != null && fileUpload.ContentLength > 0)
                {
                    string folderPath = Server.MapPath("~/EmployeeDocs/");
                    string extension = Path.GetExtension(fileUpload.FileName);
                    string timeStamp = DateTime.Now.ToString("yyyyMMddHHmmss");
                    string fileName = $"{EmpId}_RemarkDoc_{timeStamp}{extension}";

                    string fullPath = Path.Combine(folderPath, fileName);
                    fileUpload.SaveAs(fullPath);

                    filePath = "~/EmployeeDocs/" + fileName;
                }
                EmployeeRemark newRem = new EmployeeRemark
                {
                    EmpId = EmpId,
                    Type = Type,               
                    Remark = Remark,
                    FilePath = filePath,      
                    CreatedOn = DateTime.Now
                };

                db.EmployeeRemarks.Add(newRem);
                db.SaveChanges();

                TempData["Message"] = "Remark saved successfully!";
            }
            catch (Exception ex)
            {
                TempData["Error"] = "Upload failed: " + ex.Message;
            }
            if (EmpId == 0)
            {
                throw new Exception("EmpId is ZERO — hidden field missing");
            }
            return RedirectToAction("Index");
        }


        public ActionResult Delete(int id)
            {
            if (Session["Role"] == null || Session["Role"].ToString() != "Admin")
            {
                return RedirectToAction("Index", "Employee");
            }
            try
            {
                // Find the document record
                var doc = db.EmployeeRemarks.Find(id);
                if (doc != null)
                {
                    //int? empId = doc.EmpId;
                    // 1. Delete File from Disk
                    string fullPath = Server.MapPath(doc.FilePath);
                    if (System.IO.File.Exists(fullPath))
                    {
                        System.IO.File.Delete(fullPath);
                    }

                    //// 2. Delete Record from Database
                    //int? associatedEmpId = doc.EmpId; // Remember ID for redirect
                    db.EmployeeRemarks.Remove(doc);
                    db.SaveChanges();

                    TempData["Message"] = "Remark deleted successfully.";

                    // Redirect back to the filtered view for that employee
                    return RedirectToAction("Index");
                }
            }
            catch (Exception ex)
            {
                TempData["Error"] = "Error deleting document: " + ex.Message;
            }

            return RedirectToAction("Index");
        }

        // OPTIONAL: Download Feature
        public ActionResult Download(string filePath)
        {
            if (Session["Role"] == null || Session["Role"].ToString() != "Admin")
            {
                return RedirectToAction("Index", "Remark");
            }
            if (!string.IsNullOrEmpty(filePath))
            {
                string absolutePath = Server.MapPath(filePath);
                if (System.IO.File.Exists(absolutePath))
                {
                    byte[] fileBytes = System.IO.File.ReadAllBytes(absolutePath);
                    string fileName = Path.GetFileName(absolutePath);
                    return File(fileBytes, System.Net.Mime.MediaTypeNames.Application.Octet, fileName);
                }
            }
            return HttpNotFound();
        }

    }
}