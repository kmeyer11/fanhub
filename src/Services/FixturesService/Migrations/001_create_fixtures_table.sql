CREATE TABLE IF NOT EXISTS fixtures (
    id SERIAL PRIMARY KEY,
    home_team VARCHAR(100) NOT NULL,
    away_team VARCHAR(100) NOT NULL,
    match_date TIMESTAMPTZ NOT NULL,
    competition VARCHAR(100) NOT NULL,
    venue VARCHAR(100) NOT NULL,
    home_score INT,
    away_score INT,
    status VARCHAR(20) NOT NULL DEFAULT 'scheduled'
);