using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using mwowp.Web.Data;
using mwowp.Web.Models;
using Microsoft.AspNetCore.Identity;

namespace mwowp.Web.Controllers
{
    public class WorkOrderController : Controller
    {
        private readonly ApplicationDbContext _context;
        private readonly UserManager<ApplicationUser> _userManager;

        public WorkOrderController(ApplicationDbContext context, UserManager<ApplicationUser> userManager)
        {
            _context = context;
            _userManager = userManager;
        }

        public async Task<IActionResult> Create()
        {
            ViewBag.Assets = await _context.Assets
                                    .Where(a => a.Status == AssetStatus.OnRepair)
                                    .ToListAsync();
            return View();
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create([Bind("AssetId,Title,Description,Priority")] WorkOrder workOrder)
        {
            if (ModelState.IsValid)
            {
                var user = await _userManager.GetUserAsync(User);
                workOrder.CreatedByUserId = user.Id;
                workOrder.Status = WorkOrderStatus.Created;
                workOrder.CreatedAt = DateTime.UtcNow;
                _context.WorkOrders.Add(workOrder);
                await _context.SaveChangesAsync();
                return RedirectToAction(nameof(Index));
            }

            // Eğer model state geçersizse, ViewBag tekrar set edilmeli!
            ViewBag.Assets = await _context.Assets
                                    .Where(a => a.Status == AssetStatus.OnRepair)
                                    .ToListAsync();
            return View(workOrder);
        }


        // GET: WorkOrder/Index
        public async Task<IActionResult> Index()
        {
            var workOrders = await _context.WorkOrders
                                .Include(wo => wo.Asset)
                                .Include(wo => wo.CreatedByUser)
                                .Include(wo => wo.AssignedToUser)
                                .ToListAsync();
            return View(workOrders);
        }

        public async Task<IActionResult> MyTasks()
        {
            var user = await _userManager.GetUserAsync(User);
            var tasks = await _context.WorkOrders
                                .Include(wo => wo.Asset)
                                .Where(wo => wo.AssignedToUserId == user.Id)
                                .ToListAsync();
            return View(tasks);
        }



        // GET: /WorkOrder/Assign/5
        [HttpGet]
        public async Task<IActionResult> Assign(int id)
        {
            var workOrder = await _context.WorkOrders
                                .Include(wo => wo.Asset)
                                .Include(wo => wo.AssignedToUser)
                                .FirstOrDefaultAsync(wo => wo.Id == id);

            if (workOrder == null)
                return NotFound();

            // Sadece manager erişebilir
            var currentUser = await _userManager.GetUserAsync(User);
            var roles = await _userManager.GetRolesAsync(currentUser);
            if (!roles.Contains("Manager"))
                return Forbid();

            // Teknik personel listesi
            ViewBag.Technicians = await _userManager.GetUsersInRoleAsync("Technician");

            return View(workOrder);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Assign(int id, string assignedToUserId, WorkOrderStatus status)
        {
            var workOrder = await _context.WorkOrders.FindAsync(id);
            if (workOrder == null)
                return NotFound();

            workOrder.AssignedToUserId = assignedToUserId;
            workOrder.Status = status;
            await _context.SaveChangesAsync();

            return RedirectToAction(nameof(Index));
        }

    }
}

