---
name: New Card
about: Submit a card to the collection
title: "[CARD] <universes beyond card name>"
labels: ''
assignees: ''
---

# Double-check collector info
The collector box on MTG cardbuilder has six pieces of info that should be filled out:
- Box 1: rarity and UWC collector number. Rarity is C,U,R, or M. The collector number should be a 4-digit number that is 1 greater than the newest card (first in [the gallery](https://madelson.github.io/universes-within-collection/)) or the [most recent card issue submission](https://github.com/madelson/universes-within-collection/issues)). Example: `R 0195`
- Box 2: link to original set and collector number. Should look like `=FIC 0066`. You can get set codes from the card's scryfall URL if you don't know them.
- Box 3: new set code. Always `UWC`
- Box 4: always `EN`
- Box 5: artist name
- Box 6: current year
- [ ] Looks good!

# Card Image
Paste the image from https://mtgcardbuilder.com/.

# mtgcardbuilder URL
URL where you saved your card in mtgcardbuilder. Example: https://mtgcardbuilder.com/creator/?id=625927

# Metadata
Please fill out this metadata. If any field doesn't apply to your card, leave it blank.

```json
{
	"name": "Original card name",
	"nickname": "In-universe name",
	"contributor": "Your github username. You can see it when hovering over your profile picture",
	"artist": "Artist name if not using the original art. Except for public domain art, must be a name from https://github.com/madelson/universes-within-collection/blob/main/docs/artists.md or someone you've received permission from",
	"artName": "What the artist named the artwork if not using the original art",
	"artUrl": "URL where I can view the art if not using the original art",
	"mtgCardBuilderId": <number taken from the end of the mtgcardbuilder URL>
}
```

If you have additional info like a second artwork for the back of a double-faced card, please include it here.

Here is a complete example for reference:
```json
{
	"name": "Vrestin, Menoptra Leader",
	"nickname": "Redgin, Fearless Rider",
	"contributor": "KolaghanStorm",
	"artist": "Thomas Ricci",
	"artName": "Dragonslayer Mosquito",
	"artUrl": "https://artrhino.artstation.com/projects/WXqdoN",
	"mtgCardBuilderId": 621568
}
```

