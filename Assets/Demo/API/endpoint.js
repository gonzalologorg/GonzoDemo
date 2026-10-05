const express = require("express");
const argon2 = require("argon2");
const driver = require("./driver.js");
const pool = require("./pool.js");
const app = express();
require("dotenv").config();
const connection = driver.connection;

app.post("/signup", (req, res) => {
    const { username, password, email } = req.body;
    argon2.hash(password).then(hash => {
        driver.registerPlayer(username, hash, email, (success, token) => {
            if (success) {
                res.status(200).send({
                    status: "success",
                    token: token
                });
            } else {
                res.status(400).send("Player already exists");
            }
        });
    }).catch(err => {
        console.error(err);
        res.status(500).send("Internal Server Error");
    });
});

app.post("/login", (req, res) => {
    const { username, password } = req.body;
    argon2.hash(password).then(hash => {
        driver.loginPlayer(username, hash, (success, token) => {
        if (success) {
            res.status(200).send({
                status: "success",
                token: token
            });
        } else {
            res.status(400).send("Invalid username or password");
        }
    });
    }).catch(err => {
        console.error(err);
        res.status(500).send("Internal Server Error");
    });
});

app.post("/disconnect", (req, res) => {
    const { token } = req.body;
    if (pool.getPlayerData(token)) {
        delete pool.players[token];
        res.status(200).send({
            status: "success"
        });
    } else {
        res.status(400).send("Invalid token");
    }
});

app.post("/createcharacter", (req, res) => {
    const { token, name, key } = req.body;
    if (key !== process.env.API_SECRET) {
        res.status(403).send("Invalid API key");
        return;
    }

    const playerData = pool.getPlayerData(token);
    if (playerData) {
        driver.createCharacter(playerData.player.id, name, token, (success, newChar) => {
            if (success) {
                res.status(200).send({
                    status: "success",
                    character: newChar
                });
            } else {
                res.status(400).send("Failed to create character");
            }
        });
    } else {
        res.status(400).send("Invalid token");
    }
});

app.get("/getplayerdata", (req, res) => {
    const { token } = req.query;
    const playerData = pool.getPlayerData(token);
    if (playerData) {
        res.status(200).send({
            status: "success",
            playerData: playerData
        });
    } else {
        res.status(400).send("Invalid token");
    }
});

app.post("/updatecharacter", (req, res) => {
    const { secret, charId, field, value } = req.body;
    if (secret !== process.env.API_SECRET) {
        res.status(403).send("Invalid API secret");
        return;
    }

    let token = pool.getTokenByCharId(charId);
    if (!token) {
        res.status(400).send("Invalid character ID");
        return;
    }

    const query = `
        UPDATE characters
        SET ${field} = ?
        WHERE id = ? LIMIT 1;
    `;

    let char = pool.getCharData(charId);
    if (!char) {
        res.status(400).send("Character not found in cache");
        return;
    }

    char[field] = value;
    connection.query(query, [value, charId], (error, results, fields) => {
        if (error) {
            console.error(error);
            res.status(500).send("Internal Server Error");
            return;
        }
        if (results.affectedRows > 0) {
            res.status(200).send({
                status: "success"
            });
        } else {
            res.status(400).send("Character not found");
        }
    });
});

app.listen(process.env.EXPRESS_PORT, () => {
    console.log(`Server is running on port ${process.env.EXPRESS_PORT}`);
});