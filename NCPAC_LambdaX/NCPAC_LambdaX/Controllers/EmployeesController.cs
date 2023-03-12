﻿using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using NCPAC_LambdaX.Data;
using NCPAC_LambdaX.Models;
using NCPAC_LambdaX.Utilities;
using NCPAC_LambdaX.ViewModels;
using OfficeOpenXml.Style;
using OfficeOpenXml;

namespace NCPAC_LambdaX.Controllers
{
    [Authorize(Roles = "Admin")]
    public class EmployeesController : Controller
    {
        private readonly NCPACContext _context;
        private readonly ApplicationDbContext _identityContext;
        private readonly IMyEmailSender _emailSender;
        private readonly UserManager<IdentityUser> _userManager;

        public EmployeesController(NCPACContext context,
            ApplicationDbContext identityContext, IMyEmailSender emailSender,
            UserManager<IdentityUser> userManager)
        {
            _context = context;
            _identityContext = identityContext;
            _emailSender = emailSender;
            _userManager = userManager;
        }

        // GET: Employees
        public async Task<IActionResult> Index()
        {
            var employees = await _context.Employees
                .Select(e => new EmployeeAdminVM
                {
                    Email = e.Email,
                    Active = e.Active,
                    ID = e.ID,
                    FirstName = e.FirstName,
                    LastName = e.LastName,
                    Phone = e.Phone
                }).ToListAsync();

            foreach (var e in employees)
            {
                var user = await _userManager.FindByEmailAsync(e.Email);
                if (user != null)
                {
                    e.UserRoles = (List<string>)await _userManager.GetRolesAsync(user);
                }
            };
            return View(employees);
        }

        // GET: Employee/Create
        public IActionResult Create()
        {
            EmployeeAdminVM employee = new EmployeeAdminVM();
            PopulateAssignedRoleData(employee);
            return View(employee);
        }

        // POST: Employee/Create
        // To protect from overposting attacks, enable the specific properties you want to bind to, for 
        // more details, see http://go.microsoft.com/fwlink/?LinkId=317598.
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create([Bind("FirstName,LastName,Phone," +
            "Email")] Employee employee, string[] selectedRoles)
        {

            try
            {
                if (ModelState.IsValid)
                {
                    _context.Add(employee);
                    await _context.SaveChangesAsync();

                    InsertIdentityUser(employee.Email, selectedRoles);

                    //Send Email to new Employee - commented out till email configured
                    await InviteUserToResetPassword(employee, null);

                    return RedirectToAction(nameof(Index));
                }
            }
            catch (DbUpdateException dex)
            {
                if (dex.GetBaseException().Message.Contains("UNIQUE constraint failed"))
                {
                    ModelState.AddModelError("Email", "Unable to save changes. Remember, you cannot have duplicate Email addresses.");
                }
                else
                {
                    ModelState.AddModelError("", "Unable to save changes. Try again, and if the problem persists see your system administrator.");
                }
            }
            //We are here because something went wrong and need to redisplay
            EmployeeAdminVM employeeAdminVM = new EmployeeAdminVM
            {
                Email = employee.Email,
                Active = employee.Active,
                ID = employee.ID,
                FirstName = employee.FirstName,
                LastName = employee.LastName,
                Phone = employee.Phone
            };
            foreach (var role in selectedRoles)
            {
                employeeAdminVM.UserRoles.Add(role);
            }
            PopulateAssignedRoleData(employeeAdminVM);
            return View(employeeAdminVM);
        }

        // GET: Employees/Edit/5
        public async Task<IActionResult> Edit(int? id)
        {
            if (id == null)
            {
                return NotFound();
            }

            var employee = await _context.Employees
                .Where(e => e.ID == id)
                .Select(e => new EmployeeAdminVM
                {
                    Email = e.Email,
                    Active = e.Active,
                    ID = e.ID,
                    FirstName = e.FirstName,
                    LastName = e.LastName,
                    Phone = e.Phone
                }).FirstOrDefaultAsync();

            if (employee == null)
            {
                return NotFound();
            }

            //Get the user from the Identity system
            var user = await _userManager.FindByEmailAsync(employee.Email);
            if (user != null)
            {
                //Add the current roles
                var r = await _userManager.GetRolesAsync(user);
                employee.UserRoles = (List<string>)r;
            }
            PopulateAssignedRoleData(employee);

            return View(employee);
        }

