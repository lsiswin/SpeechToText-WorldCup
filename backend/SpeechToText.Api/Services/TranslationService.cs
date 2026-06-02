using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Text.Json;
using System.Threading.Tasks;
using Microsoft.Extensions.Configuration;
using SpeechToText.Api.Models;

namespace SpeechToText.Api.Services
{
    public class TranslationService : ITranslationService
    {
        private readonly IConfiguration _configuration;

        public TranslationService(IConfiguration configuration)
        {
            _configuration = configuration;
        }

        public async Task<List<TranscriptionSegment>> TranslateSegmentsAsync(List<TranscriptionSegment> segments, string targetLanguage = "zh-CN")
        {
            if (segments == null || segments.Count == 0)
            {
                return new List<TranscriptionSegment>();
            }

            var provider = _configuration["Translation:Provider"] ?? "GoogleFree";
            var apiKey = _configuration["Translation:ApiKey"];

            if (provider.Equals("DeepSeek", StringComparison.OrdinalIgnoreCase) && !string.IsNullOrWhiteSpace(apiKey))
            {
                try
                {
                    Console.WriteLine("[TranslationService] Translating subtitles using DeepSeek API...");
                    return await TranslateWithDeepSeekAsync(segments, targetLanguage);
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"[TranslationService] DeepSeek translation failed: {ex.Message}. Falling back to Google Free Translate.");
                    return await TranslateWithGoogleFreeAsync(segments, targetLanguage);
                }
            }

            Console.WriteLine("[TranslationService] Translating subtitles using Google Free Translate...");
            return await TranslateWithGoogleFreeAsync(segments, targetLanguage);
        }

        private async Task<List<TranscriptionSegment>> TranslateWithDeepSeekAsync(List<TranscriptionSegment> segments, string targetLanguage)
        {
            var apiKey = _configuration["Translation:ApiKey"];
            var baseUrl = _configuration["Translation:BaseUrl"] ?? "https://api.deepseek.com";
            var model = _configuration["Translation:Model"] ?? "deepseek-chat";

            if (baseUrl.EndsWith("/"))
            {
                baseUrl = baseUrl.Substring(0, baseUrl.Length - 1);
            }
            var apiUrl = $"{baseUrl}/v1/chat/completions";

            var results = new List<TranscriptionSegment>();
            var batchSize = 25; // Safe size for context and response length

            using var client = new HttpClient();
            client.DefaultRequestHeaders.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", apiKey);
            client.Timeout = TimeSpan.FromMinutes(3);

            for (int i = 0; i < segments.Count; i += batchSize)
            {
                var batch = segments.Skip(i).Take(batchSize).ToList();
                var payloadItems = batch.Select(s => new { id = s.Index, text = s.Text }).ToList();
                var payloadJson = JsonSerializer.Serialize(payloadItems);

                var requestBody = new
                {
                    model = model,
                    messages = new[]
                    {
                        new { role = "system", content = "You are a professional video subtitle translator. Translate the text segments in the given JSON array into natural, concise Simplified Chinese. Return the translation as a JSON array matching the exact structure and keys: [{\"id\": number, \"text\": \"translated_text\"}]. Return ONLY the raw JSON array. Do not include markdown code block syntax (like ```json), explanations, or notes." },
                        new { role = "user", content = payloadJson }
                    },
                    temperature = 0.2
                };

                var httpContent = new StringContent(JsonSerializer.Serialize(requestBody), System.Text.Encoding.UTF8, "application/json");
                
                try
                {
                    var response = await client.PostAsync(apiUrl, httpContent);
                    response.EnsureSuccessStatusCode();

                    var responseJsonStr = await response.Content.ReadAsStringAsync();
                    using var responseDoc = JsonDocument.Parse(responseJsonStr);
                    var choices = responseDoc.RootElement.GetProperty("choices");
                    var content = choices[0].GetProperty("message").GetProperty("content").GetString() ?? "";

                    content = content.Trim();
                    
                    // Strip Markdown fence blocks if present
                    if (content.StartsWith("```"))
                    {
                        var lines = content.Split('\n');
                        content = string.Join("\n", lines.Skip(1).Take(lines.Length - 2)).Trim();
                        if (content.EndsWith("```"))
                        {
                            content = content.Substring(0, content.Length - 3).Trim();
                        }
                    }

                    var translatedBatch = JsonSerializer.Deserialize<List<TranslatedSegment>>(content);
                    if (translatedBatch != null)
                    {
                        foreach (var s in batch)
                        {
                            var match = translatedBatch.FirstOrDefault(tb => tb.Id == s.Index);
                            results.Add(new TranscriptionSegment
                            {
                                Index = s.Index,
                                Start = s.Start,
                                End = s.End,
                                Text = match != null && !string.IsNullOrWhiteSpace(match.Text) ? match.Text : s.Text
                            });
                        }
                        continue;
                    }
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"[TranslationService] Batch translation parsing failed for batch index {i}. Error: {ex.Message}. Falling back to Google Free for this batch.");
                }

                // Fallback for this specific batch
                var fallbackBatch = await TranslateBatchWithGoogleFreeAsync(batch, targetLanguage);
                results.AddRange(fallbackBatch);
            }

