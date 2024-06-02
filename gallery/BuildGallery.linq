<Query Kind="Program">
  <NuGetReference Version="12.13.1">Azure.Storage.Blobs</NuGetReference>
  <NuGetReference Version="13.0.3">Newtonsoft.Json</NuGetReference>
  <Namespace>Newtonsoft.Json</Namespace>
  <Namespace>Newtonsoft.Json.Linq</Namespace>
  <Namespace>Newtonsoft.Json.Serialization</Namespace>
  <Namespace>System.Net</Namespace>
  <Namespace>System.Net.Http</Namespace>
  <Namespace>System.Threading.Tasks</Namespace>
</Query>

#nullable enable

bool BustCache = false;
TimeSpan Expiry = TimeSpan.FromDays(7);

async Task Main()
{
	var universesBeyondCards = await GetUniversesBeyondCardsAsync();
	
	var universesWithinCardsByName = GetUniversesWithinCards()
		.ToDictionary(c => c.Info.Name);
	var universesWithinCardsMissingUniversesBeyondCards = universesWithinCardsByName.Keys
		.Except(universesBeyondCards.Select(c => c.Card.Name))
		.ToArray();
	if (universesWithinCardsMissingUniversesBeyondCards.Any())
	{
		throw new InvalidOperationException($"No UB card found for {string.Join(", ", universesWithinCardsMissingUniversesBeyondCards)}");
	}
		
	var artistsInfo = GetArtistsInfo();
	var approvedArtistsByName = artistsInfo.Approved.ToDictionary(a => a.Name);

	List<GalleryCard> galleryCards = [];
	foreach (var card in universesBeyondCards.OrderBy(c => c.Card.Released_At).ThenBy(c => c.Card.Collector_Number))
	{
		var universesBeyondName = card.Card.Flavor_Name ?? card.Card.Name;
		var universesWithinCard = universesWithinCardsByName.TryGetValue(card.Card.Name, out var c) ? c : null;
		var universesWithinName = card.OfficialUniversesWithinCard?.Name ?? universesWithinCard?.Info.Nickname;
		
		ApprovedArtistInfo? artist;
		if (universesWithinCard != null)
		{
			if (card.OfficialUniversesWithinCard != null)
			{
				$"{universesWithinCard.Info.Name} has official universes within card!".Dump();
			}

			artist = universesWithinCard.Info.Artist is { } artistName
				? approvedArtistsByName[artistName]
				: null;

			if (artist?.ApprovedWorks is { } works && !works.Contains(universesWithinCard.Info.ArtName, StringComparer.OrdinalIgnoreCase))
			{
				throw new InvalidOperationException($"Unapproved artwork {universesWithinCard.Info.ArtName}");
			}
		}
		else { artist = null; }
		
		galleryCards.Add(new()
		{
			Name = universesBeyondName,
			Nickname = universesWithinName == universesBeyondName ? null : universesWithinName,
			ContributionInfo = card.OfficialUniversesWithinCard is null && universesWithinCard != null
				? new()
				{
					Contributor = universesWithinCard.Info.Contributor,
					Artist = universesWithinCard.Info.Artist,
					ArtistUrl = artist?.Url,
					ArtName = universesWithinCard.Info.ArtName,
					ArtUrl = universesWithinCard.Info.ArtUrl,
					MtgCardBuilderId = universesWithinCard.Info.MtgCardBuilderId,
				}
				: null,
			UniversesBeyondImage = card.Card.GetFrontImage(),
			UniversesBeyondBackImage = card.Card.GetBackImage(),
			UniversesWithinImage = card.OfficialUniversesWithinCard?.GetFrontImage() ?? MakeUrlFromCardPath(universesWithinCard?.FrontImage),
			UniversesWithinBackImage = card.OfficialUniversesWithinCard?.GetBackImage() ?? MakeUrlFromCardPath(universesWithinCard?.BackImage),
		});
	}
	
	var galleryData = new { cards = galleryCards };

	File.WriteAllText(Path.Combine(Path.GetDirectoryName(Util.CurrentQueryPath)!, "galleryData.js"), $"const data = {JsonConvert.SerializeObject(galleryData, Newtonsoft.Json.Formatting.Indented)}; export default data;");
	
	StringBuilder artistsPage = new();
	artistsPage.AppendLine("# Approved artists").AppendLine();
	
	artistsPage.AppendLine("These artists have granted permission for their works to be used to make cards for this project. Thanks!");
	
	artistsPage.AppendLine("| Name/Handle | Notes |").AppendLine("| - | - |");
	
	var universesWithinCardsByArt = universesWithinCardsByName.Values
		.Where(c => c.Info.Artist != null)
		.ToDictionary(c => new { Artist = c.Info.Artist!, Work = c.Info.ArtName! });
	foreach (var artist in artistsInfo.Approved.OrderBy(a => a.ApprovedWorks != null).ThenBy(a => a.Name))
	{
		artistsPage.Append($"| [{artist.Name}]({artist.Url}) | ");
		if (artist.ApprovedWorks != null)
		{
			artistsPage.Append("So far, only the following works have been approved for use:");
			foreach (var work in artist.ApprovedWorks
				.Select(w => new { name = w, card = universesWithinCardsByArt.TryGetValue(new { Artist = artist.Name, Work = w }, out var card) ? card : null })
				.OrderBy(w => w.card != null)
				.ThenBy(w => w.name, StringComparer.OrdinalIgnoreCase))
			{
				artistsPage.Append("<br/>");
				if (work.card != null)
				{
					artistsPage.Append($"- ~{work.name}~ (used on {work.card.Info.Name})");
				}
				else
				{
					artistsPage.Append($"- {work.name}");
				}
			}
		}
		artistsPage.AppendLine("|");
	}
	
	artistsPage.AppendLine("# Non-approved artists").AppendLine();
	
	artistsPage.AppendLine("After being asked, these artists have asked _not_ to have their work used for this project.");
	
	foreach (var artist in artistsInfo.Declined)
	{
		artistsPage.AppendLine($"- {artist.Name}"); 
	}
	
	File.WriteAllText(Path.Combine(Path.GetDirectoryName(Util.CurrentQueryPath)!, "..", "docs", "artists.md"), artistsPage.ToString());	
}

