using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using mwowp.Web.Data;
using mwowp.Web.Models;

var builder = WebApplication.CreateBuilder(args);

// =========================
// 1. Connection String & DbContext
// =========================
builder.Services.AddDbContext<ApplicationDbContext>(options =>
    options.UseSqlServer(builder.Configuration.GetConnectionString("DefaultConnection")));

// =========================
// 2. Identity
// =========================
builder.Services.AddIdentity<ApplicationUser, IdentityRole>(options =>
{
    options.Password.RequireDigit = true;
    options.Password.RequiredLength = 6;
    options.Password.RequireNonAlphanumeric = false;
    options.Password.RequireUppercase = true;
    options.Password.RequireLowercase = true;

    options.User.RequireUniqueEmail = true;
})
.AddEntityFrameworkStores<ApplicationDbContext>()
.AddDefaultTokenProviders();

// =========================
// 3. MVC & Razor
// =========================
builder.Services.AddControllersWithViews();
builder.Services.AddRazorPages();

// =========================
// 4. SignalR
// =========================
builder.Services.AddSignalR();

// =========================
// 5. Application Services (opsiyonel)
// =========================
// Örnek: builder.Services.AddScoped<IWorkOrderService, WorkOrderService>();


var app = builder.Build();

// =========================
// 6. Middleware
// =========================


if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Home/Error");
    app.UseHsts();
}

app.UseHttpsRedirection();
app.UseStaticFiles();

app.UseRouting();

app.UseAuthentication();
app.UseAuthorization();
app.MapDefaultControllerRoute();

// =========================
// 7. Routes
// =========================
app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Home}/{action=Index}/{id?}");

app.MapRazorPages();

// =========================
// 8. SignalR Hubs
// =========================
//app.MapHub<WorkOrderHub>("/hubs/workorders");

using (var scope = app.Services.CreateScope())
{
    var services = scope.ServiceProvider;
    try
    {
        // Seed Roller ve Test Kullanıcılar
        await DbInitializer.SeedRolesAndAdminAsync(services);
    }
    catch (Exception ex)
    {
        var logger = services.GetRequiredService<ILogger<Program>>();
        logger.LogError(ex, "DbInitializer çalıştırılamadı");
    }
}

// =========================
// 9. Run
// =========================
app.Run();

