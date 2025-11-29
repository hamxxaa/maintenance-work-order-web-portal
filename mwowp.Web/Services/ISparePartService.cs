using mwowp.Web.Models;

namespace mwowp.Web.Services
{
    public interface ISparePartService
    {
        Task AddRequestToWorkOrderAsync(int workOrderId, int sparePartId, int quantityUsed, string currentUserId, IEnumerable<string> roles);
        Task ApproveSparePartAsync(int workOrderSparePartId, string currentUserId);
        Task RejectSparePartAsync(int workOrderSparePartId, string currentUserId);
    }
}