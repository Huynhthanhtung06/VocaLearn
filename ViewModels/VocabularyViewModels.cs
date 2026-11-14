using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Mvc.Rendering;
using VocaLearn.Models;

namespace VocaLearn.ViewModels;

public class VocabularyListViewModel
{
    public List<Vocabulary> Items { get; set; } = new();
    public List<Category> Categories { get; set; } = new();
    public List<string> Lessons { get; set; } = new();
    public string? LessonName { get; set; }
    public string? SearchTerm { get; set; }
    public int? CategoryId { get; set; }
    public VocabularyStatus? Status { get; set; }
    public bool? NeedsPracticeOnly { get; set; }

    public int PageIndex { get; set; } = 1;
    public int PageSize { get; set; } = 12;
    public int TotalItems { get; set; }
    public int TotalPages => (int)Math.Ceiling((double)TotalItems / PageSize);

    public bool HasPreviousPage => PageIndex > 1;
    public bool HasNextPage => PageIndex < TotalPages;
}

public class VocabularyFormViewModel
{
    public int Id { get; set; }

    [Required(ErrorMessage = "English word is required")]
    [MaxLength(150)]
    [Display(Name = "English Word")]
    public string Word { get; set; } = string.Empty;

    [Required(ErrorMessage = "Vietnamese meaning is required")]
    [MaxLength(300)]
    [Display(Name = "Vietnamese Meaning")]
    public string Meaning { get; set; } = string.Empty;

    [MaxLength(500)]
    [Display(Name = "Example Sentence")]
    public string? Example { get; set; }

    [MaxLength(100)]
    [Display(Name = "Pronunciation / IPA")]
    public string? Pronunciation { get; set; }

    [MaxLength(100)]
    public string LessonName { get; set; } = "Lesson 1";

    [Required(ErrorMessage = "Please select a category")]
    [Display(Name = "Category")]
    public int CategoryId { get; set; }

    [Display(Name = "Learning Status")]
    public VocabularyStatus Status { get; set; } = VocabularyStatus.NotLearned;

    [Display(Name = "Needs Practice Flag")]
    public bool NeedsPractice { get; set; }

    public IEnumerable<SelectListItem> Categories { get; set; } = new List<SelectListItem>();
}

public class CategoryViewModel
{
    public int Id { get; set; }

    [Required(ErrorMessage = "Category name is required")]
    [MaxLength(100)]
    public string Name { get; set; } = string.Empty;

    [MaxLength(250)]
    public string? Description { get; set; }

    [MaxLength(30)]
    public string Color { get; set; } = "#3B82F6";

    public int WordCount { get; set; }
}
