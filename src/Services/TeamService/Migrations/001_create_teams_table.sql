CREATE TABLE IF NOT EXISTS teams (
    id SERIAL PRIMARY KEY,
    name VARCHAR(100) NOT NULL,
    short_name VARCHAR(20) NOT NULL,
    country VARCHAR(100) NOT NULL,
    league VARCHAR(100) NOT NULL,
    stadium VARCHAR(100) NOT NULL,
    founded INT NOT NULL,
    badge_url VARCHAR(255) NOT NULL
);