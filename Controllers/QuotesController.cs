using Microsoft.AspNetCore.Mvc;
using MotivationQuotesAPI.Models;
using Microsoft.EntityFrameworkCore;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.IO;
using DotNetEnv;
using Telegram.Bot;
using Telegram.Bot.Types.Enums;

namespace MotivationQuotesAPI.Controllers
{
    [Route("quotes")]
    [ApiController]
    public class QuotesController : ControllerBase
    {
        private readonly QuotesDbContext _dbContext; //підключення до бази даних
        private readonly HttpClient _httpClient; // клієнт для запитів до зовнішнього API

        public QuotesController(QuotesDbContext dbContext, IHttpClientFactory httpClientFactory)
        {
            _dbContext = dbContext;
            _httpClient = httpClientFactory.CreateClient();
        }

        [HttpGet]
        public IActionResult GetInfo()
        {
            return Ok("Quotes API is working ✅");
        }

        // Отримати випадкову цитату з зовнішнього API
        [HttpGet("random")]
        public async Task<IActionResult> GetRandomQuote()
        {
            HttpResponseMessage response;
            try
            {
                response = await _httpClient.GetAsync("https://zenquotes.io/api/quotes");
            }
            catch (Exception ex)
            {
                return StatusCode(500, $"Не вдалося здійснити запит до зовнішнього API: {ex.Message}");
            }

            if (!response.IsSuccessStatusCode)
            {
                return StatusCode((int)response.StatusCode, "Помилка при отриманні цитат.");
            }

            try
            {
                var content = await response.Content.ReadAsStringAsync();

                var quotes = JsonSerializer.Deserialize<List<ApiQuote>>(content, new JsonSerializerOptions
                {
                    PropertyNameCaseInsensitive = true
                });

                if (quotes == null || quotes.Count == 0)
                {
                    return NotFound("Цитати не знайдені.");
                }

                var random = new Random();
                var randomQuote = quotes[random.Next(quotes.Count)];

                // Зберігаємо в історію пошуків
                var searchHistory = new SearchHistory
                {
                    Query = $"{randomQuote.QuoteText} — {randomQuote.Author}",
                    SearchDate = DateTime.UtcNow
                };

                _dbContext.SearchHistories.Add(searchHistory);
                await _dbContext.SaveChangesAsync();

                // Перевіряємо, чи вже є така цитата в базі
                var existingQuote = await _dbContext.Quotes
                    .FirstOrDefaultAsync(q => q.Text == randomQuote.QuoteText && q.Author == randomQuote.Author);

                if (existingQuote == null)
                {
                    existingQuote = new Quote
                    {
                        Text = randomQuote.QuoteText,
                        Author = randomQuote.Author
                    };

                    _dbContext.Quotes.Add(existingQuote);
                    await _dbContext.SaveChangesAsync();
                }

                return Ok(new
                {
                    id = existingQuote.Id,
                    text = existingQuote.Text,
                    author = existingQuote.Author,
                });
            }
            catch (JsonException ex)
            {
                return StatusCode(500, $"Помилка обробки JSON: {ex.Message}");
            }
            catch (Exception ex)
            {
                return StatusCode(500, $"Невідома помилка: {ex.Message}");
            }
        }

        // Додати цитату до улюблених
        [HttpPost("favorites/add")]
        public async Task<IActionResult> AddToFavorites([FromBody] Quote quote)
        {
            if (quote == null || string.IsNullOrEmpty(quote.Text) || string.IsNullOrEmpty(quote.Author))
            {
                return BadRequest("Текст і автор цитати є обов'язковими.");
            }

            // Перевіряємо, чи цитата вже існує в базі
            var existingQuote = await _dbContext.Quotes.FirstOrDefaultAsync(q => q.Text == quote.Text && q.Author == quote.Author);

            if (existingQuote == null)
            {
                // Якщо цитата не існує, додаємо її
                existingQuote = new Quote
                {
                    Text = quote.Text,
                    Author = quote.Author
                };
                _dbContext.Quotes.Add(existingQuote);
                await _dbContext.SaveChangesAsync();
            }

            // Перевіряємо, чи цитата вже в улюблених
            var existingFavorite = await _dbContext.Favorites.AnyAsync(f => f.QuoteId == existingQuote.Id);

            if (existingFavorite)
            {
                return Conflict("Цитата вже додана до улюблених.");
            }

            // Додаємо цитату до улюблених
            var favorite = new Favorite
            {
                QuoteId = existingQuote.Id
            };
            _dbContext.Favorites.Add(favorite);
            await _dbContext.SaveChangesAsync();

            return Ok(new { message = "Цитата успішно додана до улюблених." });
        }

