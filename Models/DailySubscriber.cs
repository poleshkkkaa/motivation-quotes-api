namespace MotivationQuotesAPI.Models
{
    public class DailySubscriber
    {
        public int Id { get; set; }
        public long ChatId { get; set; }
        public TimeSpan PreferredTime { get; set; } 
    }

}

