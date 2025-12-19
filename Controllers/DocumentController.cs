using System;
using System.IO;
using System.Linq;
using System.Web;
using System.Web.Mvc;
using EmployeeApp.Models; // Change to your actual namespace

namespace EmployeeApp.Controllers
{
    [Authorize] // Ensure user is logged in
    public class DocumentController : Controller
    {
        EmployeeEntities db = new EmployeeEntities();

        // GET: Manage Documents Page
        public ActionResult Index(int? id)
        {
            // 1. SORT BY EMPID: Order the list by EmpId instead of Name
            // We maintain the selection using the 'id' parameter in the SelectList constructor
            var empList = db.Employees.OrderBy(e => e.EmpId).ToList();
            ViewBag.EmpList = new SelectList(empList, "EmpId", "Name", id);

            // 2. FILTER SYSTEM:
            // Start with all documents
            var docsQuery = db.vw_EmployeeDocs.AsQueryable();

            // If an Employee is selected (id is not null), filter the list
            if (id.HasValue)
            {
                docsQuery = docsQuery.Where(x => x.EmpId == id.Value);
            }

            // Execute query
            var docs = docsQuery.OrderByDescending(x => x.CreatedOn).ToList();

            // Pass the current selection to the view (for the Upload form to know who is selected)
            ViewBag.CurrentEmpId = id;

            return View(docs);
        }

        // POST: Upload Document
        [HttpPost]
        public ActionResult Upload(int EmpId, string DocName, string DocDesc, HttpPostedFileBase fileUpload)
        {
            if (fileUpload != null && fileUpload.ContentLength > 0)
            {
                try
                {
                    // 1. Create Folder if it doesn't exist
                    string folderPath = Server.MapPath("~/EmployeeDocs/");
                    if (!Directory.Exists(folderPath))
                    {
                        Directory.CreateDirectory(folderPath);
                    }

                    // 2. Generate Custom Filename: EmpId_DocName_yyyyMMddHHmmss.ext
                    string extension = Path.GetExtension(fileUpload.FileName);
                    string safeDocName = DocName.Replace(" ", "_"); // Clean up spaces
                    string timeStamp = DateTime.Now.ToString("yyyyMMddHHmmss");

                    string fileName = $"{EmpId}_{safeDocName}_{timeStamp}{extension}";
                    string fullPath = Path.Combine(folderPath, fileName);

                    // 3. Save File to Disk
                    fileUpload.SaveAs(fullPath);

                    // 4. Save Entry to Database
                    EmployeeDoc newDoc = new EmployeeDoc();
                    newDoc.EmpId = EmpId;
                    newDoc.DocName = DocName;
                    newDoc.DocDesc = DocDesc;
                    newDoc.DocPath = "~/EmployeeDocs/" + fileName; // Store relative path
                    newDoc.CreatedOn = DateTime.Now;

                    // Get Logged in User ID (assuming generic Int ID logic from previous chats)
                    newDoc.CreatedBy = Convert.ToInt32(Session["UserID"]); 

                    db.EmployeeDocs.Add(newDoc);
                    db.SaveChanges();

                    TempData["Message"] = "Document uploaded successfully!";
                }
                catch (Exception ex)
                {
                    TempData["Error"] = "Upload failed: " + ex.Message;
                }
            }
            else
            {
                TempData["Error"] = "Please select a valid file.";
            }

            return RedirectToAction("Index");
        }

        public ActionResult Delete(int id)
        {
            try
            {
                // Find the document record
                var doc = db.EmployeeDocs.Find(id);
                if (doc != null)
                {
                    // 1. Delete File from Disk
                    string fullPath = Server.MapPath(doc.DocPath);
                    if (System.IO.File.Exists(fullPath))
                    {
                        System.IO.File.Delete(fullPath);
                    }

                    // 2. Delete Record from Database
                    int? associatedEmpId = doc.EmpId; // Remember ID for redirect
                    db.EmployeeDocs.Remove(doc);
                    db.SaveChanges();

                    TempData["Message"] = "Document deleted successfully.";

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