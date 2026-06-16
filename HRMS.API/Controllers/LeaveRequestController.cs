using HRMS.API.Data;
using HRMS.API.DTOs;
using HRMS.API.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace HRMS.API.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    [Authorize]
    public class LeaveRequestController : ControllerBase
    {
        private readonly ApplicationDbContext _context;

        public LeaveRequestController(ApplicationDbContext context)
        {
            _context = context;
        }

        [HttpGet]
        [Authorize(Roles = "Admin,HR")]
        public async Task<IActionResult> GetAllLeaveRequests()
        {
            var leaves = await _context.LeaveRequests
                .Include(l => l.Employee)
                .Include(l => l.LeaveType)
                .ToListAsync();

            return Ok(leaves);
        }

        [HttpGet("{id}")]
        public async Task<IActionResult> GetLeaveRequest(int id)
        {
            var leave = await _context.LeaveRequests
                .Include(l => l.Employee)
                .Include(l => l.LeaveType)
                .FirstOrDefaultAsync(l => l.LeaveRequestId == id);

            if (leave == null)
                return NotFound();

            return Ok(leave);
        }

        [HttpPost]
        [Authorize(Roles = "Admin,HR,Employee")]
        public async Task<IActionResult> ApplyLeave(LeaveRequestCreateDto dto)
        {
            var leaveRequest = new LeaveRequest
            {
                EmployeeId = dto.EmployeeId,
                LeaveTypeId = dto.LeaveTypeId,
                FromDate = dto.FromDate,
                ToDate = dto.ToDate,
                Reason = dto.Reason,
                Status = "Pending",
                CreatedAt = DateTime.Now
            };

            _context.LeaveRequests.Add(leaveRequest);
            await _context.SaveChangesAsync();

            return Ok(leaveRequest);
        }

        [HttpPut("approve/{id}")]
        [Authorize(Roles = "Admin,HR")]
        public async Task<IActionResult> ApproveLeave(int id)
        {
            var leave = await _context.LeaveRequests.FindAsync(id);

            if (leave == null)
                return NotFound();

            leave.Status = "Approved";
            await _context.SaveChangesAsync();

            return Ok("Leave approved successfully");
        }

        [HttpPut("reject/{id}")]
        [Authorize(Roles = "Admin,HR")]
        public async Task<IActionResult> RejectLeave(int id)
        {
            var leave = await _context.LeaveRequests.FindAsync(id);

            if (leave == null)
                return NotFound();

            leave.Status = "Rejected";
            await _context.SaveChangesAsync();

            return Ok("Leave rejected successfully");
        }
    }
}