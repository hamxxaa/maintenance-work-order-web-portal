using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using mwowp.Web.Data;
using mwowp.Web.Models;
using Microsoft.AspNetCore.Authorization;

namespace mwowp.Web.Controllers
{
    [Authorize] // Tüm istekler için oturum zorunlu
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
        // Yalnızca normal kullanıcı kendi varlıklarını görebilir
        [Authorize(Roles = "User")]
        public async Task<IActionResult> MyAssets()
        {
            var user = await _userManager.GetUserAsync(User);
            var assets = await _context.Assets
                                .Where(a => a.OwnerUserId == user.Id)
                                .ToListAsync();
            return View(assets);
        }

        // GET: /Asset/Create
        // Yalnızca normal kullanıcı varlık oluşturabilir
        [Authorize(Roles = "User")]
        public IActionResult Create()
        {
            return View();
        }

        // POST: /Asset/Create
        [HttpPost]
        [ValidateAntiForgeryToken]
        [Authorize(Roles = "User")]
        public async Task<IActionResult> Create([Bind("Name,Brand,Model,SerialNumber")] Asset asset)
        {
            ModelState.Remove("OwnerUserId");
            ModelState.Remove("OwnerUser");
            ModelState.Remove("Status"); 
            ModelState.Remove("CreatedAt");

            if (ModelState.IsValid)
            {
                var user = await _userManager.GetUserAsync(User);
                asset.OwnerUserId = user.Id;
                asset.OwnerUser = user;

                asset.Status = AssetStatus.New; 
                asset.CreatedAt = DateTime.UtcNow;

                _context.Assets.Add(asset);
                await _context.SaveChangesAsync();

                return RedirectToAction(nameof(MyAssets));
            }
            return View(asset);
        }
    }
}