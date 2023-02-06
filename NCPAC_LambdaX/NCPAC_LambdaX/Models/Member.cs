using NCPAC_LambdaX.Models;
using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Diagnostics.Metrics;
using System.Linq;
using System.Threading.Tasks;
using System.Xml.Linq;

namespace NCPAC_LambdaX.Models
{
    public class Member : IValidatableObject
    {
        public int ID { get; set; }

        [Display(Name = "Full Name")]
        public string MemberName
        {
            get
            {
                string? mn = MiddleName;
                if (MiddleName == null)
                {
                    mn = "";
                }
                return Salutation
                    + " " + FirstName + " " + mn + " " + LastName;
            }
        }

        [Display(Name = "Home Address")]
        public string? HomeAddress
        {
            get
            {
                string? wc = City;
                string? wsa = StreetAddress;
                string? pc = PostalCodeFormatted;
                string? p = ProvinceID;
                if (WorkCity == null)
                {
                    wc = "";
                }
                if (WorkStreetAddress == null)
                {
                    wsa = "";
                }
                if (WorkPostalCodeFormatted == null)
                {
                    pc = "";
                }
                if (WorkProvinceID == null)
                {
                    p = "";
                }
                return wsa
                    + " " + wc + " " + p + " " + pc;

            }
        }

        [Display(Name = "Work Address")]
        public string? WorkAddress
        {
            get
            {
                string? wc = WorkCity;
                string? wsa = WorkStreetAddress;
                string? pc = WorkPostalCodeFormatted;
                string? p = WorkProvinceID;
                if (WorkCity == null)
                {
                    wc = "";
                }
                if (WorkStreetAddress == null)
                {
                    wsa = "";
                }
                if (WorkPostalCodeFormatted == null)
                {
                    pc = "";
                }
                if (WorkProvinceID == null)
                {
                    p = "";
                }
                return wsa
                    + " " + wc + " " + p + " " + pc;

            }
        }

        [Display(Name = "Home Phone")]
        public string? PhoneFormatted
        {
            get
            {
                return "(" + Phone.Substring(0, 3) + ") " + Phone.Substring(3, 3) + "-" + Phone[6..];
            }
        }

        [Display(Name = "Work Phone")]
        public string? WorkPhoneFormatted
        {
            get
            {
                if (WorkPhone != null)
                {

                    return "(" + WorkPhone.Substring(0, 3) + ") " + WorkPhone.Substring(3, 3) + "-" + WorkPhone[6..];

                }
                return "";

            }
        }


        [Display(Name = "Work Postal Code")]
        public string? WorkPostalCodeFormatted
        {
            get
            {
                if (WorkPostalCode != null)
                {

                    return WorkPostalCode.Substring(0, 3) + " " + WorkPostalCode[3..];

                }
                return "";

            }
        }

        [Display(Name = "Postal Code")]
        public string? PostalCodeFormatted
        {
            get
            {
                if (PostalCode != null)
                {

                    return PostalCode.Substring(0, 3) + " " + PostalCode[3..];

                }
                return "";

            }
        }

        [Display(Name = "First Name")]
        [Required(ErrorMessage = "You cannot leave the first name empty.")]
        [StringLength(30, ErrorMessage = "First name cannot be more than 30 characters long.")]
        public string FirstName { get; set; }

        [Display(Name = "Middle Name")]
        [StringLength(30, ErrorMessage = "Middle name cannot be more than 30 characters long.")]
        public string? MiddleName { get; set; }

        [Display(Name = "Last Name")]
        [Required(ErrorMessage = "You cannot leave the last name empty.")]
        [StringLength(50, ErrorMessage = "Last name cannot be more than 50 characters long.")]
        public string LastName { get; set; }

        [Display(Name = "Salutation")]
        public string? Salutation { get; set; }

        [Display(Name = "Street Adress")]
        [StringLength(200, ErrorMessage = "Street Adress cannot be more than 200 characters long.")]
        public string? StreetAddress { get; set; }

        [Display(Name = "City")]
        [StringLength(100, ErrorMessage = "City cannot be more than 100 characters long.")]
        public string? City { get; set; }

