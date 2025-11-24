using Microsoft.AspNetCore.Identity;

namespace mwowp.Web.Models
{
    public class ApplicationUser : IdentityUser
{
    public string FullName { get; set; }
    public bool IsTechnician { get; set; }
    public bool IsManager { get; set; }

    // Navigation
    public ICollection<WorkOrder> CreatedWorkOrders { get; set; }
    public ICollection<WorkOrder> AssignedWorkOrders { get; set; }
}
}
