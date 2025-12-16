using System;
using System.Linq; // Required for EF queries (FirstOrDefault, Any)
using System.Web.Mvc;
using System.Web.Security;
using EmployeeApp.Models; // Make sure this matches your Model namespace
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.IO;

namespace EmployeeAppMVC.Controllers
{
    public class AccountController : Controller
    {
        // 1. Create the Database Connection
        EmployeeEntities db = new EmployeeEntities();

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
            if (Session["CaptchaAnswer"] == null)
            {
                ViewBag.Message = "Session expired. Please refresh the page.";
                return View(u);
            }

            int correctAnswer = Convert.ToInt32(Session["CaptchaAnswer"]);
            int userAnswer = 0;
            int.TryParse(captchaInput, out userAnswer);

            // If Captcha is WRONG
            if (userAnswer != correctAnswer)
            {
                ViewBag.Message = "Wrong Captcha sum! Please try again.";
                return View(u);
            }

            // 2. INPUT VALIDATION
            if (!ModelState.IsValid)
            {
                return View(u);
            }

            // 3. DATABASE CHECK (Entity Framework)
            // Strategy: Find username first (DB), then check password case-sensitively (Memory)
            var user = db.Users.FirstOrDefault(x => x.Username == u.Username);

            if (user != null)
            {
                // Strict Case-Sensitive Password Check
                if (string.Equals(user.Password, u.Password, StringComparison.Ordinal))
                {
                    // Success! Create the Cookie
                    FormsAuthentication.SetAuthCookie(u.Username, false);
                    return RedirectToAction("Index", "Employee");
                }
            }

            // If we reach here, either user was null OR password was wrong
            ViewBag.Message = "Invalid Username or Password";
            return View(u);
        }

        // GET: Register
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
                // 1. Check if Username already exists
                bool exists = db.Users.Any(x => x.Username == u.Username);

                if (exists)
                {
                    ViewBag.Message = "Username already taken!";
                    return View(u);
                }

                // 2. Insert new user (EF Way)
                db.Users.Add(u);
                db.SaveChanges(); // Commits the INSERT command

                // Success! Redirect to Login
                return RedirectToAction("Login");
            }

            return View(u);
        }

        // --- CAPTCHA IMAGE GENERATOR (Kept exactly the same) ---
        public ActionResult CaptchaImage()
        {
            Random rand = new Random();
            int num1 = rand.Next(1, 10);
            int num2 = rand.Next(1, 10);

            Session["CaptchaAnswer"] = num1 + num2;

            using (Bitmap bmp = new Bitmap(150, 50))
            using (Graphics g = Graphics.FromImage(bmp))
            {
                g.Clear(Color.Gray);

                for (int i = 0; i < 200; i++)
                {
                    int x = rand.Next(0, 150);
                    int y = rand.Next(0, 50);
                    bmp.SetPixel(x, y, Color.LightGray);
                }

                string text = $"{num1} + {num2} = ?";
                Font font = new Font("Arial", 20, FontStyle.Bold);
                g.DrawString(text, font, Brushes.DarkBlue, 10, 10);

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