        [Required(ErrorMessage = "Province is required.")]
        [Display(Name = "Province")]
        public string? ProvinceID { get; set; }
        public Province? Province { get; set; }


        [DataType(DataType.PostalCode)]
        [Display(Name = "Postal Code")]
        [RegularExpression(@"^[ABCEGHJ-NPRSTVXY]{1}[0-9]{1}[ABCEGHJ-NPRSTV-Z]{1}[0-9]{1}[ABCEGHJ-NPRSTV-Z]{1}[0-9]{1}$", ErrorMessage = "Invalid Postal Code.")]
        public string? PostalCode { get; set; }

        [Required(ErrorMessage = "Phone number is required.")]
        [RegularExpression("^\\d{10}$", ErrorMessage = "Please enter a valid 10-digit phone number (no spaces).")]
        [DataType(DataType.PhoneNumber)]
        [StringLength(10)]
        public string? Phone { get; set; }

        [EmailAddress(ErrorMessage = "Please enter a valid email address.")]
        [DataType(DataType.EmailAddress)]
        [Display(Name = "Personal Email")]
        public string? Email { get; set; }

        [Display(Name = "Work Street Adress")]
        [StringLength(200, ErrorMessage = "Street Adress cannot be more than 200 characters long.")]
        public string? WorkStreetAddress { get; set; }

        [Display(Name = "Work City")]
        [StringLength(100, ErrorMessage = "City cannot be more than 100 characters long.")]
        public string? WorkCity { get; set; }

        [Display(Name = "Work Province")]
        public string? WorkProvinceID { get; set; }
        public Province? WorkProvince { get; set; }

        [StringLength(6, ErrorMessage = "Please enter a valid 6 character Postal code with no spaces.")]
        [DataType(DataType.PostalCode)]
        [RegularExpression(@"^[ABCEGHJ-NPRSTVXY]{1}[0-9]{1}[ABCEGHJ-NPRSTV-Z]{1}[0-9]{1}[ABCEGHJ-NPRSTV-Z]{1}[0-9]{1}$", ErrorMessage = "Invalid Postal Code.")]
        [Display(Name = "Work Postal Code")]
        public string? WorkPostalCode { get; set; }

        [RegularExpression("^\\d{10}$", ErrorMessage = "Please enter a valid 10-digit phone number (no spaces).")]
        [DataType(DataType.PhoneNumber)]
        [StringLength(10)]
        [Display(Name = "Work Phone")]
        public string? WorkPhone { get; set; }

        [RegularExpression(@"^[a-zA-Z0-9.!#$%&'*+/=?^_`{|}~-]+@[a-zA-Z0-9-]+(?:\.[a-zA-Z0-9-]+)*$", ErrorMessage = "Invalid Email Address.")]
        [DataType(DataType.EmailAddress)]
        [Display(Name = "Work Email")]
        public string? WorkEmail { get; set; }

        [Display(Name = "Preffered Email")]
        public string? MailPrefferenceID { get; set; }
        public MailPrefference? MailPrefference { get; set; }

        [Display(Name = "Educational Summary")]
        public string? EducationalSummary { get; set; }

        [Display(Name = "Is a Niagara College Graduate")]
        public bool IsNCGrad { get; set; }

        [Display(Name = "Occupational Summary")]
        public string? OccupationalSummary { get; set; }

        [DataType(DataType.Date)]
        [DisplayFormat(DataFormatString = "{0:yyyy-MM-dd}", ApplyFormatInEditMode = true)]
        [Display(Name = "Date Joined")]
        public DateTime? DateJoined { get; set; }

        [Display(Name = "Is Active")]
        public bool IsActive { get; set; }

        [Display(Name = "Commitees")]
        public ICollection<MemberCommitee> MemberCommitees { get; set; } = new HashSet<MemberCommitee>();

        public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
        {
            if (DateJoined > DateTime.Today)
            {
                yield return new ValidationResult("Date Joined cannot be in the future.", new[] { "DateJoined" });
            }
        }
    }
}
