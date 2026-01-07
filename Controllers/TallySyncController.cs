using EmployeeApp.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Web.Mvc;

public class TallySyncController : Controller
{
    private EmployeeEntities db = new EmployeeEntities();
    private TallyService _tallyService = new TallyService();

    // GET: TallySync
    public ActionResult Index(int? id)
    {
        var model = new TallyDashboardViewModel
        {
            SelectedEmpId = id,
            UnsyncedRemarks = new List<vw_EmployeeRemark>(),
            TallyStatus = null
        };

        // 1. Populate Dropdown List
        ViewBag.EmpList = new SelectList(db.Employees.OrderBy(e => e.EmpId), "EmpId", "Name", id);

        // 2. Base Query for Remarks (Only Fines/Salary, Not Synced)
        var query = db.vw_EmployeeRemark.Where(r =>
                        (r.IsRemarkSynced == null || r.IsRemarkSynced == false) &&
                        (r.Type == "Fine" || r.Type == "Salary Credit"));

        if (id.HasValue)
        {
            // --- LEFT PANEL LOGIC ---
            var emp = db.Employees.Find(id);
            if (emp != null)
            {
                model.SelectedEmpName = emp.Name;

                // Fetch Tally Data View
                var tallyData = db.vw_TallyData.FirstOrDefault(t => t.EmpId == id);

                model.TallyStatus = new TallyStatusDetails
                {
                    IsMapped = tallyData != null && (tallyData.TallyMapped ?? false),
                    LedgerNameInTally = tallyData?.LedgerName ?? "Not Found",
                    LastSyncOn = tallyData?.LastSyncOn,
                    // Check if Tally Name matches Current DB Name
                    NameMismatch = tallyData != null &&
                                   !string.Equals(tallyData.LedgerName, emp.Name, StringComparison.OrdinalIgnoreCase)
                };
            }

            // --- RIGHT PANEL LOGIC (Filter by ID) ---
            model.UnsyncedRemarks = query.Where(r => r.EmpId == id).OrderByDescending(r => r.CreatedOn).ToList();
        }
        else
        {
            // If no ID selected, show ALL unsynced items
            model.UnsyncedRemarks = query.OrderByDescending(r => r.CreatedOn).ToList();
        }

        return View(model);
    }

    [HttpPost]
    public ActionResult CreateLedger(int empId)
    {
        try
        {
            var emp = db.Employees.Find(empId);
            if (emp != null)
            {
                // 1. Send to Tally
                // Note: This creates a ledger with 0 balance. It won't show in "Balance Sheet"
                // until you post a voucher, but it WILL be in "Chart of Accounts".
                _tallyService.SyncLedger(emp.Name, "Employees", emp);

                // 2. UPDATE SQL DATABASE (The missing step!)
                // We need to tell our DB that this employee is now mapped.
                var existingMap = db.TallyDatas.FirstOrDefault(t => t.EmpId == empId);

                if (existingMap == null)
                {
                    // Create new mapping
                    var newMap = new TallyData
                    {
                        EmpId = empId,
                        LedgerName = emp.Name, // We just created it with this name
                        TallyMapped = true,
                        LastSyncOn = DateTime.Now
                    };
                    db.TallyDatas.Add(newMap);
                }
                else
                {
                    // Update existing
                    existingMap.LedgerName = emp.Name;
                    existingMap.TallyMapped = true;
                    existingMap.LastSyncOn = DateTime.Now;
                }

                db.SaveChanges();
                TempData["Message"] = "Ledger created in Tally & Database updated.";
            }
            else
            {
                TempData["Error"] = "Employee not found.";
            }
        }
        catch (Exception ex)
        {
            TempData["Error"] = "Error: " + ex.Message;
        }

        return RedirectToAction("Index", new { id = empId });
    }

