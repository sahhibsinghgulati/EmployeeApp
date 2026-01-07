using Antlr.Runtime.Misc;
using EmployeeApp.Models;
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

    // CHANGE 1: Update parameter from 'string empName' to 'Employee emp'
    public string PostVoucherToTally(Employee emp, string type, decimal amount, string remark)
    {
        try
        {
            // 1. SETUP
            string voucherTypeName = "Journal";
            string debitLedger = "";
            string creditLedger = "";
            string debitGroup = "";
            string creditGroup = "";

            // CHANGE 2: Extract the name from the Employee object
            string safeEmpName = emp.Name.Replace("\\", "\\\\").Replace("\"", "\\\"");
            string safeType = type.Replace("\\", "\\\\").Replace("\"", "\\\"");
            string safeRemark = (remark ?? "").Replace("\\", "\\\\").Replace("\"", "\\\"");

            if (type.Contains("Fine"))
            {
                // FINE: Employee (Dr) pays Company (Cr)
                debitLedger = safeEmpName;
                debitGroup = "Employees";

                creditLedger = "Fines & Penalties";
                creditGroup = "Indirect Incomes";
            }
            else
            {
                // SALARY: Company (Dr) pays Employee (Cr)
                debitLedger = "Salary Expense";
                debitGroup = "Indirect Expenses";

                creditLedger = safeEmpName;
                creditGroup = "Employees";
            }

            // 2. SYNC MASTERS (The New Step)
            //Log("Syncing Ledgers...");

            //// CHANGE 3: Pass the 'emp' object to SyncLedger ONLY if the ledger is the employee
            //// This ensures Address/PAN/GSTIN are sent for the person, but null is sent for "Fines" or "Salary Expense"
            SyncLedger(debitLedger, debitGroup, (debitLedger == safeEmpName) ? emp : null);
            SyncLedger(creditLedger, creditGroup, (creditLedger == safeEmpName) ? emp : null);

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
                new { name = "svCurrentCompany", value = "Employees" }
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

                    // 2. VOUCHER FIELDS
                    date = DateTime.Now.ToString("yyyyMMdd"),
                    effectivedate = DateTime.Now.ToString("yyyyMMdd"),
                    vouchertypename = voucherTypeName,
                    narration = !string.IsNullOrEmpty(safeRemark) ? safeRemark : ("Auto-entry: " + safeType),
                    
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

    public void SyncLedger(string name, string group, Employee emp = null)
    {
        try
        {
            if (string.IsNullOrEmpty(name)) return;

            // 1. EXTRACT DATA FROM EMPLOYEE OBJECT
            string mailingName = null;
            //string address = null;
            string[] addressLines = null;
            string state = null;
            string country = null;
            string pan = null;
            string gstin = null;
            string gstType = null;

            if (emp != null)
            {
                mailingName = emp.Name;
                //address = emp.Address;
                if (!string.IsNullOrEmpty(emp.Address))
                    addressLines = emp.Address.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries);

                //if (!string.IsNullOrEmpty(emp.State)) state = emp.State;
                //if (!string.IsNullOrEmpty(emp.Country)) country = emp.Country;
                //if (!string.IsNullOrEmpty(emp.PanNumber)) pan = emp.PanNumber;
                //if (!string.IsNullOrEmpty(emp.GstNumber)) gstin = emp.GstNumber;

                //// Auto-detect GST Type
                //gstType = !string.IsNullOrEmpty(gstin) ? "Regular" : "Unregistered";
            }

            // 2. PREPARE SAFE STRINGS
            string safeName = name.Replace("\\", "\\\\").Replace("\"", "\\\"");
            string safeGroup = group.Replace("\\", "\\\\").Replace("\"", "\\\"");
            string companyName = "Employees";

            // 3. BUILD DYNAMIC DICTIONARY
            var ledgerObject = new Dictionary<string, object>();

            // Mandatory
            ledgerObject["name"] = safeName;
            ledgerObject["parent"] = safeGroup;
            ledgerObject["isbillwiseon"] = false;

            //// Conditional: Only add if value exists
            //if (!string.IsNullOrEmpty(mailingName)) ledgerObject["mailingname"] = mailingName;

            //if (!string.IsNullOrEmpty(address))
            //{
            //    // Tally needs address as an array of lines
            //    ledgerObject["address"] = address.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries);
            //}

            //if (!string.IsNullOrEmpty(state)) ledgerObject["statename"] = state;
            //if (!string.IsNullOrEmpty(country)) ledgerObject["countryofresidence"] = country;

            //if (!string.IsNullOrEmpty(pan)) ledgerObject["incometaxnumber"] = pan;
            //if (!string.IsNullOrEmpty(gstType)) ledgerObject["gstregistrationtype"] = gstType;
            //if (!string.IsNullOrEmpty(gstin)) ledgerObject["partygstin"] = gstin;

            // 4. WRAP IN TALLY MESSAGE
            //var payload = new
            //{
            //    static_variables = new[]
            //    {
            //        new { name = "svMstImportFormat", value = "jsonex" },
            //        new { name = "svCurrentCompany", value = companyName }
            //    },
            //    tallymessage = new[]
            //    {
            //        new
            //        {
            //            // 1. METADATA
            //            metadata = new
            //            {
            //                type = "Ledger",
            //                action = "Alter", // CHANGED: Lowercase "create" enforces Group check
            //                name = safeName
            //            },

            //            // 2. DATA
            //            name = safeName,
            //            parent = safeGroup, // This will now be read correctly

            //            // Defaults
            //            isbillwiseon = false,
            //            iscostcentreson = false,
            //            openingbalance = 0.00
            //        }
            //    }
            //};
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
                        action = "Alter",
                        name = safeName
                    },

                    // 2. DATA (Siblings to Metadata)
                    name = safeName,
                    parent = safeGroup,
                    
                    // Optional Fields (Mapped from variables above)
                    mailingname = mailingName,
                    ledgeraddress = addressLines,
                    ledstatename = state,
                    countryofresidence = country,
                    incometaxnumber = pan,
                    partygstin = gstin,
                    gstregistrationtype = gstType,

                    // Defaults
                    isbillwiseon = false,
                    iscostcentreson = false,
                    openingbalance = 0.00
                }
            }
            };
            // 5. SEND
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
            // Log("Ledger Sync (" + safeName + "): " + result);
        }
        catch (Exception ex)
        {
            // Log("Ledger Sync Failed: " + ex.Message);
        }
    }

    //private void SyncLedger(string ledgerName, string groupName)
    //{
    //    try
    //    {
    //        string safeName = ledgerName.Replace("\\", "\\\\").Replace("\"", "\\\"");
    //        string safeGroup = groupName.Replace("\\", "\\\\").Replace("\"", "\\\"");

    //        // Set your specific Tally Company Name here
    //        string companyName = "Employees";

    //        var payload = new
    //        {
    //            static_variables = new[]
    //            {
    //                new { name = "svMstImportFormat", value = "jsonex" },
    //                new { name = "svCurrentCompany", value = companyName }
    //            },
    //            tallymessage = new[]
    //            {
    //                new
    //                {
    //                    // 1. METADATA
    //                    metadata = new
    //                    {
    //                        type = "Ledger",
    //                        action = "Alter", // CHANGED: Lowercase "create" enforces Group check
    //                        name = safeName
    //                    },

    //                    // 2. DATA
    //                    name = safeName,
    //                    parent = safeGroup, // This will now be read correctly

    //                    // Defaults
    //                    isbillwiseon = false,
    //                    iscostcentreson = false,
    //                    openingbalance = 0.00
    //                }
    //            }
    //        };

    //        string jsonBody = new JavaScriptSerializer().Serialize(payload);

    //        var headers = new Dictionary<string, string>
    //        {
    //            { "Tallyrequest", "Import" },
    //            { "type", "Data" },
    //            { "Id", "All Masters" },
    //            { "Version", "1" },
    //            { "detailed-response", "Yes" }
    //        };

    //        string result = SendJsonToTally(jsonBody, headers);

    //        // Log the result. Note: If you see "Name already exists", that is GOOD. 
    //        // It means the ledger is ready for the voucher.
    //        Log("Ledger Sync (" + safeName + "): " + result);
    //    }
    //    catch (Exception ex)
    //    {
    //        Log("Ledger Sync Failed: " + ex.Message);
    //    }
    //}

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

    public string SendJsonToTally(string jsonPayload, Dictionary<string, string> headers)
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