            return results.OrderBy(r => r.Index).ToList();
        }

        private async Task<List<TranscriptionSegment>> TranslateWithGoogleFreeAsync(List<TranscriptionSegment> segments, string targetLanguage)
        {
            var tasks = segments.Select(async s =>
            {
                try
                {
                    var translatedText = await GoogleTranslateAsync(s.Text, targetLanguage);
                    return new TranscriptionSegment
                    {
                        Index = s.Index,
                        Start = s.Start,
                        End = s.End,
                        Text = translatedText
                    };
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"[TranslationService] Google translate failed for segment {s.Index}: {ex.Message}");
                    return new TranscriptionSegment
                    {
                        Index = s.Index,
                        Start = s.Start,
                        End = s.End,
                        Text = s.Text
                    };
                }
            }).ToList();

            var results = await Task.WhenAll(tasks);
            return results.OrderBy(r => r.Index).ToList();
        }

        private async Task<List<TranscriptionSegment>> TranslateBatchWithGoogleFreeAsync(List<TranscriptionSegment> batch, string targetLanguage)
        {
            var tasks = batch.Select(async s =>
            {
                try
                {
                    var translatedText = await GoogleTranslateAsync(s.Text, targetLanguage);
                    return new TranscriptionSegment
                    {
                        Index = s.Index,
                        Start = s.Start,
                        End = s.End,
                        Text = translatedText
                    };
                }
                catch
                {
                    return s;
                }
            }).ToList();

            var results = await Task.WhenAll(tasks);
            return results.ToList();
        }

        private async Task<string> GoogleTranslateAsync(string text, string targetLanguage)
        {
            if (string.IsNullOrWhiteSpace(text)) return string.Empty;

            using var client = new HttpClient();
            client.DefaultRequestHeaders.UserAgent.ParseAdd("Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/120.0.0.0 Safari/537.36");
            
            var url = $"https://translate.googleapis.com/translate_a/single?client=gtx&sl=auto&tl={targetLanguage}&dt=t&q={Uri.EscapeDataString(text)}";
            
            var response = await client.GetAsync(url);
            response.EnsureSuccessStatusCode();
            
            var jsonStr = await response.Content.ReadAsStringAsync();
            using var doc = JsonDocument.Parse(jsonStr);
            var root = doc.RootElement;
            var array = root[0];
            
            var sb = new System.Text.StringBuilder();
            foreach (var item in array.EnumerateArray())
            {
                sb.Append(item[0].GetString());
            }
            return sb.ToString().Trim();
        }

        private class TranslatedSegment
        {
            [System.Text.Json.Serialization.JsonPropertyName("id")]
            public int Id { get; set; }

            [System.Text.Json.Serialization.JsonPropertyName("text")]
            public string Text { get; set; } = string.Empty;
        }
    }
}