[JsonObject(NamingStrategyType = typeof(CamelCaseNamingStrategy))]
class GalleryCard
{
	public required string Name { get; set; }
	[JsonProperty(NullValueHandling = NullValueHandling.Ignore)]
	public required string? Nickname { get; set; }
	[JsonProperty(NullValueHandling = NullValueHandling.Ignore)]
	public required GalleryCardContributionInfo? ContributionInfo { get; set; }
	public required string UniversesBeyondImage { get; set; }
	[JsonProperty(NullValueHandling = NullValueHandling.Ignore)]
	public required string? UniversesBeyondBackImage { get; set; }
	[JsonProperty(NullValueHandling = NullValueHandling.Ignore)]
	public required string? UniversesWithinImage { get; set; }
	[JsonProperty(NullValueHandling = NullValueHandling.Ignore)]
	public required string? UniversesWithinBackImage { get; set; }
}

[JsonObject(NamingStrategyType = typeof(CamelCaseNamingStrategy))]
class GalleryCardContributionInfo
{
	public required string Contributor { get; set; }
	[JsonProperty(NullValueHandling = NullValueHandling.Ignore)]
	public required string? Artist { get; set; }
	[JsonProperty(NullValueHandling = NullValueHandling.Ignore)]
	public required Uri? ArtistUrl { get; set; }
	[JsonProperty(NullValueHandling = NullValueHandling.Ignore)]
	public required string? ArtName { get; set; }
	[JsonProperty(NullValueHandling = NullValueHandling.Ignore)]
	public required Uri? ArtUrl { get; set; }
	public required string MtgCardBuilderId { get; set; }
}

static readonly string CardsDirectory = Path.Combine(Path.GetDirectoryName(Util.CurrentQueryPath)!, "..", "cards");

HttpClient Client = new();

string? MakeUrlFromCardPath(string? path) => path != null ? $"./cards/{path}" : null;

List<UniversesWithinCard> GetUniversesWithinCards()
{
	var cards = JsonConvert.DeserializeObject<Dictionary<string, UniversesWithinCardInfo>>(File.ReadAllText(Path.Combine(CardsDirectory, "cards.json")))!;
		
	List<UniversesWithinCard> results = [];
	foreach (var (id, info) in cards)
	{
		if (info.Artist != null
			&& (info.ArtName is null || info.ArtUrl is null))
		{
			throw new JsonException($"Bad art info for id {id}");			
		}
		
		var nameSplit = info.Name.Split(" // ", count: 2);
		var card = new UniversesWithinCard
		{
			Id = id,
			Info = info,
			FrontImage = $"{nameSplit[0]}.png",
			BackImage = nameSplit.Length > 1 ? $"{nameSplit[1]}.png" : null
		};
		
		foreach (var path in new[] { card.FrontImage, card.BackImage }.Where(i => i != null))
		{
			if (!File.Exists(Path.Combine(CardsDirectory, path!))) { throw new FileNotFoundException(path); }
		}

		results.Add(card);
	}
	
	return results;
}

record UniversesWithinCardInfo(
	[property: JsonProperty(Required = Required.Always)] string Name,
	string? Nickname,
	[property: JsonProperty(Required = Required.Always)] string Contributor,
	string? Artist,
	string? ArtName,
	Uri? ArtUrl,
	bool ArtCrop,
	[property: JsonProperty(Required = Required.Always)] string MtgCardBuilderId
);

class UniversesWithinCard
{
	public required string Id { get; set; }
	public required UniversesWithinCardInfo Info { get; set; }
	public required string FrontImage { get; set; }
	public string? BackImage { get; set; }
}

ArtistsInfo GetArtistsInfo() => JsonConvert.DeserializeObject<ArtistsInfo>(File.ReadAllText(Path.Combine(CardsDirectory, "artists.json")))!;

