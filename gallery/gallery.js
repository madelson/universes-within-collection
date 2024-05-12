import { reactive, html } from 'https://esm.sh/@arrow-js/core@1.0.0-alpha.10';
import data from './galleryData.js';

const STATE_UNIVERSES_WITHIN_FRONT = 'UWCFront',
	STATE_UNIVERSES_WITHIN_BACK = 'UWCBack',
	STATE_UNIVERSES_BEYOND_FRONT = 'UBFront',
	STATE_UNIVERSES_BEYOND_BACK = 'UBBack';

const state = reactive({
	cards: data.cards.map(c => ({ ...c, state: c.universesWithinImage ? STATE_UNIVERSES_WITHIN_FRONT : STATE_UNIVERSES_BEYOND_FRONT })),
	searchTerm: '',
	showAllUniversesBeyondCards: false
});

html`<div class="container">
	<div class="search-box">
		<input type="text" @input="${e => state.searchTerm = e.target.value}" placeholder="Search..." />
		<div class="show-universes-beyond-container">
			<input type="checkbox" id="show-universes-beyond-checkbox" @change="${e => state.showAllUniversesBeyondCards = !!e.target.checked}" />
			<label for="show-universes-beyond-checkbox">Show all Universes Beyond cards</label>
		</div>
	</div>
	<div class="card-grid">
		${() => getCards().map(cardTemplate)}
	</div>
</div>`(document.getElementById('app'));

console.log(data);

function cardTemplate(card) {
	return html`<div class="card">
		<img src="${() => getImageUrl(card)}" alt="${() => card.name}" />
		<div class="attribution">
		${card.contributor ? `UWC version contributed by ${card.contributor}`
			: card.universesWithinImage ? 'Official Universes Within card'
			: ''}
		</div>
	</div>`;
}

function getImageUrl(card) {
	switch (card.state) {
		case STATE_UNIVERSES_WITHIN_FRONT:
			return card.universesWithinImage;
		case STATE_UNIVERSES_WITHIN_BACK:
			return card.universesWithinBackImage;
		case STATE_UNIVERSES_BEYOND_FRONT:
			return card.universesBeyondImage;
		case STATE_UNIVERSES_BEYOND_BACK:
			return card.universesBeyondBackImage;
		default:
			throw new Error(`State = '${state}'`);
	}
}

function getCards() {
	const showAllUniversesBeyondMatcher = c => state.showAllUniversesBeyondCards || (c.universesWithinImage != null && !c.hasOfficialUniversesWithinCard);
	const searchTermMatcher = getSearchTermMatcher();

	return state.cards.filter(c => showAllUniversesBeyondMatcher(c) && searchTermMatcher(c));
}

function getSearchTermMatcher() {
	const searchTerm = state.searchTerm.trim();
	if (searchTerm === '') { return () => true; }

	const words = searchTerm.toLowerCase().split(/\s+/);
	const matchesWords = s =>
	{
		const lower = s.toLowerCase();
		for (const word of words) {
			if (!lower.includes(word)) { return false; }
		}
		return true;
	};
	return c => matchesWords(c.name) || (c.nickname != null && matchesWords(c.nickname));
}