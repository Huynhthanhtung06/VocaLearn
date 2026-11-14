using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using VocaLearn.Models;
using VocaLearn.Services;

namespace VocaLearn.Controllers;

[Authorize]
public class HomeController : Controller
{
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly IProgressService _progressService;

    public HomeController(
        UserManager<ApplicationUser> userManager,
        IProgressService progressService)
    {
        _userManager = userManager;
        _progressService = progressService;
    }

    [HttpGet]
    public async Task<IActionResult> Index()
    {
        var user = await _userManager.GetUserAsync(User);
        if (user == null)
        {
            return RedirectToAction("Login", "Account");
        }

        var model = await _progressService.GetDashboardDataAsync(user.Id, user.FullName);
        return View(model);
    }
}