record ArtistInfo(
	[property: JsonProperty(Required = Required.Always)] string Name,
	[property: JsonProperty(Required = Required.Always)] string Contact);
	
record ApprovedArtistInfo(
	string Name,
	string Contact,
	[property: JsonProperty(Required = Required.Always)] Uri Url,
	string Notes,
	string[] ApprovedWorks): ArtistInfo(Name, Contact);
	
record ArtistsInfo(
	[property: JsonProperty(Required = Required.Always)] ApprovedArtistInfo[] Approved,
	[property: JsonProperty(Required = Required.Always)] ArtistInfo[] Declined);

async Task<List<CardInfo>> GetUniversesBeyondCardsAsync()
{
	var allCards = await CacheAsync("all-cards", GetAllCardsAsync);
	// unique:prints is needed to make sure we capture all sets that contain UB cards. Some promo set codes like pltr won't show up otherwise
	var universesBeyondCards = await CacheAsync("universes-beyond-cards", () => SearchAsync("is:ub -is:reprint unique:prints"));
	
	var allCardsByOracleId = allCards.ToLookup(c => c.Oracle_Id);
	var universesBeyondSets = universesBeyondCards.Select(c => c.Set).ToHashSet();
	
	return universesBeyondCards.GroupBy(c => c.Oracle_Id)
		.Select(g => g.MinBy(c => c.Released_At)!)
		.Select(c => new CardInfo 
		{
			Card = c,
			OfficialUniversesWithinCard = allCardsByOracleId[c.Oracle_Id].Where(cc => !universesBeyondSets.Contains(cc.Set)).OrderBy(cc => cc.Released_At).FirstOrDefault()
		})
		.Where(c => c.OfficialUniversesWithinCard is null || c.OfficialUniversesWithinCard.Released_At > c.Card.Released_At)
		.ToList();
}

class CardInfo
{
	public required Card Card { get; set; }
	public required Card? OfficialUniversesWithinCard { get; set; }
}

async Task<List<Card>> GetAllCardsAsync()
{
	"Downloading all cards...".Dump();
	
	using var bulkDataMetaResponse = await Client.GetAsync("https://api.scryfall.com/bulk-data");
	bulkDataMetaResponse.EnsureSuccessStatusCode();
	var bulkDataJson = JObject.Parse(await bulkDataMetaResponse.Content.ReadAsStringAsync());
	var downloadUri = ((JArray)bulkDataJson["data"]!).Single(o => o["name"]!.ToObject<string>() == "Default Cards")["download_uri"]!.ToObject<string>();
	
	using var bulkDataResponse = await Client.GetAsync(downloadUri);
	bulkDataResponse.EnsureSuccessStatusCode();
	var bulkDataCards = JsonConvert.DeserializeObject<List<Card>>(await bulkDataResponse.Content.ReadAsStringAsync())!;
	return bulkDataCards;
}

async Task<List<Card>> SearchAsync(string query)
{
	$"Searching '{query}'...".Dump();
	
	List<Card> cards = [];
	var url = $"https://api.scryfall.com/cards/search?q={WebUtility.UrlEncode(query)}";
	do
	{
		using var response = await Client.GetAsync(url);
		response.EnsureSuccessStatusCode();
		var json = await response.Content.ReadAsStringAsync();
		var deserialized = JsonConvert.DeserializeObject<SearchResponse>(json)!;
		cards.AddRange(deserialized.Data);
		url = deserialized.Next_Page;
	}
	while (url != null);
	
	return cards;
}

record SearchResponse(
	string Next_Page,
	List<Card> Data,
	[property: JsonExtensionData] Dictionary<string, object> ExtensionData
);

record Card(
	string Name,
	string Flavor_Name,
	string Oracle_Id,
	Dictionary<string, string> Image_Uris,
	CardFace[] Card_Faces,
	string Set,
	DateTime Released_At,
	string Collector_Number)
{
	public string GetFrontImage() => (Image_Uris ?? Card_Faces[0].Image_Uris)["normal"];
	public string? GetBackImage() => Card_Faces?[1].Image_Uris?["normal"];
}

record CardFace(string Name, Dictionary<string, string> Image_Uris);

async Task<T> CacheAsync<T>(string key, Func<Task<T>> valueFactory)
{	
	var cacheFile = Path.Combine(Path.GetTempPath(), Util.CurrentQuery.Name, $"{key}.json");
	if (!BustCache && File.Exists(cacheFile) && File.GetLastWriteTimeUtc(cacheFile) + Expiry >= DateTime.Now)
	{
		try { return JsonConvert.DeserializeObject<T>(File.ReadAllText(cacheFile))!; }
		catch (Exception ex) { ex.Dump($"Cache file read error for {key}"); }
	}
	
	var result = await valueFactory();
	
	Directory.CreateDirectory(Path.GetDirectoryName(cacheFile)!);
	File.WriteAllText(cacheFile, JsonConvert.SerializeObject(result));
	
	return result;
}