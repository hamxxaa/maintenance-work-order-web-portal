using Microsoft.AspNetCore.Mvc.ModelBinding;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
namespace mwowp.Web.Models
{
    public class Equipment
    {
        [Key]
        public int Id { get; set; }
        public string Name { get; set; }
        public string Type { get; set; }
        public EquipmentStatus Status { get; set; }

        public DateTime? PurchaseDate { get; set; }
        public DateTime CreatedAt { get; set; }

        // Navigation
        public ICollection<WorkOrderEquipment> WorkOrderEquipments { get; set; }

    }

    public class SparePart
    {
        public int Id { get; set; }
        public string Name { get; set; }
        public int Stock { get; set; }
        public decimal UnitPrice { get; set; }

        // Navigation
        public ICollection<WorkOrderSparePart> WorkOrderSpareParts { get; set; }

    }

    public class Asset
    {
        public int Id { get; set; }
        public string Name { get; set; }
        public string Brand { get; set; }
        public string Model { get; set; }
        public string SerialNumber { get; set; }


        public string OwnerUserId { get; set; }
        public ApplicationUser OwnerUser { get; set; }

        public AssetStatus Status { get; set; }
        public DateTime CreatedAt { get; set; }

        // Navigation
        [BindNever]
        public ICollection<WorkOrder>? WorkOrders { get; set; }

    }
}