        // Отримати список усіх улюблених цитат
        [HttpGet("favorites/list")]
        public async Task<IActionResult> GetFavorites()
        {
            var favorites = await _dbContext.Favorites.Include(f => f.Quote).ToListAsync();

            if (favorites.Count == 0)
            {
                return Ok(new
                {
                    count = 0,
                    quotes = new List<object>()
                });
            }

            return Ok(new
            {
                count = favorites.Count,
                quotes = favorites.Select(f => new
                {
                    f.Quote.Id,
                    f.Quote.Text,
                    f.Quote.Author
                })
            });
        }

        // Видалити цитату з улюблених
        [HttpDelete("favorites/delete/{id}")]
        public async Task<IActionResult> RemoveFromFavorites(int id)
        {
            var favorite = await _dbContext.Favorites.FirstOrDefaultAsync(f => f.QuoteId == id);

            if (favorite == null)
            {
                return NotFound("Цитату не знайдено серед улюблених.");
            }

            _dbContext.Favorites.Remove(favorite);
            await _dbContext.SaveChangesAsync();

            return Ok(new { message = "Цитата успішно видалена з улюблених." });
        }

        //показати історію пошуку
        [HttpGet("history")]
        public async Task<IActionResult> GetSearchHistory()
        {
            var history = await _dbContext.SearchHistories.OrderByDescending(h => h.SearchDate).Take(5).ToListAsync();

            if (history.Count == 0)
            {
                return NotFound("Історія порожня.");
            }

            return Ok(history);
        }

        //очищити історію пошуку
        [HttpDelete("history/clear")]
        public async Task<IActionResult> ClearSearchHistory()
        {
            var allHistory = await _dbContext.SearchHistories.ToListAsync();

            if (allHistory == null || allHistory.Count == 0)
            {
                return NotFound("Історії пошуку цитат ще немає.");
            }

            _dbContext.SearchHistories.RemoveRange(allHistory);
            await _dbContext.SaveChangesAsync();

            return Ok(new { message = "Історію пошуку успішно очищено." });
        }

        //картинка з цитатами
        [HttpGet("image")]
        public async Task<IActionResult> GetQuoteImage()
        {
            using var httpClient = new HttpClient();
            var response = await httpClient.GetAsync("https://zenquotes.io/api/image");

            if (!response.IsSuccessStatusCode)
            {
                return StatusCode((int)response.StatusCode, "Не вдалося отримати зображення.");
            }

            var imageBytes = await response.Content.ReadAsByteArrayAsync();
            var contentType = response.Content.Headers.ContentType?.MediaType ?? "image/jpeg";

            return File(imageBytes, contentType);
        }

        //для перегляду найулюбленіших цитат усіх користувачів (за кількістю збережень)
        [HttpGet("top")]
        public async Task<IActionResult> GetTopFavorites()
        {
            var top = await _dbContext.Favorites
                .GroupBy(f => f.QuoteId)
                .Select(g => new
                {
                    QuoteId = g.Key,
                    Count = g.Count()
                })
                .OrderByDescending(x => x.Count)
                .Take(5)
                .Join(_dbContext.Quotes, g => g.QuoteId, q => q.Id, (g, q) => new
                {
                    q.Id,
                    q.Text,
                    q.Author,
                    FavoritesCount = g.Count
                })
                .ToListAsync();

            return Ok(top);
        }

