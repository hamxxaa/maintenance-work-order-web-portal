using Microsoft.AspNetCore.Http;
using mwowp.Web.Models;

namespace mwowp.Web.Services
{
    public interface IAttachmentService
    {
        Task<IReadOnlyList<WorkOrderAttachment>> SaveWorkOrderImagesAsync(int workOrderId, string webRootPath, IEnumerable<IFormFile> images);
    }
}