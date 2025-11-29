using Microsoft.EntityFrameworkCore;
using mwowp.Web.Data;
using mwowp.Web.Models;

namespace mwowp.Web.Services
{
    public sealed class WorkOrderService : IWorkOrderService
    {
        private readonly ApplicationDbContext _db;
        private readonly IWorkOrderHistoryService _history;

        public WorkOrderService(ApplicationDbContext db, IWorkOrderHistoryService history)
        {
            _db = db;
            _history = history;
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
            }
            if (oldStatus != status)
            {
                await _history.LogStatusAsync(wo, manager, oldStatus, status);
            }

        }

        public async Task CompleteAsync(int workOrderId, string currentUserId, IEnumerable<string> roles, string repairReport)
        {
            var wo = await _db.WorkOrders
                .Include(w => w.WorkOrderEquipments)
                    .ThenInclude(we => we.Equipment)
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

            await _db.SaveChangesAsync();
            await _history.LogCompletedAsync(workOrderId, currentUserId);
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

            _db.Inspections.Add(inspection);
            await _db.SaveChangesAsync();
            await _history.LogInspectionAsync(workOrderId, inspectorId);
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

            // Güncelle
            workOrder.Priority = priority;
            var effectivePriority = workOrder.Priority ?? PriorityLevel.Medium;
            workOrder.SLAEndTime = SlaDefinitions.GetSlaEndDate(workOrder.CreatedAt, effectivePriority);

            workOrder.AssignedToUserId = assignedToUserId;
            workOrder.AssignedById = managerUserId;
            workOrder.Status = WorkOrderStatus.Assigned;

            await _db.SaveChangesAsync();

            // History loglarý
            if (oldAssignee != assignedToUserId)
            {
                await _history.LogAssignmentAsync(workOrder, managerUserId, oldAssignee, assignedToUserId);
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
    }
}