        // підписка на щодену цитату
        [HttpPost("daily")]
        public async Task<IActionResult> SubscribeToDaily([FromQuery] long chatId, [FromQuery] TimeSpan time)
        {
            var exists = await _dbContext.DailySubscribers.AnyAsync(s => s.ChatId == chatId);
            if (exists)
                return Conflict(new { message = "Ви вже підписані." });

            _dbContext.DailySubscribers.Add(new DailySubscriber
            {
                ChatId = chatId,
                PreferredTime = time.ToString(@"HH\:mm")
            });

            await _dbContext.SaveChangesAsync();
            return Ok(new { message = "Підписка оформлена успішно!" });
        }

        //відписка від щодених цитат
        [HttpPost("unsubscribe")]
        public async Task<IActionResult> UnsubscribeFromDaily([FromQuery] long chatId)
        {
            try
            {
                var subscriber = await _dbContext.DailySubscribers.FirstOrDefaultAsync(s => s.ChatId == chatId);
                if (subscriber == null)
                    return NotFound(new { message = "Вас не знайдено в списку підписників." });

                _dbContext.DailySubscribers.Remove(subscriber);
                await _dbContext.SaveChangesAsync();

                return Ok(new { message = "Ви успішно відписалися." });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { message = $"Server error: {ex.Message}" });
            }
        }

        [HttpPost("daily/send")]
        public async Task<IActionResult> SendDailyQuotes([FromQuery] string time)
        {
            Console.WriteLine($"⏰ Час запиту: {time}");

            var subscribers = await _dbContext.DailySubscribers
                .Where(s => s.PreferredTime == time)
                .ToListAsync();

            Console.WriteLine($"👥 Підписників знайдено: {subscribers.Count}");

            if (!subscribers.Any())
                return Ok("👥 Немає підписників на цей час.");

            using var httpClient = new HttpClient();
            var response = await httpClient.GetAsync("https://motivation-quotes-api-production.up.railway.app/quotes/random");

            Console.WriteLine($"🌐 Статус цитати: {response.StatusCode}");

            if (!response.IsSuccessStatusCode)
                return StatusCode((int)response.StatusCode, "❌ Не вдалося отримати цитату.");

            var json = await response.Content.ReadAsStringAsync();
            Console.WriteLine($"📦 JSON від API: {json}");

            var quote = JsonSerializer.Deserialize<ApiQuote>(json, new JsonSerializerOptions { PropertyNameCaseInsensitive = true });

            if (quote == null)
            {
                Console.WriteLine("⚠️ Помилка: Цитата пуста.");
                return StatusCode(500, "⚠️ Невірні дані цитати.");
            }

            var botToken = Environment.GetEnvironmentVariable("BOT_TOKEN");
            if (string.IsNullOrEmpty(botToken))
            {
                Console.WriteLine("⚠️ BOT_TOKEN не знайдено.");
                return StatusCode(500, "⚠️ BOT_TOKEN не встановлений в змінних середовища.");
            }

            var botClient = new TelegramBotClient(botToken);

            foreach (var user in subscribers)
            {
                string msg = $"💬 \"{quote.QuoteText}\"\n— {quote.Author}";
                await botClient.SendTextMessageAsync(user.ChatId, msg);
            }

            Console.WriteLine("✅ Успішно надіслано всім.");
            return Ok("✅ Цитати надіслано всім підписникам.");
        }


        [HttpGet("rating/{quoteId}")]
        public async Task<IActionResult> GetRating(int quoteId)
        {
            var quote = await _dbContext.Quotes.FindAsync(quoteId);
            if (quote == null)
                return NotFound("Цитату не знайдено.");

            var likes = await _dbContext.QuoteReactions.CountAsync(r => r.QuoteId == quoteId && r.IsLike);
            var dislikes = await _dbContext.QuoteReactions.CountAsync(r => r.QuoteId == quoteId && !r.IsLike);

            return Ok(new { Likes = likes, Dislikes = dislikes });
        }

    }
}

