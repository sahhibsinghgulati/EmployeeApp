using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;
using System.Web.Mvc;
using System.Web.Optimization;
using System.Web.Routing;

namespace EmployeeApp
{
    public class MvcApplication : System.Web.HttpApplication
    {
        protected void Application_Start()
        {
            AreaRegistration.RegisterAllAreas();
            FilterConfig.RegisterGlobalFilters(GlobalFilters.Filters);
            RouteConfig.RegisterRoutes(RouteTable.Routes);
            BundleConfig.RegisterBundles(BundleTable.Bundles);
        }
        protected void Application_AcquireRequestState(object sender, EventArgs e)
        {
            // 1. Check if the current request has access to Session
            if (HttpContext.Current.Session != null)
            {
                // 2. Check if User is "Logged In" (Cookie exists) 
                //    BUT Session["Role"] is empty (Server forgot them)
                if (Request.IsAuthenticated && Session["Role"] == null)
                {
                    // 3. Force Logout
                    System.Web.Security.FormsAuthentication.SignOut();
                    Session.Abandon();

                    // 4. Redirect to Login Page
                    Response.Redirect("~/Account/Login");
                }
            }
        }
    }
}
