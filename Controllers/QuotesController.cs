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
        public async Task<IActionResult> GetRandomQuote([FromQuery] long userId)
        {
            var response = await _httpClient.GetAsync("https://zenquotes.io/api/quotes");
            if (!response.IsSuccessStatusCode)
                return StatusCode((int)response.StatusCode, "Помилка при отриманні цитат.");

            var content = await response.Content.ReadAsStringAsync();
            var quotes = JsonSerializer.Deserialize<List<ApiQuote>>(content, new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true
            });

            if (quotes == null || quotes.Count == 0)
                return NotFound("Цитати не знайдені.");

            var random = new Random();
            var randomQuote = quotes[random.Next(quotes.Count)];

            // Шукаємо цитату в базі
            var existingQuote = await _dbContext.Quotes.FirstOrDefaultAsync(q => q.Text == randomQuote.QuoteText && q.Author == randomQuote.Author);
            if (existingQuote == null)
            {
                existingQuote = new Quote
                {
                    Text = randomQuote.QuoteText,
                    Author = randomQuote.Author,
                    Likes = 0,
                    Dislikes = 0
                };
                _dbContext.Quotes.Add(existingQuote);
                await _dbContext.SaveChangesAsync();
            }

            // Перевірка чи користувач вже бачив цю цитату
            var alreadySeen = await _dbContext.SearchHistories
                .AnyAsync(h => h.UserId == userId && h.Query == $"{existingQuote.Text} — {existingQuote.Author}");

            if (!alreadySeen)
            {
                var history = new SearchHistory
                {
                    UserId = userId,
                    Query = $"{existingQuote.Text} — {existingQuote.Author}",
                    SearchDate = DateTime.UtcNow
                };
                _dbContext.SearchHistories.Add(history);
                await _dbContext.SaveChangesAsync();
            }

            // Перевірити, чи користувач переглянув усі цитати
            var seenCount = await _dbContext.SearchHistories
                .CountAsync(h => h.UserId == userId);
            var totalCount = await _dbContext.Quotes.CountAsync();

            if (seenCount >= totalCount)
            {
                // Очистити історію тільки цього користувача
                var userHistory = _dbContext.SearchHistories.Where(h => h.UserId == userId);
                _dbContext.SearchHistories.RemoveRange(userHistory);
                await _dbContext.SaveChangesAsync();
            }

            return Ok(new
            {
                id = existingQuote.Id,
                text = existingQuote.Text,
                author = existingQuote.Author,
                likes = existingQuote.Likes,
                dislikes = existingQuote.Dislikes
            });
        }


        // Додати цитату до улюблених
        [HttpPost("favorites/add")]
        public async Task<IActionResult> AddToFavorites([FromBody] Quote quote)
        {
            if (quote == null || string.IsNullOrEmpty(quote.Text) || string.IsNullOrEmpty(quote.Author))
                return BadRequest("Текст і автор цитати є обов'язковими.");

            // Перевіряємо, чи цитата вже є в базі
            var existingQuote = await _dbContext.Quotes
                .FirstOrDefaultAsync(q => q.Text == quote.Text && q.Author == quote.Author);

            if (existingQuote == null)
            {
                existingQuote = new Quote
                {
                    Text = quote.Text,
                    Author = quote.Author
                };
                _dbContext.Quotes.Add(existingQuote);
                await _dbContext.SaveChangesAsync();
            }

            // Чи вже додано в улюблені
            bool alreadyFavorite = await _dbContext.Favorites
                .AnyAsync(f => f.QuoteId == existingQuote.Id && f.UserId == quote.UserId);

            if (alreadyFavorite)
                return Conflict("Цитата вже в улюблених.");

            var favorite = new Favorite
            {
                QuoteId = existingQuote.Id,
                UserId = quote.UserId
            };
            _dbContext.Favorites.Add(favorite);
            await _dbContext.SaveChangesAsync();

            return Ok(new { message = "Цитата додана до улюблених." });
        }

        [HttpGet("favorites/list")]
        public async Task<IActionResult> GetFavorites([FromQuery] long userId)
        {
            var favorites = await _dbContext.Favorites
                .Include(f => f.Quote)
                .Where(f => f.UserId == userId)
                .ToListAsync();

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
        public async Task<IActionResult> RemoveFromFavorites(int id, [FromQuery] long userId)
        {
            var favorite = await _dbContext.Favorites
                .FirstOrDefaultAsync(f => f.QuoteId == id && f.UserId == userId);

            if (favorite == null)
                return NotFound("Цитату не знайдено серед улюблених.");

            _dbContext.Favorites.Remove(favorite);
            await _dbContext.SaveChangesAsync();

            return Ok(new { message = "Цитата видалена з улюблених." });
        }


        //показати історію пошуку
        [HttpGet("history")]
        public async Task<IActionResult> GetSearchHistory([FromQuery] long userId)
        {
            var history = await _dbContext.SearchHistories
                .Where(h => h.UserId == userId)
                .OrderByDescending(h => h.SearchDate)
                .Take(5)
                .ToListAsync();

            if (history.Count == 0)
                return NotFound("Історія порожня.");

            return Ok(history);
        }

        //очищити історію пошуку
        [HttpDelete("history/clear")]
        public async Task<IActionResult> ClearSearchHistory([FromQuery] long userId)
        {
            var userHistory = _dbContext.SearchHistories.Where(h => h.UserId == userId).ToList();

            if (!userHistory.Any())
                return NotFound("Історії ще немає.");

            _dbContext.SearchHistories.RemoveRange(userHistory);
            await _dbContext.SaveChangesAsync();

            return Ok(new { message = "Історія очищена." });
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

        [HttpPost("react")]
        public async Task<IActionResult> ReactToQuote([FromBody] ReactionRequest request)
        {
            var quote = await _dbContext.Quotes.FindAsync(request.QuoteId);

            if (quote == null) return NotFound("Цитату не знайдено.");

            var existing = await _dbContext.QuoteReactions
                .FirstOrDefaultAsync(r => r.QuoteId == request.QuoteId && r.UserId == request.UserId);

            if (existing != null)
            {
                if (existing.ReactionType == request.ReactionType)
                    return Ok(new { Likes = quote.Likes, Dislikes = quote.Dislikes });

                if (existing.ReactionType == "like") quote.Likes--;
                else if (existing.ReactionType == "dislike") quote.Dislikes--;

                _dbContext.QuoteReactions.Remove(existing);
            }

            var reaction = new QuoteReaction
            {
                QuoteId = request.QuoteId,
                UserId = request.UserId,
                ReactionType = request.ReactionType
            };

            if (reaction.ReactionType == "like") quote.Likes++;
            else if (reaction.ReactionType == "dislike") quote.Dislikes++;

            _dbContext.QuoteReactions.Add(reaction);
            await _dbContext.SaveChangesAsync();

            Console.WriteLine($"Reaction: {request.ReactionType}, QuoteId: {request.QuoteId}, UserId: {request.UserId}");
            Console.WriteLine($"Likes: {quote.Likes}, Dislikes: {quote.Dislikes}");

            return Ok(new { Likes = quote.Likes, Dislikes = quote.Dislikes });
        }

    }
}

