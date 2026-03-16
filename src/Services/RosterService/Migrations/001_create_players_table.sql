CREATE TABLE IF NOT EXISTS players (
    id SERIAL PRIMARY KEY,
    name VARCHAR(100) NOT NULL,
    position VARCHAR(50) NOT NULL,
    nationality VARCHAR(100) NOT NULL,
    shirt_number INT NOT NULL,
    date_of_birth DATE NOT NULL,
    team_name VARCHAR(100) NOT NULL,
    status VARCHAR(20) NOT NULL DEFAULT 'active'
);