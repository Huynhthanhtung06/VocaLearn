using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using VocaLearn.Models;
using VocaLearn.Services;
using VocaLearn.ViewModels;

namespace VocaLearn.Controllers;

[Authorize]
public class FlashcardsController : Controller
{
    private readonly IFlashcardService _flashcardService;
    private readonly UserManager<ApplicationUser> _userManager;

    public FlashcardsController(
        IFlashcardService flashcardService,
        UserManager<ApplicationUser> userManager)
    {
        _flashcardService = flashcardService;
        _userManager = userManager;
    }

    private string GetUserId() => _userManager.GetUserId(User)!;

    [HttpGet]
    public async Task<IActionResult> Index(int? categoryId, string filter = "all", string? lesson = null)
    {
        var userId = GetUserId();
        var model = await _flashcardService.GetFlashcardsAsync(userId, categoryId, filter, lesson);
        return View(model);
    }

    [HttpPost]
    public async Task<IActionResult> Action([FromBody] FlashcardActionModel model)
    {
        if (model == null || model.VocabularyId <= 0)
        {
            return BadRequest(new { success = false, message = "Invalid request" });
        }

        var userId = GetUserId();
        var success = await _flashcardService.RecordFlashcardActionAsync(model.VocabularyId, model.Action, userId);

        return Json(new { success });
    }
}
