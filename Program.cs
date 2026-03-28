namespace CustomWebTools
{
    public class Program
    {
        public static void Main(string[] args)
        {
            var builder = WebApplication.CreateBuilder(args);

            // Add services to the container.
            builder.Services.AddControllersWithViews();
            builder.Services.AddAntiforgery(options =>
            {
                // default header many clients use for AJAX
                options.HeaderName = "RequestVerificationToken";
            });

            var app = builder.Build();

            // Configure the HTTP request pipeline.
            if (!app.Environment.IsDevelopment())
            {
                app.UseExceptionHandler("/Home/Error");
                // The default HSTS value is 30 days. You may want to change this for production scenarios, see https://aka.ms/aspnetcore-hsts.
                app.UseHsts();
            }

            app.UseHttpsRedirection();
            app.UseStaticFiles();

            app.UseRouting();

            app.UseAntiforgery();
            app.UseAuthorization();

            app.MapControllerRoute(
                name: "text-compare",
                pattern: "text-compare",
                defaults: new { controller = "Tools", action = "TextCompare" });

            app.MapControllerRoute(
                name: "similarity-score",
                pattern: "similarity-score",
                defaults: new { controller = "Tools", action = "SimilarityScore" });

            app.MapControllerRoute(
                name: "json-viewer",
                pattern: "json-viewer",
                defaults: new { controller = "Tools", action = "JsonViewer" });

            app.MapControllerRoute(
                name: "character-count",
                pattern: "character-count",
                defaults: new { controller = "Tools", action = "CharacterCount" });

            app.MapControllerRoute(
                name: "word-to-number",
                pattern: "word-to-number",
                defaults: new { controller = "Tools", action = "WordToNumber" });

            // SEO alias (keep old path working)
            app.MapControllerRoute(
                name: "word-number-converter",
                pattern: "word-number-converter",
                defaults: new { controller = "Tools", action = "WordToNumber" });

            app.MapControllerRoute(
                name: "changelog",
                pattern: "changelog",
                defaults: new { controller = "Home", action = "Changelog" });

            // Alias for sharing (release notes == changelog)
            app.MapControllerRoute(
                name: "release-notes",
                pattern: "release-notes",
                defaults: new { controller = "Home", action = "Changelog" });

            app.MapControllerRoute(
                name: "default",
                pattern: "{controller=Home}/{action=Index}/{id?}");

            app.Run();
        }
    }
}
