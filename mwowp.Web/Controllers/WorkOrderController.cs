using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using mwowp.Web.Data;
using mwowp.Web.Models;
using mwowp.Web.Services;
using System.Security.Claims;

namespace mwowp.Web.Controllers
{
    [Authorize]
    public class WorkOrderController : Controller
    {
        private readonly ApplicationDbContext _context;
        private readonly UserManager<ApplicationUser> _userManager;
        private readonly IWebHostEnvironment _env;
        private readonly IWorkOrderService _workOrderService;
        private readonly IAttachmentService _attachmentService;
        private readonly IEquipmentService _equipmentService;
        private readonly ISparePartService _sparePartService;

        public WorkOrderController(
            ApplicationDbContext context,
            UserManager<ApplicationUser> userManager,
            IWebHostEnvironment env,
            IWorkOrderService workOrderService,
            IAttachmentService attachmentService,
            IEquipmentService equipmentService,
            ISparePartService sparePartService)
        {
            _context = context;
            _userManager = userManager;
            _env = env;
            _workOrderService = workOrderService;
            _attachmentService = attachmentService;
            _equipmentService = equipmentService;
            _sparePartService = sparePartService;
        }

        // Sadece normal kullanıcılar iş emri oluşturabilir
        [Authorize(Roles = "User")]
        public async Task<IActionResult> Create()
        {
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            ViewBag.Assets = await _context.Assets.Where(a => a.OwnerUserId == userId).ToListAsync();
            return View();
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        [Authorize(Roles = "User")]
        public async Task<IActionResult> Create([Bind("AssetId,Title,Description,Priority")] WorkOrder workOrder, List<IFormFile>? images)
        {
            ModelState.Remove("CreatedByUserId");
            ModelState.Remove("CreatedByUser");
            ModelState.Remove("Status");
            ModelState.Remove("Asset");

            if (!ModelState.IsValid)
            {
                var currentUserId = User.FindFirstValue(ClaimTypes.NameIdentifier);
                ViewBag.Assets = await _context.Assets.Where(a => a.OwnerUserId == currentUserId).ToListAsync();
                return View(workOrder);
            }

            var user = await _userManager.GetUserAsync(User);
            var created = await _workOrderService.CreateAsync(workOrder, user.Id);

            if (images != null && images.Count > 0)
            {
                await _attachmentService.SaveWorkOrderImagesAsync(created.Id, _env.WebRootPath, images);
            }

            return RedirectToAction(nameof(MyOrders));
        }

        // Yalnızca Manager tüm iş emirlerini görebilir
        [Authorize(Roles = "Manager")]
        public async Task<IActionResult> Index()
        {
            var workOrders = await _context.WorkOrders
                .Include(wo => wo.Asset)
                .Include(wo => wo.CreatedByUser)
                .Include(wo => wo.AssignedToUser)
                .ToListAsync();
            return View(workOrders);
        }

        // Yalnızca Technician kendi görevlerini görebilir
        [Authorize(Roles = "Technician")]
        public async Task<IActionResult> MyTasks()
        {
            var user = await _userManager.GetUserAsync(User);
            var tasks = await _context.WorkOrders
                .Include(wo => wo.Asset)
                .Where(wo => wo.AssignedToUserId == user.Id)
                .ToListAsync();
            return View(tasks);
        }

        // Yalnızca User kendi oluşturduğu emirleri görebilir
        [Authorize(Roles = "User")]
        public async Task<IActionResult> MyOrders()
        {
            var user = await _userManager.GetUserAsync(User);
            var tasks = await _context.WorkOrders
                .Include(wo => wo.Asset)
                .Where(wo => wo.CreatedByUserId == user.Id)
                .ToListAsync();
            return View(tasks);
        }

        // Edit ekranı: Manager düzenleyebilir
        [HttpGet]
        [Authorize(Roles = "Manager")]
        public async Task<IActionResult> EditOrder(int id)
        {
            var order = await _context.WorkOrders
                .Include(wo => wo.Asset)
                .Include(wo => wo.CreatedByUser)
                .Include(wo => wo.AssignedToUser)
                .FirstOrDefaultAsync(wo => wo.Id == id);
            if (order == null) return NotFound();

            ViewBag.Assignees = await _userManager.GetUsersInRoleAsync("Technician");
            return View(order);
        }

        // Görev detayını şu kullanıcılar görebilsin:
        // - Manager
        // - Atanan teknisyen
        // - İş emrini oluşturan kullanıcı
        [Authorize(Roles = "Manager,Technician,User")]
        public async Task<IActionResult> ViewTask(int id)
        {
            var order = await _context.WorkOrders
                .Include(wo => wo.Asset)
                .Include(wo => wo.CreatedByUser)
                .Include(wo => wo.AssignedToUser)
                .Include(wo => wo.Attachments)
                .Include(wo => wo.History)
                 .ThenInclude(h => h.ChangedByUser)
                .FirstOrDefaultAsync(wo => wo.Id == id);
            if (order == null) return NotFound();

            var currentUser = await _userManager.GetUserAsync(User);
            var roles = await _userManager.GetRolesAsync(currentUser);

            var canView = roles.Contains("Manager") ||
                          order.AssignedToUserId == currentUser.Id ||
                          order.CreatedByUserId == currentUser.Id;
            if (!canView) return Forbid();

            ViewBag.Equipments = await _context.Equipments.Where(e => e.Status == EquipmentStatus.Available).ToListAsync();
            ViewBag.SpareParts = await _context.SpareParts.Where(sp => sp.Stock > 0).ToListAsync();
            ViewBag.WorkOrderEquipments = await _context.WorkOrderEquipments.Where(woe => woe.WorkOrderId == id).Include(woe => woe.Equipment).ToListAsync();
            ViewBag.WorkOrderSpareParts = await _context.WorkOrderSpareParts.Where(wosp => wosp.WorkOrderId == id).Include(wosp => wosp.SparePart).ToListAsync();

            return View(order);
        }

        // Inspection: Manager denetleyebilir
        [Authorize(Roles = "Manager")]
        public async Task<IActionResult> Inspection(int id)
        {
            var order = await _context.WorkOrders
                .Include(wo => wo.Asset)
                .Include(wo => wo.CreatedByUser)
                .Include(wo => wo.AssignedToUser)
                .FirstOrDefaultAsync(wo => wo.Id == id);
            if (order == null) return NotFound();

            ViewBag.Assignees = await _userManager.GetUsersInRoleAsync("Technician");
            return View(order);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        [Authorize(Roles = "Manager")]
        public async Task<IActionResult> Inspection(int id, int rating, string comments)
        {
            if (rating < 1 || rating > 5)
                ModelState.AddModelError(nameof(rating), "Puan 1 ile 5 arasında olmalıdır.");

            var order = await _context.WorkOrders
                .Include(wo => wo.Asset)
                .Include(wo => wo.CreatedByUser)
                .Include(wo => wo.AssignedToUser)
                .FirstOrDefaultAsync(wo => wo.Id == id);
            if (order == null) return NotFound();

            if (!ModelState.IsValid)
            {
                ViewBag.Assignees = await _userManager.GetUsersInRoleAsync("Technician");
                return View(order);
            }

            var user = await _userManager.GetUserAsync(User);
            await _workOrderService.InspectAsync(id, user.Id, rating, comments);

            return RedirectToAction(nameof(Index));
        }

        // Tamamlama: Manager ve atanan teknisyen tamamlayabilsin
        [HttpPost]
        [ValidateAntiForgeryToken]
        [Authorize(Roles = "Manager,Technician")]
        public async Task<IActionResult> Complete(int id, string repairReport)
        {
            if (string.IsNullOrWhiteSpace(repairReport))
            {
                ModelState.AddModelError(nameof(repairReport), "Onarım raporu zorunludur.");
                return await ViewTask(id);
            }

            var currentUser = await _userManager.GetUserAsync(User);
            var roles = await _userManager.GetRolesAsync(currentUser);
            await _workOrderService.CompleteAsync(id, currentUser.Id, roles, repairReport);
            return RedirectToAction(nameof(ViewTask), new { id });
        }

        // Ekipman ekleme: sadece atanan teknisyen veya manager
        [HttpPost]
        [ValidateAntiForgeryToken]
        [Authorize(Roles = "Manager,Technician")]
        public async Task<IActionResult> AddEquipment(int id, int equipmentId, string? usageNotes)
        {
            var currentUser = await _userManager.GetUserAsync(User);
            var roles = await _userManager.GetRolesAsync(currentUser);

            try
            {
                await _equipmentService.AddToWorkOrderAsync(id, equipmentId, usageNotes, currentUser.Id, roles);
                return RedirectToAction(nameof(ViewTask), new { id });
            }
            catch (UnauthorizedAccessException)
            {
                return Forbid();
            }
            catch (InvalidOperationException ex)
            {
                return BadRequest(ex.Message);
            }
            catch (KeyNotFoundException)
            {
                return NotFound();
            }
        }

        // Parça ekleme: sadece atanan teknisyen veya manager
        [HttpPost]
        [ValidateAntiForgeryToken]
        [Authorize(Roles = "Manager,Technician")]
        public async Task<IActionResult> AddSparePart(int id, int sparePartId, int quantityUsed)
        {
            var currentUser = await _userManager.GetUserAsync(User);
            var roles = await _userManager.GetRolesAsync(currentUser);

            try
            {
                await _sparePartService.AddToWorkOrderAsync(id, sparePartId, quantityUsed, currentUser.Id, roles);
                return RedirectToAction(nameof(ViewTask), new { id });
            }
            catch (ArgumentOutOfRangeException)
            {
                return BadRequest("Miktar pozitif olmalı.");
            }
            catch (UnauthorizedAccessException)
            {
                return Forbid();
            }
            catch (InvalidOperationException ex)
            {
                return BadRequest(ex.Message);
            }
            catch (KeyNotFoundException)
            {
                return NotFound();
            }
        }

        // Edit post: Manager
        [Authorize(Roles = "Manager")]
        public async Task<IActionResult> EditOrder(WorkOrder model)
        {
            var workOrder = await _context.WorkOrders
                .Include(wo => wo.Asset)
                .Include(wo => wo.CreatedByUser)
                .Include(wo => wo.AssignedToUser)
                .FirstOrDefaultAsync(wo => wo.Id == model.Id);

            if (workOrder == null) return NotFound();

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

            var manager = await _userManager.GetUserAsync(User);
            await _workOrderService.UpdateAssignmentAndPriorityAsync(
                workOrderId: model.Id,
                assignedToUserId: model.AssignedToUserId,
                priority: model.Priority,
                managerUserId: manager.Id);

            return RedirectToAction(nameof(Index));
        }
    }
}