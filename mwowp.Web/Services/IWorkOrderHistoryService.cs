using mwowp.Web.Models;

namespace mwowp.Web.Services
{
    public interface IWorkOrderHistoryService
    {
        Task LogAsync(int workOrderId, string changedByUserId, string action, string? oldValue, string? newValue);
        Task LogCreatedAsync(WorkOrder workOrder, string createdByUserId);
        Task LogStatusAsync(WorkOrder workOrder, string changedByUserId, WorkOrderStatus? oldStatus, WorkOrderStatus? newStatus);
        Task LogAssignmentAsync(WorkOrder workOrder, string changedByUserId, string? oldAssigneeId, string? newAssigneeId);
        Task LogPriorityAsync(WorkOrder workOrder, string changedByUserId, PriorityLevel? oldPriority, PriorityLevel? newPriority);
        Task LogEquipmentAddedAsync(int workOrderId, string changedByUserId);
        Task LogSparePartAddedAsync(int workOrderId, string changedByUserId);
        Task LogAttachmentAddedAsync(int workOrderId, string filePath);
        Task LogInspectionAsync(int workOrderId, string changedByUserId);
        Task LogCompletedAsync(int workOrderId, string changedByUserId);
    }
}