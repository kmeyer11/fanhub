CREATE TABLE IF NOT EXISTS standings (
    id SERIAL PRIMARY KEY,
    team_name VARCHAR(100) NOT NULL,
    league VARCHAR(100) NOT NULL,
    season INT NOT NULL,
    rank INT NOT NULL,
    points INT NOT NULL,
    played INT NOT NULL,
    won INT NOT NULL,
    drawn INT NOT NULL,
    lost INT NOT NULL,
    goals_for INT NOT NULL,
    goals_against INT NOT NULL,
    goal_difference INT NOT NULL,
    UNIQUE (team_name, league, season)
);