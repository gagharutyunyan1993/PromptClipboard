namespace PromptClipboard.Application.Tests;

using PromptClipboard.Application.Services;

public class SearchRankingServiceTests
{
    [Theory]
    [InlineData("#email rest of query", "rest of query", "email", null)]
    [InlineData("#Jira", "", "jira", null)]
    [InlineData("lang:ru some query", "some query", null, "ru")]
    [InlineData("#email lang:en query", "query", "email", "en")]
    [InlineData("plain query", "plain query", null, null)]
    [InlineData("", "", null, null)]
    public void ParseQuery_ExtractsFilters(string input, string expectedQuery, string? expectedTag, string? expectedLang)
    {
        var (query, tag, lang) = SearchRankingService.ParseQuery(input);
        Assert.Equal(expectedQuery, query);
        Assert.Equal(expectedTag, tag);
        Assert.Equal(expectedLang, lang);
    }
}
