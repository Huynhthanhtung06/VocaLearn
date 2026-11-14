using System;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using VocaLearn.Models;
using VocaLearn.Services;
using VocaLearn.ViewModels;

namespace VocaLearn.Controllers;

[Authorize]
public class VocabularyController : Controller
{
    private readonly IVocabularyService _vocabularyService;
    private readonly UserManager<ApplicationUser> _userManager;

    public VocabularyController(
        IVocabularyService vocabularyService,
        UserManager<ApplicationUser> userManager)
    {
        _vocabularyService = vocabularyService;
        _userManager = userManager;
    }

    private string GetUserId() => _userManager.GetUserId(User)!;

    [HttpGet]
    public async Task<IActionResult> Index(
        string? search,
        int? categoryId,
        VocabularyStatus? status,
        bool? needsPracticeOnly,
        string? lessonName,
        int pageIndex = 1)
    {
        var userId = GetUserId();
        var model = await _vocabularyService.GetVocabulariesAsync(
            userId, search, categoryId, status, needsPracticeOnly, lessonName, pageIndex, pageSize: 12);

        return View(model);
    }

    [HttpGet]
    public async Task<IActionResult> Create()
    {
        var userId = GetUserId();
        var categories = await _vocabularyService.GetCategoriesAsync(userId);

        var model = new VocabularyFormViewModel
        {
            Categories = categories.Select(c => new SelectListItem(c.Name, c.Id.ToString()))
        };

        return View(model);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(VocabularyFormViewModel model)
    {
        var userId = GetUserId();

        if (await _vocabularyService.WordExistsAsync(model.Word, userId))
        {
            ModelState.AddModelError("Word", "This word already exists in your vocabulary list.");
        }

        if (!ModelState.IsValid)
        {
            var categories = await _vocabularyService.GetCategoriesAsync(userId);
            model.Categories = categories.Select(c => new SelectListItem(c.Name, c.Id.ToString()));
            return View(model);
        }

        var vocab = new Vocabulary
        {
            Word = model.Word.Trim(),
            Meaning = model.Meaning.Trim(),
            Example = model.Example?.Trim(),
            Pronunciation = model.Pronunciation?.Trim(),
            LessonName = "Lesson 1",
            CategoryId = model.CategoryId,
            Status = model.Status,
            NeedsPractice = model.NeedsPractice
        };

        await _vocabularyService.CreateAsync(vocab, userId);
        TempData["SuccessMessage"] = $"Word '{vocab.Word}' added successfully!";
        return RedirectToAction(nameof(Index));
    }

    [HttpGet]
    public async Task<IActionResult> Edit(int id)
    {
        var userId = GetUserId();
        var vocab = await _vocabularyService.GetByIdAsync(id, userId);

        if (vocab == null)
        {
            return NotFound();
        }

        var categories = await _vocabularyService.GetCategoriesAsync(userId);
        var model = new VocabularyFormViewModel
        {
            Id = vocab.Id,
            Word = vocab.Word,
            Meaning = vocab.Meaning,
            Example = vocab.Example,
            Pronunciation = vocab.Pronunciation,
            LessonName = vocab.LessonName,
            CategoryId = vocab.CategoryId,
            Status = vocab.Status,
            NeedsPractice = vocab.NeedsPractice,
            Categories = categories.Select(c => new SelectListItem(c.Name, c.Id.ToString(), c.Id == vocab.CategoryId))
        };

        return View(model);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Edit(int id, VocabularyFormViewModel model)
    {
        var userId = GetUserId();

        if (await _vocabularyService.WordExistsAsync(model.Word, userId, excludeId: id))
        {
            ModelState.AddModelError("Word", "Another word with this spelling already exists.");
        }

        if (!ModelState.IsValid)
        {
            var categories = await _vocabularyService.GetCategoriesAsync(userId);
            model.Categories = categories.Select(c => new SelectListItem(c.Name, c.Id.ToString(), c.Id == model.CategoryId));
            return View(model);
        }

        var vocab = new Vocabulary
        {
            Id = id,
            Word = model.Word.Trim(),
            Meaning = model.Meaning.Trim(),
            Example = model.Example?.Trim(),
            Pronunciation = model.Pronunciation?.Trim(),
            LessonName = model.LessonName,
            CategoryId = model.CategoryId,
            Status = model.Status,
            NeedsPractice = model.NeedsPractice
        };

        var success = await _vocabularyService.UpdateAsync(vocab, userId);
        if (!success)
        {
            return NotFound();
        }

        TempData["SuccessMessage"] = $"Word '{vocab.Word}' updated successfully!";
        return RedirectToAction(nameof(Index));
    }

    [HttpGet]
    public async Task<IActionResult> Details(int id)
    {
        var userId = GetUserId();
        var vocab = await _vocabularyService.GetByIdAsync(id, userId);

        if (vocab == null)
        {
            return NotFound();
        }

        return View(vocab);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Delete(int id)
    {
        var userId = GetUserId();
        var success = await _vocabularyService.DeleteAsync(id, userId);

        if (success)
        {
            TempData["SuccessMessage"] = "Vocabulary word removed.";
        }
        else
        {
            TempData["ErrorMessage"] = "Could not delete word or it does not exist.";
        }

        return RedirectToAction(nameof(Index));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> DeleteAll()
    {
        var deletedCount = await _vocabularyService.DeleteAllAsync(GetUserId());

        if (deletedCount > 0)
        {
            TempData["SuccessMessage"] = $"Deleted all {deletedCount} vocabulary word(s).";
        }
        else
        {
            TempData["ErrorMessage"] = "Your vocabulary library is already empty.";
        }

        return RedirectToAction(nameof(Index));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> DeleteMany(List<int>? selectedIds)
    {
        if (selectedIds == null || selectedIds.Count == 0)
        {
            TempData["ErrorMessage"] = "Please select at least one word to delete.";
            return RedirectToAction(nameof(Index));
        }

        var deletedCount = await _vocabularyService.DeleteManyAsync(selectedIds, GetUserId());
        TempData["SuccessMessage"] = $"Deleted {deletedCount} vocabulary word(s).";
        return RedirectToAction(nameof(Index));
    }
}
