namespace mwowp.Web.Models
{
    public enum EquipmentStatus
    {
        Available,
        InUse,
        UnderMaintenance,
        Retired
    }

    public enum WorkOrderStatus
    {
        Created,
        Assigned,
        PartsOrdered,
        InProgress,
        Completed,
        Inspected,
        Canceled,
    }

    public enum PriorityLevel
    {
        Low,
        Medium,
        High,
        Critical
    }

    public enum AssetStatus
    {
        OnRepair,
        SentToOwner,
        PickedUpByOwner
    }
}
