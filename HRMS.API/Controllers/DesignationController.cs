using HRMS.API.Data;
using HRMS.API.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace HRMS.API.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class DesignationController : ControllerBase
    {
        private readonly ApplicationDbContext _context;

        public DesignationController(ApplicationDbContext context)
        {
            _context = context;
        }

        [HttpGet]
        public async Task<IActionResult> GetDesignations()
        {
            var designations = await _context.Designations
                .Include(d => d.Department)
                .ToListAsync();

            return Ok(designations);
        }

        [HttpGet("{id}")]
        public async Task<IActionResult> GetDesignation(int id)
        {
            var designation = await _context.Designations
                .Include(d => d.Department)
                .FirstOrDefaultAsync(d => d.DesignationId == id);

            if (designation == null)
                return NotFound();

            return Ok(designation);
        }

        [HttpPost]
        public async Task<IActionResult> CreateDesignation(Designation designation)
        {
            _context.Designations.Add(designation);
            await _context.SaveChangesAsync();

            return Ok(designation);
        }

        [HttpPut("{id}")]
        public async Task<IActionResult> UpdateDesignation(int id, Designation designation)
        {
            if (id != designation.DesignationId)
                return BadRequest();

            _context.Entry(designation).State = EntityState.Modified;
            await _context.SaveChangesAsync();

            return Ok(designation);
        }

        [HttpDelete("{id}")]
        public async Task<IActionResult> DeleteDesignation(int id)
        {
            var designation = await _context.Designations.FindAsync(id);

            if (designation == null)
                return NotFound();

            _context.Designations.Remove(designation);
            await _context.SaveChangesAsync();

            return Ok();
        }
    }
}