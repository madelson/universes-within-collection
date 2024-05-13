import { reactive, html } from 'https://esm.sh/@arrow-js/core@1.0.0-alpha.10';
import data from './galleryData.js';

const state = reactive({
	cards: data.cards.map(c => ({ ...c, showUniversesWithin: !!c.universesWithinImage, showFront: true })),
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

function cardTemplate(card) {
	return html`<div class="card">
		<img src="${() => getImageUrl(card)}" alt="${() => card.name}" />
		<div class="attribution">
		${card.contributor ? `UWC version contributed by ${card.contributor}`
			: card.universesWithinImage ? 'Official Universes Within card'
			: ''}
		</div>
		<div class="controls">
			${() => card.universesWithinImage != null &&
				html`<a @click="${() => card.showUniversesWithin = !card.showUniversesWithin}" 
					title="${() => `Show Universes ${card.showUniversesWithin ? "Beyond" : "Within"}`}" href="javascript:void(0)">
					${() => card.showUniversesWithin ? "UB" : "UW"}
				</a>`}
			${() => card.universesBeyondBackImage != null &&
				html`<a @click="${() => card.showFront = !card.showFront}" title="Turn over" href="javascript:void(0)">
					${() => card.showFront ? "BACK" : "FRONT"}
				</a>`}
			${() => card.mtgCardBuilderId != null &&
				html`<a href="https://mtgcardbuilder.com/creator/?id=${card.mtgCardBuilderId}" target="_blank" title="Open in MTG Card Builder">
					MTG Card Builder
				</a>`}
		</div>
	</div>`;
}

function getImageUrl(card) {
	return card.showUniversesWithin
		? card.showFront
			? card.universesWithinImage
			: card.universesWithinBackImage
		: card.showFront
			? card.universesBeyondImage
			: card.universesBeyondBackImage;
}

function getCards() {
	const showAllUniversesBeyondMatcher = c => state.showAllUniversesBeyondCards || (c.universesWithinImage != null && c.mtgCardBuilderId != null);
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