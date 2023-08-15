using System.Collections.Concurrent;

namespace blizzardCrawler.crawl;

internal class TokenBucket
{
    private readonly BlockingCollection<Token> _tokens;
    private readonly System.Timers.Timer _timer;
    private readonly int _maxTokens;
    private readonly int _waitTimeInMilliseconds;

    public TokenBucket(int maxNumberOfTokens, int refillRateInMilliseconds, int waitTimeInMilliseconds)
    {
        _maxTokens = maxNumberOfTokens;
        _timer = new System.Timers.Timer(refillRateInMilliseconds);
        _tokens = new BlockingCollection<Token>(maxNumberOfTokens);
        _waitTimeInMilliseconds = waitTimeInMilliseconds;
        Init(maxNumberOfTokens);
    }

    private void Init(int maxNumberOfTokens)
    {
        foreach (var _ in Enumerable.Range(0, maxNumberOfTokens))
            _tokens.Add(new Token());

        _timer.AutoReset = true;
        _timer.Enabled = true;
        _timer.Elapsed += OnTimerElapsed;
    }

    private void OnTimerElapsed(object? sender, System.Timers.ElapsedEventArgs e)
    {
        foreach (var _ in Enumerable.Range(0, _maxTokens - _tokens.Count))
            _tokens.Add(new Token());
    }

    public async Task<bool> UseTokenAsync(CancellationToken token = default)
    {
        if (!_tokens.TryTake(out var _))
        {
            await Task.Delay(_waitTimeInMilliseconds, token);
            if (!_tokens.TryTake(out var _))
            {
                return false;
            }
        }
        return true;
    }
}

internal record Token();