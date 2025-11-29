namespace mwowp.Web.Services
{
    public interface IEquipmentService
    {
        Task AddToWorkOrderAsync(int workOrderId, int equipmentId, string? usageNotes, string currentUserId, IEnumerable<string> roles);
    }
}