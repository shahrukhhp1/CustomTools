using System.Diagnostics;
using System.Globalization;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using CustomWebTools.Models;
using Microsoft.AspNetCore.Mvc;

namespace CustomWebTools.Controllers
{
    public class HomeController : Controller
    {
        private readonly ILogger<HomeController> _logger;

        public HomeController(ILogger<HomeController> logger)
        {
            _logger = logger;
        }

        public IActionResult Index()
        {
            return View();
        }

        public IActionResult Privacy()
        {
            return View();
        }

        public IActionResult About()
        {
            return View();
        }

        public IActionResult Contact()
        {
            return View();
        }

        public IActionResult Changelog()
        {
            ViewData["Title"] = "Changelog";
            ViewData["MetaDescription"] = "What’s new in DailyTools — product updates and improvements.";
            return View();
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public IActionResult Compare([FromForm] string text1, [FromForm] string text2)
        {
            text1 ??= "";
            text2 ??= "";

            // Normalize line endings for consistent comparison.
            text1 = text1.Replace("\r\n", "\n").Replace("\r", "\n");
            text2 = text2.Replace("\r\n", "\n").Replace("\r", "\n");

            var l1lines = text1.Split('\n');
            var l2lines = text2.Split('\n');

            var totalLines = Math.Max(l1lines.Length, l2lines.Length);
            var changedLines = 0;
            for (var i = 0; i < totalLines; i++)
            {
                var a = i < l1lines.Length ? l1lines[i] : "";
                var b = i < l2lines.Length ? l2lines[i] : "";
                if (!string.Equals(a, b, StringComparison.Ordinal)) changedLines++;
            }
            var sameLines = Math.Max(0, totalLines - changedLines);
            var lineSimilarity = totalLines == 0 ? 1.0 : (double)sameLines / totalLines;

            static void AppendHtmlEncoded(StringBuilder sb, ReadOnlySpan<char> value)
            {
                // Fast minimal HTML encoding without allocations.
                for (var i = 0; i < value.Length; i++)
                {
                    switch (value[i])
                    {
                        case '&': sb.Append("&amp;"); break;
                        case '<': sb.Append("&lt;"); break;
                        case '>': sb.Append("&gt;"); break;
                        case '"': sb.Append("&quot;"); break;
                        case '\'': sb.Append("&#39;"); break;
                        default: sb.Append(value[i]); break;
                    }
                }
            }

            static void AppendPositionalCompare(StringBuilder sb, string a, string b)
            {
                var len1 = a.Length;
                if (len1 == 0)
                {
                    sb.Append("<br/>");
                    return;
                }

                var runStart = 0;
                var runClass = "";

                for (var j = 0; j < len1; j++)
                {
                    var c1 = a[j];
                    var c2 = j < b.Length ? b[j] : '\0';
                    var cls = (c1 == c2) ? "greenback" : "redback";

                    if (j == 0)
                    {
                        runClass = cls;
                        runStart = 0;
                        continue;
                    }

                    if (!string.Equals(runClass, cls, StringComparison.Ordinal))
                    {
                        sb.Append("<span class='").Append(runClass).Append("'>");
                        AppendHtmlEncoded(sb, a.AsSpan(runStart, j - runStart));
                        sb.Append("</span>");

                        runClass = cls;
                        runStart = j;
                    }
                }

                sb.Append("<span class='").Append(runClass).Append("'>");
                AppendHtmlEncoded(sb, a.AsSpan(runStart, len1 - runStart));
                sb.Append("</span>");
                sb.Append("<br/>");
            }

            static void AppendAlignedCompare(StringBuilder sb, string a, string b)
            {
                // Align using Myers diff and render edits:
                // - equal chars: greenback
                // - inserted/deleted/replaced chars: redback
                // This makes insertions (e.g. extra space in text2) visible instead of looking "all green".
                var n = a.Length;
                var m = b.Length;
                if (n == 0 && m == 0)
                {
                    sb.Append("<br/>");
                    return;
                }

                var max = n + m;
                var offset = max;
                var v = new int[2 * max + 1];
                Array.Fill(v, -1);
                v[offset + 1] = 0;

                // Store V for backtracking (after each D iteration).
                var trace = new List<int[]>(capacity: Math.Min(max + 1, 512));
                var lastD = 0;

                for (var d = 0; d <= max; d++)
                {
                    for (var k = -d; k <= d; k += 2)
                    {
                        var idx = k + offset;
                        int x;
                        if (k == -d || (k != d && v[idx - 1] < v[idx + 1]))
                        {
                            x = v[idx + 1]; // down: consume from b (insertion relative to a)
                        }
                        else
                        {
                            x = v[idx - 1] + 1; // right: consume from a (deletion relative to a)
                        }

                        var y = x - k;

                        while (x < n && y < m && y >= 0 && a[x] == b[y])
                        {
                            x++;
                            y++;
                        }

                        v[idx] = x;
                        if (x >= n && y >= m)
                        {
                            lastD = d;
                            trace.Add((int[])v.Clone());
                            goto DiffDone;
                        }
                    }

                    trace.Add((int[])v.Clone());
                    lastD = d;
                }

            DiffDone:
                static void AppendHtmlEncodedChar(StringBuilder sb, char ch)
                {
                    switch (ch)
                    {
                        case '&': sb.Append("&amp;"); break;
                        case '<': sb.Append("&lt;"); break;
                        case '>': sb.Append("&gt;"); break;
                        case '"': sb.Append("&quot;"); break;
                        case '\'': sb.Append("&#39;"); break;
                        default: sb.Append(ch); break;
                    }
                }

                // Backtrack to build an operation list (equal/insert/delete).
                var ops = new List<(char ch, bool isEqual)>(n + m);
                var x2 = n;
                var y2 = m;

                for (var d = lastD; d > 0; d--)
                {
                    var vPrev = trace[d - 1];
                    var k = x2 - y2;
                    var idx = k + offset;

                    int prevK;
                    if (k == -d || (k != d && vPrev[idx - 1] < vPrev[idx + 1]))
                    {
                        prevK = k + 1;
                    }
                    else
                    {
                        prevK = k - 1;
                    }

                    var prevX = vPrev[prevK + offset];
                    var prevY = prevX - prevK;

                    while (x2 > prevX && y2 > prevY)
                    {
                        // Diagonal move: matched character
                        ops.Add((a[x2 - 1], true));
                        x2--;
                        y2--;
                    }

                    // One edit step (insert/delete). We only advance indices; no match marking here.
                    if (x2 == prevX)
                    {
                        // Insertion relative to 'a' (i.e., character exists in b only)
                        if (y2 > 0) ops.Add((b[y2 - 1], false));
                        y2--;
                    }
                    else
                    {
                        // Deletion from 'a' (i.e., character exists in a only)
                        if (x2 > 0) ops.Add((a[x2 - 1], false));
                        x2--;
                    }
                }

                // Flush any remaining leading matches
                while (x2 > 0 && y2 > 0 && a[x2 - 1] == b[y2 - 1])
                {
                    ops.Add((a[x2 - 1], true));
                    x2--;
                    y2--;
                }
                // Remaining leading edits
                while (x2 > 0)
                {
                    ops.Add((a[x2 - 1], false));
                    x2--;
                }
                while (y2 > 0)
                {
                    ops.Add((b[y2 - 1], false));
                    y2--;
                }

                ops.Reverse();

                if (ops.Count == 0)
                {
                    sb.Append("<br/>");
                    return;
                }

                // Output grouped spans for fewer DOM nodes.
                var runStart = 0;
                var runClass = ops[0].isEqual ? "greenback" : "redback";
                for (var i = 1; i < ops.Count; i++)
                {
                    var cls = ops[i].isEqual ? "greenback" : "redback";
                    if (!string.Equals(runClass, cls, StringComparison.Ordinal))
                    {
                        sb.Append("<span class='").Append(runClass).Append("'>");
                        for (var j = runStart; j < i; j++)
                        {
                            AppendHtmlEncodedChar(sb, ops[j].ch);
                        }
                        sb.Append("</span>");
                        runClass = cls;
                        runStart = i;
                    }
                }

                sb.Append("<span class='").Append(runClass).Append("'>");
                for (var j = runStart; j < ops.Count; j++)
                {
                    AppendHtmlEncodedChar(sb, ops[j].ch);
                }
                sb.Append("</span>");
                sb.Append("<br/>");
            }

            var sb = new StringBuilder(capacity: Math.Min(1024 * 1024, Math.Max(text1.Length, 1024)));
            sb.Append("<div class='compareStats'>Changed lines: ")
              .Append(changedLines)
              .Append(" / ")
              .Append(totalLines)
              .Append(" &nbsp;·&nbsp; Similarity (rough): ")
              .Append(Math.Round(lineSimilarity * 100))
              .Append("%</div><br/>");

            for (var lineIndex = 0; lineIndex < l1lines.Length; lineIndex++)
            {
                var l1line = l1lines[lineIndex];
                var l2line = lineIndex < l2lines.Length ? l2lines[lineIndex] : "";

                // Use aligned diff for typical lines; fall back for very long lines to avoid heavy computation.
                const int maxAlignedLength = 4000;
                if (l1line.Length <= maxAlignedLength && l2line.Length <= maxAlignedLength)
                {
                    AppendAlignedCompare(sb, l1line, l2line);
                }
                else
                {
                    AppendPositionalCompare(sb, l1line, l2line);
                }
            }

            return Content(sb.ToString(), "text/html; charset=utf-8");
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public IActionResult Similarity(
            [FromForm] string text1,
            [FromForm] string text2,
            [FromForm] bool removeDiacritics = true,
            [FromForm] bool ignoreCase = true,
            [FromForm] bool stripPunctuation = true,
            [FromForm] bool collapseWhitespace = true,
            [FromForm] bool removeStopwords = false,
            [FromForm] int wordShingleSize = 3,
            [FromForm] int charGramSize = 5)
        {
            text1 ??= "";
            text2 ??= "";

            var warnings = new List<string>();
            var notes = new List<string>();

            const int maxInputChars = 300_000;
            if (text1.Length > maxInputChars)
            {
                text1 = text1[..maxInputChars];
                warnings.Add($"Text A was truncated to {maxInputChars:n0} characters for performance.");
            }
            if (text2.Length > maxInputChars)
            {
                text2 = text2[..maxInputChars];
                warnings.Add($"Text B was truncated to {maxInputChars:n0} characters for performance.");
            }

            wordShingleSize = Math.Clamp(wordShingleSize, 2, 8);
            charGramSize = Math.Clamp(charGramSize, 3, 10);

            var normA = Normalize(text1, removeDiacritics, ignoreCase, stripPunctuation, collapseWhitespace);
            var normB = Normalize(text2, removeDiacritics, ignoreCase, stripPunctuation, collapseWhitespace);

            var tokensA = Tokenize(normA, removeStopwords);
            var tokensB = Tokenize(normB, removeStopwords);

            if (removeStopwords)
            {
                notes.Add("Stopwords removed (can reduce similarity on very short texts).");
            }

            var tfA = TermFrequency(tokensA);
            var tfB = TermFrequency(tokensB);
            var cosine = CosineSimilarity(tfA, tfB);

            var shinglesA = WordShingles(tokensA, wordShingleSize);
            var shinglesB = WordShingles(tokensB, wordShingleSize);
            var shingleJ = Jaccard(shinglesA, shinglesB);

            var charsA = normA;
            var charsB = normB;
            var charA = CharGrams(charsA, charGramSize);
            var charB = CharGrams(charsB, charGramSize);
            var charJ = Jaccard(charA, charB);

            // Heuristic combined score:
            // - cosine (bag of words) helps re-ordering + mild paraphrase
            // - word shingles catch copy-with-small-edits
            // - char grams catch minor formatting/typo changes
            var combined =
                0.50 * cosine +
                0.35 * shingleJ +
                0.15 * charJ;
            combined = Math.Clamp(combined, 0, 1);

            // Basic "paraphrase-like" hint: high cosine but low shingles can indicate re-ordering/paraphrase.
            if (cosine >= 0.75 && shingleJ <= 0.35)
            {
                notes.Add("High word overlap but low phrase overlap: could be heavy re-ordering/paraphrase (or shared topic terms).");
            }
            if (tokensA.Length < 20 || tokensB.Length < 20)
            {
                notes.Add("Short text: similarity scores are less stable.");
            }

            var commonTokenCount = tfA.Keys.Count(k => tfB.ContainsKey(k));

            return Json(new
            {
                combinedScore = combined,
                cosineScore = cosine,
                wordShingleJaccard = shingleJ,
                charGramJaccard = charJ,
                wordShingleSize,
                charGramSize,
                tokenCountA = tokensA.Length,
                tokenCountB = tokensB.Length,
                uniqueTokenCountA = tfA.Count,
                uniqueTokenCountB = tfB.Count,
                commonTokenCount,
                warnings,
                notes
            });

            static string Normalize(string input, bool removeDiacritics, bool ignoreCase, bool stripPunctuation, bool collapseWhitespace)
            {
                input = input.Replace("\r\n", "\n").Replace('\r', '\n');

                if (removeDiacritics)
                {
                    var formD = input.Normalize(NormalizationForm.FormD);
                    var sb = new StringBuilder(formD.Length);
                    foreach (var ch in formD)
                    {
                        var uc = CharUnicodeInfo.GetUnicodeCategory(ch);
                        if (uc != UnicodeCategory.NonSpacingMark)
                        {
                            sb.Append(ch);
                        }
                    }
                    input = sb.ToString().Normalize(NormalizationForm.FormC);
                }

                if (ignoreCase)
                {
                    input = input.ToLowerInvariant();
                }

                if (stripPunctuation)
                {
                    var sb = new StringBuilder(input.Length);
                    foreach (var ch in input)
                    {
                        if (char.IsLetterOrDigit(ch) || char.IsWhiteSpace(ch))
                        {
                            sb.Append(ch);
                        }
                        else
                        {
                            sb.Append(' ');
                        }
                    }
                    input = sb.ToString();
                }

                if (collapseWhitespace)
                {
                    var sb = new StringBuilder(input.Length);
                    var prevSpace = true;
                    foreach (var ch in input)
                    {
                        var isWs = char.IsWhiteSpace(ch);
                        if (isWs)
                        {
                            if (!prevSpace) sb.Append(' ');
                            prevSpace = true;
                        }
                        else
                        {
                            sb.Append(ch);
                            prevSpace = false;
                        }
                    }
                    input = sb.ToString().Trim();
                }

                return input;
            }

            static string[] Tokenize(string normalized, bool removeStopwords)
            {
                if (string.IsNullOrWhiteSpace(normalized))
                    return Array.Empty<string>();

                var parts = normalized.Split(' ', StringSplitOptions.RemoveEmptyEntries);
                if (!removeStopwords)
                    return parts;

                // Minimal English stopword set; keep small to avoid surprises.
                // (This is optional, user-controlled.)
                var stop = Stopwords;
                var list = new List<string>(parts.Length);
                foreach (var p in parts)
                {
                    if (!stop.Contains(p)) list.Add(p);
                }
                return list.ToArray();
            }

            static Dictionary<string, int> TermFrequency(string[] tokens)
            {
                var dict = new Dictionary<string, int>(StringComparer.Ordinal);
                foreach (var t in tokens)
                {
                    if (dict.TryGetValue(t, out var c)) dict[t] = c + 1;
                    else dict[t] = 1;
                }
                return dict;
            }

            static double CosineSimilarity(Dictionary<string, int> a, Dictionary<string, int> b)
            {
                if (a.Count == 0 || b.Count == 0) return a.Count == b.Count ? 1.0 : 0.0;

                double dot = 0;
                double normA = 0;
                double normB = 0;

                foreach (var kv in a)
                {
                    var va = kv.Value;
                    normA += (double)va * va;
                    if (b.TryGetValue(kv.Key, out var vb))
                    {
                        dot += (double)va * vb;
                    }
                }

                foreach (var vb in b.Values)
                {
                    normB += (double)vb * vb;
                }

                var denom = Math.Sqrt(normA) * Math.Sqrt(normB);
                if (denom <= 0) return 0;
                return Math.Clamp(dot / denom, 0, 1);
            }

            static HashSet<string> WordShingles(string[] tokens, int n)
            {
                var set = new HashSet<string>(StringComparer.Ordinal);
                if (tokens.Length == 0) return set;
                if (tokens.Length < n)
                {
                    set.Add(string.Join(' ', tokens));
                    return set;
                }
                for (var i = 0; i <= tokens.Length - n; i++)
                {
                    set.Add(string.Join(' ', tokens, i, n));
                }
                return set;
            }

            static HashSet<string> CharGrams(string text, int n)
            {
                var set = new HashSet<string>(StringComparer.Ordinal);
                if (string.IsNullOrEmpty(text)) return set;
                if (text.Length <= n)
                {
                    set.Add(text);
                    return set;
                }
                for (var i = 0; i <= text.Length - n; i++)
                {
                    set.Add(text.Substring(i, n));
                }
                return set;
            }

            static double Jaccard(HashSet<string> a, HashSet<string> b)
            {
                if (a.Count == 0 && b.Count == 0) return 1.0;
                if (a.Count == 0 || b.Count == 0) return 0.0;

                HashSet<string> small = a.Count <= b.Count ? a : b;
                HashSet<string> large = ReferenceEquals(small, a) ? b : a;

                var inter = 0;
                foreach (var x in small)
                {
                    if (large.Contains(x)) inter++;
                }
                var union = a.Count + b.Count - inter;
                if (union <= 0) return 0.0;
                return Math.Clamp((double)inter / union, 0, 1);
            }
        }

        private static readonly HashSet<string> Stopwords = new(StringComparer.Ordinal)
        {
            "a","an","and","are","as","at","be","but","by","for","from","has","have","he","her","hers","him","his",
            "i","if","in","into","is","it","its","me","my","of","on","or","our","ours","she","so","that","the",
            "their","theirs","them","then","there","these","they","this","those","to","was","we","were","what","when",
            "where","which","who","why","will","with","you","your","yours"
        };

        [HttpPost]
        [ValidateAntiForgeryToken]
        public IActionResult JsonView([FromForm] string input, [FromForm] bool attemptRepair = true)
        {
            input ??= "";

            const int maxChars = 600_000;
            var warnings = new List<string>();
            if (input.Length > maxChars)
            {
                input = input[..maxChars];
                warnings.Add($"Input truncated to {maxChars:n0} characters for performance.");
            }

            input = input.TrimStart('\uFEFF'); // BOM, if present

            // 1) Strict parse
            if (TryFormatStrict(input, out var strictFormatted, out var strictErr, out var strictLine, out var strictCol))
            {
                return Json(new
                {
                    statusKind = "valid",
                    statusText = "Valid JSON (formatted).",
                    isValidJson = true,
                    repaired = false,
                    displayText = strictFormatted,
                    warnings,
                    errorMessage = (string?)null,
                    errorLine = (int?)null,
                    errorColumn = (int?)null
                });
            }

            if (!attemptRepair)
            {
                var structured = StructureJsonish(input, out var structWarnings);
                warnings.AddRange(structWarnings);
                return Json(new
                {
                    statusKind = "invalid-structured",
                    statusText = "JSON is not valid (structured view).",
                    isValidJson = false,
                    repaired = false,
                    displayText = structured,
                    warnings = new[]
                    {
                        "Tried to structure, but JSON is not valid.",
                        strictErr
                    }.Where(x => !string.IsNullOrWhiteSpace(x)).Concat(warnings).ToArray()
                    ,
                    errorMessage = strictErr,
                    errorLine = strictLine,
                    errorColumn = strictCol
                });
            }

            // 2) Repair attempt (best-effort)
            var repaired = RepairJsonish(input, out var repairNotes);
            warnings.AddRange(repairNotes);

            if (TryFormatStrict(repaired, out var repairedFormatted, out var repairedErr, out var repairedLine, out var repairedCol))
            {
                return Json(new
                {
                    statusKind = "repaired",
                    statusText = "Repaired JSON (formatted).",
                    isValidJson = true,
                    repaired = true,
                    displayText = repairedFormatted,
                    warnings = new[] { "Auto-fixed common issues (review output)." }.Concat(warnings).ToArray(),
                    errorMessage = (string?)null,
                    errorLine = (int?)null,
                    errorColumn = (int?)null
                });
            }

            // 3) Still not parseable => structured token view
            var structuredFallback = StructureJsonish(repaired, out var fallbackWarnings);
            warnings.AddRange(fallbackWarnings);
            warnings.Add(repairedErr);

            return Json(new
            {
                statusKind = "invalid-partial",
                statusText = "Tried to fix and structure but JSON is not valid.",
                isValidJson = false,
                repaired = false,
                displayText = structuredFallback,
                warnings = new[]
                {
                    "Tried to fix and structure but JSON is not valid."
                }.Where(x => !string.IsNullOrWhiteSpace(x)).Concat(warnings).ToArray()
                ,
                errorMessage = repairedErr,
                errorLine = repairedLine,
                errorColumn = repairedCol
            });

            static bool TryFormatStrict(string json, out string formatted, out string error, out int? errorLine, out int? errorColumn)
            {
                formatted = "";
                error = "";
                errorLine = null;
                errorColumn = null;
                try
                {
                    using var doc = JsonDocument.Parse(json, new JsonDocumentOptions
                    {
                        AllowTrailingCommas = false,
                        CommentHandling = JsonCommentHandling.Disallow
                    });
                    formatted = JsonSerializer.Serialize(doc.RootElement, new JsonSerializerOptions
                    {
                        WriteIndented = true
                    });
                    return true;
                }
                catch (JsonException jex)
                {
                    error = jex.Message;
                    if (jex.LineNumber.HasValue) errorLine = (int)jex.LineNumber.Value + 1;
                    if (jex.BytePositionInLine.HasValue) errorColumn = (int)jex.BytePositionInLine.Value + 1;
                    return false;
                }
                catch (Exception ex)
                {
                    error = ex.Message;
                    return false;
                }
            }

            static string RepairJsonish(string inputJson, out List<string> notes)
            {
                notes = new List<string>();
                var s = inputJson;

                // Remove JS-style comments (common in "JSON")
                var noComments = StripComments(s);
                if (!string.Equals(noComments, s, StringComparison.Ordinal))
                {
                    notes.Add("Removed comments (// and /* */).");
                    s = noComments;
                }

                // Remove trailing commas before } or ]
                var noTrailing = Regex.Replace(s, @",(\s*[}\]])", "$1");
                if (!string.Equals(noTrailing, s, StringComparison.Ordinal))
                {
                    notes.Add("Removed trailing commas.");
                    s = noTrailing;
                }

                // Quote unquoted property names: { a: 1 } -> { "a": 1 }
                // This is best-effort; it only targets simple identifier-like keys.
                var beforeKeys = s;
                s = Regex.Replace(
                    s,
                    @"(?<=\{|,)\s*([A-Za-z_][A-Za-z0-9_\-]*)\s*:",
                    m => $" \"{m.Groups[1].Value}\":",
                    RegexOptions.CultureInvariant);
                if (!string.Equals(beforeKeys, s, StringComparison.Ordinal))
                {
                    notes.Add("Quoted unquoted object keys.");
                }

                // Convert single-quoted strings to double-quoted (very common in relaxed JSON).
                // Best-effort: only converts '...'-style tokens, not apostrophes inside words.
                var beforeSingles = s;
                s = Regex.Replace(s, @"'([^'\\]*(?:\\.[^'\\]*)*)'", m =>
                {
                    var inner = m.Groups[1].Value.Replace("\"", "\\\"");
                    return $"\"{inner}\"";
                });
                if (!string.Equals(beforeSingles, s, StringComparison.Ordinal))
                {
                    notes.Add("Converted single-quoted strings to double quotes.");
                }

                // Normalize Python/JS-ish literals sometimes seen in "JSON"
                var beforeLits = s;
                s = Regex.Replace(s, @"\bTrue\b", "true");
                s = Regex.Replace(s, @"\bFalse\b", "false");
                s = Regex.Replace(s, @"\bNone\b", "null");
                if (!string.Equals(beforeLits, s, StringComparison.Ordinal))
                {
                    notes.Add("Normalized True/False/None literals.");
                }

                return s;

                static string StripComments(string input)
                {
                    if (string.IsNullOrEmpty(input)) return input;
                    var sb = new StringBuilder(input.Length);
                    var i = 0;
                    var inString = false;
                    char stringQuote = '"';

                    while (i < input.Length)
                    {
                        var c = input[i];
                        if (inString)
                        {
                            sb.Append(c);
                            if (c == '\\' && i + 1 < input.Length)
                            {
                                sb.Append(input[i + 1]);
                                i += 2;
                                continue;
                            }
                            if (c == stringQuote) inString = false;
                            i++;
                            continue;
                        }

                        if (c == '"' || c == '\'')
                        {
                            inString = true;
                            stringQuote = c;
                            sb.Append(c);
                            i++;
                            continue;
                        }

                        // Line comment //
                        if (c == '/' && i + 1 < input.Length && input[i + 1] == '/')
                        {
                            i += 2;
                            while (i < input.Length && input[i] != '\n') i++;
                            continue;
                        }

                        // Block comment /* */
                        if (c == '/' && i + 1 < input.Length && input[i + 1] == '*')
                        {
                            i += 2;
                            while (i + 1 < input.Length && !(input[i] == '*' && input[i + 1] == '/')) i++;
                            i = Math.Min(input.Length, i + 2);
                            continue;
                        }

                        sb.Append(c);
                        i++;
                    }

                    return sb.ToString();
                }
            }

            static string StructureJsonish(string inputJson, out List<string> notes)
            {
                notes = new List<string>();
                if (string.IsNullOrWhiteSpace(inputJson))
                {
                    notes.Add("Empty input.");
                    return "";
                }

                var s = inputJson.Replace("\r\n", "\n").Replace('\r', '\n');

                var sb = new StringBuilder(Math.Min(256_000, s.Length + 256));
                var indent = 0;
                var inString = false;
                char quote = '"';
                var escape = false;
                var depthBalance = 0;

                void NewLine()
                {
                    sb.Append('\n');
                    sb.Append(' ', indent * 2);
                }

                // Token-ish pretty printer that tries to keep structure even when invalid.
                for (var i = 0; i < s.Length; i++)
                {
                    var c = s[i];
                    if (inString)
                    {
                        sb.Append(c);
                        if (escape)
                        {
                            escape = false;
                            continue;
                        }
                        if (c == '\\')
                        {
                            escape = true;
                            continue;
                        }
                        if (c == quote)
                        {
                            inString = false;
                        }
                        continue;
                    }

                    switch (c)
                    {
                        case '"':
                        case '\'':
                            inString = true;
                            quote = c;
                            sb.Append(c);
                            break;

                        case '{':
                        case '[':
                            sb.Append(c);
                            depthBalance++;
                            indent++;
                            NewLine();
                            break;

                        case '}':
                        case ']':
                            depthBalance--;
                            indent = Math.Max(0, indent - 1);
                            NewLine();
                            sb.Append(c);
                            break;

                        case ',':
                            sb.Append(c);
                            NewLine();
                            break;

                        case ':':
                            sb.Append(": ");
                            break;

                        case '\n':
                            // ignore original newlines; we control formatting
                            break;

                        default:
                            if (char.IsWhiteSpace(c))
                            {
                                // collapse whitespace
                                if (sb.Length > 0 && sb[^1] != ' ' && sb[^1] != '\n')
                                    sb.Append(' ');
                            }
                            else
                            {
                                sb.Append(c);
                            }
                            break;
                    }
                }

                if (inString) notes.Add("Unterminated string detected (input ended inside a quote).");
                if (depthBalance != 0) notes.Add("Unbalanced braces/brackets detected (structure may be incomplete).");

                return sb.ToString().Trim();
            }
        }

        [ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
        public IActionResult Error()
        {
            return View(new ErrorViewModel { RequestId = Activity.Current?.Id ?? HttpContext.TraceIdentifier });
        }
    }
}
