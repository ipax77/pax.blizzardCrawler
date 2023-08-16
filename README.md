# dotnet blizzard API Starcarft II match info crawler
A .NET project for crawling and retrieving Starcraft II match information from the Blizzard API.

## Prerequisites
Before you begin, ensure you have met the following requirements:

.NET SDK 8 preview 7 [link to installation guide](https://dotnet.microsoft.com/en-us/download/dotnet/8.0)

Blizzard API Credentials - You need to register your application on the Blizzard Developer Portal to obtain a ClientID and ClientSecret. [link to registration guide](https://develop.battle.net/documentation/guides/getting-started)


## Sample usage
### Clone the repository
```bash
git clone https://github.com/ipax77/pax.blizzardCrawler.git
cd pax.blizzardCrawler/src/blizzardCrawler.cli
```

### Configuration
```
./src/blizzardCrawler.cli/appsettings.Development.json
```
Edit the configuration file appsettings.Development.json with your Blizzard API credentials and desired settings.
```json
{
  "ServerConfig": {
    "BlizzardAPI": {
      "ClientName": "YOUR_CLIENT_NAME",
      "ClientId": "YOUR_CLIENT_ID",
      "ClientSecret": "YOUR_CLIENT_SECRET"
    },
    "MaxRequestsPerSecond": 100,
    "MaxRequestsPerHour": 36000,
    "CrawlerHttpThreadsCount": 30,
    "HttpRequestTimeoutInSeconds": 10
  }
}
```

### Run
```bash
dotnet run
```

### Sample Code
[Program.cs](./src/blizzardCrawler.cli/Program.cs)
```csharp
using var scope = serviceProvider.CreateScope();
var crawlHandler = scope.ServiceProvider.GetRequiredService<ICrawlerHandler>();
var options = scope.ServiceProvider.GetRequiredService<IOptions<BlizzardAPIOptions>>();

List<MatchInfoResult> results = new();

await foreach(var matchInfo in crawlHandler.GetMatchInfos(players, options.Value))
{
    results.Add(matchInfo);
}
```

## Operation
The CrawlerHttpThreadsCount setting controls the maximum number of parallel HTTP requests for crawling match information while respecting the rate limits of the Blizzard API. The HTTP request threads are divided between the main channel and a retry channel, with adjustments based on the number of retries.

Requests will be retried automatically if any of the following status codes are received: 503, 504, and 429.

Due to the retries it is possible that the foreach loop is running infinite. It is possible to pass a cancellation token and check the status if a cancellation is necessary.

## Contributing
Contributions are welcome! If you find any issues or want to enhance the project, feel free to open a pull request.

## License
This project is licensed under the [GNU Affero General Public License](LICENSE).
