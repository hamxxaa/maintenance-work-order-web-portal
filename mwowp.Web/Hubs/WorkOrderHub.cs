using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.SignalR;
using mwowp.Web.Models;

namespace mwowp.Web.Hubs
{
    [Authorize]
    public class WorkOrderHub : Hub
    {
        private readonly UserManager<ApplicationUser> _userManager;

        public WorkOrderHub(UserManager<ApplicationUser> userManager)
        {
            _userManager = userManager;
        }

        public override async Task OnConnectedAsync()
        {
            var user = await _userManager.GetUserAsync(Context.User);
            if (user != null)
            {
                var roles = await _userManager.GetRolesAsync(user);
                if (roles.Contains("Manager"))
                {
                    await Groups.AddToGroupAsync(Context.ConnectionId, "Managers");
                }
            }

            await base.OnConnectedAsync();
        }

        public override async Task OnDisconnectedAsync(Exception? exception)
        {
            var user = await _userManager.GetUserAsync(Context.User);
            if (user != null)
            {
                var roles = await _userManager.GetRolesAsync(user);
                if (roles.Contains("Manager"))
                {
                    await Groups.RemoveFromGroupAsync(Context.ConnectionId, "Managers");
                }
            }

            await base.OnDisconnectedAsync(exception);
        }
    }
}