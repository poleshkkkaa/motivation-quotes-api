using MotivationQuotesAPI.Models;

namespace MotivationQuotesAPI.Models
{
    public class Favorite
    {
        public int Id { get; set; }
        public int QuoteId { get; set; }
        public Quote Quote { get; set; } = null!;
    }
}

