using mwowp.Web.Data;
using mwowp.Web.Models;

namespace mwowp.Web.Services
{
    public sealed class AttachmentService : IAttachmentService
    {
        private readonly ApplicationDbContext _db;
        private readonly IWorkOrderHistoryService _history;
        private static readonly HashSet<string> AllowedExt = new(StringComparer.OrdinalIgnoreCase) { ".jpg", ".jpeg", ".png", ".gif", ".webp" };
        private const int MaxSizeBytes = 5 * 1024 * 1024;

        public AttachmentService(ApplicationDbContext db, IWorkOrderHistoryService history)
        {
            _db = db;
            _history = history;
        }

        public async Task<IReadOnlyList<WorkOrderAttachment>> SaveWorkOrderImagesAsync(int workOrderId, string webRootPath, IEnumerable<IFormFile> images)
        {
            var uploadRoot = Path.Combine(webRootPath, "uploads", "workorders", workOrderId.ToString());
            Directory.CreateDirectory(uploadRoot);

            var attachments = new List<WorkOrderAttachment>();

            foreach (var img in images)
            {
                if (img.Length <= 0) continue;
                if (!img.ContentType.StartsWith("image/", StringComparison.OrdinalIgnoreCase)) continue;
                if (img.Length > MaxSizeBytes) continue;

                var ext = Path.GetExtension(img.FileName);
                if (!AllowedExt.Contains(ext)) continue;

                var fileName = $"{Guid.NewGuid()}{ext}";
                var physicalPath = Path.Combine(uploadRoot, fileName);
                using (var fs = new FileStream(physicalPath, FileMode.Create))
                {
                    await img.CopyToAsync(fs);
                }

                var relativePath = Path.Combine("uploads", "workorders", workOrderId.ToString(), fileName).Replace("\\", "/");

                attachments.Add(new WorkOrderAttachment
                {
                    WorkOrderId = workOrderId,
                    FilePath = relativePath,
                    CreatedAt = DateTime.UtcNow
                });
                await _history.LogAttachmentAddedAsync(workOrderId, relativePath);
            }

            if (attachments.Count > 0)
            {
                _db.WorkOrderAttachments.AddRange(attachments);
                await _db.SaveChangesAsync();
            }

            return attachments;
        }
    }
}