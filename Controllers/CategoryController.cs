using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using VocaLearn.Models;
using VocaLearn.Services;
using VocaLearn.ViewModels;

namespace VocaLearn.Controllers;

[Authorize]
public class CategoryController : Controller
{
    private readonly IVocabularyService _vocabularyService;
    private readonly UserManager<ApplicationUser> _userManager;

    public CategoryController(
        IVocabularyService vocabularyService,
        UserManager<ApplicationUser> userManager)
    {
        _vocabularyService = vocabularyService;
        _userManager = userManager;
    }

    private string GetUserId() => _userManager.GetUserId(User)!;

    [HttpGet]
    public async Task<IActionResult> Index()
    {
        var userId = GetUserId();
        var categories = await _vocabularyService.GetCategoriesAsync(userId);

        var viewModels = categories.Select(c => new CategoryViewModel
        {
            Id = c.Id,
            Name = c.Name,
            Description = c.Description,
            Color = c.Color,
            WordCount = c.Vocabularies.Count
        }).ToList();

        return View(viewModels);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(CategoryViewModel model)
    {
        var userId = GetUserId();

        if (!ModelState.IsValid)
        {
            TempData["ErrorMessage"] = "Category name is required.";
            return RedirectToAction(nameof(Index));
        }

        var category = new Category
        {
            Name = model.Name.Trim(),
            Description = model.Description?.Trim(),
            Color = string.IsNullOrWhiteSpace(model.Color) ? "#3B82F6" : model.Color.Trim(),
            UserId = userId
        };

        await _vocabularyService.CreateCategoryAsync(category, userId);
        TempData["SuccessMessage"] = $"Category '{category.Name}' created!";
        return RedirectToAction(nameof(Index));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Edit(CategoryViewModel model)
    {
        var userId = GetUserId();

        if (!ModelState.IsValid)
        {
            TempData["ErrorMessage"] = "Invalid category details.";
            return RedirectToAction(nameof(Index));
        }

        var category = new Category
        {
            Id = model.Id,
            Name = model.Name.Trim(),
            Description = model.Description?.Trim(),
            Color = string.IsNullOrWhiteSpace(model.Color) ? "#3B82F6" : model.Color.Trim()
        };

        var success = await _vocabularyService.UpdateCategoryAsync(category, userId);
        if (success)
        {
            TempData["SuccessMessage"] = $"Category '{category.Name}' updated!";
        }
        else
        {
            TempData["ErrorMessage"] = "Category not found.";
        }

        return RedirectToAction(nameof(Index));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Delete(int id)
    {
        var userId = GetUserId();
        var success = await _vocabularyService.DeleteCategoryAsync(id, userId);

        if (success)
        {
            TempData["SuccessMessage"] = "Category deleted. Associated words were preserved.";
        }
        else
        {
            TempData["ErrorMessage"] = "Could not delete category.";
        }

        return RedirectToAction(nameof(Index));
    }
}
