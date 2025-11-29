using Microsoft.EntityFrameworkCore;
using mwowp.Web.Data;
using mwowp.Web.Models;

namespace mwowp.Web.Services
{
    public sealed class SparePartService : ISparePartService
    {
        private readonly ApplicationDbContext _db;
        private readonly IWorkOrderHistoryService _history;

        public SparePartService(ApplicationDbContext db, IWorkOrderHistoryService history)
        {
            _db = db;
            _history = history;
        }

        public async Task AddToWorkOrderAsync(int workOrderId, int sparePartId, int quantityUsed, string currentUserId, IEnumerable<string> roles)
        {
            if (quantityUsed <= 0) throw new ArgumentOutOfRangeException(nameof(quantityUsed));

            var workOrder = await _db.WorkOrders.FindAsync(workOrderId) ?? throw new KeyNotFoundException();
            var canModify = roles.Contains("Manager") || workOrder.AssignedToUserId == currentUserId;
            if (!canModify) throw new UnauthorizedAccessException();
            if (workOrder.Status == WorkOrderStatus.Completed) throw new InvalidOperationException("Tamamlanan iþ emrine parça eklenemez.");

            var sparePart = await _db.SpareParts.FirstOrDefaultAsync(sp => sp.Id == sparePartId) ?? throw new KeyNotFoundException();

            var wosp = new WorkOrderSparePart
            {
                WorkOrderId = workOrderId,
                SparePartId = sparePartId,
                QuantityUsed = quantityUsed
            };
            _db.WorkOrderSpareParts.Add(wosp);

            if (workOrder.Status == WorkOrderStatus.Assigned || workOrder.Status == WorkOrderStatus.InProgress)
                workOrder.Status = WorkOrderStatus.PartsOrdered;

            await _db.SaveChangesAsync();
            await _history.LogSparePartAddedAsync(workOrderId, currentUserId);
        }
    }
}