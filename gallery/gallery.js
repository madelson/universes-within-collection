import { reactive, html } from 'https://esm.sh/@arrow-js/core@1.0.0-alpha.10';
import data from './galleryData.js';

const state = reactive({
	cards: data.cards.map(c => ({ ...c, showUniversesWithin: !!c.universesWithinImage, showFront: true })),
	searchTerm: '',
	showAllUniversesBeyondCards: false
});

html`
<header>
	<div class="header-top">
		<h1>Universes Within Collection - Card Gallery</h1>
		<a href="https://github.com/madelson/universes-within-collection" target="_blank" class="project-link">Project Site</a>
	</div>
	<div class="header-bottom">
		<input type="text" 
			@input="${e => state.searchTerm = e.target.value}"
			placeholder="Search...">
		<label>
			<input type="checkbox" 
				id="show-universes-beyond-checkbox"
				@change="${e => state.showAllUniversesBeyondCards = !!e.target.checked}"> Show all Universes Beyond cards
		</label>
	</div>
</header>
<div class="container">
	<div class="card-grid">
		${() => getCards().map(cardTemplate)}
	</div>
	<div>
		<br/><center><small><em>UWC card images are <a href="https://raw.githubusercontent.com/madelson/universes-within-collection/main/LICENSE.txt">free for personal use as proxies and may not be sold</a>. Usage of non-Magic art on UWC cards is done with permission from the Artist.</em></small></center>
	</div>
</div>`(document.body);

function cardTemplate(card) {
	return html`<div class="card">
		<a href="${() => getImageUrl(card)}" target="_blank">
			<img src="${() => getImageUrl(card)}" alt="${() => card.name}" />
		</a>
		<div class="attribution">
		${() => card.contributionInfo 
			? html`<div>UWC version contributed by ${card.contributionInfo.contributor}
				${() => artAttribution(card.showFront ? card.contributionInfo.front : card.contributionInfo.back)}</div>`
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
			${() => card.contributionInfo &&
				html`<a href="${() => "https://mtgcardbuilder.com/creator/?id=" + (card.showFront ? card.contributionInfo.front : card.contributionInfo.back).mtgCardBuilderId}" target="_blank" title="Open in MTG Card Builder">
					MTG Card Builder
				</a>`}
		</div>
	</div>`;
}

function artAttribution(face) {
	return !face.artist ? ''
		: face.isMtgArt ? html`<span><br/>MTG ART: <i><a href="${face.artUrl}" target="_blank" >${face.artName}</a></i> by ${face.artist}</span>`
		: html`<span><br/>ART: <i><a href="${face.artUrl}" target="_blank">${face.artName}</a></i> by <a href="${face.artistUrl}">${face.artist}</a></span>`;
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
	const showAllUniversesBeyondMatcher = c => state.showAllUniversesBeyondCards || c.contributionInfo != null;
	const searchTermMatcher = getSearchTermMatcher();

	return state.cards.filter(c => showAllUniversesBeyondMatcher(c) && searchTermMatcher(c));
}

function getSearchTermMatcher() {
	const searchTerm = state.searchTerm.trim();
	if (searchTerm === '') { return () => true; }

	const words = searchTerm.toLowerCase().split(/\s+/);
	const matchesWords = s =>
	{
		if (s == null) { return false; }
		const lower = s.toLowerCase();
		for (const word of words) {
			if (!lower.includes(word)) { return false; }
		}
		return true;
	};
	return c => matchesWords(c.name) || matchesWords(c.nickname) || matchesWords(c.contributionInfo?.front?.artist) || matchesWords(c.contributionInfo?.back?.artist);
}