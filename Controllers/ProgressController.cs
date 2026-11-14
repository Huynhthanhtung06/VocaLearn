using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using VocaLearn.Models;
using VocaLearn.Services;

namespace VocaLearn.Controllers;

[Authorize]
public class ProgressController : Controller
{
    private readonly IProgressService _progressService;
    private readonly UserManager<ApplicationUser> _userManager;

    public ProgressController(
        IProgressService progressService,
        UserManager<ApplicationUser> userManager)
    {
        _progressService = progressService;
        _userManager = userManager;
    }

    [HttpGet]
    public async Task<IActionResult> Index()
    {
        var userId = _userManager.GetUserId(User)!;
        var model = await _progressService.GetAnalyticsAsync(userId);
        return View(model);
    }
}
