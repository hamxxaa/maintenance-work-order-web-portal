using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using mwowp.Web.Data;
using mwowp.Web.Models;
using System.Security.Claims;
using Microsoft.AspNetCore.Http;
using System.IO;
using Microsoft.AspNetCore.Authorization;

namespace mwowp.Web.Controllers
{
    [Authorize] // Tüm controller için kimlik doğrulama şart
    public class WorkOrderController : Controller
    {
        private readonly ApplicationDbContext _context;
        private readonly UserManager<ApplicationUser> _userManager;
        private readonly IWebHostEnvironment _env;

        public WorkOrderController(ApplicationDbContext context, UserManager<ApplicationUser> userManager, IWebHostEnvironment env)
        {
            _context = context;
            _userManager = userManager;
            _env = env;
        }

        // Sadece normal kullanıcılar iş emri oluşturabilir
        [Authorize(Roles = "User")]
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
        [Authorize(Roles = "User")]
        public async Task<IActionResult> Create([Bind("AssetId,Title,Description,Priority")] WorkOrder workOrder, List<IFormFile>? images)
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
                if (asset != null)
                {
                    asset.Status = AssetStatus.OnRepair;
                    _context.Assets.Update(asset);
                    workOrder.Asset = asset;
                }
                workOrder.CreatedAt = DateTime.UtcNow;
                _context.WorkOrders.Add(workOrder);
                await _context.SaveChangesAsync();

                // Resim yükleme
                if (images != null && images.Count > 0)
                {
                    var allowedExt = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { ".jpg", ".jpeg", ".png", ".gif", ".webp" };
                    var maxSizeBytes = 5 * 1024 * 1024; // 5MB
                    var uploadRoot = Path.Combine(_env.WebRootPath, "uploads", "workorders", workOrder.Id.ToString());
                    Directory.CreateDirectory(uploadRoot);

                    var attachments = new List<WorkOrderAttachment>();

                    foreach (var img in images)
                    {
                        if (img.Length == 0) continue;
                        if (!img.ContentType.StartsWith("image/", StringComparison.OrdinalIgnoreCase)) continue;
                        if (img.Length > maxSizeBytes) continue;

                        var ext = Path.GetExtension(img.FileName);
                        if (!allowedExt.Contains(ext)) continue;

                        var fileName = $"{Guid.NewGuid()}{ext}";
                        var physicalPath = Path.Combine(uploadRoot, fileName);
                        using (var fs = new FileStream(physicalPath, FileMode.Create))
                        {
                            await img.CopyToAsync(fs);
                        }

                        // Göreli path (wwwroot sonrası)
                        var relativePath = Path.Combine("uploads", "workorders", workOrder.Id.ToString(), fileName).Replace("\\", "/");

                        attachments.Add(new WorkOrderAttachment
                        {
                            WorkOrderId = workOrder.Id,
                            FilePath = relativePath,
                            CreatedAt = DateTime.UtcNow
                        });
                    }

                    if (attachments.Count > 0)
                    {
                        _context.WorkOrderAttachments.AddRange(attachments);
                        await _context.SaveChangesAsync();
                    }
                }

                return RedirectToAction(nameof(MyOrders));
            }

            var currentUserId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            ViewBag.Assets = await _context.Assets
                                    .Where(a => a.OwnerUserId == currentUserId)
                                    .ToListAsync();
            return View(workOrder);
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
            if (order == null)
                return NotFound();

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
                                .FirstOrDefaultAsync(wo => wo.Id == id);
            if (order == null)
                return NotFound();

            var currentUser = await _userManager.GetUserAsync(User);
            var roles = await _userManager.GetRolesAsync(currentUser);

            var canView =
                roles.Contains("Manager") ||
                order.AssignedToUserId == currentUser.Id ||
                order.CreatedByUserId == currentUser.Id;

            if (!canView)
                return Forbid();

            ViewBag.Equipments = await _context.Equipments
                                        .Where(e => e.Status == EquipmentStatus.Available)
                                        .ToListAsync();

