using System.Collections.Concurrent;

namespace blizzardCrawler.crawl;

/// <summary>
/// Represents a token bucket implementation for rate limiting.
/// </summary>
public class TokenBucket : IDisposable
{
    private readonly BlockingCollection<Token> _tokens;
    private readonly System.Timers.Timer _timer;
    private readonly int _maxTokens;
    private readonly int _waitTimeInMilliseconds;
    private bool _disposed = false;

    /// <summary>
    /// Initializes a new instance of the TokenBucket class.
    /// </summary>
    /// <param name="maxNumberOfTokens">The maximum number of tokens the bucket can hold.</param>
    /// <param name="refillRateInMilliseconds">The interval at which tokens are added to the bucket.</param>
    /// <param name="waitTimeInMilliseconds">The time to wait when attempting to use a token and none are available.</param>
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

    /// <summary>
    /// Asynchronously attempts to use a token from the bucket. If no token is available, waits for a specified time.
    /// </summary>
    /// <param name="token">A cancellation token to cancel the asynchronous operation.</param>
    /// <returns><c>true</c> if a token is successfully acquired, <c>false</c> otherwise.</returns>
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

    /// <summary>
    /// Disposes of the resources held by the TokenBucket instance.
    /// </summary>
    public void Dispose()
    {
        Dispose(true);
        GC.SuppressFinalize(this);
    }

    protected virtual void Dispose(bool disposing)
    {
        if (!_disposed)
        {
            if (disposing)
            {
                _timer.Elapsed -= OnTimerElapsed;
                _timer.Dispose();
                _tokens.Dispose();
            }
            _disposed = true;
        }
    }
    ~TokenBucket()
    {
        Dispose(false);
    }
}

internal record Token();