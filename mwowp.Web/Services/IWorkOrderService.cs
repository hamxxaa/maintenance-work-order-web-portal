using mwowp.Web.Models;

namespace mwowp.Web.Services
{
    public interface IWorkOrderService
    {
        Task<WorkOrder> CreateAsync(WorkOrder input, string userId);
        Task AssignAsync(int workOrderId, string assignedToUserId, WorkOrderStatus status);
        Task CompleteAsync(int workOrderId, string currentUserId, IEnumerable<string> roles, string repairReport);
        Task<Inspection> InspectAsync(int workOrderId, string inspectorId, int rating, string comments);
        Task UpdateAssignmentAndPriorityAsync(int workOrderId, string? assignedToUserId, PriorityLevel? priority, string managerUserId);
        Task CancelWorkOrderAsync(int workOrderId, string cancelledByUserId);
    }
}