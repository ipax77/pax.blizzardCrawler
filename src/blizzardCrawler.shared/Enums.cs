namespace blizzardCrawler.shared;

public enum Decision
{
    None = 0,
    Win = 1,
    Loss = 2
}

public enum Region
{
    None = 0,
    Am = 1,
    Eu = 2,
    As = 3,
    Cn = 5,
}

public enum Speed
{
    None = 0,
    Slower = 1,
    Slow = 2,
    Normal = 3,
    Fast = 4,
    Faster = 5
}

public static class EnumExtensions
{
    public static Decision GetDecision(string decisionString)
    {
        if (Enum.TryParse(decisionString, ignoreCase: true, out Decision decision))
        {
            return decision;
        }
        return Decision.None;
    }

    public static Region GetRegion(int regionId)
    {
        if (Enum.IsDefined(typeof(Region), regionId))
        {
            return (Region)regionId;
        }
        return Region.None;
    }

    public static Speed GetSpeed(string speedString)
    {
        if (Enum.TryParse(speedString, ignoreCase: true, out Speed speed))
        {
            return speed;
        }
        return Speed.None;
    }
}