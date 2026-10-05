const mysql = require("mysql2");
const pool = require("./pool.js");
require("dotenv").config();
const connection = mysql.createConnection({
    host: process.env.MYSQL_HOST,
    user: process.env.MYSQL_USER,
    password: process.env.MYSQL_PASSWORD,
    database: process.env.MYSQL_DATABASE
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

function loginPlayer(username, hash_pass, result) {
    const query = `
        SELECT * FROM players WHERE username = ? AND hash_pass = ?
    `;
    connection.query(query, [username, hash_pass], (error, results, fields) => {
        if (error) throw error;
        if (results.length > 0) {
            console.log("Player logged in successfully.");
            // Install characters on player data and setup the pool
            connection.query(`SELECT * FROM characters WHERE player_id = ?`, [results[0].id], (error, results, fields) => {
                if (error) throw error;
                console.log("Player characters retrieved successfully.");
                pool.installPlayerLoaded({
                    player: results[0],
                    characters: results
                });
                result(true, results);
            });
        } else {
            console.log("Invalid username or password.");
            result(false);
        }
    });
}

function createCharacter(player_id, name, token, result) {
    let playerData = pool.getPlayerData(token);
    if (!playerData) {
        result(false);
        return;
    }

    const creation = `
        INSERT INTO characters (player_id, name, inventory, abilities, equipment)
        VALUES (?, ?, '[]', '[]', '[]')
    `;
    connection.query(creation, [player_id, name], (error, results, fields) => {
        if (error) throw error;
        const newChar = {
            id: results.insertId,
            player_id: player_id,
            name: name,
            inventory: '[]',
            abilities: '[]',
            equipment: '[]'
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