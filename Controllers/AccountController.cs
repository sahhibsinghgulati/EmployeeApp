using System;
using System.Data.SqlClient;
using System.Web.Mvc;
using System.Web.Security;
using EmployeeAppMVC.Models;
using System.Drawing; // Import this at the top!
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.IO;

namespace EmployeeAppMVC.Controllers
{
    public class AccountController : Controller
    {
        string connStr = System.Configuration.ConfigurationManager.ConnectionStrings["EmpDBConnection"].ConnectionString;

        // GET: Login Page
        public ActionResult Login()
        {
            return View();
        }

        // POST: Check Login
        [HttpPost]
        public ActionResult Login(User u, string captchaInput)
        {
            // 1. Validate Captcha FIRST
            int correctAnswer = Convert.ToInt32(Session["CaptchaAnswer"]);
            int userAnswer = 0;
            int.TryParse(captchaInput, out userAnswer);

            // If Captcha is WRONG
            if (userAnswer != correctAnswer)
            {
                ViewBag.Message = "Wrong Captcha sum! Please try again.";

                // REGENERATE CAPTCHA (Critical Step!)
                

                return View(u);
            }

            // --- 2. INPUT VALIDATION (e.g. Empty Password) ---
            if (!ModelState.IsValid)
            {
                // REGENERATE CAPTCHA (Critical Step!)
                

                return View(u);
            }
            using (SqlConnection conn = new SqlConnection(connStr))
            {
                // Check if user exists in DB
                string sql = @"SELECT COUNT(*) FROM Users 
                       WHERE CAST(Username AS VARBINARY(MAX)) = CAST(@u AS VARBINARY(MAX)) 
                       AND CAST(Password AS VARBINARY(MAX)) = CAST(@p AS VARBINARY(MAX))";
                SqlCommand cmd = new SqlCommand(sql, conn);
                cmd.Parameters.AddWithValue("@u", u.Username);
                cmd.Parameters.AddWithValue("@p", (object)u.Password ?? ""); // Note: For real apps, use Hashing!

                conn.Open();
                int count = (int)cmd.ExecuteScalar();

                if (count > 0)
                {
                    // Success! Create the Cookie
                    FormsAuthentication.SetAuthCookie(u.Username, false);
                    return RedirectToAction("Index", "Employee");
                }
                else
                {
                    ViewBag.Message = "Invalid Username or Password";
                    
                    return View();
                }
            }
        }
       
        public ActionResult Register()
        {
            return View();
        }

        // POST: Register New User
        [HttpPost]
        public ActionResult Register(User u)
        {
            if (ModelState.IsValid)
            {
                using (SqlConnection conn = new SqlConnection(connStr))
                {
                    conn.Open();

                    // 1. Check if Username already exists
                    string checkSql = "SELECT COUNT(*) FROM Users WHERE Username = @u";
                    SqlCommand checkCmd = new SqlCommand(checkSql, conn);
                    checkCmd.Parameters.AddWithValue("@u", u.Username);
                    int exists = (int)checkCmd.ExecuteScalar();

                    if (exists > 0)
                    {
                        ViewBag.Message = "Username already taken!";
                        return View(u);
                    }

                    // 2. Insert new user
                    string sql = "INSERT INTO Users (Username, Password) VALUES (@u, @p)";
                    SqlCommand cmd = new SqlCommand(sql, conn);
                    cmd.Parameters.AddWithValue("@u", u.Username);
                    cmd.Parameters.AddWithValue("@p", u.Password); // Consider hashing in real apps!

                    cmd.ExecuteNonQuery();
                }

                // Success! Redirect to Login
                return RedirectToAction("Login");
            }

            return View(u);
        }

        public ActionResult CaptchaImage()
        {
            // 1. Generate Numbers
            Random rand = new Random();
            int num1 = rand.Next(1, 10);
            int num2 = rand.Next(1, 10);

            // 2. Save Answer to Session
            Session["CaptchaAnswer"] = num1 + num2;

            // 3. Create the Image
            using (Bitmap bmp = new Bitmap(150, 50)) // Width: 150, Height: 50
            using (Graphics g = Graphics.FromImage(bmp))
            {

                g.Clear(Color.Gray); // Background color

                // Add some "Noise" (dots) to confuse bots
                for (int i = 0; i < 200; i++)
                {
                    int x = rand.Next(0, 150);
                    int y = rand.Next(0, 50);
                    bmp.SetPixel(x, y, Color.LightGray);
                }

                // Draw the text (The Math Problem)
                string text = $"{num1} + {num2} = ?";
                Font font = new Font("Arial", 20, FontStyle.Bold);
                g.DrawString(text, font, Brushes.DarkBlue, 10, 10);

                // 4. Save to Memory Stream
                using (MemoryStream ms = new MemoryStream())
                {
                    bmp.Save(ms, ImageFormat.Png);
                    return File(ms.ToArray(), "image/png");
                }
            }
        }
        // GET: Logout
        public ActionResult Logout()
        {
            FormsAuthentication.SignOut();
            return RedirectToAction("Login");
        }

    }
}