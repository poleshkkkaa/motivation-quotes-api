namespace MotivationQuotesAPI.Models
{
    public class SearchHistory
    {
        public int Id { get; set; }
        public string? Query { get; set; }
        public DateTime SearchDate { get; set; }
        public long UserId { get; set; }
    }
}

