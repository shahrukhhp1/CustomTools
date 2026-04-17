using Microsoft.AspNetCore.Mvc;

namespace CustomWebTools.Controllers
{
    public class ToolsController : Controller
    {
        public IActionResult TextCompare()
        {
            ViewData["Title"] = "Text Comparison Tool – Highlight Differences Between Two Texts | DailyTools";
            ViewData["MetaDescription"] = "Free online text comparison tool. Paste two versions of your text, spec, or code snippet and instantly highlight differences. Fast, browser-based, no signup.";
            return View();
        }

        public IActionResult SimilarityScore()
        {
            ViewData["Title"] = "Text Similarity Checker – Percentage Score with Shingles & Grams | DailyTools";
            ViewData["MetaDescription"] = "Calculate text similarity percentage between two texts. Supports normalization options, word shingles, and character grams. Great for content, SEO, and code comments.";
            return View();
        }

        public IActionResult JsonViewer()
        {
            ViewData["Title"] = "JSON Viewer & Formatter – Pretty Print & Repair JSON Online | DailyTools";
            ViewData["MetaDescription"] = "Online JSON viewer and formatter. Validate, pretty print, copy/download JSON, and try best-effort repair for broken JSON payloads. Built for developers and API debugging.";
            return View();
        }

        public IActionResult CharacterCount()
        {
            ViewData["Title"] = "Character Counter – Count Characters, Words & Lines Online | DailyTools";
            ViewData["MetaDescription"] = "Simple online character counter. Get characters, characters without spaces, words, and lines. Presets for Tweet/X and SEO meta title/description limits.";
            return View();
        }

        public IActionResult WordToNumber()
        {
            ViewData["Title"] = "Word to Number & Number to Word Converter (English) | DailyTools";
            ViewData["MetaDescription"] = "Convert words to numbers and numbers to words online. English-only, short-scale, supports negatives and decimals. Handy for finance, reports, and forms.";
            return View();
        }

        public IActionResult AppScreenshotGenerator()
        {
            ViewData["Title"] = "App Screenshot Generator (iOS & Android) – Create Store Images Online | DailyTools";
            ViewData["MetaDescription"] = "Generate App Store / Play Store style screenshots in your browser. Add backgrounds and text, and export low/high-resolution PNGs. No upload required.";
            return View();
        }
    }
}