        // POST: Employees/Edit/5
        // To protect from overposting attacks, enable the specific properties you want to bind to, for 
        // more details, see http://go.microsoft.com/fwlink/?LinkId=317598.
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(int id, bool Active, string[] selectedRoles)
        {
            var employeeToUpdate = await _context.Employees
                .FirstOrDefaultAsync(m => m.ID == id);
            if (employeeToUpdate == null)
            {
                return NotFound();
            }

            //Note the current Email and Active Status
            bool ActiveStatus = employeeToUpdate.Active;
            string databaseEmail = employeeToUpdate.Email;


            if (await TryUpdateModelAsync<Employee>(employeeToUpdate, "",
                e => e.FirstName, e => e.LastName, e => e.Phone, e => e.Email, e => e.Active))
            {
                try
                {
                    await _context.SaveChangesAsync();
                    //Save successful so go on to related changes

                    //Check for changes in the Active state
                    if (employeeToUpdate.Active == false && ActiveStatus == true)
                    {
                        //Deactivating them so delete the IdentityUser
                        //This deletes the user's login from the security system
                        await DeleteIdentityUser(employeeToUpdate.Email);

                    }
                    else if (employeeToUpdate.Active == true && ActiveStatus == false)
                    {
                        //You reactivating the user, create them and
                        //give them the selected roles
                        InsertIdentityUser(employeeToUpdate.Email, selectedRoles);
                    }
                    else if (employeeToUpdate.Active == true && ActiveStatus == true)
                    {
                        //No change to Active status so check for a change in Email
                        //If you Changed the email, Delete the old login and create a new one
                        //with the selected roles
                        if (employeeToUpdate.Email != databaseEmail)
                        {
                            //Add the new login with the selected roles
                            InsertIdentityUser(employeeToUpdate.Email, selectedRoles);

                            //This deletes the user's old login from the security system
                            await DeleteIdentityUser(databaseEmail);
                        }
                        else
                        {
                            //Finially, Still Active and no change to Email so just Update
                            await UpdateUserRoles(selectedRoles, employeeToUpdate.Email);
                        }
                    }

                    return RedirectToAction(nameof(Index));
                }
                catch (DbUpdateConcurrencyException)
                {
                    if (!EmployeeExists(employeeToUpdate.ID))
                    {
                        return NotFound();
                    }
                    else
                    {
                        throw;
                    }
                }
                catch (DbUpdateException dex)
                {
                    if (dex.GetBaseException().Message.Contains("UNIQUE constraint failed"))
                    {
                        ModelState.AddModelError("Email", "Unable to save changes. Remember, you cannot have duplicate Email addresses.");
                    }
                    else
                    {
                        ModelState.AddModelError("", "Unable to save changes. Try again, and if the problem persists see your system administrator.");
                    }
                }
            }
            //We are here because something went wrong and need to redisplay
            EmployeeAdminVM employeeAdminVM = new EmployeeAdminVM
            {
                Email = employeeToUpdate.Email,
                Active = employeeToUpdate.Active,
                ID = employeeToUpdate.ID,
                FirstName = employeeToUpdate.FirstName,
                LastName = employeeToUpdate.LastName,
                Phone = employeeToUpdate.Phone
            };
            foreach (var role in selectedRoles)
            {
                employeeAdminVM.UserRoles.Add(role);
            }
            PopulateAssignedRoleData(employeeAdminVM);
            return View(employeeAdminVM);
        }

        private void PopulateAssignedRoleData(EmployeeAdminVM employee)
        {//Prepare checkboxes for all Roles
            var allRoles = _identityContext.Roles;
            var currentRoles = employee.UserRoles;
            var viewModel = new List<RoleVM>();
            foreach (var r in allRoles)
            {
                viewModel.Add(new RoleVM
                {
                    RoleId = r.Id,
                    RoleName = r.Name,
                    Assigned = currentRoles.Contains(r.Name)
                });
            }
            ViewBag.Roles = viewModel;
        }

        private async Task UpdateUserRoles(string[] selectedRoles, string Email)
        {
            var _user = await _userManager.FindByEmailAsync(Email);//IdentityUser
            if (_user != null)
            {
                var UserRoles = (List<string>)await _userManager.GetRolesAsync(_user);//Current roles user is in

                if (selectedRoles == null)
                {
                    //No roles selected so just remove any currently assigned
                    foreach (var r in UserRoles)
                    {
                        await _userManager.RemoveFromRoleAsync(_user, r);
                    }
                }
                else
                {
                    //At least one role checked so loop through all the roles
                    //and add or remove as required

                    //We need to do this next line because foreach loops don't always work well
                    //for data returned by EF when working async.  Pulling it into an IList<>
                    //first means we can safely loop over the colleciton making async calls and avoid
                    //the error 'New transaction is not allowed because there are other threads running in the session'
                    IList<IdentityRole> allRoles = _identityContext.Roles.ToList<IdentityRole>();

                    foreach (var r in allRoles)
                    {
                        if (selectedRoles.Contains(r.Name))
                        {
                            if (!UserRoles.Contains(r.Name))
                            {
                                await _userManager.AddToRoleAsync(_user, r.Name);
                            }
                        }
                        else
                        {
                            if (UserRoles.Contains(r.Name))
                            {
                                await _userManager.RemoveFromRoleAsync(_user, r.Name);
                            }
                        }
                    }
                }
            }
        }

        private void InsertIdentityUser(string Email, string[] selectedRoles)
        {
            //Create the IdentityUser in the IdentitySystem
            //Note: this is similar to what we did in ApplicationSeedData
            if (_userManager.FindByEmailAsync(Email).Result == null)
            {
                IdentityUser user = new IdentityUser
                {
                    UserName = Email,
                    Email = Email,
                    EmailConfirmed = true //since we are creating it!
                };
                //Create a random password with a default 8 characters
                string password = MakePassword.Generate();
                IdentityResult result = _userManager.CreateAsync(user, password).Result;

                if (result.Succeeded)
                {
                    foreach (string role in selectedRoles)
                    {
                        _userManager.AddToRoleAsync(user, role).Wait();
                    }
                }
            }
            else
            {
                TempData["message"] = "The Login Account for " + Email + " was already in the system.";
            }
        }

