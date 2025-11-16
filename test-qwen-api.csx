// Qwen API Test - Правильна структура запиту
// Використовуйте цей код для швидкого тестування API

using System;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;

var client = new HttpClient();

// Правильна структура: data містить масив з одним рядком (промптом)
var payload = new 
{ 
    data = new[] 
    { 
        @"Ти асистент пошукової системи Vetale Search. Користувач ввів пошуковий запит: ""веб розробка"".

Твоє завдання:
1. Зрозуміти намір користувача
2. Написати короткий (2-3 речення) підсумок того, що шукає користувач
3. Дати корисну пораду щодо пошуку

Відповідай українською мовою, коротко та по суті.

Підсумок:" 
    } 
};

var json = JsonSerializer.Serialize(payload);
Console.WriteLine("=== REQUEST ===");
Console.WriteLine(json);
Console.WriteLine();

var response = await client.PostAsync(
    "https://qwen-qwen2-5-7b-instruct.hf.space/run/predict",
    new StringContent(json, Encoding.UTF8, "application/json")
);

var raw = await response.Content.ReadAsStringAsync();
Console.WriteLine("=== RESPONSE ===");
Console.WriteLine(raw);
Console.WriteLine();

// Parse response
using var doc = JsonDocument.Parse(raw);
if (doc.RootElement.TryGetProperty("data", out var dataElement) &&
    dataElement.GetArrayLength() > 0)
{
    var generatedText = dataElement[0].GetString();
    Console.WriteLine("=== AI SUMMARY ===");
    Console.WriteLine(generatedText);
}

