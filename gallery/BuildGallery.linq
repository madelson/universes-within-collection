<Query Kind="Program">
  <NuGetReference Version="13.0.3">Newtonsoft.Json</NuGetReference>
  <NuGetReference>Tinify</NuGetReference>
  <Namespace>Newtonsoft.Json</Namespace>
  <Namespace>Newtonsoft.Json.Linq</Namespace>
  <Namespace>Newtonsoft.Json.Serialization</Namespace>
  <Namespace>System.Net</Namespace>
  <Namespace>System.Net.Http</Namespace>
  <Namespace>System.Runtime.CompilerServices</Namespace>
  <Namespace>System.Threading.Tasks</Namespace>
  <Namespace>TinifyAPI</Namespace>
</Query>

#nullable enable

bool BustCache = false;
TimeSpan Expiry = TimeSpan.FromDays(7);
string RawImageRepository = Path.GetFullPath(Path.Combine(Path.GetDirectoryName(Util.CurrentQueryPath)!, "..", "..", "universes-within-collection-reference", "Raw Cards"));

async Task Main()
{
	await CompressCardImagesAsync();
	
	var universesBeyondCards = await GetUniversesBeyondCardsAsync();
	
	var universesWithinCardsByName = GetUniversesWithinCards()
		.ToDictionary(c => c.Info.Name);
	var universesWithinCardsMissingUniversesBeyondCards = universesWithinCardsByName.Keys
		.Except(universesBeyondCards.Select(c => c.Card.Name))
		.Except(universesBeyondCards.Select(c => DisambiguatedName(c.Card)))
		.ToArray();
	if (universesWithinCardsMissingUniversesBeyondCards.Any())
	{
		throw new InvalidOperationException($"No UB card found for {string.Join(", ", universesWithinCardsMissingUniversesBeyondCards)}");
	}
	
	// Generate gallery data

	var artistsInfo = GetArtistsInfo();
	var approvedArtistsByName = artistsInfo.Approved.ToDictionary(a => a.Name);

	Dictionary<GalleryCard, (Card Card, UniversesWithinCard? UniversesWithinCard)> galleryCards = [];
	foreach (var card in universesBeyondCards)
	{
		var universesBeyondName = card.Card.Flavor_Name ?? card.Card.Name;
		var universesWithinCard = !card.Card.Layout.Contains("token") && universesWithinCardsByName.TryGetValue(card.Card.Name, out var c) ? c
			: universesWithinCardsByName.TryGetValue(DisambiguatedName(card.Card), out c) ? c
			: null;
		var universesWithinName = card.OfficialUniversesWithinCard?.Name ?? universesWithinCard?.Info.Nickname;
		
		ApprovedArtistInfo? artist, backArtist;
		if (universesWithinCard != null)
		{
			if (card.OfficialUniversesWithinCard != null)
			{
				$"{universesWithinCard.Info.Name} has official universes within card!".Dump();
			}

			artist = GetArtist(universesWithinCard.Info.Artist, universesWithinCard.Info.ArtName, universesWithinCard.Info.IsMtgArt);
			backArtist = GetArtist(universesWithinCard.Info.BackArtist, universesWithinCard.Info.BackArtName, universesWithinCard.Info.IsBackMtgArt);
			
			ApprovedArtistInfo? GetArtist(string? artistName, string? artName, bool isMtgArt)
			{
				var artistInfo = artistName != null && !isMtgArt
					? approvedArtistsByName[artistName]
					: null;
				if (artistInfo?.ApprovedWorks is { } works && !works.Contains(artName, StringComparer.OrdinalIgnoreCase))
				{
					throw new InvalidOperationException($"Unapproved artwork {artName}");
				}
				return artistInfo;
			}
		}
		else { artist = backArtist = null; }

		GalleryCard galleryCard = new()
		{
			Name = universesBeyondName,
			Nickname = universesWithinName == universesBeyondName ? null : universesWithinName,
			ContributionInfo = card.OfficialUniversesWithinCard is null && universesWithinCard != null
				? new()
				{
					Contributor = universesWithinCard.Info.Contributor,
					Front = new()
					{
						Artist = universesWithinCard.Info.Artist,
						ArtistUrl = artist?.Url,
						ArtName = universesWithinCard.Info.ArtName,
						ArtUrl = universesWithinCard.Info.ArtUrl,
						IsMtgArt = universesWithinCard.Info.IsMtgArt,
						MtgCardBuilderId = universesWithinCard.Info.MtgCardBuilderId,
					},
					Back = universesWithinCard.Info.BackMtgCardBuilderId is null
						? null
						: new()
						{
							Artist = universesWithinCard.Info.BackArtist,
							ArtistUrl = backArtist?.Url,
							ArtName = universesWithinCard.Info.BackArtName,
							ArtUrl = universesWithinCard.Info.BackArtUrl,
							IsMtgArt = universesWithinCard.Info.IsBackMtgArt,
							MtgCardBuilderId = universesWithinCard.Info.BackMtgCardBuilderId,
						}
				}
				: null,
			UniversesBeyondImage = card.Card.GetFrontImage(),
			UniversesBeyondBackImage = card.Card.GetBackImage(),
			UniversesWithinImage = card.OfficialUniversesWithinCard?.GetFrontImage() ?? MakeUrlFromCardPath(universesWithinCard?.FrontImage),
			UniversesWithinBackImage = card.OfficialUniversesWithinCard?.GetBackImage() ?? MakeUrlFromCardPath(universesWithinCard?.BackImage),
		};
		galleryCards.Add(galleryCard, (card.Card, universesWithinCard));
	}
	
	var galleryData = new 
	{ 
		cards = galleryCards.OrderBy(p => p.Value.UniversesWithinCard is null)
			.ThenBy(p => p.Value.UniversesWithinCard?.Id.StartsWith('T'))
			.ThenByDescending(p => p.Value.UniversesWithinCard?.Id)
			.ThenBy(p => p.Value.Card.Released_At)
			.ThenBy(p => p.Value.Card.Collector_Number)
			.Select(p => p.Key)
	};
	File.WriteAllText(Path.Combine(Path.GetDirectoryName(Util.CurrentQueryPath)!, "galleryData.js"), $"const data = {JsonConvert.SerializeObject(galleryData, Newtonsoft.Json.Formatting.Indented)}; export default data;");

	// Generate card data (for importer consumption)

	Uri ToAbsoluteUrl(string relativeUrl) => new($"https://madelson.github.io/universes-within-collection{relativeUrl.TrimStart('.')}");
	var cardData = galleryCards.Where(p => p.Key.ContributionInfo != null)
		.Select(p => new CardData(
			p.Value.Card.Oracle_Id!.Value,
			p.Key.Name, 
			p.Key.Nickname, 
			ToAbsoluteUrl(p.Key.UniversesWithinImage!), 
			p.Key.UniversesWithinBackImage != null ? ToAbsoluteUrl(p.Key.UniversesWithinBackImage) : null))
		.ToArray();
	File.WriteAllText(Path.Combine(Path.GetDirectoryName(Util.CurrentQueryPath)!, "cardData.json"), JsonConvert.SerializeObject(cardData, Newtonsoft.Json.Formatting.Indented));
	
	// Generate artists page
	
	StringBuilder artistsPage = new();
	artistsPage.AppendLine("# Approved artists").AppendLine();
	
	artistsPage.AppendLine("These artists have granted permission for their works to be used to make cards for this project. Thanks!");
	
	artistsPage.AppendLine("| Name/Handle | Notes |").AppendLine("| - | - |");
	
	var universesWithinCardsByArt = universesWithinCardsByName.Values
		.Where(c => c.Info.Artist != null)
		.ToLookup(c => new { Artist = c.Info.Artist!, Work = c.Info.ArtName! });
	foreach (var artist in artistsInfo.Approved.OrderBy(a => a.ApprovedWorks != null).ThenBy(a => a.Name))
	{
		artistsPage.Append($"| [{artist.Name}]({artist.Url}) | ");
		if (artist.ApprovedWorks != null)
		{
			artistsPage.Append("So far, only the following works have been approved for use:");
			foreach (var work in artist.ApprovedWorks
				.Select(w => new { name = w, cards = universesWithinCardsByArt[new { Artist = artist.Name, Work = w }] })
				.OrderBy(w => w.cards.Count())
				.ThenBy(w => w.name, StringComparer.OrdinalIgnoreCase))
			{
				if (work.cards.Count(c => !c.Info.IsArtCrop) > 1) { throw new InvalidOperationException($"Art duplication: {string.Join(", ", work.cards.Select(c => c.Info.Name))}"); }
				
				artistsPage.Append("<br/>");
				if (work.cards.Any())
				{
					artistsPage.Append($"- ~{work.name}~ (used on {string.Join(", ", work.cards.Select(c => c.Info.Name))})");
				}
				else
				{
					artistsPage.Append($"- {work.name}");
				}
			}
		}
		artistsPage.AppendLine("|");
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
	public required GalleryCardFace Front { get; set; }
	[JsonProperty(NullValueHandling = NullValueHandling.Ignore)]
	public required GalleryCardFace? Back { get; set; }
}

[JsonObject(NamingStrategyType = typeof(CamelCaseNamingStrategy))]
class GalleryCardFace
{
	[JsonProperty(NullValueHandling = NullValueHandling.Ignore)]
	public required string? Artist { get; set; }
	[JsonProperty(NullValueHandling = NullValueHandling.Ignore)]
	public required Uri? ArtistUrl { get; set; }
	[JsonProperty(NullValueHandling = NullValueHandling.Ignore)]
	public required string? ArtName { get; set; }
	[JsonProperty(NullValueHandling = NullValueHandling.Ignore)]
	public required Uri? ArtUrl { get; set; }
	[JsonProperty(DefaultValueHandling = DefaultValueHandling.Ignore)]
	public required bool IsMtgArt { get; set; }
	public required string MtgCardBuilderId { get; set; }
}

[JsonObject(NamingStrategyType = typeof(CamelCaseNamingStrategy))]
record CardData(
	Guid OracleId,
	string Name,
	[property: JsonProperty(NullValueHandling = NullValueHandling.Ignore)]
	string? Nickname,
	Uri image,
	[property: JsonProperty(NullValueHandling = NullValueHandling.Ignore)]
	Uri? backImage);

static readonly string CardsDirectory = Path.Combine(Path.GetDirectoryName(Util.CurrentQueryPath)!, "..", "cards");

HttpClient Client = new()
{
	// required by scryfall: https://scryfall.com/docs/api
	DefaultRequestHeaders =
	{
		Accept = { new("application/json") },
    	UserAgent = { new("UniversesWithinCollection", "1.0") }
	},
	// helps with slow bulk downloads
	Timeout = TimeSpan.FromMinutes(10),
};

static string DisambiguatedName(Card card) => $"{card.Name} ({card.Set.ToUpperInvariant()} {card.Collector_Number})";

// github pages doesn't support "+" for spaces
string? MakeUrlFromCardPath(string? path) => path != null ? $"./cards/{WebUtility.UrlEncode(path).Replace("+", "%20")}" : null;

async Task CompressCardImagesAsync()
{
	if (!Directory.Exists(RawImageRepository)) { throw new DirectoryNotFoundException(RawImageRepository); }
	
	Tinify.Key = JsonConvert.DeserializeObject<Dictionary<string, string>>(File.ReadAllText(Path.Combine(Path.GetDirectoryName(Util.CurrentQueryPath)!, "credentials.json")))!["tinypng"];
	
	var rawCardImages = Directory.GetFiles(CardsDirectory, "*.png")
		.Where(c => new FileInfo(c).Length > 2_000_000);
	await Parallel.ForEachAsync(rawCardImages, async (rawCardImage, _) =>
	{
		$"Compressing {Path.GetFileName(rawCardImage)}".Dump();
		var rawImageStorePath = Path.Combine(RawImageRepository, Path.GetFileName(rawCardImage));
		if (File.Exists(rawImageStorePath))
		{
			var rawImageHistoryPath = Path.Combine(RawImageRepository, "Old Versions", Path.GetFileNameWithoutExtension(rawCardImage) + DateTime.Now.ToString("yyyyMMddHHmmss") + Path.GetExtension(rawCardImage));
			Directory.CreateDirectory(Path.GetDirectoryName(rawImageHistoryPath)!);
			File.Move(rawImageStorePath, rawImageHistoryPath);
		}
		File.Copy(rawCardImage, rawImageStorePath);
		await (await Tinify.FromFile(rawCardImage)).ToFile(rawCardImage);
		if (new FileInfo(rawCardImage).Length > 1_500_000)
		{
			$"Warning: compressed {rawCardImage} is still large!".Dump();
		}
	});
}

List<UniversesWithinCard> GetUniversesWithinCards()
{
	var cards = JsonConvert.DeserializeObject<Dictionary<string, UniversesWithinCardInfo>>(File.ReadAllText(Path.Combine(CardsDirectory, "cards.json")))!;
		
	List<UniversesWithinCard> results = [];
	foreach (var (id, info) in cards)
	{
		var artIndicators = new[] { info.Artist is null, info.ArtName is null, info.ArtUrl is null };
		if (artIndicators.Distinct().Count() != 1)
		{
			throw new JsonException($"Bad art info for id {id}");			
		}
		if (info.IsMtgArt && info.Artist is null)
		{
			throw new JsonException($"Missing artist info for MTG art for id {id}");
		}
		
		var backArtIndicators = new[] { info.BackArtist is null, info.BackArtName is null, info.BackArtUrl is null };
		if (backArtIndicators.Distinct().Count() != 1)
		{
			throw new JsonException($"Bad back art info for id {id}");
		}
		if (info.IsBackMtgArt && info.BackArtist is null)
		{
			throw new JsonException($"Missing artist info for back MTG art for id {id}");
		}
		
		var nameSplit = info.Name.Split(" // ", count: 2);
		var card = new UniversesWithinCard
		{
			Id = id,
			Info = info,
			FrontImage = $"{ReplaceInvalidChars(nameSplit[0])}.png",
			BackImage = nameSplit.Length > 1 ? $"{ReplaceInvalidChars(nameSplit[1])}.png" : null
		};
		
		foreach (var path in new[] { card.FrontImage, card.BackImage }.Where(i => i != null))
		{
			if (!File.Exists(Path.Combine(CardsDirectory, path!))) { throw new FileNotFoundException(Path.GetFullPath(Path.Combine(CardsDirectory, path!))); }
		}

		results.Add(card);
		
		string ReplaceInvalidChars(string name) => InvalidFilenameCharsRegex.Replace(name, "_");
	}
	
	return results;
}

static readonly Regex InvalidFilenameCharsRegex = new($"[{string.Join(string.Empty, Path.GetInvalidFileNameChars().Select(ch => Regex.Escape(ch.ToString())))}]");

record UniversesWithinCardInfo(
	[property: JsonProperty(Required = Required.Always)] string Name,
	string? Nickname,
	[property: JsonProperty(Required = Required.Always)] string Contributor,
	string? Artist,
	string? ArtName,
	Uri? ArtUrl,
	bool IsArtCrop,
	bool IsMtgArt,
	[property: JsonProperty(Required = Required.Always)] string MtgCardBuilderId,
	string? BackArtist,
	string? BackArtName,
	Uri? BackArtUrl,
	bool IsBackArtCrop,
	bool IsBackMtgArt,
	string? BackMtgCardBuilderId);

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
	var universesBeyondCards = await CacheAsync("universes-beyond-cards", () => SearchAsync("is:ub -is:reprint -is:digital unique:prints"));
	
	var allCardsByOracleId = allCards.ToLookup(c => c.Oracle_Id);
	var universesBeyondSets = universesBeyondCards.Select(c => c.Set).ToHashSet();
	// Above we exclude reprints since we don't care to include in-universe cards which were reprinted in UB sets. However, pltc is a case where
	// a UB card was reprinted in another UB set. We need to make sure that set is tagged as UB so that it doesn't look like an official UW printing.
	// This is not a complete solution because at some point we'll surely get a UB reprint into a non-promotional UB set.
	universesBeyondSets.UnionWith(universesBeyondSets.Select(s => "p" + s).ToArray());
	
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
	Guid? Oracle_Id,
	Dictionary<string, string> Image_Uris,
	CardFace[] Card_Faces,
	string Set,
	DateTime Released_At,
	string Collector_Number,
	string Layout)
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