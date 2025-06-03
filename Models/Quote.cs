namespace MotivationQuotesAPI.Models
{
    public class Quote
    {
        public int Id { get; set; }
        public string Text { get; set; } = "";
        public string Author { get; set; } = "";
        public bool IsFavorite { get; set; }
        public long UserId { get; set; }
        public int Likes { get; set; } = 0;
        public int Dislikes { get; set; } = 0;
    }
}


