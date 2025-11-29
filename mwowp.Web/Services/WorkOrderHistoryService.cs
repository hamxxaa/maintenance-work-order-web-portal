using Microsoft.EntityFrameworkCore;
using mwowp.Web.Data;
using mwowp.Web.Models;

namespace mwowp.Web.Services
{
    public class WorkOrderHistoryService : IWorkOrderHistoryService
    {
        private readonly ApplicationDbContext _context;

        public WorkOrderHistoryService(ApplicationDbContext context)
        {
            _context = context;
        }

        public async Task LogAsync(int workOrderId, string? changedByUserId, string action, string? oldValue, string? newValue)
        {
            var entry = new WorkOrderHistory
            {
                WorkOrderId = workOrderId,
                ChangedByUserId = changedByUserId,
                Action = action,
                OldValue = oldValue,
                NewValue = newValue,
                CreatedAt = DateTime.UtcNow
            };

            _context.WorkOrderHistories.Add(entry);
            await _context.SaveChangesAsync();
        }

        public async Task LogCreatedAsync(WorkOrder workOrder, string createdByUserId)
            => await LogAsync(workOrder.Id, createdByUserId, "Created", null, null);

        public async Task LogStatusAsync(WorkOrder workOrder, string changedByUserId, WorkOrderStatus? oldStatus, WorkOrderStatus? newStatus)
            => await LogAsync(workOrder.Id, changedByUserId, "StatusChanged", oldStatus.ToString(), newStatus.ToString());

        public async Task LogAssignmentAsync(WorkOrder workOrder, string changedByUserId, string? oldAssigneeId, string? newAssigneeId)
            => await LogAsync(workOrder.Id, changedByUserId, "AssignedTo", oldAssigneeId, newAssigneeId);

        public async Task LogPriorityAsync(WorkOrder workOrder, string changedByUserId, PriorityLevel? oldPriority, PriorityLevel? newPriority)
            => await LogAsync(workOrder.Id, changedByUserId, "PriorityChanged", oldPriority.ToString(), newPriority.ToString());

        public async Task LogEquipmentAddedAsync(int workOrderId, string changedByUserId)
            => await LogAsync(workOrderId, changedByUserId, "EquipmentAdded", null, null);

        public async Task LogSparePartRequestedAsync(int workOrderId, string changedByUserId)
            => await LogAsync(workOrderId, changedByUserId, "SparePartRequested", null, null);

        public async Task LogAttachmentAddedAsync(int workOrderId, string filePath)
            => await LogAsync(workOrderId, null, "AttachmentAdded", null, filePath);

        public async Task LogInspectionAsync(int workOrderId, string changedByUserId)
            => await LogAsync(workOrderId, changedByUserId, "InspectionRecorded", null, null);

        public async Task LogCompletedAsync(int workOrderId, string changedByUserId)
            => await LogAsync(workOrderId, changedByUserId, "Completed", null ,null);

        public async Task LogSparePartApprovedAsync(int workOrderId, string changedByUserId)
            => await LogAsync(workOrderId, changedByUserId, "SparePartApproved", null, null);

        public async Task LogSparePartRejectedAsync(int workOrderId, string changedByUserId)
    => await LogAsync(workOrderId, changedByUserId, "SparePartRejected", null, null);
    }
}