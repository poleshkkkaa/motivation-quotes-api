﻿namespace MotivationQuotesAPI.Models
{
    public class QuoteReaction
    {
        public int Id { get; set; }
        public int QuoteId { get; set; }
        public long UserId { get; set; }
        public string? ReactionType { get; set; }

    }

}

