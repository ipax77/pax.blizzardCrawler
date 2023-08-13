using blizzardCrawler.db;
using blizzardCrawler.shared;

namespace blizzardCrawler.services;

internal static class BlMatchExtension
{
    public static MatchDto ConvertToMatchDto(this BlMatch match, int regionId)
    {
        Decision decision = Decision.None;
        if (Enum.TryParse(typeof(Decision), match.Decision, ignoreCase: true, out var matchDecision)
            && matchDecision is Decision parsedMatchDecision)
        {
            decision = parsedMatchDecision;
        }

        var matchDto = new MatchDto
        {
            // MatchDate = DateTimeOffset.FromUnixTimeSeconds(match.Date).UtcDateTime,
            MatchDateUnixTimestamp = match.Date,
            Decision = decision,
            Region = (Region)regionId
        };
        return matchDto;
    }

    public static MatchInfo ConvertToMatchInfo(this MatchDto matchDto, int playerId)
    {
        return new()
        {
            PlayerId = playerId,
            MatchDateUnixTimestamp = matchDto.MatchDateUnixTimestamp,
            Decision = matchDto.Decision,
            Region = matchDto.Region
        };
    }

    public static MatchInfo ConvertToMatchInfo(this BlMatch match, int regionId)
    {
        Decision decision = Decision.None;
        if (Enum.TryParse(typeof(Decision), match.Decision, ignoreCase: true, out var matchDecision)
            && matchDecision is Decision parsedMatchDecision)
        {
            decision = parsedMatchDecision;
        }

        return new MatchInfo
        {
            // MatchDate = DateTimeOffset.FromUnixTimeSeconds(match.Date).UtcDateTime,
            MatchDateUnixTimestamp = match.Date,
            Decision = decision,
            Region = (Region)regionId
        };
    }
}