using System;
using System.ComponentModel.DataAnnotations;
using System.Web; // Required for HttpPostedFileBase

namespace EmployeeAppMVC.Models
{
    public class Employee
    {
        public int EmpID { get; set; }
        public string Name { get; set; }

        public string fatherName { get; set; } // Matches image column name

        public string Gender { get; set; }

        [DataType(DataType.Date)]
        [DisplayFormat(DataFormatString = "{0:dd-MM-yyyy}", ApplyFormatInEditMode = true)]
        public DateTime? DOB { get; set; }

        public string Address { get; set; }

        public string Department { get; set; }

        public string Mobile { get; set; }

        public string Email { get; set; }

        public string Aadhaar { get; set; }

        [Display(Name = "Joining Date")]
        [DataType(DataType.Date)]
        [DisplayFormat(DataFormatString = "{0:dd-MM-yyyy}", ApplyFormatInEditMode = true)]
        public DateTime? JoiningDate { get; set; }

        // Stores the string path in Database (e.g., "~/Images/pic.jpg")
        public string ImagePath { get; set; }

        // Used for the actual file upload mechanism (Not stored in DB)
        public HttpPostedFileBase ImageUpload { get; set; }
    }
}