using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using mwowp.Web.Data;
using mwowp.Web.Models;
using System.Security.Claims;

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
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            ViewBag.Assets = await _context.Assets
                                    .Where(a => a.OwnerUserId == userId)
                                    .ToListAsync();
            return View();
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create([Bind("AssetId,Title,Description,Priority")] WorkOrder workOrder)
        {
            ModelState.Remove("CreatedByUserId");
            ModelState.Remove("CreatedByUser");
            ModelState.Remove("Status");
            ModelState.Remove("Asset");

            if (ModelState.IsValid)
            {
                var user = await _userManager.GetUserAsync(User);
                workOrder.CreatedByUserId = user.Id;
                workOrder.Status = WorkOrderStatus.Created;
                var asset = await _context.Assets
                .AsNoTracking()
                .FirstOrDefaultAsync(a => a.Id == workOrder.AssetId);
                if (asset != null) {
                    asset.Status = AssetStatus.OnRepair;
                    _context.Assets.Update(asset);
                    workOrder.Asset = asset;
                }
                workOrder.CreatedAt = DateTime.UtcNow;
                _context.WorkOrders.Add(workOrder);
                await _context.SaveChangesAsync();
                return RedirectToAction(nameof(MyOrders));
            }

            // Eğer model state geçersizse, ViewBag tekrar set edilmeli!
            ViewBag.Assets = await _context.Assets
                                    .Where(a => a.OwnerUserId == workOrder.CreatedByUserId)
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
        public async Task<IActionResult> MyOrders()
        {
            var user = await _userManager.GetUserAsync(User);
            var tasks = await _context.WorkOrders
                                .Include(wo => wo.Asset)
                                .Where(wo => wo.CreatedByUserId == user.Id)
                                .ToListAsync();
            return View(tasks);
        }
        [HttpGet]
        public async Task<IActionResult> EditOrder(int id)
        {
            var order = await _context.WorkOrders
                                .Include(wo => wo.Asset)
                                .Include(wo => wo.CreatedByUser)
                                .Include(wo => wo.AssignedToUser)
                                .FirstOrDefaultAsync(wo => wo.Id == id);
            ViewBag.Assignees = await _userManager.GetUsersInRoleAsync("Technician");
            return View(order);
        }

        public async Task<IActionResult> ViewTask(int id)
        {
            var order = await _context.WorkOrders
                                .Include(wo => wo.Asset)
                                .Include(wo => wo.CreatedByUser)
                                .Include(wo => wo.AssignedToUser)
                                .FirstOrDefaultAsync(wo => wo.Id == id);
            ViewBag.Equipments = await _context.Equipments
                                        .Where(e => e.Status == EquipmentStatus.Available)
                                        .ToListAsync();

            ViewBag.SpareParts = await _context.SpareParts
                                        .Where(sp => sp.Stock > 0)
                                        .ToListAsync();
            ViewBag.WorkOrderEquipments = await _context.WorkOrderEquipments
                                        .Where(woe => woe.WorkOrderId == id)
                                        .Include(woe=>woe.Equipment)
                                        .ToListAsync();
            ViewBag.WorkOrderSpareParts = await _context.WorkOrderSpareParts
                                        .Where(wosp => wosp.WorkOrderId == id)
                                        .Include(wosp => wosp.SparePart)
                                        .ToListAsync();
            return View(order);
        }

        public async Task<IActionResult> Inspection(int id)
        {
            var order = await _context.WorkOrders
                                .Include(wo => wo.Asset)
                                .Include(wo => wo.CreatedByUser)
                                .Include(wo => wo.AssignedToUser)
                                .FirstOrDefaultAsync(wo => wo.Id == id);
            ViewBag.Assignees = await _userManager.GetUsersInRoleAsync("Technician");
            return View(order);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Inspection(int id, int rating, string comments)
        {
            if (rating < 1 || rating > 5)
            {
                ModelState.AddModelError(nameof(rating), "Puan 1 ile 5 arasında olmalıdır.");
            }

            var order = await _context.WorkOrders
                .Include(wo => wo.Asset)
                .Include(wo => wo.CreatedByUser)
                .Include(wo => wo.AssignedToUser)
                .FirstOrDefaultAsync(wo => wo.Id == id);

            if (order == null)
                return NotFound();

            if (!ModelState.IsValid)
            {
                ViewBag.Assignees = await _userManager.GetUsersInRoleAsync("Technician");
                return View(order);
            }

            var user = await _userManager.GetUserAsync(User);

            var inspection = new Inspection
            {
                WorkOrderId = id,
                InspectorId = user.Id,
                InspectionDate = DateTime.UtcNow,
                Rating = rating,
                Comments = comments
            };
            order.Status = WorkOrderStatus.Inspected;

            _context.Inspections.Add(inspection);
            await _context.SaveChangesAsync();

            return RedirectToAction(nameof(Index));
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Complete(int id)
        {
            var workOrder = await _context.WorkOrders.FindAsync(id);
            if (workOrder == null)
                return NotFound();

            if (workOrder.Status != WorkOrderStatus.Completed &&
                workOrder.Status != WorkOrderStatus.Inspected &&
                workOrder.Status != WorkOrderStatus.Canceled)
            {
                workOrder.Status = WorkOrderStatus.Completed;
                workOrder.CompletedAt = DateTime.UtcNow;
                await _context.SaveChangesAsync();
            }

            return RedirectToAction(nameof(ViewTask), new { id });
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> AddEquipment(int id, int equipmentId, string? usageNotes)
        {
            var workOrder = await _context.WorkOrders.FindAsync(id);
            if (workOrder == null)
                return NotFound();

            var equipment = await _context.Equipments.FirstOrDefaultAsync(e => e.Id == equipmentId);
            if (equipment == null)
                return NotFound();

            // Atama
            var woe = new WorkOrderEquipment
            {
                WorkOrderId = id,
                EquipmentId = equipmentId,
                UsageNotes = usageNotes ?? string.Empty,
                AssignedAt = DateTime.UtcNow,
                UsedAt = DateTime.UtcNow
            };
            equipment.Status = EquipmentStatus.InUse;

            _context.WorkOrderEquipments.Add(woe);
            _context.Equipments.Update(equipment);

            if (workOrder.Status == WorkOrderStatus.Assigned)
                workOrder.Status = WorkOrderStatus.InProgress;

            await _context.SaveChangesAsync();
            return RedirectToAction(nameof(ViewTask), new { id });
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> AddSparePart(int id, int sparePartId, int quantityUsed)
        {
            if (quantityUsed <= 0)
                return BadRequest("Miktar pozitif olmalı.");

            var workOrder = await _context.WorkOrders.FindAsync(id);
            if (workOrder == null)
                return NotFound();

            var sparePart = await _context.SpareParts.FirstOrDefaultAsync(sp => sp.Id == sparePartId);
            if (sparePart == null)
                return NotFound();

            var wosp = new WorkOrderSparePart
            {
                WorkOrderId = id,
                SparePartId = sparePartId,
                QuantityUsed = quantityUsed
            };
            _context.WorkOrderSpareParts.Add(wosp);

            if (workOrder.Status == WorkOrderStatus.Assigned || workOrder.Status == WorkOrderStatus.InProgress)
                workOrder.Status = WorkOrderStatus.PartsOrdered;

            await _context.SaveChangesAsync();
            return RedirectToAction(nameof(ViewTask), new { id });
        }

        public async Task<IActionResult> EditOrder(WorkOrder model)
        {
            var workOrder = await _context.WorkOrders
                .Include(wo => wo.Asset)
                .Include(wo => wo.CreatedByUser)
                .Include(wo => wo.AssignedToUser)
                .FirstOrDefaultAsync(wo => wo.Id == model.Id);

            if (workOrder == null)
                return NotFound();

            workOrder.Priority = model.Priority;
            workOrder.AssignedToUserId = model.AssignedToUserId;
            workOrder.Status = WorkOrderStatus.Assigned;

            // Diğer alanlar değişmeyecek (Title, Description vb.)
            ModelState.Remove("CreatedByUserId");
            ModelState.Remove("CreatedByUser");
            ModelState.Remove("Asset");
            ModelState.Remove("Description");
            ModelState.Remove("Title");

            if (!ModelState.IsValid)
            {
                ViewBag.Assignees = await _userManager.GetUsersInRoleAsync("Technician");
                return View(workOrder);
            }

            await _context.SaveChangesAsync();
            return RedirectToAction(nameof(Index));
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

