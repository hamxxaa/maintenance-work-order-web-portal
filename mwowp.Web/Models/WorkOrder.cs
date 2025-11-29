using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace mwowp.Web.Models
{
    public class WorkOrder
    {
        [Key]
        [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
        public int Id { get; set; }
        public string Title { get; set; }
        public string Description { get; set; }

        public string CreatedByUserId { get; set; }
        public ApplicationUser CreatedByUser { get; set; }

        public string? AssignedToUserId { get; set; }
        public ApplicationUser? AssignedToUser { get; set; }

        public string? AssignedById { get; set; }
        public ApplicationUser? AssignedBy { get; set; }

        public int AssetId { get; set; }
        public Asset Asset { get; set; }

        public WorkOrderStatus Status { get; set; }

        public PriorityLevel? Priority { get; set; }

        public string? RepairReport { get; set; }

        public DateTime? SLAEndTime { get; set; }
        public DateTime? CompletedAt { get; set; }
        public DateTime CreatedAt { get; set; }

        // Navigation
        public ICollection<WorkOrderSparePart>? WorkOrderSpareParts { get; set; }
        public ICollection<WorkOrderEquipment>? WorkOrderEquipments { get; set; }
        public ICollection<WorkOrderAttachment>? Attachments { get; set; }
        public ICollection<WorkOrderHistory>? History { get; set; }
    }

    public class WorkOrderAttachment
    {
        public int Id { get; set; }
        public int WorkOrderId { get; set; }
        public WorkOrder WorkOrder { get; set; }

        public string FilePath { get; set; }
        public DateTime CreatedAt { get; set; }
    }

    public class WorkOrderHistory
    {
        public int Id { get; set; }

        public int WorkOrderId { get; set; }
        public WorkOrder WorkOrder { get; set; }

        public string Action { get; set; }
        public string? OldValue { get; set; }
        public string? NewValue { get; set; }

        public string? ChangedByUserId { get; set; }
        public ApplicationUser? ChangedByUser { get; set; }

        public DateTime CreatedAt { get; set; }
    }

    public class WorkOrderEquipment
    {
        public int Id { get; set; }

        public int WorkOrderId { get; set; }
        public WorkOrder WorkOrder { get; set; }

        public int EquipmentId { get; set; }
        public Equipment Equipment { get; set; }

        public string UsageNotes { get; set; }
        public DateTime UsedAt { get; set; }

        public DateTime AssignedAt { get; set; }
        public DateTime? ReturnedAt { get; set; }
    }

    public class WorkOrderSparePart
    {
        public int Id { get; set; }

        public int WorkOrderId { get; set; }
        public WorkOrder WorkOrder { get; set; }

        public int SparePartId { get; set; }
        public SparePart SparePart { get; set; }
        
        public SparePartStatus Status { get; set; }

        public int QuantityUsed { get; set; }
    }
}