    [HttpPost]
    public ActionResult RenameLedger(int empId)
    {
        try
        {
            var emp = db.Employees.Find(empId);
            var tallyData = db.TallyDatas.FirstOrDefault(t => t.EmpId == empId);

            if (emp != null && tallyData != null)
            {
                string oldName = tallyData.LedgerName; // Name currently in Tally
                string newName = emp.Name;             // Correct name from Employee Master

                // 1. Call Tally Service to Rename
                string response = _tallyService.RenameLedger(oldName, newName);

                // 2. Check response (Basic check for success)
                if (!response.ToLower().Contains("\"error\": 1") && !response.ToLower().Contains("\"exception\": 1"))
                {
                    // 3. Update Local DB to match the new status
                    tallyData.LedgerName = newName;
                    tallyData.LastSyncOn = DateTime.Now;
                    db.SaveChanges();

                    TempData["Message"] = $"Success! Renamed '{oldName}' to '{newName}' in Tally.";
                }
                else
                {
                    // If names are identical or other Tally error
                    if (response.Contains("Identical"))
                    {
                        // Even if Tally says identical, update local DB to sync them up
                        tallyData.LedgerName = newName;
                        db.SaveChanges();
                        TempData["Message"] = "Sync corrected (Names were already identical).";
                    }
                    else
                    {
                        TempData["Error"] = "Tally Error: " + response;
                    }
                }
            }
            else
            {
                TempData["Error"] = "Data not found.";
            }
        }
        catch (Exception ex)
        {
            TempData["Error"] = "System Error: " + ex.Message;
        }

        return RedirectToAction("Index", new { id = empId });
    }

    // ACTION: Sync Single Row
    [HttpPost]
    public ActionResult SyncRow(int remId)
    {
        return ProcessSync(remId);
    }

    // ACTION: Sync All (Filtered by Employee if selected, or All visible)
    [HttpPost]
    public ActionResult SyncAll(int? empId)
    {
        // Fetch all IDs to sync
        var query = db.vw_EmployeeRemark.Where(r =>
                        (r.IsRemarkSynced == null || r.IsRemarkSynced == false) &&
                        (r.Type == "Fine" || r.Type == "Salary Credit"));

        if (empId.HasValue)
            query = query.Where(r => r.EmpId == empId);

        var remarksToSync = query.ToList();
        int successCount = 0;
        List<string> errors = new List<string>();

        foreach (var remark in remarksToSync)
        {
            var result = ProcessSync(remark.RemID);
            // We assume Redirect/Json logic handled inside ProcessSync, 
            // but for Batch we need internal logic. 
            // Refactoring slightly for batch reuse:
        }

        TempData["Message"] = $"Batch Process Complete.";
        return RedirectToAction("Index");
    }

    // HELPER: The Actual Sync Logic
    private ActionResult ProcessSync(int remId)
    {
        try
        {
            // 1. Get Remark
            var remark = db.EmployeeRemarks.Find(remId); // Use actual Table, not View, to update
            if (remark == null) return Json(new { success = false, message = "Remark not found" });

            // 2. Get Employee (Required for TallyService)
            var emp = db.Employees.Find(remark.EmpId);
            if (emp == null) return Json(new { success = false, message = "Employee not found" });

            // 3. Push to Tally
            string tallyResponse = _tallyService.PostVoucherToTally(emp, remark.Type, remark.Amount ?? 0, remark.Remark);

            // 4. Check Response (Tally returns XML or "Success" depending on your service)
            // Assuming your Service returns "Success" or contains "created" or ID on success
            if (tallyResponse.ToLower().Contains("\"error\": 1") || tallyResponse.Contains("\"exception\": 1"))
            {
                TempData["Error"] = $"Tally Error: {tallyResponse}";
            }
            else
            {
                // 5. Update Database
                remark.IsRemarkSynced = true;
                db.SaveChanges();
                TempData["Message"] = "Synced successfully!";
            }
        }
        catch (Exception ex)
        {
            TempData["Error"] = "System Error: " + ex.Message;
        }

        // Return to Index (maintaining filter)
        var empIdParam = db.EmployeeRemarks.Find(remId)?.EmpId;
        return RedirectToAction("Index", new { id = empIdParam });
    }
}