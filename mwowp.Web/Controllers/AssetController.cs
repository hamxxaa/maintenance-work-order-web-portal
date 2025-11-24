using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using mwowp.Web.Data;
using mwowp.Web.Models;
using Microsoft.AspNetCore.Identity;

namespace mwowp.Web.Controllers
{
    public class AssetController : Controller
    {
        private readonly ApplicationDbContext _context;
        private readonly UserManager<ApplicationUser> _userManager;

        public AssetController(ApplicationDbContext context, UserManager<ApplicationUser> userManager)
        {
            _context = context;
            _userManager = userManager;
        }

        // GET: /Asset/MyAssets
        public async Task<IActionResult> MyAssets()
        {
            var user = await _userManager.GetUserAsync(User);
            var assets = await _context.Assets
                                .Where(a => a.OwnerUserId == user.Id)
                                .ToListAsync();
            return View(assets);
        }

        // GET: /Asset/Create
        public IActionResult Create()
        {
            return View();
        }

        // POST: /Asset/Create
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create([Bind("Name,Brand,Model,SerialNumber")] Asset asset)
        {
            ModelState.Remove("OwnerUserId");
            ModelState.Remove("OwnerUser");
            ModelState.Remove("Status"); // Status de formdan gelmediği için hata verebilir
            ModelState.Remove("CreatedAt");
            ModelState.Remove("WorkOrders");

            if (ModelState.IsValid)
            {
                var user = await _userManager.GetUserAsync(User);
                asset.OwnerUserId = user.Id;
                asset.Status = AssetStatus.OnRepair; // veya default status
                asset.CreatedAt = DateTime.UtcNow;

                _context.Assets.Add(asset);
                await _context.SaveChangesAsync();

                return RedirectToAction(nameof(MyAssets));
            }
            return View(asset);
        }
    }
}
