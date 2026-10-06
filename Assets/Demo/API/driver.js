const mysql = require("mysql2");
const argon2 = require("argon2");
const pool = require("./pool.js");
require("dotenv").config();
const connection = mysql.createConnection({
    host: process.env.MYSQL_HOST,
    user: process.env.MYSQL_USER,
    password: process.env.MYSQL_PASSWORD,
    database: process.env.MYSQL_DATABASE,
    port: process.env.MYSQL_PORT
});

function createTables() {
    const usersTableQuery = `
        CREATE TABLE IF NOT EXISTS players (
            id INT AUTO_INCREMENT PRIMARY KEY,
            username VARCHAR(32) NOT NULL,
            hash_pass VARCHAR(255) NOT NULL,
            creation_date TIMESTAMP DEFAULT CURRENT_TIMESTAMP,
            email VARCHAR(96) NOT NULL,
            premium_currency INT DEFAULT 0
        )
    `;

    const characterTableQuery = `
        CREATE TABLE IF NOT EXISTS characters (
            id INT AUTO_INCREMENT PRIMARY KEY,
            player_id INT NOT NULL,
            name VARCHAR(32) NOT NULL,
            level INT DEFAULT 1,
            experience INT DEFAULT 0,
            money INT DEFAULT 0,
            side INT DEFAULT 0,
            portrait INT DEFAULT 0,
            inventory TEXT NOT NULL,
            abilities TEXT NOT NULL,
            equipment TEXT NOT NULL,
            creation_date TIMESTAMP DEFAULT CURRENT_TIMESTAMP,
            FOREIGN KEY (player_id) REFERENCES players(id)
        )
    `;

    connection.query(usersTableQuery, (error, results, fields) => {
        if (error) throw error;
        console.log("Users table created or already exists.");
    });

    connection.query(characterTableQuery, (error, results, fields) => {
        if (error) throw error;
        console.log("Characters table created or already exists.");
    });
}

function registerPlayer(username, hash_pass, email, result) {
    const check = `
        SELECT * FROM players WHERE username = ? or email = ? 
    `;
    connection.query(check, [username, email], (error, results, fields) => {
        if (error) throw error;
        if (results.length > 0) {
            console.log("Player already exists.");
            result(false);
            return;
        }
        const creation = `
            INSERT INTO players (username, hash_pass, email)
            VALUES (?, ?, ?)
        `;
        connection.query(creation, [username, hash_pass, email], (error, results, fields) => {
            if (error) throw error;
            console.log("Player registered successfully.");
            // Install the newly registered player into the pool
            const token = pool.installPlayerLoaded({
                player: {
                    id: results.insertId,
                    username: username,
                    hash_pass: hash_pass,
                    email: email
                },
                characters: []
            });
            result(true, token);
        });
    });
}

function loginPlayer(username, password, result) {
    const query = `
        SELECT * FROM players WHERE username = ? LIMIT 1
    `;
    connection.query(query, [username], async (error, results, fields) => {
        if (error) throw error;
        if (results.length === 0) {
            console.log("Invalid username or password.");
            result(false);
            return;
        }

        try {
            const passwordMatches = await argon2.verify(results[0].hash_pass, password);
            if (!passwordMatches) {
                console.log("Invalid username or password.");
                result(false);
                return;
            }
        } catch (error) {
            console.error("Could not verify player password:", error);
            result(false);
            return;
        }

        console.log("Player logged in successfully.");
        // Install characters on player data and setup the pool
        connection.query(`SELECT * FROM characters WHERE player_id = ?`, [results[0].id], (error, characters, fields) => {
            if (error) throw error;
            console.log("Player characters retrieved successfully.");

            // convert inventory, abilities, and equipment from JSON strings to objects
            characters.forEach(char => {
                char.inventory = JSON.parse(char.inventory);
                char.abilities = JSON.parse(char.abilities);
                char.equipment = JSON.parse(char.equipment);
            });
            // A previous session may still own cached character data and
            // its character-to-token mappings. Clear it before issuing
            // the token for this login.
            pool.invalidatePlayerById(results[0].id);
            const token = pool.installPlayerLoaded({
                player: results[0],
                characters: characters
            });
            result(true, token, results[0], characters);
        });
    });
}

function createCharacter(player_id, name, side, portrait, token, result) {
    let playerData = pool.getPlayerData(token);
    if (!playerData) {
        result(false);
        return;
    }

    const creation = `
        INSERT INTO characters (player_id, name, inventory, abilities, equipment, side, portrait)
        VALUES (?, ?, '[]', '[]', '[]', ?, ?)
    `;
    connection.query(creation, [player_id, name, side, portrait], (error, results, fields) => {
        if (error) throw error;
        const newChar = {
            id: results.insertId,
            player_id: player_id,
            name: name,
            inventory: '[]',
            abilities: '[]',
            equipment: '[]',
            side: side,
            portrait: portrait
        }
        console.log("Character created successfully.");
        playerData.characters.push(newChar);
        pool.getTokenByCharId(newChar.id); // Ensure the knownTokens mapping is updated
        result(true, newChar);
    });
}

module.exports = {
    createTables,
    registerPlayer,
    loginPlayer,
    createCharacter
};

createTables();