            ViewBag.SpareParts = await _context.SpareParts
                                        .Where(sp => sp.Stock > 0)
                                        .ToListAsync();
            ViewBag.WorkOrderEquipments = await _context.WorkOrderEquipments
                                        .Where(woe => woe.WorkOrderId == id)
                                        .Include(woe => woe.Equipment)
                                        .ToListAsync();
            ViewBag.WorkOrderSpareParts = await _context.WorkOrderSpareParts
                                        .Where(wosp => wosp.WorkOrderId == id)
                                        .Include(wosp => wosp.SparePart)
                                        .ToListAsync();
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
            if (order == null)
                return NotFound();

            ViewBag.Assignees = await _userManager.GetUsersInRoleAsync("Technician");
            return View(order);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        [Authorize(Roles = "Manager")]
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

        // Tamamlama: Manager ve atanan teknisyen tamamlayabilsin
        [HttpPost]
        [ValidateAntiForgeryToken]
        [Authorize(Roles = "Manager,Technician")]
        public async Task<IActionResult> Complete(int id)
        {
            var workOrder = await _context.WorkOrders.FindAsync(id);
            if (workOrder == null)
                return NotFound();

            var currentUser = await _userManager.GetUserAsync(User);
            var roles = await _userManager.GetRolesAsync(currentUser);

            var canComplete =
                roles.Contains("Manager") ||
                workOrder.AssignedToUserId == currentUser.Id;

            if (!canComplete)
                return Forbid();

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

        // Ekipman ekleme: sadece atanan teknisyen veya manager
        [HttpPost]
        [ValidateAntiForgeryToken]
        [Authorize(Roles = "Manager,Technician")]
        public async Task<IActionResult> AddEquipment(int id, int equipmentId, string? usageNotes)
        {
            var workOrder = await _context.WorkOrders.FindAsync(id);
            if (workOrder == null)
                return NotFound();

            var currentUser = await _userManager.GetUserAsync(User);
            var roles = await _userManager.GetRolesAsync(currentUser);

            var canModify =
                roles.Contains("Manager") ||
                workOrder.AssignedToUserId == currentUser.Id;

            if (!canModify)
                return Forbid();

            if (workOrder.Status == WorkOrderStatus.Completed)
                return BadRequest("Tamamlanan iş emrine ekipman eklenemez.");

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

        // Parça ekleme: sadece atanan teknisyen veya manager
        [HttpPost]
        [ValidateAntiForgeryToken]
        [Authorize(Roles = "Manager,Technician")]
        public async Task<IActionResult> AddSparePart(int id, int sparePartId, int quantityUsed)
        {
            if (quantityUsed <= 0)
                return BadRequest("Miktar pozitif olmalı.");

            var workOrder = await _context.WorkOrders.FindAsync(id);
            if (workOrder == null)
                return NotFound();

            var currentUser = await _userManager.GetUserAsync(User);
            var roles = await _userManager.GetRolesAsync(currentUser);

            var canModify =
                roles.Contains("Manager") ||
                workOrder.AssignedToUserId == currentUser.Id;

            if (!canModify)
                return Forbid();

            if (workOrder.Status == WorkOrderStatus.Completed)
                return BadRequest("Tamamlanan iş emrine parça eklenemez.");

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

        // Edit post: Manager
        [Authorize(Roles = "Manager")]
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
            var effectivePriority = workOrder.Priority ?? PriorityLevel.Medium;
            workOrder.SLAEndTime = SlaDefinitions.GetSlaEndDate(workOrder.CreatedAt, effectivePriority);
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
        [Authorize(Roles = "Manager")]
        public async Task<IActionResult> Assign(int id)
        {
            var workOrder = await _context.WorkOrders
                                .Include(wo => wo.Asset)
                                .Include(wo => wo.AssignedToUser)
                                .FirstOrDefaultAsync(wo => wo.Id == id);

            if (workOrder == null)
                return NotFound();

            // Teknik personel listesi
            ViewBag.Technicians = await _userManager.GetUsersInRoleAsync("Technician");

            return View(workOrder);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        [Authorize(Roles = "Manager")]
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