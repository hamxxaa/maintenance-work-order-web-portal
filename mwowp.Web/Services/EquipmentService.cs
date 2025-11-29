using Microsoft.EntityFrameworkCore;
using mwowp.Web.Data;
using mwowp.Web.Models;

namespace mwowp.Web.Services
{
    public sealed class EquipmentService : IEquipmentService
    {
        private readonly ApplicationDbContext _db;
        private readonly IWorkOrderHistoryService _history;

        public EquipmentService(ApplicationDbContext db, IWorkOrderHistoryService history)
        {
            _db = db;
            _history = history;
        }

        public async Task AddToWorkOrderAsync(int workOrderId, int equipmentId, string? usageNotes, string currentUserId, IEnumerable<string> roles)
        {
            var workOrder = await _db.WorkOrders.FindAsync(workOrderId) ?? throw new KeyNotFoundException();
            var canModify = roles.Contains("Manager") || workOrder.AssignedToUserId == currentUserId;
            if (!canModify) throw new UnauthorizedAccessException();
            if (workOrder.Status == WorkOrderStatus.Completed) throw new InvalidOperationException("Tamamlanan iþ emrine ekipman eklenemez.");

            var equipment = await _db.Equipments.FirstOrDefaultAsync(e => e.Id == equipmentId) ?? throw new KeyNotFoundException();

            var woe = new WorkOrderEquipment
            {
                WorkOrderId = workOrderId,
                EquipmentId = equipmentId,
                UsageNotes = usageNotes ?? string.Empty,
                AssignedAt = DateTime.UtcNow,
                UsedAt = DateTime.UtcNow
            };
            equipment.Status = EquipmentStatus.InUse;

            _db.WorkOrderEquipments.Add(woe);
            _db.Equipments.Update(equipment);

            if (workOrder.Status == WorkOrderStatus.Assigned)
                workOrder.Status = WorkOrderStatus.InProgress;

            await _db.SaveChangesAsync();
            await _history.LogEquipmentAddedAsync(workOrderId, currentUserId);
        }
    }
}