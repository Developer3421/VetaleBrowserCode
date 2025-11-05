using Microsoft.Playwright;
using System;
using System.Threading.Tasks;

class Program
{
    public static async Task Main()
    {
        // Ініціалізація Playwright
        using var playwright = await Playwright.CreateAsync();

        // Запускаємо Chromium у headless режимі (без UI)
        await using var browser = await playwright.Chromium.LaunchAsync(new()
        {
            Headless = true // постав false якщо хочеш бачити браузер
        });

        var context = await browser.NewContextAsync();
        var page = await context.NewPageAsync();

        // Відкриваємо сторінку
        await page.GotoAsync("https://example.com");

        // Отримуємо HTML-код
        string html = await page.ContentAsync();

        // Виконуємо JavaScript прямо у сторінці
        string title = await page.EvaluateAsync<string>("() => document.title");

        // Зберігаємо скріншот
        await page.ScreenshotAsync(new()
        {
            Path = "example.png",
            FullPage = true
        });

        Console.WriteLine($"Title: {title}");
        Console.WriteLine($"HTML length: {html.Length}");
    }
}

