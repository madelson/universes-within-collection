<Query Kind="Program">
  <NuGetReference Version="13.0.3">Newtonsoft.Json</NuGetReference>
  <Namespace>Newtonsoft.Json</Namespace>
  <Namespace>Newtonsoft.Json.Linq</Namespace>
  <Namespace>System.Net</Namespace>
  <Namespace>System.Net.Http</Namespace>
  <Namespace>System.Threading.Tasks</Namespace>
</Query>

bool BustCache = false;

async Task Main()
{
	var universesBeyondCards = await GetUniversesBeyondCardsAsync();
	
	var universesWithinCardsByName = GetUniversesWithinCards()
		.ToDictionary(c => c.Info.Name);

	List<object> galleryCards = [];
	foreach (var card in universesBeyondCards.OrderBy(c => c.Card.Released_At).ThenBy(c => c.Card.Collector_Number))
	{
		var universesBeyondName = card.Card.Flavor_Name ?? card.Card.Name;
		var universesWithinCard = universesWithinCardsByName.TryGetValue(card.Card.Name, out var c) ? c : null;
		var universesWithinName = card.OfficialUniversesWithinCard?.Name ?? universesWithinCard?.Info.Nickname;
		
		galleryCards.Add(new
		{
			name = universesBeyondName,
			nickname = universesWithinName == universesBeyondName ? null : universesWithinName,
			contributor = card.OfficialUniversesWithinCard is null ? universesWithinCard?.Info.Contributor : null,
			universesBeyondImage = card.Card.GetFrontImage(),
			universesBeyondBackImage = card.Card.GetBackImage(),
			universesWithinImage = card.OfficialUniversesWithinCard?.GetFrontImage() ?? MakeUrlFromCardPath(universesWithinCard?.FrontImage),
			universesWithinBackImage = card.OfficialUniversesWithinCard?.GetBackImage() ?? MakeUrlFromCardPath(universesWithinCard?.BackImage),
			hasOfficialUniversesWithinCard = card.OfficialUniversesWithinCard != null,
		});
	}
	
	var galleryData = new { cards = galleryCards };

	File.WriteAllText(Path.Combine(Path.GetDirectoryName(Util.CurrentQueryPath), "galleryData.js"), $"const data = {JsonConvert.SerializeObject(galleryData)}; export default data;");
}

HttpClient Client = new();

string MakeUrlFromCardPath(string path) => path != null ? $"./cards/{path}" : null;

List<UniversesWithinCard> GetUniversesWithinCards()
{
	var cardsDirectory = Path.Combine(Path.GetDirectoryName(Util.CurrentQueryPath), "..", "cards");
	var cards = JsonConvert.DeserializeObject<Dictionary<string, UniversesWithinCardInfo>>(File.ReadAllText(Path.Combine(cardsDirectory, "cards.json")));
		
	List<UniversesWithinCard> results = [];
	foreach (var (id, info) in cards)
	{
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
			if (!File.Exists(Path.Combine(cardsDirectory, path))) { throw new FileNotFoundException(path); }
			var artPath = Path.ChangeExtension(path, null) + " ART.jpg";
			if (!File.Exists(Path.Combine(cardsDirectory, artPath))) { throw new FileNotFoundException(artPath); }
		}
		
		results.Add(card);
	}
	
	return results;
}

record UniversesWithinCardInfo(
	[property: JsonProperty(Required = Required.Always)] string Name,
	string Nickname,
	[property: JsonProperty(Required = Required.Always)] string Contributor,
	[property: JsonProperty(Required = Required.Always)] string MtgCardBuilderId
);

class UniversesWithinCard
{
	public required string Id { get; set; }
	public required UniversesWithinCardInfo Info { get; set; }
	public required string FrontImage { get; set; }
	public string BackImage { get; set; }
}

async Task<List<CardInfo>> GetUniversesBeyondCardsAsync()
{
	var allCards = await CacheAsync("all-cards", GetAllCardsAsync);
	var universesBeyondCards = await CacheAsync("universes-beyond-cards", () => SearchAsync("is:ub -is:reprint"));

	var allCardsByOracleId = allCards.ToLookup(c => c.Oracle_Id);
	var universesBeyondSets = universesBeyondCards.Select(c => c.Set).ToHashSet();
	
	return universesBeyondCards.GroupBy(c => c.Oracle_Id)
		.Select(g => g.MinBy(c => c.Released_At))
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
	public Card Card { get; set; }
	public Card OfficialUniversesWithinCard { get; set; }
}

async Task<List<Card>> GetAllCardsAsync()
{
	"Downloading all cards...".Dump();
	
	using var bulkDataMetaResponse = await Client.GetAsync("https://api.scryfall.com/bulk-data");
	bulkDataMetaResponse.EnsureSuccessStatusCode();
	var bulkDataJson = JObject.Parse(await bulkDataMetaResponse.Content.ReadAsStringAsync());
	var downloadUri = ((JArray)bulkDataJson["data"]).Single(o => o["name"].ToObject<string>() == "Default Cards")["download_uri"].ToObject<string>();
	
	using var bulkDataResponse = await Client.GetAsync(downloadUri);
	bulkDataResponse.EnsureSuccessStatusCode();
	var bulkDataCards = JsonConvert.DeserializeObject<List<Card>>(await bulkDataResponse.Content.ReadAsStringAsync());
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
		var deserialized = JsonConvert.DeserializeObject<SearchResponse>(json);
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
	public string GetBackImage()
	{
		try { return Card_Faces?[1].Image_Uris?["normal"]; }
		catch
		{
			throw;
		}
	}
}

record CardFace(string Name, Dictionary<string, string> Image_Uris);

async Task<T> CacheAsync<T>(string key, Func<Task<T>> valueFactory)
{	
	var cacheFile = Path.Combine(Path.GetTempPath(), Util.CurrentQuery.Name, $"{key}.json");
	if (!BustCache && File.Exists(cacheFile) && File.GetLastWriteTimeUtc(cacheFile) + TimeSpan.FromHours(2) >= DateTime.Now)
	{
		try { return JsonConvert.DeserializeObject<T>(File.ReadAllText(cacheFile)); }
		catch (Exception ex) { ex.Dump($"Cache file read error for {key}"); }
	}
	
	var result = await valueFactory();
	
	Directory.CreateDirectory(Path.GetDirectoryName(cacheFile));
	File.WriteAllText(cacheFile, JsonConvert.SerializeObject(result));
	
	return result;
}