namespace MotivationQuotesAPI.Models
{
    public class SearchHistory
    {
        public int Id { get; set; }
        public string Query { get; set; } = string.Empty;
        public DateTime SearchDate { get; set; }
    }
}

