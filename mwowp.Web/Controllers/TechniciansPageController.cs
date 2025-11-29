using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using mwowp.Web.Data;
using mwowp.Web.Models;

namespace mwowp.Web.Controllers
{
    [Authorize]
    public class TechniciansPageController : Controller
    {
        private readonly ApplicationDbContext _context;
        private readonly UserManager<ApplicationUser> _userManager;

        public TechniciansPageController(ApplicationDbContext context, UserManager<ApplicationUser> userManager)
        {
            _context = context;
            _userManager = userManager;
        }

        [Authorize(Roles = "Manager")]
        public async Task<IActionResult> List()
        {
            var technicians = await _userManager.GetUsersInRoleAsync("Technician");
            return View("TechniciansList", technicians);
        }

        [Authorize(Roles = "Manager")]
        public async Task<IActionResult> Details(string id)
        {
            if (string.IsNullOrWhiteSpace(id))
                return NotFound();

            var technician = await _userManager.FindByIdAsync(id);
            if (technician == null)
                return NotFound();

            var workOrders = await _context.WorkOrders
                .Include(wo => wo.Asset)
                .Include(wo => wo.CreatedByUser)
                .Where(wo => wo.AssignedToUserId == id)
                .ToListAsync();

            ViewBag.Technician = technician;
            ViewBag.WorkOrders = workOrders;

            return View("TechnicianDetails");
        }
    }
}
