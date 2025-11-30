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

    public enum SparePartStatus
    {
        Requested,
        Approved,
        Rejected
    }

    public enum AssetStatus
    {
        OnRepair,
        SentToOwner,
        New
    }

    // Python dict benzeri: PriorityLevel -> TimeSpan (gün cinsinden)
    public static class SlaDefinitions
    {
        // İhtiyaç halinde bu sözlüğü tek yerden güncelleyebilirsiniz.
        public static readonly System.Collections.Generic.IReadOnlyDictionary<PriorityLevel, System.TimeSpan> DaysByPriority
            = new System.Collections.Generic.Dictionary<PriorityLevel, System.TimeSpan>
            {
                // Örnek: Low: 30 gün, Medium: 15 gün
                { PriorityLevel.Low, System.TimeSpan.FromDays(30) },
                { PriorityLevel.Medium, System.TimeSpan.FromDays(15) },

                // İhtiyaca göre eklemeler:
                { PriorityLevel.High, System.TimeSpan.FromDays(7) },
                { PriorityLevel.Critical, System.TimeSpan.FromDays(2) },
            };

        // Başlangıç tarihine göre SLA bitiş tarihini hesaplar.
        public static System.DateTime GetSlaEndDate(System.DateTime startDate, PriorityLevel priority)
        {
            var duration = DaysByPriority.TryGetValue(priority, out var span)
                ? span
                : System.TimeSpan.Zero;

            return startDate.Add(duration);
        }
    }
    // PriorityLevel -> Ücret (decimal) eşlemesi
    public static class FeeDefinitions
    {
        // İhtiyaç halinde bu sözlüğü tek yerden güncelleyebilirsiniz.
        // Para birimi: varsayılan olarak yerel birim (örn. TRY) - sadece numerik değer tutulur.
        public static readonly System.Collections.Generic.IReadOnlyDictionary<PriorityLevel, decimal> FeeByPriority
            = new System.Collections.Generic.Dictionary<PriorityLevel, decimal>
            {
                // Örnek ücretler (isteğe göre güncellenebilir):
                { PriorityLevel.Low, 20m },
                { PriorityLevel.Medium, 30m },
                { PriorityLevel.High, 50m },
                { PriorityLevel.Critical, 100m },
            };

        // İlgili öncelik için ücreti döndürür; bulunamazsa 0 döner.
        public static decimal GetFee(PriorityLevel priority)
        {
            return FeeByPriority.TryGetValue(priority, out var fee) ? fee : 0m;
        }
    }
}
