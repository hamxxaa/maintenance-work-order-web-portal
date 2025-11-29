using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.SignalR;
using mwowp.Web.Data;
using mwowp.Web.Models;
using mwowp.Web.Hubs;

namespace mwowp.Web.Services
{
    public sealed class SparePartService : ISparePartService
    {
        private readonly ApplicationDbContext _db;
        private readonly IWorkOrderHistoryService _history;
        private readonly IHubContext<WorkOrderHub> _hubContext;

        public SparePartService(
            ApplicationDbContext db,
            IWorkOrderHistoryService history,
            IHubContext<WorkOrderHub> hubContext)
        {
            _db = db;
            _history = history;
            _hubContext = hubContext;
        }

        public async Task AddRequestToWorkOrderAsync(int workOrderId, int sparePartId, int quantityUsed, string currentUserId, IEnumerable<string> roles)
        {
            if (quantityUsed <= 0) throw new ArgumentOutOfRangeException(nameof(quantityUsed));

            var workOrder = await _db.WorkOrders.FindAsync(workOrderId) ?? throw new KeyNotFoundException();
            var canModify = roles.Contains("Manager") || workOrder.AssignedToUserId == currentUserId;
            if (!canModify) throw new UnauthorizedAccessException();
            if (workOrder.Status == WorkOrderStatus.Completed) throw new InvalidOperationException("Tamamlanan iþ emrine parça eklenemez.");

            var sparePart = await _db.SpareParts.FirstOrDefaultAsync(sp => sp.Id == sparePartId) ?? throw new KeyNotFoundException();

            if (sparePart.Stock < quantityUsed)
                throw new InvalidOperationException($"Yetersiz stok: istenen miktar {quantityUsed}, mevcut stok {sparePart.Stock}.");

            var wosp = new WorkOrderSparePart
            {
                WorkOrderId = workOrderId,
                SparePartId = sparePartId,
                QuantityUsed = quantityUsed,
                Status = SparePartStatus.Requested
            };

            _db.WorkOrderSpareParts.Add(wosp);

            if (workOrder.Status == WorkOrderStatus.Assigned || workOrder.Status == WorkOrderStatus.InProgress)
                workOrder.Status = WorkOrderStatus.PartsOrdered;

            await _db.SaveChangesAsync();
            await _history.LogSparePartRequestedAsync(workOrderId, currentUserId);

            // Ýstek oluþtuðunda iþ emrini oluþturan kullanýcýya bildirim
            if (!string.IsNullOrWhiteSpace(workOrder.CreatedByUserId))
            {
                await _hubContext.Clients.User(workOrder.CreatedByUserId).SendAsync(
                    "SparePartRequested",
                    new
                    {
                        workOrderId,
                        sparePartId,
                        partName = sparePart.Name,
                        quantity = quantityUsed,
                        requestedBy = currentUserId,
                        status = wosp.Status.ToString()
                    });
            }
        }

        public async Task ApproveSparePartAsync(int workOrderSparePartId, string currentUserId)
        {
            var wosp = await _db.WorkOrderSpareParts
                .Include(x => x.WorkOrder)
                .Include(x => x.SparePart)
                .FirstOrDefaultAsync(x => x.Id == workOrderSparePartId)
                ?? throw new KeyNotFoundException();

            if (wosp.Status != SparePartStatus.Requested)
                throw new InvalidOperationException("Yalnýzca 'Requested' durumundaki parça talepleri onaylanabilir.");

            var workOrder = wosp.WorkOrder ?? throw new InvalidOperationException("Ýþ emri iliþkilendirmesi bulunamadý.");
            if (workOrder.CreatedByUserId != currentUserId)
                throw new UnauthorizedAccessException();

            var sparePart = wosp.SparePart ?? await _db.SpareParts.FindAsync(wosp.SparePartId) ?? throw new KeyNotFoundException();

            if (sparePart.Stock < wosp.QuantityUsed)
                throw new InvalidOperationException("Onay için yeterli stok bulunmuyor.");

            sparePart.Stock -= wosp.QuantityUsed;
            wosp.Status = SparePartStatus.Approved;
            var oldWoStatus = workOrder.Status;

            if (workOrder.Status == WorkOrderStatus.PartsOrdered)
                workOrder.Status = WorkOrderStatus.InProgress;

            await _db.SaveChangesAsync();
            await _history.LogSparePartApprovedAsync(workOrder.Id, currentUserId);
            await _history.LogStatusAsync(workOrder, currentUserId, oldWoStatus, workOrder.Status);

            // Onaylandýðýnda atanmýþ teknisyene bildirim
            if (!string.IsNullOrWhiteSpace(workOrder.AssignedToUserId))
            {
                await _hubContext.Clients.User(workOrder.AssignedToUserId).SendAsync(
                    "SparePartApproved",
                    new
                    {
                        workOrderId = workOrder.Id,
                        sparePartId = sparePart.Id,
                        partName = sparePart.Name,
                        quantity = wosp.QuantityUsed,
                        approvedBy = currentUserId,
                        status = wosp.Status.ToString()
                    });
            }
        }

        public async Task RejectSparePartAsync(int workOrderSparePartId, string currentUserId)
        {
            var wosp = await _db.WorkOrderSpareParts
               .Include(x => x.WorkOrder)
               .Include(x => x.SparePart)
               .FirstOrDefaultAsync(x => x.Id == workOrderSparePartId)
               ?? throw new KeyNotFoundException();

            if (wosp.Status != SparePartStatus.Requested)
                throw new InvalidOperationException("Yalnýzca 'Requested' durumundaki parça talepleri reddedilebilir.");

            var workOrder = wosp.WorkOrder ?? throw new InvalidOperationException("Ýþ emri iliþkilendirmesi bulunamadý.");
            if (workOrder.CreatedByUserId != currentUserId)
                throw new UnauthorizedAccessException();

            wosp.Status = SparePartStatus.Rejected;

            await _db.SaveChangesAsync();
            await _history.LogSparePartRejectedAsync(workOrder.Id, currentUserId);

            // Red bildirimi (opsiyonel) teknisyene iletilsin
            if (!string.IsNullOrWhiteSpace(workOrder.AssignedToUserId))
            {
                await _hubContext.Clients.User(workOrder.AssignedToUserId).SendAsync(
                    "SparePartRejected",
                    new
                    {
                        workOrderId = workOrder.Id,
                        sparePartId = wosp.SparePartId,
                        quantity = wosp.QuantityUsed,
                        rejectedBy = currentUserId,
                        status = wosp.Status.ToString()
                    });
            }
        }
    }
}