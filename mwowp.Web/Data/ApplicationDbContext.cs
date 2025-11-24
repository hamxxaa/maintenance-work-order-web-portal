using mwowp.Web.Models;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;

namespace mwowp.Web.Data
{
    public class ApplicationDbContext : IdentityDbContext<ApplicationUser>
    {
        public ApplicationDbContext(DbContextOptions<ApplicationDbContext> options)
            : base(options)
        {
        }

        public DbSet<Asset> Assets { get; set; }
        public DbSet<Equipment> Equipments { get; set; }
        public DbSet<WorkOrder> WorkOrders { get; set; }
        public DbSet<WorkOrderEquipment> WorkOrderEquipments { get; set; }
        public DbSet<WorkOrderSparePart> WorkOrderSpareParts { get; set; }
        public DbSet<SparePart> SpareParts { get; set; }
        public DbSet<WorkOrderAttachment> WorkOrderAttachments { get; set; }
        public DbSet<WorkOrderHistory> WorkOrderHistories { get; set; }
        public DbSet<Inspection> Inspections { get; set; }


        protected override void OnModelCreating(ModelBuilder builder)
        {
            base.OnModelCreating(builder);

            // WorkOrder - Asset
            builder.Entity<WorkOrder>()
                .HasOne(wo => wo.Asset)
                .WithMany(a => a.WorkOrders)
                .HasForeignKey(wo => wo.AssetId)
                .OnDelete(DeleteBehavior.Restrict);

            // WorkOrder - CreatedByUser
            builder.Entity<WorkOrder>()
                .HasOne(wo => wo.CreatedByUser)
                .WithMany(u => u.CreatedWorkOrders)
                .HasForeignKey(wo => wo.CreatedByUserId)
                .OnDelete(DeleteBehavior.Restrict);

            // WorkOrder - AssignedToUser
            builder.Entity<WorkOrder>()
                .HasOne(wo => wo.AssignedToUser)
                .WithMany(u => u.AssignedWorkOrders)
                .HasForeignKey(wo => wo.AssignedToUserId)
                .OnDelete(DeleteBehavior.Restrict);

            // WorkOrderEquipment
            builder.Entity<WorkOrderEquipment>()
                .HasOne(woe => woe.WorkOrder)
                .WithMany(wo => wo.WorkOrderEquipments)
                .HasForeignKey(woe => woe.WorkOrderId)
                .OnDelete(DeleteBehavior.Cascade);

            builder.Entity<WorkOrderEquipment>()
                .HasOne(woe => woe.Equipment)
                .WithMany(e => e.WorkOrderEquipments)
                .HasForeignKey(woe => woe.EquipmentId)
                .OnDelete(DeleteBehavior.Restrict);

            // WorkOrderSparePart
            builder.Entity<WorkOrderSparePart>()
                .HasOne(wos => wos.WorkOrder)
                .WithMany(wo => wo.WorkOrderSpareParts)
                .HasForeignKey(wos => wos.WorkOrderId)
                .OnDelete(DeleteBehavior.Cascade);

            builder.Entity<WorkOrderSparePart>()
                .HasOne(wos => wos.SparePart)
                .WithMany(sp => sp.WorkOrderSpareParts)
                .HasForeignKey(wos => wos.SparePartId)
                .OnDelete(DeleteBehavior.Restrict);

            // WorkOrderAttachment
            builder.Entity<WorkOrderAttachment>()
                .HasOne(att => att.WorkOrder)
                .WithMany(wo => wo.Attachments)
                .HasForeignKey(att => att.WorkOrderId)
                .OnDelete(DeleteBehavior.Cascade);

            // WorkOrderHistory
            builder.Entity<WorkOrderHistory>()
                .HasOne(h => h.WorkOrder)
                .WithMany(wo => wo.History)
                .HasForeignKey(h => h.WorkOrderId)
                .OnDelete(DeleteBehavior.Cascade);

            builder.Entity<WorkOrderHistory>()
                .HasOne(h => h.ChangedByUser)
                .WithMany()
                .HasForeignKey(h => h.ChangedByUserId)
                .OnDelete(DeleteBehavior.Restrict);

            // Inspection
            builder.Entity<Inspection>()
                .HasOne(i => i.WorkOrder)
                .WithMany()
                .HasForeignKey(i => i.WorkOrderId)
                .OnDelete(DeleteBehavior.Cascade);

            builder.Entity<Inspection>()
                .HasOne(i => i.Inspector)
                .WithMany()
                .HasForeignKey(i => i.InspectorId)
                .OnDelete(DeleteBehavior.Restrict);

            // Enum conversions
            builder.Entity<WorkOrder>()
                .Property(wo => wo.Status)
                .HasConversion<string>();

            builder.Entity<WorkOrder>()
                .Property(wo => wo.Priority)
                .HasConversion<string>();

            builder.Entity<Asset>()
                .Property(a => a.Status)
                .HasConversion<string>();

            builder.Entity<Equipment>()
                .Property(e => e.Status)
                .HasConversion<string>();
        }
    }
}
