using Antlr.Runtime.Misc;
using System;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Text;
using System.Web;
using System.Web.Script.Serialization;
using static System.Collections.Specialized.BitVector32;

public class TallyService
{
    private string _tallyUrl = "http://localhost:9000";

    public string PostVoucherToTally(string empName, string type, decimal amount)
    {
        try
        {
            // 1. SETUP
            string voucherTypeName = "Journal";
            string debitLedger = "";
            string creditLedger = "";
            string debitGroup = "";
            string creditGroup = "";

            string safeEmpName = empName.Replace("\\", "\\\\").Replace("\"", "\\\"");
            string safeType = type.Replace("\\", "\\\\").Replace("\"", "\\\"");

            if (type.Contains("Fine"))
            {
                // FINE: Employee (Dr) pays Company (Cr)
                debitLedger = safeEmpName;
                debitGroup = "Sundry Creditors"; // FIXED: Assigned Group

                creditLedger = "Fines & Penalties";
                creditGroup = "Indirect Incomes"; // FIXED: Assigned Group
            }
            else
            {
                // SALARY: Company (Dr) pays Employee (Cr)
                debitLedger = "Salary Expense";
                debitGroup = "Indirect Expenses"; // FIXED: Assigned Group

                creditLedger = safeEmpName;
                creditGroup = "Sundry Creditors"; // FIXED: Assigned Group
            }

            // 2. SYNC MASTERS (The New Step)
            // We use 'Alter' to Create-or-Update. This ensures they exist before we post the voucher.
            Log("Syncing Ledgers...");
            SyncLedger(debitLedger, debitGroup);
            SyncLedger(creditLedger, creditGroup);

            // 2. AMOUNTS (Balanced)
            string absAmount = Math.Abs(amount).ToString("0.00");
            string debitAmountStr = "-" + absAmount; // Negative
            string creditAmountStr = absAmount;      // Positive

            // 3. CONSTRUCT PAYLOAD (Matching Sample Structure)
            var voucherPayload = new
            {
                static_variables = new[]
                {
                new { name = "svVchImportFormat", value = "jsonex" },
                new { name = "svCurrentCompany", value = "Employees" } // Ensure this matches your Tally Company Name exactly
            },
                tallymessage = new[]
                {
                new
                {
                    // 1. METADATA
                    metadata = new
                    {
                        type = "Voucher",
                        vchtype = voucherTypeName,
                        action = "Create",
                        objview = "Accounting Voucher View"
                    },

                    // 2. VOUCHER FIELDS (Directly here, NO "voucher" wrapper)
                    date = DateTime.Now.ToString("yyyyMMdd"),
                    effectivedate = DateTime.Now.ToString("yyyyMMdd"),
                    vouchertypename = voucherTypeName,
                    narration = "Auto-entry: " + safeType,
                    
                    // 3. LEDGER ENTRIES
                    ledgerentries = new object[]
                    {
                        // DEBIT ENTRY
                        new {
                            ledgername = debitLedger,
                            isdeemedpositive = true,   // Boolean true
                            amount = debitAmountStr    // Negative
                        },
                        // CREDIT ENTRY
                        new {
                            ledgername = creditLedger,
                            isdeemedpositive = false,  // Boolean false
                            amount = creditAmountStr   // Positive
                        }
                    }
                }
            }
            };

            string jsonBody = new JavaScriptSerializer().Serialize(voucherPayload);

            // Note: We don't need the ".replace" hack anymore because "ledgerentries" 
            // is a valid C# name (unlike "ledgerentries.list").

            Log("Sending JSON to Tally...");

            var headers = new Dictionary<string, string>
        {
            { "Tallyrequest", "Import" },
            { "type", "Data" },
            { "Id", "Vouchers" },
            { "Version", "1" },
            { "detailed-response", "Yes" }
        };

            return SendJsonToTally(jsonBody, headers);
        }
        catch (Exception ex)
        {
            Log("CRITICAL ERROR: " + ex.Message);
            return "Local Error: " + ex.Message;
        }
    }

