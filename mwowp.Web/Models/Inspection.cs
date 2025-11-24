using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace mwowp.Web.Models
{
    public class Inspection
    {
        public int Id { get; set; }

        public string InspectorId { get; set; }
        public ApplicationUser Inspector { get; set; }

        public int WorkOrderId { get; set; }
        public WorkOrder WorkOrder { get; set; }

        public DateTime InspectionDate { get; set; }
        public int Rating { get; set; } // 1 to 5
        public string Comments { get; set; }
    }
}
