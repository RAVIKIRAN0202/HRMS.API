using HRMS.API.Data;
using HRMS.API.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using HRMS.API.DTOs;
using Microsoft.AspNetCore.Authorization;

namespace HRMS.API.Controllers
{
    [Authorize]
    [Route("api/[controller]")]
    [ApiController]

  
    public class EmployeeController : ControllerBase
    {
        private readonly ApplicationDbContext _context;

        public EmployeeController(ApplicationDbContext context)
        {
            _context = context;
        }

        [HttpGet]
        [Authorize(Roles = "Admin,HR")]
        public async Task<IActionResult> GetEmployees()
        {
            var employees = await _context.Employees
                .Include(e => e.User)
                .Include(e => e.Department)
                .Include(e => e.Designation)
                .ToListAsync();

            return Ok(employees);
        }

        [HttpGet("{id}")]
        [Authorize(Roles = "Admin,HR,Employee")]
        public async Task<IActionResult> GetEmployee(int id)
        {
            var employee = await _context.Employees
                .Include(e => e.User)
                .Include(e => e.Department)
                .Include(e => e.Designation)
                .FirstOrDefaultAsync(e => e.EmployeeId == id);

            if (employee == null)
                return NotFound();

            return Ok(employee);
        }

        [HttpPost]
        [Authorize(Roles = "Admin,HR")]
        public async Task<IActionResult> CreateEmployee(EmployeeCreateDto dto)
        {
            var employee = new Employee
            {
                UserId = dto.UserId,
                EmployeeCode = dto.EmployeeCode,
                DepartmentId = dto.DepartmentId,
                DesignationId = dto.DesignationId,
                DateOfJoining = dto.DateOfJoining,
                Phone = dto.Phone,
                Address = dto.Address,
                Salary = dto.Salary,
                Status = dto.Status,
                CreatedAt = DateTime.Now
            };

            _context.Employees.Add(employee);
            await _context.SaveChangesAsync();

            return Ok(employee);
        }

        [HttpPut("{id}")]
        [Authorize(Roles = "Admin,HR")]
        public async Task<IActionResult> UpdateEmployee(int id, Employee employee)
        {
            if (id != employee.EmployeeId)
                return BadRequest();

            _context.Entry(employee).State = EntityState.Modified;
            await _context.SaveChangesAsync();

            return Ok(employee);
        }

        [HttpDelete("{id}")]
        [Authorize(Roles = "Admin")]
        public async Task<IActionResult> DeleteEmployee(int id)
        {
            var employee = await _context.Employees.FindAsync(id);

            if (employee == null)
                return NotFound();

            _context.Employees.Remove(employee);
            await _context.SaveChangesAsync();

            return Ok();
        }
    }
}