    private void SyncLedger(string ledgerName, string groupName)
    {
        try
        {
            string safeName = ledgerName.Replace("\\", "\\\\").Replace("\"", "\\\"");
            string safeGroup = groupName.Replace("\\", "\\\\").Replace("\"", "\\\"");

            // Set your specific Tally Company Name here
            string companyName = "Employees";

            var payload = new
            {
                static_variables = new[]
                {
                    new { name = "svMstImportFormat", value = "jsonex" },
                    new { name = "svCurrentCompany", value = companyName }
                },
                tallymessage = new[]
                {
                    new
                    {
                        // 1. METADATA
                        metadata = new
                        {
                            type = "Ledger",
                            action = "Alter", // CHANGED: Lowercase "create" enforces Group check
                            name = safeName
                        },
                        
                        // 2. DATA
                        name = safeName,
                        parent = safeGroup, // This will now be read correctly
                        
                        // Defaults
                        isbillwiseon = false,
                        iscostcentreson = false,
                        openingbalance = 0.00
                    }
                }
            };

            string jsonBody = new JavaScriptSerializer().Serialize(payload);

            var headers = new Dictionary<string, string>
            {
                { "Tallyrequest", "Import" },
                { "type", "Data" },
                { "Id", "All Masters" },
                { "Version", "1" },
                { "detailed-response", "Yes" }
            };

            string result = SendJsonToTally(jsonBody, headers);

            // Log the result. Note: If you see "Name already exists", that is GOOD. 
            // It means the ledger is ready for the voucher.
            Log("Ledger Sync (" + safeName + "): " + result);
        }
        catch (Exception ex)
        {
            Log("Ledger Sync Failed: " + ex.Message);
        }
    }

    // ---------------------------------------------------------
    // RENAME LEDGER (Call this when Employee Name is edited)
    // ---------------------------------------------------------
    public string RenameLedger(string oldName, string newName)
    {
        try
        {
            // If names are the same, do nothing
            if (oldName.Trim().Equals(newName.Trim(), StringComparison.OrdinalIgnoreCase))
                return "Names are identical, no update needed.";

            string safeOldName = oldName.Replace("\\", "\\\\").Replace("\"", "\\\"");
            string safeNewName = newName.Replace("\\", "\\\\").Replace("\"", "\\\"");
            string companyName = "Employees"; // Your Tally Company Name

            var payload = new
            {
                static_variables = new[]
                {
                    new { name = "svMstImportFormat", value = "jsonex" },
                    new { name = "svCurrentCompany", value = companyName }
                },
                tallymessage = new[]
                {
                    new
                    {
                        // METADATA
                        metadata = new
                        {
                            type = "Ledger",
                            action = "Alter", // Must be Alter
                            name = safeOldName // Metadata points to the EXISTING (Old) Name
                        },

                        // DATA
                        // The 'name' field gets the NEW Name
                        name = safeNewName,
                        
                        // The 'oldname' field tells Tally which ledger to find
                        oldname = safeOldName
                    }
                }
            };

            string jsonBody = new JavaScriptSerializer().Serialize(payload);

            var headers = new Dictionary<string, string>
            {
                { "Tallyrequest", "Import" },
                { "type", "Data" },
                { "Id", "All Masters" },
                { "Version", "1" },
                { "detailed-response", "Yes" }
            };

            Log($"Renaming Ledger from '{oldName}' to '{newName}'...");
            return SendJsonToTally(jsonBody, headers);
        }
        catch (Exception ex)
        {
            Log("Rename Failed: " + ex.Message);
            return "Local Error: " + ex.Message;
        }
    }

    private string SendJsonToTally(string jsonPayload, Dictionary<string, string> headers)
    {
        try
        {
            HttpWebRequest request = (HttpWebRequest)WebRequest.Create(_tallyUrl);
            request.Method = "POST";
            foreach (var head in headers) request.Headers.Add(head.Key, head.Value);
            request.ContentType = "application/json";

            byte[] bytes = Encoding.UTF8.GetBytes(jsonPayload);
            request.ContentLength = bytes.Length;

            using (Stream requestStream = request.GetRequestStream())
            {
                requestStream.Write(bytes, 0, bytes.Length);
            }

            using (HttpWebResponse response = (HttpWebResponse)request.GetResponse())
            {
                using (StreamReader streamReader = new StreamReader(response.GetResponseStream(), true))
                {
                    return streamReader.ReadToEnd();
                }
            }
        }
        catch (Exception ex)
        {
            return "Connection Failed: " + ex.Message;
        }
    }

    private void Log(string message)
    {
        try
        {
            string path = HttpContext.Current.Server.MapPath("~/TallyLog.txt");
            File.AppendAllText(path, $"[{DateTime.Now}] {message}{Environment.NewLine}");
        }
        catch { }
    }
}