using System.Text.Json.Serialization;

public class ApiQuote
{
    [JsonPropertyName("q")]
    public string QuoteText { get; set; } = string.Empty;

    [JsonPropertyName("a")]
    public string Author { get; set; } = string.Empty;

}
