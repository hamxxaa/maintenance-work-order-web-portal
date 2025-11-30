using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;
using mwowp.Web.Data;
using mwowp.Web.Hubs;
using mwowp.Web.Models;
using System.Text;

namespace mwowp.Web.Services
{
    public sealed class WorkOrderService : IWorkOrderService
    {
        private readonly ApplicationDbContext _db;
        private readonly IWorkOrderHistoryService _history;
        private readonly IHubContext<WorkOrderHub> _hubContext;


        public WorkOrderService(ApplicationDbContext db, IWorkOrderHistoryService history, IHubContext<WorkOrderHub> hubContext)
        {
            _db = db;
            _history = history;
            _hubContext = hubContext;
        }

        public async Task<WorkOrder> CreateAsync(WorkOrder input, string userId)
        {
            input.CreatedByUserId = userId;
            input.Status = WorkOrderStatus.Created;
            input.CreatedAt = DateTime.UtcNow;

            var asset = await _db.Assets.AsNoTracking().FirstOrDefaultAsync(a => a.Id == input.AssetId);
            if (asset != null)
            {
                asset.Status = AssetStatus.OnRepair;
                _db.Assets.Update(asset);
                input.Asset = asset;
            }

            _db.WorkOrders.Add(input);
            await _db.SaveChangesAsync();
            await _history.LogCreatedAsync(input, userId);
            await _hubContext.Clients.Group("Managers").SendAsync(
                "WorkOrderCreated",
                new
                {
                    id = input.Id,
                    title = input.Title,
                    assetName = input.Asset?.Name,
                    createdBy = input.CreatedByUser?.FullName ?? input.CreatedByUserId,
                    priority = input.Priority?.ToString() ?? "-",
                    createdAt = input.CreatedAt.ToLocalTime().ToString("g")
                });
            return input;
        }

        public async Task AssignAsync(int workOrderId, string assignedToUserId, WorkOrderStatus status)
        {
            var wo = await _db.WorkOrders.FindAsync(workOrderId);
            if (wo == null) throw new KeyNotFoundException();

            var oldAssignee = wo.AssignedToUserId;
            var oldStatus = wo.Status;
            var manager = wo.AssignedById;

            wo.AssignedToUserId = assignedToUserId;
            wo.Status = status;
            await _db.SaveChangesAsync();

            if (oldAssignee != assignedToUserId)
            {
                await _history.LogAssignmentAsync(wo, manager, oldAssignee, assignedToUserId);
                await _hubContext.Clients.User(assignedToUserId).SendAsync(
                    "WorkOrderAssigned",
                    new
                    {
                        id = wo.Id,
                        title = wo.Title,
                        assetName = wo.Asset?.Name,
                        assignedBy = manager,
                        priority = wo.Priority?.ToString() ?? "-",
                        status = wo.Status.ToString(),
                        createdBy = wo.CreatedByUser?.FullName ?? wo.CreatedByUserId
                    });
            }
            if (oldStatus != status)
            {
                await _history.LogStatusAsync(wo, manager, oldStatus, status);
            }

        }

