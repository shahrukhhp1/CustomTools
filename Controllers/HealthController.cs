using System.Diagnostics;
using System.Reflection;
using Microsoft.AspNetCore.Mvc;

namespace CustomWebTools.Controllers
{
    public class HealthController : Controller
    {
        [HttpGet]
        public IActionResult Index()
        {
            Response.Headers["X-Robots-Tag"] = "noindex, nofollow";
            var now = DateTimeOffset.UtcNow;

            DateTimeOffset? processStartUtc = null;
            double? uptimeSeconds = null;
            try
            {
                var startLocal = Process.GetCurrentProcess().StartTime;
                processStartUtc = new DateTimeOffset(startLocal).ToUniversalTime();
                uptimeSeconds = Math.Max(0, (now - processStartUtc.Value).TotalSeconds);
            }
            catch
            {
                // best-effort only
            }

            var proc = Process.GetCurrentProcess();
            var workingSetMb = proc.WorkingSet64 / 1024d / 1024d;
            var gcHeapMb = GC.GetTotalMemory(forceFullCollection: false) / 1024d / 1024d;
            const double memoryLimitMb = 1024;
            var wsPct = memoryLimitMb <= 0 ? (double?)null : Math.Clamp((workingSetMb / memoryLimitMb) * 100.0, 0, 10_000);
            var gcPct = memoryLimitMb <= 0 ? (double?)null : Math.Clamp((gcHeapMb / memoryLimitMb) * 100.0, 0, 10_000);

            var asm = Assembly.GetEntryAssembly() ?? Assembly.GetExecutingAssembly();
            var version = asm.GetName().Version?.ToString() ?? "unknown";
            var informationalVersion = asm.GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion ?? version;

            return Json(new
            {
                status = "ok",
                timeUtc = now.ToString("O"),
                environment = Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT") ?? "unknown",
                version,
                informationalVersion,
                processStartUtc = processStartUtc?.ToString("O"),
                uptimeSeconds,
                memoryLimitMb,
                memory = new
                {
                    workingSetMb = Math.Round(workingSetMb, 1),
                    gcHeapMb = Math.Round(gcHeapMb, 1),
                    workingSetPctOfLimit = wsPct.HasValue ? Math.Round(wsPct.Value, 1) : null,
                    gcHeapPctOfLimit = gcPct.HasValue ? Math.Round(gcPct.Value, 1) : null
                }
            });
        }

        [HttpGet]
        public IActionResult Ui()
        {
            Response.Headers["X-Robots-Tag"] = "noindex, nofollow";
            ViewData["Title"] = "Health status – DailyTools";
            ViewData["MetaDescription"] = "Server health status dashboard (status, uptime, version, memory).";
            ViewData["Robots"] = "noindex, nofollow";
            return View();
        }
    }
}

