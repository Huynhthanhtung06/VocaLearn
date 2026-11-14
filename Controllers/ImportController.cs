using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using VocaLearn.Models;
using VocaLearn.Services;
using VocaLearn.ViewModels;

namespace VocaLearn.Controllers;

[Authorize]
public class ImportController : Controller
{
    private readonly IExcelImportService _importService;
    private readonly UserManager<ApplicationUser> _userManager;

    public ImportController(
        IExcelImportService importService,
        UserManager<ApplicationUser> userManager)
    {
        _importService = importService;
        _userManager = userManager;
    }

    private string GetUserId() => _userManager.GetUserId(User)!;

    [HttpGet]
    public IActionResult Index()
    {
        return View(new ExcelUploadViewModel());
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Preview(ExcelUploadViewModel model)
    {
        if (model.File == null || model.File.Length == 0)
        {
            ModelState.AddModelError("File", "Please choose a valid Excel (.xlsx) file to upload.");
            return View(nameof(Index), model);
        }

        if (string.IsNullOrWhiteSpace(model.LessonName))
        {
            ModelState.AddModelError(nameof(model.LessonName), "Please enter a lesson name.");
            return View(nameof(Index), model);
        }

        var extension = Path.GetExtension(model.File.FileName).ToLowerInvariant();
        if (extension != ".xlsx")
        {
            ModelState.AddModelError("File", "Only Excel files (.xlsx format) are supported.");
            return View(nameof(Index), model);
        }

        if (model.File.Length > 5 * 1024 * 1024) // 5MB limit
        {
            ModelState.AddModelError("File", "File size cannot exceed 5MB.");
            return View(nameof(Index), model);
        }

        var userId = GetUserId();

        try
        {
            using var stream = model.File.OpenReadStream();
            var preview = await _importService.ParseAndPreviewAsync(stream, model.File.FileName, userId);

            foreach (var row in preview.Rows)
            {
                row.LessonName = model.LessonName.Trim();
            }

            if (!preview.Rows.Any())
            {
                ModelState.AddModelError("File", "The file contains no readable vocabulary rows.");
                return View(nameof(Index), model);
            }

            preview.SerializedRows = JsonSerializer.Serialize(preview.Rows);
            return View("Preview", preview);
        }
        catch (Exception ex)
        {
            ModelState.AddModelError("File", $"Failed to process Excel file: {ex.Message}");
            return View(nameof(Index), model);
        }
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Confirm(ExcelImportConfirmModel model)
    {
        if (string.IsNullOrWhiteSpace(model.SerializedRows))
        {
            TempData["ErrorMessage"] = "No import data found.";
            return RedirectToAction(nameof(Index));
        }

        var userId = GetUserId();

        try
        {
            var rows = JsonSerializer.Deserialize<List<ExcelImportRowViewModel>>(model.SerializedRows);
            if (rows == null || !rows.Any())
            {
                TempData["ErrorMessage"] = "No rows to import.";
                return RedirectToAction(nameof(Index));
            }

            int count = await _importService.SaveImportedRowsAsync(rows, model.SkipDuplicates, userId);
            TempData["SuccessMessage"] = $"Successfully imported {count} vocabulary items into your account!";
            return RedirectToAction("Index", "Vocabulary");
        }
        catch (Exception ex)
        {
            TempData["ErrorMessage"] = $"Error saving imported data: {ex.Message}";
            return RedirectToAction(nameof(Index));
        }
    }

    [HttpGet]
    public IActionResult DownloadTemplate()
    {
        var bytes = _importService.GenerateTemplate();
        return File(bytes, "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet", "VocaLearn_Template.xlsx");
    }
}