        private async Task DeleteIdentityUser(string Email)
        {
            var userToDelete = await _identityContext.Users.Where(u => u.Email == Email).FirstOrDefaultAsync();
            if (userToDelete != null)
            {
                _identityContext.Users.Remove(userToDelete);
                await _identityContext.SaveChangesAsync();
            }
        }

        private async Task InviteUserToResetPassword(Employee employee, string message)
        {
            message ??= "<!DOCTYPE html> " + "<html xmlns:v='urn:schemas-microsoft-com:vml' xmlns:o='urn:schemas-microsoft-com:office:office' lang='en'> " + "<head> " + " <title></title> " + " <meta http-equiv='Content-Type' content='text/html; charset=utf-8'> " + " <meta name='viewport' content='width=device-width, initial-scale=1.0'> " + "  " + "  " + " <link href='https://fonts.googleapis.com/css?family=Roboto+Slab' rel='stylesheet' type='text/css'> " + "  " + " <style> " + " * { " + " box-sizing: border-box; " + " } " + " body { " + " margin: 0; " + " padding: 0; " + " } " + " a[x-apple-data-detectors] { " + " color: inherit !important; " + " text-decoration: inherit !important; " + " } " + " #MessageViewBody a { " + " color: inherit; " + " text-decoration: none; " + " } " + " p { " + " line-height: inherit " + " } " + " .desktop_hide, " + " .desktop_hide table { " + " mso-hide: all; " + " display: none; " + " max-height: 0px; " + " overflow: hidden; " + " } " + " @media (max-width:670px) { " + " .desktop_hide table.icons-inner { " + " display: inline-block !important; " + " } " + " .icons-inner { " + " text-align: center; " + " } " + " .icons-inner td { " + " margin: 0 auto; " + " } " + " .image_block img.big, " + " .row-content { " + " width: 100% !important; " + " } " + " .mobile_hide { " + " display: none; " + " } " + " .stack .column { " + " width: 100%; " + " display: block; " + " } " + " .mobile_hide { " + " min-height: 0; " + " max-height: 0; " + " max-width: 0; " + " overflow: hidden; " + " font-size: 0px; " + " } " + " .desktop_hide, " + " .desktop_hide table { " + " display: table !important; " + " max-height: none !important; " + " } " + " } " + " </style> " + "</head> " + "<body style='background-color: #85a4cd; margin: 0; padding: 0; -webkit-text-size-adjust: none; text-size-adjust: none;'> " + " <table class='nl-container' width='100%' border='0' cellpadding='0' cellspacing='0' role='presentation' style='mso-table-lspace: 0pt; mso-table-rspace: 0pt; background-color: #85a4cd;'> " + " <tbody> " + " <tr> " + " <td> " + " <table class='row row-1' align='center' width='100%' border='0' cellpadding='0' cellspacing='0' role='presentation' style='mso-table-lspace: 0pt; mso-table-rspace: 0pt; background-color: #f3f6fe;'> " + " <tbody> " + " <tr> " + " <td> " + " <table class='row-content stack' align='center' border='0' cellpadding='0' cellspacing='0' role='presentation' style='mso-table-lspace: 0pt; mso-table-rspace: 0pt; color: #000000; width: 650px;' width='650'> " + " <tbody> " + " <tr> " + " <td class='column column-1' width='100%' style='mso-table-lspace: 0pt; mso-table-rspace: 0pt; font-weight: 400; text-align: left; vertical-align: top; padding-top: 15px; padding-bottom: 15px; border-top: 0px; border-right: 0px; border-bottom: 0px; border-left: 0px;'> " + " <table class='image_block block-1' width='100%' border='0' cellpadding='0' cellspacing='0' role='presentation' style='mso-table-lspace: 0pt; mso-table-rspace: 0pt;'> " + " <tr> " + " <td class='pad' style='width:100%;padding-right:0px;padding-left:0px;'> " + " <div class='alignment' align='center' style='line-height:10px'><img src='https://upload.wikimedia.org/wikipedia/commons/thumb/1/12/Niagara-college_vectorized.svg/1200px-Niagara-college_vectorized.svg.png' style='display: block; height: auto; border: 0; width: 163px; max-width: 100%;' width='163' alt='Your Logo' title='Your Logo'></div> " + " </td> " + " </tr> " + " </table> " + " </td> " + " </tr> " + " </tbody> " + " </table> " + " </td> " + " </tr> " + " </tbody> " + " </table> " + " <table class='row row-2' align='center' width='100%' border='0' cellpadding='0' cellspacing='0' role='presentation' style='mso-table-lspace: 0pt; mso-table-rspace: 0pt;'> " + " <tbody> " + " <tr> " + " <td> " + " <table class='row-content stack' align='center' border='0' cellpadding='0' cellspacing='0' role='presentation' style='mso-table-lspace: 0pt; mso-table-rspace: 0pt; color: #000000; width: 650px;' width='650'> " + " <tbody> " + " <tr> " + " <td class='column column-1' width='100%' style='mso-table-lspace: 0pt; mso-table-rspace: 0pt; font-weight: 400; text-align: left; vertical-align: top; padding-top: 5px; padding-bottom: 5px; border-top: 0px; border-right: 0px; border-bottom: 0px; border-left: 0px;'> " + " <table class='heading_block block-2' width='100%' border='0' cellpadding='0' cellspacing='0' role='presentation' style='mso-table-lspace: 0pt; mso-table-rspace: 0pt;'> " + " <tr> " + " <td class='pad' style='padding-bottom:10px;text-align:center;width:100%;padding-top:60px;'> " + " <h1 style='margin: 0; color: #ffffff; direction: ltr; font-family: 'Roboto Slab', Arial, 'Helvetica Neue', Helvetica, sans-serif; font-size: 30px; font-weight: normal; letter-spacing: 2px; line-height: 120%; text-align: center; margin-top: 0; margin-bottom: 0;'><strong>Create a New Password</strong></h1> " + " </td> " + " </tr> " + " </table> " + " <table class='image_block block-3' width='100%' border='0' cellpadding='0' cellspacing='0' role='presentation' style='mso-table-lspace: 0pt; mso-table-rspace: 0pt;'> " + " <tr> " + " <td class='pad' style='width:100%;padding-right:0px;padding-left:0px;'> " + " <div class='alignment' align='center' style='line-height:10px'><img class='big' src='https://d1oco4z2z1fhwp.cloudfront.net/templates/default/3856/GIF_password.gif' style='display: block; height: auto; border: 0; width: 500px; max-width: 100%;' width='500' alt='Wrong Password Animation' title='Wrong Password Animation'></div> " + " </td> " + " </tr> " + " </table> " + " <table class='text_block block-5' width='100%' border='0' cellpadding='0' cellspacing='0' role='presentation' style='mso-table-lspace: 0pt; mso-table-rspace: 0pt; word-break: break-word;'> " + " <tr> " + " <td class='pad' style='padding-bottom:5px;padding-left:10px;padding-right:10px;padding-top:25px;'> " + " <div style='font-family: sans-serif'> " + " <div class style='font-size: 14px; font-family: Roboto Slab, Arial, Helvetica Neue, Helvetica, sans-serif; mso-line-height-alt: 16.8px; color: #3f4d75; line-height: 1.2;'> " + " <p style='margin: 0; font-size: 14px; text-align: center; mso-line-height-alt: 16.8px;'><span style='font-size:20px;'>Your Employee account for Niagara College Policy Management Committee System was Created by your Admin!</span></p> " + " </div> " + " </div> " + " </td> " + " </tr> " + " </table> " + " <table class='text_block block-6' width='100%' border='0' cellpadding='0' cellspacing='0' role='presentation' style='mso-table-lspace: 0pt; mso-table-rspace: 0pt; word-break: break-word;'> " + " <tr> " + " <td class='pad' style='padding-bottom:5px;padding-left:10px;padding-right:10px;padding-top:5px;'> " + " <div style='font-family: sans-serif'> " + " <div class style='font-size: 14px; font-family: Roboto Slab, Arial, Helvetica Neue, Helvetica, sans-serif; mso-line-height-alt: 16.8px; color: #3f4d75; line-height: 1.2;'> " + " <p style='margin: 0; font-size: 14px; text-align: center; mso-line-height-alt: 16.8px;'><span style='font-size:22px;'>Use " + employee.Email + " as your email and reset your password.</span></p> " + " </div> " + " </div> " + " </td> " + " </tr> " + " </table> " + " <table class='button_block block-8' width='100%' border='0' cellpadding='0' cellspacing='0' role='presentation' style='mso-table-lspace: 0pt; mso-table-rspace: 0pt;'> " + " <tr> " + " <td class='pad' style='padding-bottom:10px;padding-left:10px;padding-right:10px;padding-top:30px;text-align:center;'> " + " <div class='alignment' align='center'> " + " <a href='https://ncpacproto2.azurewebsites.net/Identity/Account/ForgotPassword' target='_blank' style='text-decoration:none;display:inline-block;color:#3f4d75;background-color:#ffffff;border-radius:10px;width:auto;border-top:2px solid #3F4D75;font-weight:undefined;border-right:2px solid #3F4D75;border-bottom:2px solid #3F4D75;border-left:2px solid #3F4D75;padding-top:10px;padding-bottom:10px;font-family:Roboto Slab, Arial, Helvetica Neue, Helvetica, sans-serif;font-size:18px;text-align:center;mso-border-alt:none;word-break:keep-all;'><span style='padding-left:25px;padding-right:25px;font-size:18px;display:inline-block;letter-spacing:normal;'><span dir='ltr' style='word-break: break-word;'><span style='line-height: 36px;' dir='ltr' data-mce-style>CREATE MY PASSWORD</span></span></span></a> " + "  " + " </div> " + " </td> " + " </tr> " + " </table> " + " <table class='text_block block-10' width='100%' border='0' cellpadding='0' cellspacing='0' role='presentation' style='mso-table-lspace: 0pt; mso-table-rspace: 0pt; word-break: break-word;'> " + " <tr> " + " <td class='pad' style='padding-bottom:40px;padding-left:10px;padding-right:10px;padding-top:30px;'> " + " <div style='font-family: sans-serif'> " + " <div class style='font-size: 14px; font-family: Roboto Slab, Arial, Helvetica Neue, Helvetica, sans-serif; mso-line-height-alt: 16.8px; color: #3f4d75; line-height: 1.2;'> " + " <p style='margin: 0; font-size: 14px; text-align: center; mso-line-height-alt: 16.8px;'><span style='font-size:14px;'>Please click the button above to start resetting your password.</span></p> " + " </div> " + " </div> " + " </td> " + " </tr> " + " </table> " + " </td> " + " </tr> " + " </tbody> " + " </table> " + " </td> " + " </tr> " + " </tbody> " + " </table> " + " <table class='row row-3' align='center' width='100%' border='0' cellpadding='0' cellspacing='0' role='presentation' style='mso-table-lspace: 0pt; mso-table-rspace: 0pt; background-color: #c4d6ec;'> " + " <tbody> " + " <tr> " + " <td> " + " <table class='row-content stack' align='center' border='0' cellpadding='0' cellspacing='0' role='presentation' style='mso-table-lspace: 0pt; mso-table-rspace: 0pt; color: #000000; width: 650px;' width='650'> " + " <tbody> " + " <tr> " + " <td class='column column-1' width='100%' style='mso-table-lspace: 0pt; mso-table-rspace: 0pt; font-weight: 400; text-align: left; vertical-align: top; padding-top: 20px; padding-bottom: 20px; border-top: 0px; border-right: 0px; border-bottom: 0px; border-left: 0px;'> " + " <table class='text_block block-1' width='100%' border='0' cellpadding='10' cellspacing='0' role='presentation' style='mso-table-lspace: 0pt; mso-table-rspace: 0pt; word-break: break-word;'> " + " <tr> " + " <td class='pad'> " + " <div style='font-family: sans-serif'> " + " <div class style='font-size: 14px; font-family: Roboto Slab, Arial, Helvetica Neue, Helvetica, sans-serif; mso-line-height-alt: 16.8px; color: #3f4d75; line-height: 1.2;'> " + " <p style='margin: 0; font-size: 14px; text-align: center; mso-line-height-alt: 16.8px;'><span style='font-size:12px;'>This email was originated from the NCPAC automated mailing system</span></p> " + " </div> " + " </div> " + " </td> " + " </tr> " + " </table> " + " </td> " + " </tr> " + " </tbody> " + " </table> " + " </td> " + " </tr> " + " </tbody> " + " </table> " + " <table class='row row-4' align='center' width='100%' border='0' cellpadding='0' cellspacing='0' role='presentation' style='mso-table-lspace: 0pt; mso-table-rspace: 0pt; background-color: #f3f6fe;'> " + " <tbody> " + " <tr> " + " <td> " + " <table class='row-content stack' align='center' border='0' cellpadding='0' cellspacing='0' role='presentation' style='mso-table-lspace: 0pt; mso-table-rspace: 0pt; color: #000000; width: 650px;' width='650'> " + " <tbody> " + " <tr> " + " <td class='column column-1' width='100%' style='mso-table-lspace: 0pt; mso-table-rspace: 0pt; font-weight: 400; text-align: left; vertical-align: top; padding-top: 0px; padding-bottom: 0px; border-top: 0px; border-right: 0px; border-bottom: 0px; border-left: 0px;'> " + " <div class='spacer_block' style='height:70px;line-height:30px;font-size:1px;'> </div> " + " </td> " + " </tr> " + " </tbody> " + " </table> " + " </td> " + " </tr> " + " </tbody> " + " </table> " + " <table class='row row-5' align='center' width='100%' border='0' cellpadding='0' cellspacing='0' role='presentation' style='mso-table-lspace: 0pt; mso-table-rspace: 0pt;'> " + " <tbody> " + " <tr> " + " <td> " + " <table class='row-content stack' align='center' border='0' cellpadding='0' cellspacing='0' role='presentation' style='mso-table-lspace: 0pt; mso-table-rspace: 0pt; color: #000000; width: 650px;' width='650'> " + " <tbody> " + " <tr> " + " <td class='column column-1' width='100%' style='mso-table-lspace: 0pt; mso-table-rspace: 0pt; font-weight: 400; text-align: left; vertical-align: top; padding-top: 5px; padding-bottom: 5px; border-top: 0px; border-right: 0px; border-bottom: 0px; border-left: 0px;'> " + " <table class='icons_block block-1' width='100%' border='0' cellpadding='0' cellspacing='0' role='presentation' style='mso-table-lspace: 0pt; mso-table-rspace: 0pt;'> " + " <tr> " + " <td class='pad' style='vertical-align: middle; color: #9d9d9d; font-family: inherit; font-size: 15px; padding-bottom: 5px; padding-top: 5px; text-align: center;'> " + " <table width='100%' cellpadding='0' cellspacing='0' role='presentation' style='mso-table-lspace: 0pt; mso-table-rspace: 0pt;'> " + " <tr> " + " <td class='alignment' style='vertical-align: middle; text-align: center;'> " + "  " + "  " + " </td> " + " </tr> " + " </table> " + " </td> " + " </tr> " + " </table> " + " </td> " + " </tr> " + " </tbody> " + " </table> " + " </td> " + " </tr> " + " </tbody> " + " </table> " + " </td> " + " </tr> " + " </tbody> " + " </table> " + "</body> " + "</html>";
            if (employee.Email.Contains("niagaracollege.ca"))
            {
                message = message = "<div><div class='R1UVb' style='height: 170px; width: 100%;'><div class='qF8_5'><button type='button' class='ms-Button ms-Button--icon wD8TJ root-573' title='Show original size' aria-label='Show original size' data-is-focusable='true'><span class='ms-Button-flexContainer flexContainer-164' data-automationid='splitbuttonprimary'><i data-icon-name='FullScreen' aria-hidden='true' class='ms-Icon root-90 css-308 ms-Button-icon icon-284' style='font-family: controlIcons;'></i></span></button></div><table border='0' width='100%' height='100%' cellpadding='0' cellspacing='0' bgcolor='rgb(51, 51, 51)' style='border-spacing: 0px; font-family: Helvetica, Arial, sans-serif; background-color: rgb(51, 51, 51) !important; transform: scale(0.425, 0.425); transform-origin: left top;' data-ogsb='' data-ogab='#FFFFFF' min-scale='0.425'><tbody><tr><td align='center' valign='top' bgcolor='rgb(51, 51, 51)' style='background-color: rgb(51, 51, 51) !important; border-collapse: collapse;' data-ogsb='rgb(255, 255, 255)' data-ogab='#FFFFFF'><table width='600' cellpadding='0' cellspacing='0' border='0' class='x_container' bgcolor='rgb(51, 51, 51)' style='border-spacing: 0px; font-family: Helvetica, Arial, sans-serif; color: rgb(182, 182, 182) !important; max-width: 600px; background-color: rgb(51, 51, 51) !important;' data-ogsc='rgb(107, 107, 107)' data-ogsb='' data-ogab='#FFFFFF'><tbody><tr><td class='x_logo' style='border-collapse:collapse; text-align:center; padding-top:20px; padding-bottom:20px; padding-right:0; padding-left:0'><a href='https://www.niagaracollege.ca/' target='_blank' rel='noopener noreferrer' data-auth='NotApplicable' style='text-decoration: none; color: rgb(255, 149, 123) !important;' data-safelink='true' data-ogsc='' data-linkindex='0'><img data-imagetype='External' blockedimagesrc='https://upload.wikimedia.org/wikipedia/commons/thumb/1/12/Niagara-college_vectorized.svg/1200px-Niagara-college_vectorized.svg.png' width='90' height='30' border='0' alt='Niagara College' style='width:100%; max-width:90px; border-width:0'> </a></td></tr><tr><td class='x_container-padding x_content' bgcolor='rgb(51, 51, 51)' style='background-color: rgb(51, 51, 51) !important; padding-top: 20px; padding-bottom: 20px; border-collapse: collapse;' data-ogsb='rgb(255, 255, 255)' data-ogab='#FFFFFF'><h2 data-ogsc='' style='color: rgb(234, 234, 234) !important;'>Hi, Your NCPAC Staff Account has been created.</h2><p data-ogsc='' style='color: rgb(182, 182, 182) !important;'>Please click on the button below and go through our reset password option inorder to create your new password. Then you will be able to login to the your PAC account. Do not forget to use " + employee.Email + " as your email in Login and password reset</p><br aria-hidden='true'><p class='x_purple-cta-btn' style='font-size: 16px; line-height: 1.5; margin: 0px 0px 25px; color: rgb(182, 182, 182) !important;' data-ogsc='rgb(107, 107, 107)'><a href='https://ncpacproto2.azurewebsites.net/Identity/Account/ForgotPassword' target='_blank' rel='noopener noreferrer' data-auth='NotApplicable' class='x_button-purple' data-reset-password-link='https://ncpacproto2.azurewebsites.net/Identity/Account/ForgotPassword' style='background-color: rgb(51, 51, 51) !important; border-radius: 30px; border-width: 1px; border-style: solid; border-color: rgb(68, 31, 75); color: rgb(255, 209, 255) !important; display: inline-block; font-size: 16px; font-weight: bold; line-height: 20px; min-height: 20px; padding: 10px 30px; text-decoration: none !important;' data-safelink='true' data-ogsc='rgb(68, 31, 75)' data-ogsb='rgb(255, 255, 255)' data-linkindex='1'>Create Password </a></p></td></tr></tbody></table></td></tr></tbody></table></div><div class='R1UVb' style='height: 40px; width: 100%;'><div class='qF8_5'><button type='button' class='ms-Button ms-Button--icon wD8TJ root-573' title='Show original size' aria-label='Show original size' data-is-focusable='true'><span class='ms-Button-flexContainer flexContainer-164' data-automationid='splitbuttonprimary'><i data-icon-name='FullScreen' aria-hidden='true' class='ms-Icon root-90 css-308 ms-Button-icon icon-284' style='font-family: controlIcons;'></i></span></button></div><table border='0' width='100%' height='100%' cellpadding='0' cellspacing='0' bgcolor='rgb(51, 51, 51)' style='border-spacing: 0px; font-family: Helvetica, Arial, sans-serif; background-color: rgb(51, 51, 51) !important; transform: scale(0.425, 0.425); transform-origin: left top;' data-ogsb='' data-ogab='#FFFFFF' min-scale='0.425'><tbody><tr><td align='center' valign='top' bgcolor='rgb(51, 51, 51)' style='background-color: rgb(51, 51, 51) !important; border-collapse: collapse;' data-ogsb='rgb(255, 255, 255)' data-ogab='#FFFFFF'><table width='600' cellpadding='0' cellspacing='0' border='0' class='x_container' bgcolor='rgb(51, 51, 51)' style='border-spacing: 0px; font-family: Helvetica, Arial, sans-serif; color: rgb(182, 182, 182) !important; max-width: 600px; background-color: rgb(51, 51, 51) !important;' data-ogsc='rgb(107, 107, 107)' data-ogsb='' data-ogab='#FFFFFF'><tbody><tr><td style='border-collapse:collapse'><table cellspacing='0' border='0' cellpadding='0' align='center' width='600' bgcolor='rgb(51, 51, 51)' class='x_footer' style='border-spacing: 0px; font-family: Helvetica, Arial, sans-serif; width: 100%; background-color: rgb(51, 51, 51) !important;' data-ogsb='rgb(255, 255, 255)' data-ogab='#FFFFFF'><tbody><tr><td class='x_social' align='center' style='border-collapse: collapse; vertical-align: middle; text-align: center; color: rgb(209, 209, 209) !important; font-size: 12px; padding: 20px 20px 10px;' data-ogsc='rgb(67, 67, 67)'><table class='x_intercom-container x_intercom-align-center' align='center' style='border-spacing:0; font-family:Helvetica,Arial,sans-serif'><tbody><tr><td style='border-collapse: collapse; vertical-align: middle; text-align: center; color: rgb(209, 209, 209) !important; font-size: 12px; padding-left: 4px; padding-right: 4px;' data-ogsc='rgb(67, 67, 67)'><a href='' target='_blank' rel='noopener noreferrer' data-auth='NotApplicable' style='color: rgb(255, 149, 123) !important; display: inline-block; text-decoration: none;' data-safelink='true' data-ogsc='rgb(231, 102, 80)' data-linkindex='2'><img data-imagetype='External' blockedimagesrc='' width='21' border='0' style='width:100%; max-width:21px; border-width:0; vertical-align:middle'> </a></td><td style='border-collapse: collapse; vertical-align: middle; text-align: center; color: rgb(209, 209, 209) !important; font-size: 12px; padding-left: 4px; padding-right: 4px;' data-ogsc='rgb(67, 67, 67)'><a href='' target='_blank' rel='noopener noreferrer' data-auth='NotApplicable' style='color: rgb(255, 149, 123) !important; display: inline-block; text-decoration: none;' data-safelink='true' data-ogsc='rgb(231, 102, 80)' data-linkindex='3'><img data-imagetype='External' blockedimagesrc='' width='21' border='0' style='width:100%; max-width:21px; border-width:0; vertical-align:middle'> </a></td><td style='border-collapse: collapse; vertical-align: middle; text-align: center; color: rgb(209, 209, 209) !important; font-size: 12px; padding-left: 4px; padding-right: 4px;' data-ogsc='rgb(67, 67, 67)'><a href='' target='_blank' rel='noopener noreferrer' data-auth='NotApplicable' style='color: rgb(255, 149, 123) !important; display: inline-block; text-decoration: none;' data-safelink='true' data-ogsc='rgb(231, 102, 80)' data-linkindex='4'><img data-imagetype='External' blockedimagesrc='https://getmaple.ca/email/youtube-2x.png' width='21' border='0' style='width:100%; max-width:21px; border-width:0; vertical-align:middle'> </a></td><td style='border-collapse: collapse; vertical-align: middle; text-align: center; color: rgb(209, 209, 209) !important; font-size: 12px; padding-left: 4px; padding-right: 4px;' data-ogsc='rgb(67, 67, 67)'><a href='' target='_blank' rel='noopener noreferrer' data-auth='NotApplicable' style='color: rgb(255, 149, 123) !important; display: inline-block; text-decoration: none;' data-safelink='true' data-ogsc='rgb(231, 102, 80)' data-linkindex='5'><img data-imagetype='External' blockedimagesrc='' width='21' border='0' style='width:100%; max-width:21px; border-width:0; vertical-align:middle'> </a></td><td style='border-collapse: collapse; vertical-align: middle; text-align: center; color: rgb(209, 209, 209) !important; font-size: 12px; padding-left: 4px; padding-right: 4px;' data-ogsc='rgb(67, 67, 67)'><a href='' target='_blank' rel='noopener noreferrer' data-auth='NotApplicable' style='color: rgb(255, 149, 123) !important; display: inline-block; text-decoration: none;' data-safelink='true' data-ogsc='rgb(231, 102, 80)' data-linkindex='6'><img data-imagetype='External' blockedimagesrc='' width='21' border='0' style='width:100%; max-width:21px; border-width:0; vertical-align:middle'> </a></td></tr></tbody></table></td></tr><tr><td class='x_info' style='border-collapse: collapse; vertical-align: middle; text-align: center; color: rgb(182, 182, 182) !important; font-size: 10px; padding: 0px 20px 20px;' data-ogsc='rgb(107, 107, 107)'><br aria-hidden='true'></td></tr></tbody></table></td></tr></tbody></table></td></tr></tbody></table></div><img data-imagetype='External' blockedimagesrc='' alt='' width='1' height='1' border='0' style='height:1px!important; width:1px!important; border-width:0!important; margin-top:0!important; margin-bottom:0!important; margin-right:0!important; margin-left:0!important; padding-top:0!important; padding-bottom:0!important; padding-right:0!important; padding-left:0!important'> </div>";
                
            }
            try
            {
                await _emailSender.SendOneAsync(employee.FullName, employee.Email,
                "Account Registration", message);
                TempData["message"] = "Invitation email sent to " + employee.FullName + " at " + employee.Email;
            }
            catch (Exception)
            {
                TempData["message"] = "Could not send Invitation email to " + employee.FullName + " at " + employee.Email;
            }


        }

