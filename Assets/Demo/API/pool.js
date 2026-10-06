const crypto = require("crypto");
var players = {};
var knownTokens = {};
var cacheChars = {};

function getPlayerData(token){
    return players[token] || null;
}

function getTokenByCharId(charId){
    return knownTokens[charId] || false;
}

function getCharData(id){
    return cacheChars[id] || null;
}

function invalidatePlayer(token){
    let playerData = players[token];
    if (playerData) {
        for (const char of playerData.characters) {
            delete knownTokens[char.id];
            delete cacheChars[char.id];
        }
        delete players[token];
    }
}

// A player can only have one active cached session.  Removing the previous
// session also clears its character-to-token mappings before a new token is
// issued during login.
function invalidatePlayerById(playerId){
    for (const token of Object.keys(players)) {
        const playerData = players[token];
        if (playerData.player && playerData.player.id === playerId) {
            invalidatePlayer(token);
        }
    }
}

function installPlayerLoaded(playerData){
    let token = crypto.randomBytes(32).toString("base64url");
    players[token] = playerData;
    for (const char of playerData.characters) {
        knownTokens[char.id] = token;
        cacheChars[char.id] = char;
    }

    return token;
}

function addCharacter(token, character) {
    const playerData = getPlayerData(token);
    if (!playerData) {
        return false;
    }

    playerData.characters.push(character);
    knownTokens[character.id] = token;
    cacheChars[character.id] = character;
    return true;
}

module.exports = {
    players,
    getPlayerData,
    installPlayerLoaded,
    addCharacter,
    getTokenByCharId,
    invalidatePlayer,
    invalidatePlayerById,
    getCharData
};
