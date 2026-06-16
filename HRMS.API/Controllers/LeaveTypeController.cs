using HRMS.API.Data;
using HRMS.API.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace HRMS.API.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    [Authorize]
    public class LeaveTypeController : ControllerBase
    {
        private readonly ApplicationDbContext _context;

        public LeaveTypeController(ApplicationDbContext context)
        {
            _context = context;
        }

        [HttpGet]
        public async Task<IActionResult> GetLeaveTypes()
        {
            return Ok(await _context.LeaveTypes.ToListAsync());
        }

        [HttpPost]
        [Authorize(Roles = "Admin,HR")]
        public async Task<IActionResult> CreateLeaveType(LeaveType leaveType)
        {
            leaveType.CreatedAt = DateTime.Now;

            _context.LeaveTypes.Add(leaveType);
            await _context.SaveChangesAsync();

            return Ok(leaveType);
        }
    }
}