        public async Task CompleteAsync(int workOrderId, string currentUserId, IEnumerable<string> roles, string repairReport)
        {
            var wo = await _db.WorkOrders
                .Include(w => w.Asset)
                .Include(w => w.CreatedByUser)
                .Include(w => w.AssignedToUser)
                .Include(w => w.WorkOrderEquipments)
                    .ThenInclude(we => we.Equipment)
                .Include(w => w.WorkOrderSpareParts)
                    .ThenInclude(wosp => wosp.SparePart)
                .FirstOrDefaultAsync(w => w.Id == workOrderId);
            if (wo == null) throw new KeyNotFoundException();

            var canComplete = roles.Contains("Manager") || wo.AssignedToUserId == currentUserId;
            if (!canComplete) throw new UnauthorizedAccessException();

            if (wo.Status != WorkOrderStatus.Completed &&
                wo.Status != WorkOrderStatus.Inspected &&
                wo.Status != WorkOrderStatus.Canceled)
            {
                wo.Status = WorkOrderStatus.Completed;
                wo.CompletedAt = DateTime.UtcNow;
                wo.RepairReport = repairReport;
            }

            if (wo.WorkOrderEquipments != null && wo.WorkOrderEquipments.Any())
            {
                var now = DateTime.UtcNow;
                foreach (var woe in wo.WorkOrderEquipments.Where(e => !e.ReturnedAt.HasValue))
                {
                    woe.ReturnedAt = now;
                    if (woe.Equipment != null && woe.Equipment.Status == EquipmentStatus.InUse)
                    {
                        woe.Equipment.Status = EquipmentStatus.Available;
                    }
                }
            }

            if (wo.Status == WorkOrderStatus.Completed)
            {
                var alreadyInvoiced = await _db.Invoices.AsNoTracking().AnyAsync(i => i.WorkOrderId == wo.Id);
                if (!alreadyInvoiced)
                {
                    var approvedSpareParts = wo.WorkOrderSpareParts?
                        .Where(x => x.Status == SparePartStatus.Approved)
                        .ToList() ?? new List<WorkOrderSparePart>();

                    var effectivePriority = wo.Priority ?? PriorityLevel.Medium;
                    var serviceFee = FeeDefinitions.GetFee(effectivePriority);

                    decimal sparePartsTotal = approvedSpareParts.Sum(sp =>
                    {
                        var unit = sp.SparePart?.UnitPrice ?? 0m;
                        return unit * sp.QuantityUsed;
                    });

                    var grandTotal = serviceFee + sparePartsTotal;

                    var sb = new StringBuilder();
                    sb.AppendLine("=== Ýþ Emri Faturasý ===");
                    sb.AppendLine($"Fatura No: WO-{wo.Id:D6}");
                    sb.AppendLine($"Baþlýk: {wo.Title}");
                    sb.AppendLine($"Açýklama: {wo.Description}");
                    sb.AppendLine($"Durum: {wo.Status}");
                    sb.AppendLine($"Öncelik: {effectivePriority}");
                    sb.AppendLine($"Varlýk: {(wo.Asset?.Name ?? "-")} (Id: {wo.AssetId})");
                    sb.AppendLine($"Oluþturan: {wo.CreatedByUser?.UserName ?? "-"}");
                    sb.AppendLine($"Atanan: {wo.AssignedToUser?.UserName ?? "-"}");
                    sb.AppendLine($"Oluþturulma: {wo.CreatedAt.ToLocalTime()}");
                    if (wo.CompletedAt.HasValue)
                    {
                        sb.AppendLine($"Tamamlanma: {wo.CompletedAt.Value.ToLocalTime()}");
                    }
                    sb.AppendLine();

                    sb.AppendLine("--- Kalemler ---");
                    sb.AppendLine($"Servis Ücreti ({effectivePriority}): {serviceFee.ToString()}$");
                    if (approvedSpareParts.Any())
                    {
                        sb.AppendLine("Onaylanmýþ Yedek Parçalar:");
                        foreach (var sp in approvedSpareParts)
                        {
                            var unit = sp.SparePart?.UnitPrice ?? 0m;
                            var total = unit * sp.QuantityUsed;
                            sb.AppendLine($" - {sp.SparePart?.Name} | Miktar: {sp.QuantityUsed} | Birim: {unit.ToString()}$ | Toplam: {total.ToString()}$");
                        }
                        sb.AppendLine($"Yedek Parça Toplamý: {sparePartsTotal.ToString()}$");
                    }
                    else
                    {
                        sb.AppendLine("Onaylanmýþ yedek parça bulunmuyor.");
                    }

                    sb.AppendLine();
                    sb.AppendLine($"GENEL TOPLAM: {grandTotal.ToString()}$");
                    sb.AppendLine("=========================");

                    var invoice = new Invoice
                    {
                        UserId = wo.CreatedByUserId,
                        WorkOrderId = wo.Id,
                        InvoiceText = sb.ToString(),
                        InvoiceDate = DateTime.UtcNow
                    };
                    _db.Invoices.Add(invoice);
                }
            }

            await _db.SaveChangesAsync();
            await _history.LogCompletedAsync(workOrderId, currentUserId);

            await _hubContext.Clients.User(wo.AssignedById).SendAsync(
                "WorkOrderCompleted",
                new
                {
                    id = wo.Id,
                    title = wo.Title,
                    assetName = wo.Asset?.Name,
                    completedBy = wo.AssignedToUser?.FullName ?? currentUserId,
                    priority = wo.Priority?.ToString() ?? "-",
                    completedAt = wo.CompletedAt?.ToLocalTime().ToString("g")
                });
        }

        public async Task<Inspection> InspectAsync(int workOrderId, string inspectorId, int rating, string comments)
        {
            var order = await _db.WorkOrders
                .Include(wo => wo.Asset)
                .Include(wo => wo.CreatedByUser)
                .Include(wo => wo.AssignedToUser)
                .FirstOrDefaultAsync(wo => wo.Id == workOrderId);

            if (order == null) throw new KeyNotFoundException();

            var inspection = new Inspection
            {
                WorkOrderId = workOrderId,
                InspectorId = inspectorId,
                InspectionDate = DateTime.UtcNow,
                Rating = rating,
                Comments = comments
            };
            order.Status = WorkOrderStatus.Inspected;
            order.Asset.Status = AssetStatus.SentToOwner;

            _db.Inspections.Add(inspection);
            await _db.SaveChangesAsync();
            await _history.LogInspectionAsync(workOrderId, inspectorId);

            var recipientIds = new List<string>();
            if (!string.IsNullOrWhiteSpace(order.AssignedToUserId))
                recipientIds.Add(order.AssignedToUserId);
            if (!string.IsNullOrWhiteSpace(order.CreatedByUserId))
                recipientIds.Add(order.CreatedByUserId);

            if (recipientIds.Count > 0)
            {
                await _hubContext.Clients.Users(recipientIds).SendAsync(
                    "WorkOrderInspected",
                    new
                    {
                        id = order.Id,
                        title = order.Title,
                        assetName = order.Asset?.Name,
                        inspectedBy = inspection.InspectorId,
                        rating,
                        comments
                    });
            }
            return inspection;
        }

