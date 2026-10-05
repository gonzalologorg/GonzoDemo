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

function installPlayerLoaded(playerData){
    let token = crypto.randomBytes(32).toString("base64url");
    players[token] = playerData;
    for (const char of playerData.characters) {
        knownTokens[char.id] = token;
        cacheChars[char.id] = char;
    }

    return token;
}

module.exports = {
    players,
    getPlayerData,
    installPlayerLoaded,
    getTokenByCharId
};