using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace mwowp.Web.Models
{
    public class Invoice
    {
        public int Id { get; set; }

        public string UserId { get; set; }
        public ApplicationUser User { get; set; }

        public int WorkOrderId { get; set; }
        public WorkOrder WorkOrder { get; set; }
        public string InvoiceText{ get; set; }

        public DateTime InvoiceDate { get; set; }
    }
}
