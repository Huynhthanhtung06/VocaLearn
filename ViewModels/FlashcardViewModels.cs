using System.Collections.Generic;
using VocaLearn.Models;

namespace VocaLearn.ViewModels;

public class FlashcardItemViewModel
{
    public int Id { get; set; }
    public string Word { get; set; } = string.Empty;
    public string Meaning { get; set; } = string.Empty;
    public string? Example { get; set; }
    public string? Pronunciation { get; set; }
    public string CategoryName { get; set; } = string.Empty;
    public string LessonName { get; set; } = "Lesson 1";
    public string CategoryColor { get; set; } = "#3B82F6";
    public VocabularyStatus Status { get; set; }
    public bool NeedsPractice { get; set; }
    public int ReviewCount { get; set; }
}

public class FlashcardSessionViewModel
{
    public List<FlashcardItemViewModel> Cards { get; set; } = new();
    public List<Category> Categories { get; set; } = new();
    public int? SelectedCategoryId { get; set; }
    public string Filter { get; set; } = "all"; // all, needsPractice, unlearned
    public string SelectedLesson { get; set; } = "Lesson 1";
    public List<string> Lessons { get; set; } = new();
    public int TotalCards => Cards.Count;
}

public class FlashcardActionModel
{
    public int VocabularyId { get; set; }
    public string Action { get; set; } = string.Empty; // "know" or "practice"
}
