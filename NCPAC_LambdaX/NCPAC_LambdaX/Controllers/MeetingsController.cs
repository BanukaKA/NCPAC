﻿using System;
using System.Collections.Generic;
using System.Diagnostics.Metrics;
using System.Drawing;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
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
    [Authorize]
    public class MeetingsController : Controller
    {
        private readonly NCPACContext _context;

        public MeetingsController(NCPACContext context)
        {
            _context = context;
        }

        // GET: Meetings
        public async Task<IActionResult> Index(string SearchString, bool ShowCanceled, int? CommiteeID,
            int? page, int? pageSizeID, string actionButton, string sortDirection = "asc", string sortField = "Meeting")
        {
            ViewData["Filtering"] = "";

            //Supply SelectList for Commitees
            ViewData["CommiteeID"] = new SelectList(_context
                .Commitees
                .OrderBy(s => s.CommiteeName), "ID", "CommiteeName");


            string[] sortOptions = new[] { "Meeting", "TimeFrom" };

            var meetings = _context.Meetings
            .Include(m => m.Commitee)
            .ThenInclude(m => m.MemberCommitees)
            .AsNoTracking();

            var member = await _context.Members
                .Include(m => m.MemberCommitees)
                .Where(e => e.Email == User.Identity.Name || e.WorkEmail == User.Identity.Name)
                .AsNoTracking()
                .FirstOrDefaultAsync();
            

            if((User.IsInRole("Admin") || User.IsInRole("Supervisor") || User.IsInRole("Staff")) == false)
            {
                meetings = meetings.Where(m => (m.Commitee.MemberCommitees.Any(a => a.MemberID == member.ID)));
            }

            if (!String.IsNullOrEmpty(SearchString))
            {
                meetings = meetings.Where(p => p.MeetingTitle.ToUpper().Contains(SearchString.ToUpper()));
            }
            if (CommiteeID.HasValue)
            {
                meetings = meetings.Where(p => p.CommiteeID == CommiteeID);
                ViewData["Filtering"] = "";
            }

            if (ShowCanceled == true)
            {
                meetings = meetings.Where(p => p.IsCancelled == true);
            }
            else
            {
                meetings = meetings.Where(p => p.IsCancelled == false);
            }
            if (!String.IsNullOrEmpty(actionButton)) //Form Submitted!
            {
                page = 1;//Reset page start

                if (sortOptions.Contains(actionButton))//Change of sort is requested
                {
                    if (actionButton == sortField) //Reverse order on same field
                    {
                        sortDirection = sortDirection == "asc" ? "desc" : "asc";
                    }
                    sortField = actionButton;//Sort by the button clicked
                }
            }

            if (sortField == "Meeting")
            {
                if (sortDirection == "asc")
                {
                    meetings = meetings
                        .OrderBy(p => p.MeetingTitle);
                }
                else
                {
                    meetings = meetings
                       .OrderByDescending(p => p.MeetingTitle);
                }
            }

            if (sortField == "TimeFrom")
            {
                if (sortDirection == "asc")
                {
                    meetings = meetings
                        .OrderByDescending(p => p.TimeFrom);
                }
                else
                {
                    meetings = meetings
                       .OrderBy(p => p.TimeFrom);
                }
            }


            ViewData["sortField"] = sortField;
            ViewData["sortDirection"] = sortDirection;

            int pageSize = PageSizeHelper.SetPageSize(HttpContext, pageSizeID, "Meeting");
            ViewData["pageSizeID"] = PageSizeHelper.PageSizeList(pageSize);

            var pagedData = await PaginatedList<Meeting>.CreateAsync(meetings.AsNoTracking(), page ?? 1, pageSize);

            return View(pagedData);
        }

        // GET: Meetings/Details/5

        public async Task<IActionResult> Details(int? id)
        {
            if (id == null || _context.Meetings == null)
            {
                return NotFound();
            }

            var meeting = await _context.Meetings
            .Include(d => d.MeetingDocuments)
            .Include(m => m.ActionItems)
            .Include(m => m.Commitee)
            .ThenInclude(m => m.MemberCommitees)
            .FirstOrDefaultAsync(m => m.ID == id);
            if (meeting == null)
            {
                return NotFound();
            }

            return View(meeting);
        }

        // GET: Meetings/Create
        [Authorize(Roles = "Admin,Supervisor,Staff")]
        public IActionResult Create()
        {
            Meeting meeting = new Meeting();
            PopulateDropDownLists(meeting);
            return View();
        }

        // POST: Meetings/Create
        // To protect from overposting attacks, enable the specific properties you want to bind to.
        // For more details, see http://go.microsoft.com/fwlink/?LinkId=317598.
        [Authorize(Roles = "Admin,Supervisor,Staff")]
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create([Bind("ID,MeetingTitle,Description,TimeFrom,Minitues,CommiteeID,IsCancelled")] Meeting meeting, List<IFormFile> theFiles)
        {

            await AddDocumentsAsync(meeting, theFiles);
            _context.Add(meeting);
            await _context.SaveChangesAsync();
            return RedirectToAction(nameof(Index));

        }

        // GET: Meetings/Edit/5
        [Authorize(Roles = "Admin,Supervisor,Staff")]
        public async Task<IActionResult> Edit(int? id)
        {
            if (id == null || _context.Meetings == null)
            {
                return NotFound();
            }

            var meeting = await _context.Meetings
                .Include(m => m.Commitee)
                .Include(m => m.ActionItems)
                .Include(d => d.MeetingDocuments)
                .FirstOrDefaultAsync(d => d.ID == id);
            
            if (meeting == null)
            {
                return NotFound();
            }
            PopulateDropDownLists(meeting);
            return View(meeting);
        }

        // POST: Meetings/Edit/5
        // To protect from overposting attacks, enable the specific properties you want to bind to.
        // For more details, see http://go.microsoft.com/fwlink/?LinkId=317598.
        [HttpPost]
        [ValidateAntiForgeryToken]
        [Authorize(Roles = "Admin,Supervisor,Staff")]
        public async Task<IActionResult> Edit(int id, [Bind("ID,MeetingTitle,Description,TimeFrom,Minitues,CommiteeID,IsCancelled")] Meeting meetingToUpdate, List<IFormFile> theFiles)
        {
            if (id != meetingToUpdate.ID)
            {
                return NotFound();
            }


            try
            {
                await AddDocumentsAsync(meetingToUpdate, theFiles);
                _context.Update(meetingToUpdate);
                await _context.SaveChangesAsync();
            }
            catch (DbUpdateConcurrencyException)
            {
                if (!MeetingExists(meetingToUpdate.ID))
                {
                    return NotFound();
                }
                else
                {
                    throw;
                }
            }

            return RedirectToAction(nameof(Index));
        }

        // GET: Meetings/Delete/5
        public async Task<IActionResult> Delete(int? id)
        {
            if (id == null || _context.Meetings == null)
            {
                return NotFound();
            }

            var meeting = await _context.Meetings
                .FirstOrDefaultAsync(m => m.ID == id);
            if (meeting == null)
            {
                return NotFound();
            }

            return View(meeting);
        }

        // POST: Meetings/Delete/5
        [Authorize(Roles = "Admin,Supervisor,Staff")]
        [HttpPost, ActionName("Delete")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteConfirmed(int id)
        {
            if (_context.Meetings == null)
            {
                return Problem("Entity set 'NCPACContext.Meetings'  is null.");
            }
            var meeting = await _context.Meetings.FindAsync(id);
            if (meeting != null)
            {
                _context.Meetings.Remove(meeting);
            }
            
            await _context.SaveChangesAsync();
            return RedirectToAction(nameof(Index));
        }

        public async Task<FileContentResult> Download(int id)
        {
            var theFile = await _context.UploadedFiles
                .Include(d => d.FileContent)
                .Where(f => f.ID == id)
                .FirstOrDefaultAsync();
            return File(theFile.FileContent.Content, theFile.MimeType, theFile.FileName);
        }

        private async Task AddDocumentsAsync(Meeting meeting, List<IFormFile> theFiles)
        {
            foreach (var f in theFiles)
            {
                if (f != null)
                {
                    string mimeType = f.ContentType;
                    string fileName = Path.GetFileName(f.FileName);
                    long fileLength = f.Length;
                    //Note: you could filter for mime types if you only want to allow
                    //certain types of files.  I am allowing everything.
                    if (!(fileName == "" || fileLength == 0))//Looks like we have a file!!!
                    {
                        MeetingDocument d = new();
                        using (var memoryStream = new MemoryStream())
                        {
                            await f.CopyToAsync(memoryStream);
                            d.FileContent.Content = memoryStream.ToArray();
                        }
                        d.MimeType = mimeType;
                        d.FileName = fileName;
                        meeting.MeetingDocuments.Add(d);
                    };
                }
            }
        }

        private SelectList CommiteeList(int? selectedId)
        {
            return new SelectList(_context
                .Commitees
                .OrderBy(m => m.CommiteeName), "ID", "CommiteeName", selectedId);
        }


        private bool MeetingExists(int id)
        {
          return _context.Meetings.Any(e => e.ID == id);
        }

        


        [Authorize(Roles = "Admin,Supervisor")]
        public IActionResult DownloadMeetings()
        {
            //Get the appointments
            var meeetns = from e in _context.Meetings
                        .Include(m => m.Commitee)
                        orderby e.TimeFrom
                        select new
                        {
                            ID = e.ID,
                            Title = e.MeetingTitle,
                            Description = e.Description,
                            Commitee = e.Commitee.CommiteeName,
                            MeetingMinitues = e.Minitues.ToString() + "minitues",
                            StartTime = e.TimeFrom.ToString()
                        };

            //How many rows?
            int numRows = meeetns.Count();

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

                    var workSheet = excel.Workbook.Worksheets.Add("Meetings");

                    //Note: Cells[row, column]
                    workSheet.Cells[3, 1].LoadFromCollection(meeetns, true);


                    //Note: You can define a BLOCK of cells: Cells[startRow, startColumn, endRow, endColumn]
                    //Make Date and Patient Bold
                    workSheet.Cells[4, 1, numRows + 3, 2].Style.Font.Bold = true;

                    //Note: these are fine if you are only 'doing' one thing to the range of cells.
                    //Otherwise you should USE a range object for efficiency
                    workSheet.Cells[numRows + 5, 4].Value = meeetns.Count().ToString();
                    workSheet.Cells[numRows + 5, 3].Value = "Meeting Count:";
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
                            ExcelComment cmd = Rng.AddComment(comment, "Meeting Notes");
                            cmd.AutoFit = true;
                        }
                    }

                    //Autofit columns
                    workSheet.Cells.AutoFitColumns();
                    //Note: You can manually set width of columns as well
                    //workSheet.Column(7).Width = 10;

                    //Add a title and timestamp at the top of the report
                    workSheet.Cells[1, 1].Value = "Meetings Report";
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
                        string filename = "Meetings.xlsx";
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

        private SelectList CommiteeeSelectList(int? selectedId)
        {
            return new SelectList(_context.Commitees
                .OrderBy(d => d.CommiteeName), "ID", "CommiteeName", selectedId);
        }

        private void PopulateDropDownLists(Meeting meeting = null)
        {
            if ((meeting?.CommiteeID) != null)
            {
                ViewData["CommiteeID"] = CommiteeeSelectList(meeting.CommiteeID);
            }
            else
            {
                ViewData["MailPrefferenceID"] = CommiteeeSelectList(null);
            }
        }


    }
}
