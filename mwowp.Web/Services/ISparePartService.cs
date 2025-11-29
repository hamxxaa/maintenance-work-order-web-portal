namespace mwowp.Web.Services
{
    public interface ISparePartService
    {
        Task AddToWorkOrderAsync(int workOrderId, int sparePartId, int quantityUsed, string currentUserId, IEnumerable<string> roles);
    }
}