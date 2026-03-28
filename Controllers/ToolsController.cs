using Microsoft.AspNetCore.Mvc;

namespace CustomWebTools.Controllers
{
    public class ToolsController : Controller
    {
        public IActionResult TextCompare()
        {
            ViewData["Title"] = "Free Online Text Comparison Tool – DailyTools";
            ViewData["MetaDescription"] = "Compare two texts and highlight changes. Fast, simple, and privacy-friendly.";
            return View();
        }

        public IActionResult SimilarityScore()
        {
            ViewData["Title"] = "Text Similarity Checker (Percentage) – DailyTools";
            ViewData["MetaDescription"] = "Get a similarity percentage between two texts with normalization options and shingle/gram scoring.";
            return View();
        }

        public IActionResult JsonViewer()
        {
            ViewData["Title"] = "JSON Viewer & Formatter – Pretty Print JSON Online";
            ViewData["MetaDescription"] = "Format and validate JSON. Pretty-print output, copy/download, and try best-effort repair for broken JSON.";
            return View();
        }

        public IActionResult CharacterCount()
        {
            ViewData["Title"] = "Character Counter – Count Characters, Words, Lines";
            ViewData["MetaDescription"] = "Count characters, words, and lines instantly. Includes common presets like Tweet/X and meta tags.";
            return View();
        }

        public IActionResult WordToNumber()
        {
            ViewData["Title"] = "Word to Number & Number to Word Converter Online";
            ViewData["MetaDescription"] = "Convert English words to numbers and numbers to words. Supports negatives, decimals, and short-scale units.";
            return View();
        }
    }
}

