using System.Collections.Generic;
using Microsoft.AspNetCore.Http;

namespace VocaLearn.ViewModels;

public class ExcelUploadViewModel
{
    public IFormFile? File { get; set; }

    public string LessonName { get; set; } = string.Empty;
}

public class ExcelImportRowViewModel
{
    public int RowIndex { get; set; }
    public string Word { get; set; } = string.Empty;
    public string Meaning { get; set; } = string.Empty;
    public string? Pronunciation { get; set; }
    public string? Example { get; set; }
    public string? Category { get; set; }
    public string LessonName { get; set; } = "Lesson 1";
    public bool IsDuplicate { get; set; }
    public bool IsValid { get; set; }
    public string? ErrorMessage { get; set; }
}

public class ExcelImportPreviewViewModel
{
    public string FileName { get; set; } = string.Empty;
    public List<ExcelImportRowViewModel> Rows { get; set; } = new();
    public int TotalRows => Rows.Count;
    public int ValidCount { get; set; }
    public int DuplicateCount { get; set; }
    public int InvalidCount { get; set; }

    // Serialized JSON for confirmation submission
    public string SerializedRows { get; set; } = string.Empty;
}

public class ExcelImportConfirmModel
{
    public string SerializedRows { get; set; } = string.Empty;
    public bool SkipDuplicates { get; set; } = true;
}