        public async Task UpdateAssignmentAndPriorityAsync(int workOrderId, string? assignedToUserId, PriorityLevel? priority, string managerUserId)
        {
            var workOrder = await _db.WorkOrders
                .Include(wo => wo.Asset)
                .Include(wo => wo.CreatedByUser)
                .Include(wo => wo.AssignedToUser)
                .FirstOrDefaultAsync(wo => wo.Id == workOrderId);

            if (workOrder == null) throw new KeyNotFoundException();

            var oldAssignee = workOrder.AssignedToUserId;
            var oldPriority = workOrder.Priority;
            var oldStatus = workOrder.Status;

            workOrder.Priority = priority;
            var effectivePriority = workOrder.Priority ?? PriorityLevel.Medium;
            workOrder.SLAEndTime = SlaDefinitions.GetSlaEndDate(workOrder.CreatedAt, effectivePriority);

            workOrder.AssignedToUserId = assignedToUserId;
            workOrder.AssignedById = managerUserId;
            workOrder.Status = WorkOrderStatus.Assigned;

            await _db.SaveChangesAsync();

            if (oldAssignee != assignedToUserId)
            {
                await _history.LogAssignmentAsync(workOrder, managerUserId, oldAssignee, assignedToUserId);
                await _hubContext.Clients.User(assignedToUserId).SendAsync(
                    "WorkOrderAssigned",
                    new
                    {
                        id = workOrder.Id,
                        title = workOrder.Title,
                        assetName = workOrder.Asset?.Name,
                        assignedBy = managerUserId,
                        priority = workOrder.Priority?.ToString() ?? "-",
                        status = workOrder.Status.ToString(),
                        createdBy = workOrder.CreatedByUser?.FullName ?? workOrder.CreatedByUserId
                    });
            }
            if (oldPriority != priority)
            {
                await _history.LogPriorityAsync(workOrder, managerUserId, oldPriority, priority);
            }
            if (oldStatus != workOrder.Status)
            {
                await _history.LogStatusAsync(workOrder, managerUserId, oldStatus, workOrder.Status);
            }
        }

        public async Task CancelWorkOrderAsync(int workOrderId, string cancelledByUserId)
        {
            var wo = await _db.WorkOrders
                .Include(w => w.Asset)
                .Include(w => w.CreatedByUser)
                .Include(w => w.AssignedToUser)
                .FirstOrDefaultAsync(w => w.Id == workOrderId);
            if (wo == null) throw new KeyNotFoundException();

            // Yetki: iþ emrini oluþturan veya (opsiyonel) atayan manager iptal edebilir.
            var isOwner = wo.CreatedByUserId == cancelledByUserId;
            var isManagerAssigner = wo.AssignedById == cancelledByUserId;
            if (!isOwner && !isManagerAssigner)
                throw new UnauthorizedAccessException();

            if (wo.Status == WorkOrderStatus.Completed ||
                wo.Status == WorkOrderStatus.Inspected ||
                wo.Status == WorkOrderStatus.Canceled)
                throw new InvalidOperationException("Bu durumdaki iþ emri iptal edilemez.");

            var oldStatus = wo.Status;
            wo.Status = WorkOrderStatus.Canceled;

            if (wo.WorkOrderEquipments != null && wo.WorkOrderEquipments.Any())
            {
                var now = DateTime.UtcNow;
                foreach (var woe in wo.WorkOrderEquipments.Where(e => !e.ReturnedAt.HasValue))
                {
                    woe.ReturnedAt = now;
                    if (woe.Equipment != null && woe.Equipment.Status == EquipmentStatus.InUse)
                    {
                        woe.Equipment.Status = EquipmentStatus.Available;
                    }
                }
            }

            await _db.SaveChangesAsync();
            await _history.LogStatusAsync(wo, cancelledByUserId, oldStatus, WorkOrderStatus.Canceled);

            // Atanmýþ teknisyene iptal bildirimi
            if (!string.IsNullOrWhiteSpace(wo.AssignedToUserId))
            {
                await _hubContext.Clients.User(wo.AssignedToUserId).SendAsync(
                    "WorkOrderCanceled",
                    new
                    {
                        id = wo.Id,
                        title = wo.Title,
                        assetName = wo.Asset?.Name,
                        canceledBy = cancelledByUserId,
                        previousStatus = oldStatus.ToString()
                    });
            }
        }
    }
}