        [Authorize(Roles = "Admin")]
        public IActionResult DownloadEmployees()
        {
            //Get the appointments
            var stf = from e in _context.Employees
                          orderby e.LastName
                          select new
                          {
                              ID = e.ID,
                              FirstName = e.FirstName,
                              LastName = e.LastName,
                              IsActive = e.Active,
                              Phone = e.PhoneNumber
                          };

            //How many rows?
            int numRows = stf.Count();

            if (numRows > 0) //We have data
            {
                //Create a new spreadsheet from scratch.
                using (ExcelPackage excel = new ExcelPackage())
                {

                    //Note: you can also pull a spreadsheet out of the database if you
                    //have saved it in the normal way we do, as a Byte Array in a Model
                    //such as the UploadedFile class.
                    //
                    // Suppose...
                    //
                    // var theSpreadsheet = _context.UploadedFiles.Include(f => f.FileContent).Where(f => f.ID == id).SingleOrDefault();
                    //
                    //    //Pass the Byte[] FileContent to a MemoryStream
                    //
                    // using (MemoryStream memStream = new MemoryStream(theSpreadsheet.FileContent.Content))
                    // {
                    //     ExcelPackage package = new ExcelPackage(memStream);
                    // }

                    var workSheet = excel.Workbook.Worksheets.Add("Employees");

                    //Note: Cells[row, column]
                    workSheet.Cells[3, 1].LoadFromCollection(stf, true);


                    //Note: You can define a BLOCK of cells: Cells[startRow, startColumn, endRow, endColumn]
                    //Make Date and Patient Bold
                    workSheet.Cells[4, 1, numRows + 3, 2].Style.Font.Bold = true;

                    //Note: these are fine if you are only 'doing' one thing to the range of cells.
                    //Otherwise you should USE a range object for efficiency
                    workSheet.Cells[numRows + 5, 4].Value = stf.Count().ToString();
                    workSheet.Cells[numRows + 5, 3].Value = "Staff Count:";
                    //Set Style and backgound colour of headings
                    using (ExcelRange headings = workSheet.Cells[3, 1, 3, 5])
                    {
                        headings.Style.Font.Bold = true;
                        var fill = headings.Style.Fill;
                        fill.PatternType = ExcelFillStyle.Solid;
                        fill.BackgroundColor.SetColor(Color.LightBlue);
                    }

                    //Boy those notes are BIG!
                    //Lets put them in comments instead.
                    for (int i = 4; i < numRows + 4; i++)
                    {
                        using (ExcelRange Rng = workSheet.Cells[i, 4])
                        {
                            string[] commentWords = Rng.Value.ToString().Split(' ');
                            Rng.Value = commentWords[0] + "...";
                            //This LINQ adds a newline every 7 words
                            string comment = string.Join(Environment.NewLine, commentWords
                                .Select((word, index) => new { word, index })
                                .GroupBy(x => x.index / 4)
                                .Select(grp => string.Join(" ", grp.Select(x => x.word))));
                            ExcelComment cmd = Rng.AddComment(comment, "Staff Notes");
                            cmd.AutoFit = true;
                        }
                    }

                    //Autofit columns
                    workSheet.Cells.AutoFitColumns();
                    //Note: You can manually set width of columns as well
                    //workSheet.Column(7).Width = 10;

                    //Add a title and timestamp at the top of the report
                    workSheet.Cells[1, 1].Value = "Staff Report";
                    using (ExcelRange Rng = workSheet.Cells[1, 1, 1, 5])
                    {
                        Rng.Merge = true; //Merge columns start and end range
                        Rng.Style.Font.Bold = true; //Font should be bold
                        Rng.Style.Font.Size = 18;
                        Rng.Style.HorizontalAlignment = ExcelHorizontalAlignment.Center;
                    }
                    //Since the time zone where the server is running can be different, adjust to 
                    //Local for us.
                    DateTime utcDate = DateTime.UtcNow;
                    TimeZoneInfo esTimeZone = TimeZoneInfo.FindSystemTimeZoneById("Eastern Standard Time");
                    DateTime localDate = TimeZoneInfo.ConvertTimeFromUtc(utcDate, esTimeZone);
                    using (ExcelRange Rng = workSheet.Cells[2, 6])
                    {
                        Rng.Value = "Created: " + localDate.ToShortTimeString() + " on " +
                            localDate.ToShortDateString();
                        Rng.Style.Font.Bold = true; //Font should be bold
                        Rng.Style.Font.Size = 12;
                        Rng.Style.HorizontalAlignment = ExcelHorizontalAlignment.Right;
                    }

                    //Ok, time to download the Excel

                    try
                    {
                        Byte[] theData = excel.GetAsByteArray();
                        string filename = "Staff.xlsx";
                        string mimeType = "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet";
                        return File(theData, mimeType, filename);
                    }
                    catch (Exception)
                    {
                        return BadRequest("Could not build and download the file.");
                    }
                }
            }
            return NotFound("No data.");
        }
        private bool EmployeeExists(int id)
        {
            return _context.Employees.Any(e => e.ID == id);
        }
    }
}
